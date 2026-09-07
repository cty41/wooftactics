[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Close', 'Open')]
    [string]$Action,

    [string]$ProjectPath = '',

    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe',

    [int]$EditorProcessId = 0,

    [ValidateRange(5, 60)]
    [int]$TimeoutSeconds = 45,

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RequiredDirectory {
    param([string]$LiteralPath, [string]$Description)

    if (-not (Test-Path -LiteralPath $LiteralPath -PathType Container)) {
        throw "$Description directory does not exist: $LiteralPath"
    }
    return (Resolve-Path -LiteralPath $LiteralPath).Path
}

function Resolve-RequiredFile {
    param([string]$LiteralPath, [string]$Description)

    if (-not (Test-Path -LiteralPath $LiteralPath -PathType Leaf)) {
        throw "$Description file does not exist: $LiteralPath"
    }
    return (Resolve-Path -LiteralPath $LiteralPath).Path
}

function Write-Result {
    param([hashtable]$Value)

    $Value | ConvertTo-Json -Compress
}

function Test-CommandLineTargetsProject {
    param([string]$CommandLine, [string]$CanonicalProjectPath)

    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return $false
    }

    return (
        $CommandLine.Contains($CanonicalProjectPath) -or
        $CommandLine.Contains($CanonicalProjectPath.Replace('\', '/'))
    )
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$canonicalProjectPath = Resolve-RequiredDirectory `
    -LiteralPath (Join-Path $repoRoot 'godot') `
    -Description 'Canonical Godot project'
$requestedProjectPath = if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $canonicalProjectPath
}
else {
    Resolve-RequiredDirectory -LiteralPath $ProjectPath -Description 'Requested Godot project'
}

if (-not $requestedProjectPath.Equals(
        $canonicalProjectPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing non-canonical Godot project: $requestedProjectPath"
}

[void](Resolve-RequiredFile `
    -LiteralPath (Join-Path $requestedProjectPath 'project.godot') `
    -Description 'Godot project')
$resolvedGodotExecutable = Resolve-RequiredFile `
    -LiteralPath $GodotExecutable `
    -Description 'Godot GUI executable'

if ($Action -eq 'Close') {
    if ($EditorProcessId -le 0) {
        throw 'Close requires a positive EditorProcessId from session_manage.'
    }

    if ($DryRun) {
        Write-Result @{
            action = 'close'
            status = 'planned'
            editorProcessId = $EditorProcessId
            projectPath = $requestedProjectPath
            executable = $resolvedGodotExecutable
        }
        exit 0
    }

    $processRecord = Get-CimInstance Win32_Process -Filter "ProcessId = $EditorProcessId"
    if ($null -eq $processRecord) {
        throw "Godot Editor process does not exist: $EditorProcessId"
    }
    if ([string]::IsNullOrWhiteSpace($processRecord.ExecutablePath)) {
        throw "Cannot verify Godot Editor executable for PID $EditorProcessId"
    }
    $actualExecutable = [System.IO.Path]::GetFullPath($processRecord.ExecutablePath)
    if (-not $actualExecutable.Equals(
            $resolvedGodotExecutable,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "PID $EditorProcessId is not the pinned Godot GUI executable: $actualExecutable"
    }
    if (-not (Test-CommandLineTargetsProject `
            -CommandLine $processRecord.CommandLine `
            -CanonicalProjectPath $requestedProjectPath)) {
        throw "PID $EditorProcessId is not running the canonical Godot project: $requestedProjectPath"
    }

    $editorProcess = [System.Diagnostics.Process]::GetProcessById($EditorProcessId)
    if (-not $editorProcess.CloseMainWindow()) {
        throw "Godot Editor did not accept a normal window-close request: PID $EditorProcessId"
    }
    if (-not $editorProcess.WaitForExit($TimeoutSeconds * 1000)) {
        throw (
            "GODOT_EDITOR_CLOSE_TIMEOUT: PID $EditorProcessId remained open after " +
            "$TimeoutSeconds seconds. Preserve the process; do not force termination."
        )
    }

    Write-Result @{
        action = 'close'
        status = 'closed'
        editorProcessId = $EditorProcessId
        projectPath = $requestedProjectPath
        executable = $resolvedGodotExecutable
    }
    exit 0
}

$supportedLauncher = Resolve-RequiredFile `
    -LiteralPath (Join-Path $repoRoot 'Tools\godot\Open-GodotDev.ps1') `
    -Description 'Supported Godot development launcher'

if ($DryRun) {
    Write-Result @{
        action = 'open'
        status = 'planned'
        projectPath = $requestedProjectPath
        executable = $resolvedGodotExecutable
        launcher = $supportedLauncher
    }
    exit 0
}

$matchingProcesses = @(
    Get-CimInstance Win32_Process | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_.ExecutablePath) -and
        ([System.IO.Path]::GetFullPath($_.ExecutablePath)).Equals(
            $resolvedGodotExecutable,
            [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-CommandLineTargetsProject `
            -CommandLine $_.CommandLine `
            -CanonicalProjectPath $requestedProjectPath)
    }
)
if ($matchingProcesses.Count -gt 0) {
    $matchingIds = $matchingProcesses.ProcessId -join ', '
    throw "Canonical Godot Editor is already running; refusing a duplicate launch: $matchingIds"
}

# Agent restoration is a prepared development launch, not the supported Human
# Project Manager direct-open path. The canonical launcher owns the serial Adapter
# build, worktree user data, bootstrap argument, mutex, Codex config, and session record.
$launcherOutput = @(& $supportedLauncher `
    -Mode Agent `
    -UserDataProfile Worktree `
    -GodotExecutable $resolvedGodotExecutable)
if (-not $?) {
    throw 'Supported Godot development launcher failed during Editor restoration.'
}
$startedLine = $launcherOutput | Where-Object { $_ -match '^GODOT_EDITOR_STARTED pid=(\d+) project=' } | Select-Object -Last 1
if ($null -eq $startedLine) {
    throw "Supported Godot development launcher did not report a started Editor. Output: $($launcherOutput -join ' | ')"
}
$startedPid = [int]([regex]::Match([string]$startedLine, '^GODOT_EDITOR_STARTED pid=(\d+)').Groups[1].Value)

Write-Result @{
    action = 'open'
    status = 'started'
    editorProcessId = $startedPid
    projectPath = $requestedProjectPath
    executable = $resolvedGodotExecutable
    launcher = $supportedLauncher
}
