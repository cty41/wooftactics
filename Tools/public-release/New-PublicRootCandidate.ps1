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

function Assert-PinnedGitBytes {
    param([string]$RepositoryRoot, [string]$RepositoryPath, [string]$ObjectId, [string]$WorkingFile, [string]$DisplayPath)
    $filter = (git -C $RepositoryRoot check-attr filter -- $RepositoryPath) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Unable to inspect Git attributes: $DisplayPath" }
    if ($filter -match ': filter: lfs$') {
        $pointer = (git -C $RepositoryRoot cat-file -p $ObjectId) -join "`n"
        if ($LASTEXITCODE -ne 0 -or $pointer -notmatch 'oid sha256:(?<oid>[0-9a-f]{64})') {
            throw "Tracked LFS pointer is invalid: $DisplayPath"
        }
        $expectedSha256 = $Matches.oid
        if ($pointer -notmatch '(?m)^size (?<size>\d+)$') { throw "Tracked LFS pointer is invalid: $DisplayPath" }
        $expectedSize = [long]$Matches.size
        $actualSha256 = (Get-FileHash -LiteralPath $WorkingFile -Algorithm SHA256).Hash.ToLowerInvariant()
        $actualSize = (Get-Item -LiteralPath $WorkingFile).Length
        if ($actualSha256 -ne $expectedSha256 -or $actualSize -ne $expectedSize) {
            throw "Tracked LFS file differs from its pinned object: $DisplayPath"
        }
        return
    }
    $actualObject = (git -C $RepositoryRoot hash-object --no-filters -- $WorkingFile).Trim()
    if ($LASTEXITCODE -ne 0 -or $actualObject -ne $ObjectId) {
        throw "Tracked public file differs from its pinned Git object: $DisplayPath"
    }
}

$source = (Resolve-Path -LiteralPath $SourceRoot).Path
$destination = [IO.Path]::GetFullPath($DestinationRoot)
if (Test-Path -LiteralPath $destination) {
    throw "Public candidate destination already exists: $destination"
}

Push-Location $source
try {
    python Tools/public-release/validate_public_candidate.py --root $source --candidate
    if ($LASTEXITCODE -ne 0) { throw 'Source tree failed the public candidate policy.' }

    $trackedEntries = @(git ls-files --stage)
    if ($LASTEXITCODE -ne 0 -or $trackedEntries.Count -eq 0) {
        throw 'Unable to enumerate the public candidate source tree.'
    }
    $dirty = @(git status --porcelain)
    if ($dirty.Count -ne 0) {
        throw 'Public candidate source must be committed and clean before history reconstruction.'
    }
    $trackedFiles = [Collections.Generic.List[object]]::new()
    $gitlinks = [Collections.Generic.List[object]]::new()
    foreach ($entry in $trackedEntries) {
        if ($entry -notmatch '^(?<mode>\d{6})\s+(?<object>[0-9a-f]+)\s+\d+\t(?<path>.+)$') {
            throw "Unable to parse tracked public entry: $entry"
        }
        if ($Matches.mode -eq '160000') {
            $gitlinks.Add([ordered]@{ path = $Matches.path; commit = $Matches.object })
        } else {
            $trackedFiles.Add([ordered]@{
                path = $Matches.path
                repositoryRoot = $source
                repositoryPath = $Matches.path
                object = $Matches.object
            })
        }
    }
    foreach ($gitlink in $gitlinks) {
        $gitlinkPath = [string]$gitlink.path
        $submoduleRoot = Join-Path $source $gitlinkPath
        if (-not (Test-Path -LiteralPath $submoduleRoot -PathType Container)) {
            throw "Tracked submodule is not materialized: $gitlinkPath"
        }
        $submoduleCommit = (git -C $submoduleRoot rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or $submoduleCommit -ne [string]$gitlink.commit) {
            throw "Tracked submodule is not at its pinned commit: $gitlinkPath"
        }
        $submoduleStatus = @(git -C $submoduleRoot status --porcelain)
        if ($LASTEXITCODE -ne 0 -or $submoduleStatus.Count -ne 0) {
            throw "Tracked submodule is not clean: $gitlinkPath"
        }
        foreach ($submoduleEntry in @(git -C $submoduleRoot ls-files --stage)) {
            if ($submoduleEntry -notmatch '^(?<mode>\d{6})\s+(?<object>[0-9a-f]+)\s+\d+\t(?<path>.+)$' -or
                $Matches.mode -eq '160000') {
                throw "Unable to parse tracked submodule entry: $gitlinkPath/$submoduleEntry"
            }
            $trackedFiles.Add([ordered]@{
                path = "$gitlinkPath/$($Matches.path)"
                repositoryRoot = $submoduleRoot
                repositoryPath = $Matches.path
                object = $Matches.object
            })
        }
        if ($LASTEXITCODE -ne 0) { throw "Unable to enumerate tracked submodule files: $gitlinkPath" }
    }

    New-Item -ItemType Directory -Path $destination | Out-Null
    $expectedPaths = [Collections.Generic.List[string]]::new()
    foreach ($trackedFile in $trackedFiles) {
        $relative = [string]$trackedFile.path
        $sourcePath = [IO.Path]::GetFullPath((Join-Path $source $relative))
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Tracked public file is missing: $relative"
        }
        Assert-PinnedGitBytes -RepositoryRoot ([string]$trackedFile.repositoryRoot) `
            -RepositoryPath ([string]$trackedFile.repositoryPath) -ObjectId ([string]$trackedFile.object) `
            -WorkingFile $sourcePath -DisplayPath $relative
        $targetPath = Join-Path $destination $relative
        $targetDirectory = Split-Path -Parent $targetPath
        if (-not (Test-Path -LiteralPath $targetDirectory)) {
            New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        }
        Copy-Item -LiteralPath $sourcePath -Destination $targetPath
        $expectedPaths.Add($relative.Replace('\', '/'))
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
    $inventoryDrift = @(Compare-Object -ReferenceObject @($expectedPaths | Sort-Object) `
        -DifferenceObject @($candidateTracked | Sort-Object))
    if ($LASTEXITCODE -ne 0 -or $inventoryDrift.Count -ne 0) {
        throw "Public candidate tracked-file inventory drifted: source=$($expectedPaths.Count) candidate=$($candidateTracked.Count)"
    }
    python (Join-Path $destination 'Tools/public-release/validate_public_candidate.py') `
        --root $destination --candidate
    if ($LASTEXITCODE -ne 0) { throw 'Reconstructed public root failed policy validation.' }
    Write-Host "Public root candidate created: $destination"
    Write-Host "Root commit: $(git -C $destination rev-parse HEAD)"
}
finally {
    Pop-Location
}
