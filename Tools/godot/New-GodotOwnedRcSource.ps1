[CmdletBinding()]
param(
    [string]$SourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [Parameter(Mandatory = $true)]
    [string]$DestinationRoot,
    [string]$ManifestPath,
    [switch]$InitializeGit
)

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($SourceRoot)
$destination = [IO.Path]::GetFullPath($DestinationRoot)
if ($destination.Equals($source, [StringComparison]::OrdinalIgnoreCase) -or
    $destination.StartsWith($source.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "RC destination must be outside the source repository: $destination"
}
if (Test-Path -LiteralPath $destination) {
    throw "RC destination already exists: $destination"
}

$excludedPrefixes = @('Build/', 'artifacts/', 'Tools/checkpoint-hash-temp/')

$sourceCommit = (git -C $source rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceCommit)) {
    throw 'Unable to resolve the source commit.'
}
$trackedStatus = @(git -C $source status --porcelain=v1 --untracked-files=no)
if ($LASTEXITCODE -ne 0 -or $trackedStatus.Count -ne 0) {
    throw "RC source must have no tracked modifications: $($trackedStatus -join ', ')"
}
$trackedEntries = @(git -C $source ls-files --stage)
if ($LASTEXITCODE -ne 0 -or $trackedEntries.Count -eq 0) {
    throw 'Unable to enumerate tracked source files.'
}
$trackedFiles = [Collections.Generic.List[string]]::new()
$gitlinks = [Collections.Generic.List[object]]::new()
foreach ($entry in $trackedEntries) {
    if ($entry -notmatch '^(?<mode>\d{6})\s+(?<object>[0-9a-f]+)\s+\d+\t(?<path>.+)$') {
        throw "Unable to parse tracked source entry: $entry"
    }
    if ($Matches.mode -eq '160000') {
        $gitlinks.Add([ordered]@{ path = $Matches.path; commit = $Matches.object })
    } else {
        $trackedFiles.Add($Matches.path)
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
    $submoduleStatus = @(git -C $submoduleRoot status --porcelain=v1 --untracked-files=no)
    if ($LASTEXITCODE -ne 0 -or $submoduleStatus.Count -ne 0) {
        throw "Tracked submodule has modified files: $gitlinkPath"
    }
    $submoduleFiles = @(git -C $submoduleRoot ls-files)
    if ($LASTEXITCODE -ne 0 -or $submoduleFiles.Count -eq 0) {
        throw "Unable to enumerate tracked submodule files: $gitlinkPath"
    }
    foreach ($submoduleFile in $submoduleFiles) {
        $trackedFiles.Add("$gitlinkPath/$submoduleFile")
    }
}
python (Join-Path $source 'Tools/public-release/validate_public_candidate.py') --root $source --candidate
if ($LASTEXITCODE -ne 0) {
    throw 'RC source failed the public candidate policy before staging.'
}

New-Item -ItemType Directory -Path $destination | Out-Null
$copied = [Collections.Generic.List[object]]::new()
foreach ($relativePath in $trackedFiles) {
    $normalized = $relativePath.Replace('\', '/')
    if ($excludedPrefixes | Where-Object { $normalized.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }) {
        continue
    }
    $sourceFile = Join-Path $source $relativePath
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
        throw "Tracked source file is missing or not materialized: $relativePath"
    }
    $destinationFile = Join-Path $destination $relativePath
    $parent = Split-Path -Parent $destinationFile
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }
    Copy-Item -LiteralPath $sourceFile -Destination $destinationFile
    $copied.Add([ordered]@{
        path = $normalized
        size = (Get-Item -LiteralPath $destinationFile).Length
        sourceSha256 = (Get-FileHash -LiteralPath $destinationFile -Algorithm SHA256).Hash.ToLowerInvariant()
        stagedSha256 = ''
    })
}

foreach ($forbidden in @('Assets', 'Packages', 'ProjectSettings', 'src\Tactics.UnityOracle.Tests', '.codex')) {
    if (Test-Path -LiteralPath (Join-Path $destination $forbidden)) {
        throw "Unity or local tooling path leaked into RC source: $forbidden"
    }
}

$isolatedProject = Join-Path $destination 'godot\project.godot'
if (-not (Test-Path -LiteralPath $isolatedProject -PathType Leaf)) {
    throw "Canonical Godot project is missing from RC source: $isolatedProject"
}
$godotSolution = Join-Path $destination 'Tactics.Godot.slnx'
if (-not (Test-Path -LiteralPath $godotSolution -PathType Leaf)) {
    throw "Canonical Godot solution is missing from RC source: $godotSolution"
}
foreach ($entry in $copied) {
    $stagedFile = Join-Path $destination ([string]$entry.path)
    $entry.size = (Get-Item -LiteralPath $stagedFile).Length
    $entry.stagedSha256 = (Get-FileHash -LiteralPath $stagedFile -Algorithm SHA256).Hash.ToLowerInvariant()
}

$manifestFile = if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    Join-Path $destination 'rc-source-manifest.json'
} elseif ([IO.Path]::IsPathRooted($ManifestPath)) {
    [IO.Path]::GetFullPath($ManifestPath)
} else {
    Join-Path $destination $ManifestPath
}
$manifestParent = Split-Path -Parent $manifestFile
if (-not (Test-Path -LiteralPath $manifestParent -PathType Container)) {
    New-Item -ItemType Directory -Path $manifestParent | Out-Null
}
$manifest = [ordered]@{
    schemaVersion = 1
    sourceCommit = $sourceCommit
    boundary = 'public-source-byte-identical-v1'
    excludedPrefixes = $excludedPrefixes
    fileCount = $copied.Count
    files = @($copied | Sort-Object path)
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestFile -Encoding utf8

if ($InitializeGit) {
    git -C $destination init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize the RC staging repository.' }
    git -C $destination config user.name 'Tactics RC Builder'
    git -C $destination config user.email 'rc-builder@invalid.local'
    git -C $destination add --all
    git -C $destination commit --quiet -m "RC source snapshot $sourceCommit"
    if ($LASTEXITCODE -ne 0) { throw 'Unable to commit the RC staging snapshot.' }
}

Write-Output $destination
