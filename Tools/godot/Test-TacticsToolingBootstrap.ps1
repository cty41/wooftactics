[CmdletBinding()]
param(
    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe',
    [object]$HeldOperationLock,
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$projectRoot = Join-Path $repoRoot 'godot'
$adapterAssembly = Join-Path $projectRoot '.godot\mono\temp\bin\Debug\Tactics.Godot.Adapter.dll'
$assemblyBackup = "$adapterAssembly.bootstrap-test-backup"
$buildHelper = Join-Path $PSScriptRoot 'Build-GodotDevAssembly.ps1'
$sessionModule = Join-Path $PSScriptRoot 'GodotDevSession.psm1'
Import-Module $sessionModule -Force
$editorLaunchArgument = Get-TacticsGodotLaunchArgument
$worktreeIdentity = Get-TacticsGodotWorktreeIdentity -RepoRoot $repoRoot
$operationLock = $null
$liveProcess = $null
$temporaryPaths = [Collections.Generic.List[string]]::new()

function New-ProbePaths {
    param([Parameter(Mandatory = $true)][string]$Name)
    $token = [guid]::NewGuid().ToString('N')
    $logPath = Join-Path $env:TEMP "tactics-tooling-bootstrap-$($worktreeIdentity.Key)-$Name-$token.log"
    $temporaryPaths.Add($logPath)
    [pscustomobject]@{ Name = $Name; Token = $token; LogPath = $logPath }
}

function Get-ProbeArguments {
    param([Parameter(Mandatory = $true)]$Probe, [switch]$Prepared, [switch]$KeepAlive, [switch]$AutoBuild, [string]$ExternalBuildToken)
    $quotedProjectRoot = '"' + $projectRoot + '"'
    $quotedLogPath = '"' + $Probe.LogPath + '"'
    $arguments = @('--headless', '--editor', '--path', $quotedProjectRoot, '--log-file', $quotedLogPath)
    if (-not $KeepAlive) { $arguments += @('--quit-after', '120') }
    $userArguments = @()
    if ($Prepared) { $userArguments += $editorLaunchArgument }
    if ($AutoBuild) { $userArguments += '--tactics-tooling-auto-build-probe=v1' }
    if (-not [string]::IsNullOrWhiteSpace($ExternalBuildToken)) { $userArguments += "--tactics-tooling-external-build-probe=$ExternalBuildToken" }
    if ($userArguments.Count -gt 0) { $arguments += @('--') + $userArguments }
    $arguments
}

function Invoke-BootstrapProbe {
    param([Parameter(Mandatory = $true)][string]$Name, [switch]$Prepared)
    $probe = New-ProbePaths -Name $Name
    $arguments = Get-ProbeArguments -Probe $probe -Prepared:$Prepared
    $process = Start-Process -FilePath $GodotExecutable -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Bootstrap probe '$Name' exited with code $($process.ExitCode)." }
    if (-not (Test-Path -LiteralPath $probe.LogPath -PathType Leaf)) { throw "Bootstrap probe '$Name' did not create $($probe.LogPath)." }
    [pscustomobject]@{ Name = $Name; LogPath = $probe.LogPath; Text = Get-Content -LiteralPath $probe.LogPath -Raw }
}

function Start-LiveBootstrapProbe {
    param([string]$ExternalBuildToken)
    $probe = New-ProbePaths -Name 'direct-live-self-heal'
    $arguments = if ([string]::IsNullOrWhiteSpace($ExternalBuildToken)) {
        Get-ProbeArguments -Probe $probe -KeepAlive -AutoBuild
    }
    else {
        Get-ProbeArguments -Probe $probe -KeepAlive -ExternalBuildToken $ExternalBuildToken
    }
    $process = Start-Process -FilePath $GodotExecutable -ArgumentList $arguments -PassThru -WindowStyle Hidden
    [pscustomobject]@{ Probe = $probe; Process = $process }
}

function Wait-LogContains {
    param([Parameter(Mandatory = $true)]$Live, [Parameter(Mandatory = $true)][string]$Expected)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if ($Live.Process.HasExited) { throw "Live bootstrap PID $($Live.Process.Id) exited before '$Expected'." }
        if (Test-Path -LiteralPath $Live.Probe.LogPath -PathType Leaf) {
            $text = Get-Content -LiteralPath $Live.Probe.LogPath -Raw
            if ($text.IndexOf($Expected, [StringComparison]::Ordinal) -ge 0) { return $text }
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for '$Expected' in $($Live.Probe.LogPath)."
}

function Assert-Contains {
    param([string]$Text, [string]$Expected, [string]$Context)
    if ($Text.IndexOf($Expected, [StringComparison]::Ordinal) -lt 0) { throw "$Context did not contain '$Expected'." }
}

function Assert-HealthyBootstrapLog {
    param([string]$Text, [string]$Context)
    foreach ($forbidden in @(
        "Unable to load addon script from path: 'res://addons/tactics_tooling",
        'TacticsEditorPlugin.cs',
        'TACTICS_TOOLING_BLOCKED_DIRECT_LAUNCH'
    )) {
        if ($Text.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) {
            throw "$Context contained forbidden bootstrap failure '$forbidden'."
        }
    }
}

if (-not (Test-Path -LiteralPath $GodotExecutable -PathType Leaf)) { throw "Godot executable not found: $GodotExecutable" }
if (-not (Test-Path -LiteralPath $adapterAssembly -PathType Leaf)) { throw "Build the Debug Adapter before running this test: $adapterAssembly" }
if (-not (Test-Path -LiteralPath $buildHelper -PathType Leaf)) { throw "Build helper not found: $buildHelper" }
if (Test-Path -LiteralPath $assemblyBackup) { throw "Stale bootstrap test backup exists: $assemblyBackup" }

try {
    if ($null -ne $HeldOperationLock) {
        if ($HeldOperationLock.Identity.RepoRoot -ne $repoRoot -or
            $HeldOperationLock.Identity.MutexName -ne $worktreeIdentity.MutexName -or
            $HeldOperationLock.Mutex -isnot [Threading.Mutex] -or
            $HeldOperationLock.Mutex.SafeWaitHandle.IsClosed -or $HeldOperationLock.Mutex.SafeWaitHandle.IsInvalid) {
            throw 'HeldOperationLock must be the live operation lock for this repository.'
        }
    }
    else { $operationLock = Enter-TacticsGodotOperationLock -RepoRoot $repoRoot }
    $activeLock = if ($null -ne $HeldOperationLock) { $HeldOperationLock } else { $operationLock }
    $openEditors = @(Get-TacticsGodotEditorProcess -ProjectRoot $projectRoot)
    if ($openEditors.Count -gt 0) { throw "Close the canonical Editor before bootstrap probes. PIDs: $($openEditors.ProcessId -join ', ')" }

    $direct = Invoke-BootstrapProbe -Name 'direct-with-assembly'
    Assert-Contains $direct.Text '[Tactics Tooling Bootstrap] Controller ready. prepared=false' $direct.Name
    Assert-HealthyBootstrapLog $direct.Text $direct.Name

    $prepared = Invoke-BootstrapProbe -Name 'prepared-with-assembly' -Prepared
    Assert-Contains $prepared.Text '[Tactics Tooling Bootstrap] Controller ready. prepared=true' $prepared.Name
    Assert-HealthyBootstrapLog $prepared.Text $prepared.Name

    Move-Item -LiteralPath $adapterAssembly -Destination $assemblyBackup
    $missing = Invoke-BootstrapProbe -Name 'direct-without-assembly'
    Assert-Contains $missing.Text 'TACTICS_TOOLING_BUILD_REQUIRED prepared=false' $missing.Name
    Assert-HealthyBootstrapLog $missing.Text $missing.Name

    $preparedMissing = Invoke-BootstrapProbe -Name 'prepared-without-assembly' -Prepared
    Assert-Contains $preparedMissing.Text 'TACTICS_TOOLING_BUILD_REQUIRED prepared=true' $preparedMissing.Name
    Assert-HealthyBootstrapLog $preparedMissing.Text $preparedMissing.Name

    if ($null -eq $HeldOperationLock) {
        # The real GDScript Shell starts the helper in a child process. Release the
        # probe's parent lock so that child can acquire the same canonical lock.
        Exit-TacticsGodotOperationLock -Lock $operationLock
        $operationLock = $null
        $live = Start-LiveBootstrapProbe
    }
    else {
        # A verifier already owns the operation lock. Exercise the same token/result/
        # reload path while invoking the helper in this PowerShell process with that
        # verified live lock, rather than silently skipping the critical case.
        $externalToken = [guid]::NewGuid().ToString('N')
        $externalResult = Join-Path $projectRoot ".godot\tactics-tooling-build-result-$externalToken.json"
        $externalLog = Join-Path $projectRoot ".godot\tactics-tooling-build-$externalToken.log"
        $temporaryPaths.Add($externalResult); $temporaryPaths.Add($externalLog)
        $live = Start-LiveBootstrapProbe -ExternalBuildToken $externalToken
    }
    $liveProcess = $live.Process
    $null = Wait-LogContains -Live $live -Expected 'TACTICS_TOOLING_BUILD_REQUIRED prepared=false'
    if ($null -ne $HeldOperationLock) {
        & $buildHelper -Token $externalToken -ResultPath $externalResult -LogPath $externalLog -HeldOperationLock $activeLock
        if ($LASTEXITCODE -ne 0) { throw "Held-lock self-heal build failed with exit code $LASTEXITCODE." }
    }
    $liveText = Wait-LogContains -Live $live -Expected '[Tactics Tooling Bootstrap] Controller ready. prepared=false'
    Assert-HealthyBootstrapLog $liveText 'direct-live-self-heal'

    [pscustomobject]@{
        Succeeded = $true
        DirectLog = $direct.LogPath
        PreparedLog = $prepared.LogPath
        MissingAssemblyLog = $missing.LogPath
        PreparedMissingAssemblyLog = $preparedMissing.LogPath
        LiveSelfHealExecuted = $true
        LiveSelfHealLog = $live.Probe.LogPath
        LiveSelfHealPid = $live.Process.Id
    }
}
finally {
    if ($null -ne $liveProcess -and -not $liveProcess.HasExited) {
        & taskkill /PID $liveProcess.Id | Out-Null
        $liveProcess.WaitForExit(5000) | Out-Null
        if (-not $liveProcess.HasExited) {
            # This PID is an isolated headless probe created by this script, never the
            # user's canonical visible Editor.
            Stop-Process -Id $liveProcess.Id -Force
            $liveProcess.WaitForExit(5000) | Out-Null
        }
    }
    if (Test-Path -LiteralPath $assemblyBackup -PathType Leaf) {
        if (Test-Path -LiteralPath $adapterAssembly -PathType Leaf) { Remove-Item -LiteralPath $adapterAssembly -Force }
        Move-Item -LiteralPath $assemblyBackup -Destination $adapterAssembly
    }
    foreach ($path in $temporaryPaths) {
        if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force }
    }
    Exit-TacticsGodotOperationLock -Lock $operationLock
}
