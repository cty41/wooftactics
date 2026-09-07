[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$Candidate
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path -LiteralPath $Root).Path

function Invoke-Checked([string]$Description, [scriptblock]$Action) {
    Write-Host "== $Description =="
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Description failed with exit code $LASTEXITCODE." }
}

Push-Location $rootPath
try {
    Invoke-Checked 'Run public release policy unittest' {
        python -m unittest discover -s (Join-Path $rootPath 'Tools/public-release/tests') -p 'test_*.py'
    }
    Invoke-Checked 'Validate public source policy' {
        $arguments = @('Tools/public-release/validate_public_candidate.py', '--root', $rootPath)
        if ($Candidate) { $arguments += '--candidate' }
        python @arguments
    }
    Invoke-Checked 'Validate public dependency inventory' {
        python 'Tools/public-release/generate_dependency_report.py' --root $rootPath --check
    }
    Invoke-Checked 'Validate third-party notices' {
        python 'Tools/public-release/validate_third_party_notices.py' --root $rootPath
    }
    Write-Host "Public release verification passed: $rootPath"
}
finally {
    Pop-Location
}
