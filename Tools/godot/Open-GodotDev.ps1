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
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'Tools\migration\manifest\godot-tooling.json') -Raw | ConvertFrom-Json
$sessionModule = Join-Path $PSScriptRoot 'GodotDevSession.psm1'
$buildHelper = Join-Path $PSScriptRoot 'Build-GodotDevAssembly.ps1'
Import-Module $sessionModule -Force
$editorLaunchArgument = Get-TacticsGodotLaunchArgument

if ($Mode -eq 'Agent' -and $UserDataProfile -eq 'SharedManualQA') {
    throw 'SharedManualQA is a Human-only profile; Agent sessions must use worktree-isolated user data.'
}
foreach ($required in @($GodotExecutable, $projectFile, $buildHelper, (Join-Path $projectRoot 'addons\godot_ai\plugin.cfg'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required Godot development file is missing: $required" }
}
$expectedGodotVersion = ([string]$manifest.godotVersion).Split('-')[0]
$actualGodotVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($GodotExecutable).FileVersion
if ($actualGodotVersion -ne $expectedGodotVersion) {
    throw "Expected Godot $expectedGodotVersion, found $actualGodotVersion at $GodotExecutable."
}
$lock = $null
$buildResultPath = Join-Path $env:TEMP ("tactics-godot-dev-build-{0}.json" -f [guid]::NewGuid().ToString('N'))
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

    $buildToken = [guid]::NewGuid().ToString('N')
    & $buildHelper -Token $buildToken -ResultPath $buildResultPath -HeldOperationLock $lock
    $buildExitCode = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $buildResultPath -PathType Leaf)) {
        throw "Godot Adapter build helper did not write its result: $buildResultPath"
    }
    $buildResult = Get-Content -LiteralPath $buildResultPath -Raw | ConvertFrom-Json
    if ($buildResult.token -ne $buildToken) { throw 'Godot Adapter build helper returned a mismatched token.' }
    if ($buildExitCode -ne 0 -or $buildResult.status -ne 'succeeded' -or $buildResult.exitCode -ne 0) {
        throw "Godot Adapter Debug build failed: $($buildResult.firstError)"
    }

    if ($NoLaunch) {
        Write-Output "GODOT_DEV_READY mode=$Mode profile=$UserDataProfile assembly=$($buildResult.assemblyIdentity)"
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
    if (Test-Path -LiteralPath $buildResultPath) { Remove-Item -LiteralPath $buildResultPath -Force }
    Exit-TacticsGodotOperationLock -Lock $lock
}
