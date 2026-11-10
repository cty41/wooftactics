[CmdletBinding()]
param(
    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe',
    # Fail instead of skipping artwork steps when the optional MaLiang submodule is not initialized.
    [switch]$RequireArtwork
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Import-Module (Join-Path $PSScriptRoot 'TacticsPython.psm1') -Force
$tacticsPythonExe = Get-TacticsPythonExecutable -RepoRoot $repoRoot

foreach ($requiredFile in @($GodotExecutable, (Join-Path $repoRoot 'godot\project.godot'))) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required Godot verification file not found: $requiredFile"
    }
}

$verifyArgs = @(
    'Tools/godot/verify_godot_project.py',
    '--profile', 'windows',
    '--godot-executable', $GodotExecutable,
    '--repo-root', $repoRoot
)
if ($RequireArtwork) {
    $verifyArgs += '--require-artwork'
}

& $tacticsPythonExe @verifyArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
