[CmdletBinding()]
param(
    [ValidateSet('Agent', 'Human')][string]$Mode = 'Agent',
    [ValidateSet('Worktree', 'SharedManualQA')][string]$UserDataProfile = 'Worktree',
    [ValidateSet('phase3-observe', 'content-authoring', 'ui-input', 'presentation')]
    [string]$GodotAiProfile = 'phase3-observe',
    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe',
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $repoRoot 'godot')).Path
$projectFile = Join-Path $projectRoot 'project.godot'
$adapterProject = Join-Path $projectRoot 'Tactics.Godot.Adapter.csproj'
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'Tools\migration\manifest\godot-tooling.json') -Raw | ConvertFrom-Json
$sessionModule = Join-Path $PSScriptRoot 'GodotDevSession.psm1'
Import-Module $sessionModule -Force
$editorLaunchArgument = Get-TacticsGodotLaunchArgument

if ($Mode -eq 'Agent' -and $UserDataProfile -eq 'SharedManualQA') {
    throw 'SharedManualQA is a Human-only profile; Agent sessions must use worktree-isolated user data.'
}
foreach ($required in @($GodotExecutable, $projectFile, $adapterProject, (Join-Path $projectRoot 'addons\godot_ai\plugin.cfg'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required Godot development file is missing: $required" }
}
$expectedGodotVersion = ([string]$manifest.godotVersion).Split('-')[0]
$actualGodotVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($GodotExecutable).FileVersion
if ($actualGodotVersion -ne $expectedGodotVersion) {
    throw "Expected Godot $expectedGodotVersion, found $actualGodotVersion at $GodotExecutable."
}
$expectedSdk = [Version][string]$manifest.dotnetSdk
Push-Location $repoRoot
try {
    $actualSdkText = (dotnet --version).Trim()
}
finally {
    Pop-Location
}
$actualSdk = [Version]$actualSdkText
$expectedFeatureBand = [Math]::Floor($expectedSdk.Build / 100)
$actualFeatureBand = [Math]::Floor($actualSdk.Build / 100)
if ($actualSdk.Major -ne $expectedSdk.Major -or $actualSdk.Minor -ne $expectedSdk.Minor -or $actualFeatureBand -ne $expectedFeatureBand) {
    throw "Expected .NET SDK feature band $($expectedSdk.Major).$($expectedSdk.Minor).$($expectedFeatureBand)xx, found $actualSdkText."
}

$lock = $null
try {
    $lock = Enter-TacticsGodotOperationLock -RepoRoot $repoRoot
    $identity = $lock.Identity
    $existing = @(Get-TacticsGodotEditorProcess -ProjectRoot $projectRoot)
    if ($existing.Count -gt 1) { throw "Multiple Editors already target this worktree: $($existing.ProcessId -join ', ')" }
    if ($existing.Count -eq 1) {
        Write-Output "GODOT_EDITOR_ALREADY_OPEN pid=$($existing[0].ProcessId) project=$projectRoot"
        return
    }

    $overridePath = Join-Path $projectRoot 'override.cfg'
    $userDirectory = if ($UserDataProfile -eq 'SharedManualQA') { 'TacticsGodotManualQA' } else { $identity.UserDirectoryName }
    $override = @"
[application]

config/use_custom_user_dir=true
config/custom_user_dir_name="$userDirectory"
"@
    [IO.File]::WriteAllText($overridePath, $override.Replace("`r`n", "`n") + "`n", [Text.UTF8Encoding]::new($false))

    $configPath = Join-Path $repoRoot '.codex\config.toml'
    $createdConfig = -not (Test-Path -LiteralPath $configPath -PathType Leaf)
    $syncArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Sync-GodotAiCodexConfig.ps1'), '-ProjectRoot', $repoRoot, '-Profile', $GodotAiProfile)
    if ($createdConfig) { $syncArguments += '-Bootstrap' }
    & powershell @syncArguments
    if ($LASTEXITCODE -ne 0) { throw "Godot AI Codex configuration failed with exit code $LASTEXITCODE." }

    Push-Location $repoRoot
    try {
        & dotnet build $adapterProject -c Debug -m:1
        $buildExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    if ($buildExitCode -ne 0) { throw "Godot Adapter Debug build failed with exit code $buildExitCode." }
    $assemblyPath = Join-Path $projectRoot '.godot\mono\temp\bin\Debug\Tactics.Godot.Adapter.dll'
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) { throw "Production Adapter assembly is missing: $assemblyPath" }
    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
    if ($assemblyName.Name -ne 'Tactics.Godot.Adapter') { throw "Unexpected Adapter assembly identity: $($assemblyName.FullName)" }

    if ($NoLaunch) {
        Write-Output "GODOT_DEV_READY mode=$Mode profile=$UserDataProfile assembly=$($assemblyName.FullName)"
        if ($createdConfig) { Write-Output 'CODEX_RESTART_REQUIRED' }
        return
    }

    $quotedProjectRoot = '"' + $projectRoot + '"'
    $process = Start-Process -FilePath $GodotExecutable -ArgumentList @(
        '--editor', '--path', $quotedProjectRoot, '--', $editorLaunchArgument) -PassThru
    $sessionPath = Join-Path $projectRoot '.godot\tactics-dev-session.json'
    New-Item -ItemType Directory -Path (Split-Path $sessionPath) -Force | Out-Null
    [ordered]@{
        schemaVersion = 1; worktreeKey = $identity.Key; repoRoot = $repoRoot; projectRoot = $projectRoot
        editorPid = $process.Id; mode = $Mode; userDataProfile = $UserDataProfile
        godotAiProfile = $GodotAiProfile; startedAt = [DateTimeOffset]::Now.ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath $sessionPath -Encoding UTF8
    Write-Output "GODOT_EDITOR_STARTED pid=$($process.Id) project=$projectRoot"
    if ($createdConfig) { Write-Output 'CODEX_RESTART_REQUIRED' }
}
finally {
    Exit-TacticsGodotOperationLock -Lock $lock
}
