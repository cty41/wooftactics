param(
    [string]$Source = "D:\codes\godot-ai",
    [string]$Project = (Join-Path $PSScriptRoot "..\..\godot")
)

$ErrorActionPreference = "Stop"
$sourcePath = (Resolve-Path $Source).Path
$projectPath = (Resolve-Path $Project).Path
python (Join-Path $PSScriptRoot "godot_ai_baseline.py") $sourcePath

$pluginSource = Join-Path $sourcePath "plugin\addons\godot_ai"
$pluginTarget = Join-Path $projectPath "addons\godot_ai"
if (-not $pluginTarget.StartsWith($projectPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to modify a path outside the Godot project: $pluginTarget"
}
if (-not (Test-Path $pluginSource)) {
    throw "godot-ai plugin directory not found: $pluginSource"
}

New-Item -ItemType Directory -Force -Path $pluginTarget | Out-Null
Get-ChildItem -LiteralPath $pluginTarget -Force |
    Where-Object { $_.Name -ne "README.md" } |
    Remove-Item -Recurse -Force
Get-ChildItem -LiteralPath $pluginSource -Force |
    Copy-Item -Destination $pluginTarget -Recurse -Force
Copy-Item -LiteralPath (Join-Path $sourcePath 'LICENSE') -Destination (Join-Path $pluginTarget 'LICENSE') -Force
Write-Output "godot-ai v4.3.1 copied to $pluginTarget. Review and commit the exact vendor diff; in-Dock self-update is unsupported."
