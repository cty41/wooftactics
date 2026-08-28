[CmdletBinding()]
param(
    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe',
    [object]$HeldOperationLock
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$projectRoot = Join-Path $repoRoot 'godot'
$adapterAssembly = Join-Path $projectRoot '.godot\mono\temp\bin\Debug\Tactics.Godot.Adapter.dll'
$assemblyBackup = "$adapterAssembly.bootstrap-test-backup"
$sessionModule = Join-Path $PSScriptRoot 'GodotDevSession.psm1'
Import-Module $sessionModule -Force
$editorLaunchArgument = Get-TacticsGodotLaunchArgument
$worktreeIdentity = Get-TacticsGodotWorktreeIdentity -RepoRoot $repoRoot
$operationLock = $null

function Invoke-BootstrapProbe {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [switch]$Supported
    )

    $probeToken = [guid]::NewGuid().ToString('N')
    $logPath = Join-Path $env:TEMP "tactics-tooling-bootstrap-$($worktreeIdentity.Key)-$Name-$probeToken.log"
    $quotedProjectRoot = '"' + $projectRoot + '"'
    $quotedLogPath = '"' + $logPath + '"'
    $arguments = @('--headless', '--editor', '--path', $quotedProjectRoot, '--log-file', $quotedLogPath, '--quit-after', '120')
    if ($Supported) { $arguments += @('--', $editorLaunchArgument) }
    $process = Start-Process -FilePath $GodotExecutable -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Bootstrap probe '$Name' exited with code $($process.ExitCode)." }
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) { throw "Bootstrap probe '$Name' did not create $logPath." }
    [pscustomobject]@{ Name = $Name; LogPath = $logPath; Text = Get-Content -LiteralPath $logPath -Raw }
}

function Assert-Contains {
    param([string]$Text, [string]$Expected, [string]$Context)
    if ($Text.IndexOf($Expected, [StringComparison]::Ordinal) -lt 0) {
        throw "$Context did not contain '$Expected'."
    }
}

function Assert-OmitsLegacyAddonFailure {
    param([string]$Text, [string]$Context)
    foreach ($forbidden in @(
        "Unable to load addon script from path: 'res://addons/tactics_tooling",
        'TacticsEditorPlugin.cs'
    )) {
        if ($Text.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) {
            throw "$Context contained legacy addon failure '$forbidden'."
        }
    }
}

if (-not (Test-Path -LiteralPath $GodotExecutable -PathType Leaf)) { throw "Godot executable not found: $GodotExecutable" }
if (-not (Test-Path -LiteralPath $adapterAssembly -PathType Leaf)) { throw "Build the Debug Adapter before running this test: $adapterAssembly" }
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
    else {
        $operationLock = Enter-TacticsGodotOperationLock -RepoRoot $repoRoot
    }
    $openEditors = @(Get-TacticsGodotEditorProcess -ProjectRoot $projectRoot)
    if ($openEditors.Count -gt 0) { throw "Close the canonical Editor before bootstrap probes. PIDs: $($openEditors.ProcessId -join ', ')" }

    $direct = Invoke-BootstrapProbe -Name 'direct-with-assembly'
    Assert-Contains $direct.Text 'TACTICS_TOOLING_BLOCKED_DIRECT_LAUNCH' $direct.Name
    if ($direct.Text.IndexOf('[Tactics Tooling] Controller entering tree.', [StringComparison]::Ordinal) -ge 0) {
        throw "$($direct.Name) initialized the C# controller."
    }
    Assert-OmitsLegacyAddonFailure $direct.Text $direct.Name

    Move-Item -LiteralPath $adapterAssembly -Destination $assemblyBackup
    try {
        $missing = Invoke-BootstrapProbe -Name 'direct-without-assembly'
        Assert-Contains $missing.Text 'TACTICS_TOOLING_BLOCKED_DIRECT_LAUNCH' $missing.Name
        Assert-OmitsLegacyAddonFailure $missing.Text $missing.Name

        $incomplete = Invoke-BootstrapProbe -Name 'supported-without-assembly' -Supported
        Assert-Contains $incomplete.Text 'TACTICS_TOOLING_BOOTSTRAP_INCOMPLETE' $incomplete.Name
        Assert-OmitsLegacyAddonFailure $incomplete.Text $incomplete.Name
    }
    finally {
        if (Test-Path -LiteralPath $assemblyBackup -PathType Leaf) {
            Move-Item -LiteralPath $assemblyBackup -Destination $adapterAssembly
        }
    }

    $supported = Invoke-BootstrapProbe -Name 'supported' -Supported
    Assert-Contains $supported.Text '[Tactics Tooling Bootstrap] Controller ready.' $supported.Name
    Assert-Contains $supported.Text '[Tactics Tooling] Main screen skipped in headless editor mode.' $supported.Name
    if ($supported.Text.IndexOf('TACTICS_TOOLING_BLOCKED_DIRECT_LAUNCH', [StringComparison]::Ordinal) -ge 0) {
        throw "$($supported.Name) was incorrectly blocked."
    }
    Assert-OmitsLegacyAddonFailure $supported.Text $supported.Name

    [pscustomobject]@{
        Succeeded = $true
        DirectLog = $direct.LogPath
        MissingAssemblyLog = $missing.LogPath
        IncompleteAssemblyLog = $incomplete.LogPath
        SupportedLog = $supported.LogPath
    }
}
finally {
    if (Test-Path -LiteralPath $assemblyBackup -PathType Leaf) {
        if (Test-Path -LiteralPath $adapterAssembly -PathType Leaf) {
            Remove-Item -LiteralPath $assemblyBackup -Force
        }
        else {
            Move-Item -LiteralPath $assemblyBackup -Destination $adapterAssembly
        }
    }
    Exit-TacticsGodotOperationLock -Lock $operationLock
}
