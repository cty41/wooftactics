[CmdletBinding()]
param(
    [switch]$ImportFromUser,
    [switch]$Bootstrap,
    [switch]$Check,
    [ValidateSet('phase3-observe', 'content-authoring', 'ui-input', 'presentation')]
    [string]$Profile = 'phase3-observe',
    [string]$ProjectRoot,
    [string]$UserConfig
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Join-Path $PSScriptRoot '..\..'
}
if ([string]::IsNullOrWhiteSpace($UserConfig)) {
    $UserConfig = Join-Path $env:USERPROFILE '.codex\config.toml'
}
$selectedModes = [int][bool]$ImportFromUser + [int][bool]$Bootstrap + [int][bool]$Check
if ($selectedModes -gt 1) {
    throw '-ImportFromUser, -Bootstrap and -Check are mutually exclusive.'
}

$resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
Import-Module (Join-Path $PSScriptRoot 'TacticsPython.psm1') -Force
$tacticsPythonExe = Get-TacticsPythonExecutable -RepoRoot $resolvedRoot
$helper = Join-Path $PSScriptRoot '..\migration\godot_ai_codex_config.py'
$arguments = @(
    $helper,
    '--root', $resolvedRoot,
    '--user-config', $UserConfig
)
if (-not $Check -or $PSBoundParameters.ContainsKey('Profile')) {
    $arguments += @('--profile', $Profile)
}
if ($ImportFromUser) {
    $arguments += '--import-from-user'
}
elseif ($Bootstrap) {
    $arguments += '--bootstrap'
}
elseif ($Check) {
    $arguments += '--check'
}

& $tacticsPythonExe @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Godot AI Codex configuration failed with exit code $LASTEXITCODE."
}

if ($ImportFromUser -or $Bootstrap) {
    Write-Output "CODEX_RESTART_REQUIRED: restart the Codex task from $resolvedRoot so the project-scoped MCP entry is discovered."
}
