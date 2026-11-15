function Get-TacticsPythonExecutable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    $resolver = Join-Path $RepoRoot 'Tools/godot/lib/resolve_python.py'
    if (-not (Test-Path -LiteralPath $resolver -PathType Leaf)) {
        throw "Python resolver script not found: $resolver"
    }

    $bootstrap = $null
    if (-not [string]::IsNullOrWhiteSpace($env:PYTHON)) {
        if (Test-Path -LiteralPath $env:PYTHON -PathType Leaf) {
            $bootstrap = $env:PYTHON
        }
    }
    if (-not $bootstrap) {
        foreach ($name in @('python3', 'python')) {
            $command = Get-Command $name -ErrorAction SilentlyContinue
            if ($command) {
                $bootstrap = $command.Source
                break
            }
        }
    }
    if (-not $bootstrap) {
        throw 'No Python interpreter found. Set PYTHON to an executable path or install python3.'
    }

    $resolved = (& $bootstrap $resolver '--print-executable' 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($resolved)) {
        throw "Failed to resolve Python executable using $resolver (bootstrap: $bootstrap)."
    }
    return $resolved
}

Export-ModuleMember -Function Get-TacticsPythonExecutable
