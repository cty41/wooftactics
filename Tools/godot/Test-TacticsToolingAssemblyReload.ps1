[CmdletBinding()]
param(
    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe',
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectRoot = Join-Path $repoRoot 'godot'
$projectFile = Join-Path $projectRoot 'project.godot'
$logPath = Join-Path $env:TEMP ('tactics-tooling-reload-' + [guid]::NewGuid().ToString('N') + '.log')
$descriptorPattern = Join-Path $projectRoot '.godot\tactics-authoring-session-*.json'
$sessionModule = Join-Path $PSScriptRoot 'GodotDevSession.psm1'
Import-Module $sessionModule -Force
$editorLaunchArgument = Get-TacticsGodotLaunchArgument
$forbidden = @('InvalidCastException', 'Failed to unload assemblies', 'delegate_handle.value', 'Method not found')

if (!(Test-Path -LiteralPath $GodotExecutable)) { throw "Godot executable not found: $GodotExecutable" }
if (!(Test-Path -LiteralPath $projectFile)) { throw "Canonical Godot project not found: $projectFile" }

$canonicalEditors = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -like 'Godot*.exe' -and $_.CommandLine -match '--editor' -and
    $_.CommandLine -like ('*' + $projectRoot + '*')
})
if ($canonicalEditors.Count -ne 0) {
    throw "Reload smoke requires no canonical Editor. Found PID(s): $($canonicalEditors.ProcessId -join ', ')."
}

function Wait-Descriptor([int]$processId, [string]$differentToken = '') {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if (!(Get-Process -Id $processId -ErrorAction SilentlyContinue)) { throw "Godot PID $processId exited before Bridge became ready." }
        $candidate = Get-ChildItem -Path $descriptorPattern -File -ErrorAction SilentlyContinue |
            ForEach-Object { try { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json } catch { $null } } |
            Where-Object { $_.processId -eq $processId -and $_.state -eq 'ready' -and $_.transport -eq 'named-pipe' -and
                ([string]::IsNullOrWhiteSpace($differentToken) -or $_.sessionToken -ne $differentToken) } |
            Select-Object -First 1
        if ($candidate) { return $candidate }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for ready Bridge descriptor for PID $processId."
}

function Invoke-AuthoringPreview($descriptor, [string]$contentId) {
    $kind = if ($contentId.StartsWith('presentation.')) { 'presentation' } else { throw "Unsupported smoke ContentId: $contentId" }
    $request = [ordered]@{
        tool = 'tactics_authoring_preview'; sessionToken = $descriptor.sessionToken; projectRoot = $repoRoot
        arguments = [ordered]@{ kind = $kind; contentId = $contentId; seed = 17 }
    } | ConvertTo-Json -Depth 8 -Compress
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new('.', $descriptor.pipeName,
        [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
    $reader = $null; $writer = $null
    try {
        $pipe.Connect($TimeoutSeconds * 1000)
        $writer = [System.IO.StreamWriter]::new($pipe, [System.Text.UTF8Encoding]::new($false), 4096, $true)
        $reader = [System.IO.StreamReader]::new($pipe, [System.Text.Encoding]::UTF8, $false, 4096, $true)
        $writer.AutoFlush = $true; $writer.WriteLine($request)
        $response = $reader.ReadLine() | ConvertFrom-Json
        if (!$response.succeeded) {
            $errorText = if ($response.error) { $response.error } else { $response.diagnostics | ConvertTo-Json -Compress }
            throw "Preview failed for ${contentId}: $errorText"
        }
        return $response
    }
    finally { if ($reader) { $reader.Dispose() }; if ($writer) { $writer.Dispose() }; $pipe.Dispose() }
}

$process = $null
try {
    $quotedProjectRoot = '"' + $projectRoot + '"'
    $quotedLogPath = '"' + $logPath + '"'
    $process = Start-Process -FilePath $GodotExecutable -ArgumentList @('--editor', '--path', $quotedProjectRoot,
        '--log-file', $quotedLogPath, '--', $editorLaunchArgument) -PassThru -WindowStyle Hidden
    $before = Wait-Descriptor $process.Id
    $ids = @('presentation.skill.mage.fireball', 'presentation.status.standard-v1', 'presentation.unit.standard-v1')
    foreach ($id in $ids) { $null = Invoke-AuthoringPreview $before $id }

    & dotnet build (Join-Path $projectRoot 'Tactics.Godot.Adapter.csproj') -c Debug --no-restore --no-incremental
    if ($LASTEXITCODE -ne 0) { throw 'Adapter rebuild failed.' }
    $after = Wait-Descriptor $process.Id $before.sessionToken
    if ($after.pipeName -eq $before.pipeName) { throw 'Reload did not rotate the Bridge pipe.' }
    $post = Invoke-AuthoringPreview $after 'presentation.skill.mage.fireball'
    if ($post.evidence.values.cleanupTemporaryNodes -and $post.evidence.values.cleanupTemporaryNodes -ne '0') {
        throw "Reload preview cleanup was not zero: $($post.evidence.values.cleanupTemporaryNodes)"
    }
    $matches = Select-String -LiteralPath $logPath -Pattern $forbidden -SimpleMatch
    if ($matches) { throw "Reload log contains forbidden signatures: $($matches.Pattern -join ', ')" }
    [pscustomobject]@{ Succeeded = $true; ProcessId = $process.Id; BeforeToken = $before.sessionToken;
        AfterToken = $after.sessionToken; LogPath = $logPath; PreviewCount = 4 }
}
finally {
    if ($process -and !$process.HasExited) {
        & taskkill /PID $process.Id | Out-Null
        $process.WaitForExit(5000) | Out-Null
        if (!$process.HasExited) { Write-Warning "PID $($process.Id) did not accept normal shutdown; it was not force-killed." }
    }
}
