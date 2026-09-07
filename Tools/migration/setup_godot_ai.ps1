param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,
    [string]$Source = "D:\codes\godot-ai",
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$rootPath = (Resolve-Path -LiteralPath $Root).Path
$refresh = Join-Path $PSScriptRoot 'refresh_godot_ai_vendor.py'

python $refresh --root $rootPath --source $sourcePath --tag $Tag
if ($LASTEXITCODE -ne 0) {
    throw "godot-ai vendor refresh failed with exit code $LASTEXITCODE."
}

Write-Output "Review the complete vendor, manifest, dependency inventory, and notice diff before committing. In-Dock self-update remains unsupported."
