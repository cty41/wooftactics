[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,
    [Parameter(Mandatory = $true)]
    [string]$DestinationRoot,
    [string]$BranchName = 'main',
    [string]$CommitMessage = 'feat: publish the Godot Tactics source root'
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $SourceRoot).Path
$destination = [IO.Path]::GetFullPath($DestinationRoot)
if (Test-Path -LiteralPath $destination) {
    throw "Public candidate destination already exists: $destination"
}

Push-Location $source
try {
    & (Join-Path $source 'Tools/public-release/Verify-PublicRelease.ps1') -Root $source -Candidate
    if ($LASTEXITCODE -ne 0) { throw 'Source tree failed public release verification.' }

    $tracked = @(git ls-files)
    if ($LASTEXITCODE -ne 0 -or $tracked.Count -eq 0) {
        throw 'Unable to enumerate the public candidate source tree.'
    }
    $dirty = @(git status --porcelain)
    if ($dirty.Count -ne 0) {
        throw 'Public candidate source must be committed and clean before history reconstruction.'
    }

    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($relative in $tracked) {
        $sourcePath = [IO.Path]::GetFullPath((Join-Path $source $relative))
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Tracked public file is missing: $relative"
        }
        $targetPath = Join-Path $destination $relative
        $targetDirectory = Split-Path -Parent $targetPath
        if (-not (Test-Path -LiteralPath $targetDirectory)) {
            New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        }
        Copy-Item -LiteralPath $sourcePath -Destination $targetPath
    }

    git -C $destination init --initial-branch=$BranchName
    if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize the public candidate repository.' }
    git -C $destination lfs install --local
    # The source inventory is authoritative. Some deliberately tracked files
    # (for example the root .slnx) also match broad convenience ignore rules,
    # so reconstruction must not re-interpret .gitignore.
    git -C $destination add --force --all
    git -C $destination -c user.name=cty41 -c user.email=opensource@users.noreply.github.com `
        commit -m $CommitMessage
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create the public root commit.' }

    $commitCount = (git -C $destination rev-list --count HEAD).Trim()
    $parentLine = (git -C $destination rev-list --parents -n 1 HEAD).Trim().Split(' ')
    if ($commitCount -ne '1' -or $parentLine.Count -ne 1) {
        throw "Public candidate history is not a single root commit: count=$commitCount parents=$($parentLine.Count - 1)"
    }
    $candidateTracked = @(git -C $destination ls-files)
    if ($LASTEXITCODE -ne 0 -or $candidateTracked.Count -ne $tracked.Count) {
        throw "Public candidate tracked-file inventory drifted: source=$($tracked.Count) candidate=$($candidateTracked.Count)"
    }
    & (Join-Path $destination 'Tools/public-release/Verify-PublicRelease.ps1') `
        -Root $destination -Candidate
    if ($LASTEXITCODE -ne 0) { throw 'Reconstructed public root failed public release verification.' }
    Write-Host "Public root candidate created: $destination"
    Write-Host "Root commit: $(git -C $destination rev-parse HEAD)"
}
finally {
    Pop-Location
}
