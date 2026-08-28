[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,
    [Parameter(Mandatory = $true)]
    [string]$SourceManifestPath,
    [string]$SourceCommit,
    [string]$GodotVersion,
    [string]$DotnetSdk,
    [ValidateSet('Debug', 'Release')]
    [string]$ExportMode = 'Release',
    [ValidateSet('ExportDebug', 'ExportRelease')]
    [string]$Configuration = 'ExportRelease',
    [string]$WorkflowRunId = '',
    [string]$WorkflowRef = '',
    [ValidateSet('Auto', 'LooseAssemblies', 'PckEmbedded')]
    [string]$ManagedPayloadMode = 'Auto'
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PackageRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Package root not found: $root" }
$exe = Join-Path $root 'Tactics.exe'
$pck = Join-Path $root 'Tactics.pck'
foreach ($required in @($exe, $pck, $SourceManifestPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf) -or (Get-Item $required).Length -eq 0) {
        throw "Required non-empty RC file is missing: $required"
    }
}

# Parse the unencrypted Godot 4 PCK directory; scanning payload bytes would reject harmless path strings.
function Get-GodotPckEntries {
    param([string]$Path)

    $stream = [IO.File]::OpenRead($Path)
    try {
        $reader = [IO.BinaryReader]::new($stream, [Text.Encoding]::UTF8, $true)
        if ($reader.ReadUInt32() -ne 0x43504447) { throw 'Tactics.pck has an invalid GDPC header.' }
        $formatVersion = $reader.ReadUInt32()
        $null = $reader.ReadUInt32() # Engine major.
        $null = $reader.ReadUInt32() # Engine minor.
        $null = $reader.ReadUInt32() # Engine patch.
        $packFlags = $reader.ReadUInt32()
        $null = $reader.ReadUInt64() # File base.
        if (($packFlags -band 1) -ne 0) { throw 'Encrypted PCK directories cannot be audited.' }

        if ($formatVersion -in @(3, 4)) {
            $directoryOffset = $reader.ReadUInt64()
            $stream.Position = $directoryOffset
        }
        elseif ($formatVersion -eq 2) {
            $stream.Position += 64 # Reserved V2 header fields.
        }
        else {
            throw "Unsupported Godot PCK format version: $formatVersion"
        }

        $fileCount = $reader.ReadUInt32()
        if ($fileCount -gt 1000000) { throw "Implausible Godot PCK entry count: $fileCount" }
        for ($index = 0; $index -lt $fileCount; $index++) {
            $pathLength = $reader.ReadUInt32()
            if ($pathLength -gt 1048576) { throw "Implausible Godot PCK path length: $pathLength" }
            $pathBytes = $reader.ReadBytes($pathLength)
            if ($pathBytes.Length -ne $pathLength) { throw 'Godot PCK directory ended inside a path.' }
            [Text.Encoding]::UTF8.GetString($pathBytes)
            $null = $reader.ReadUInt64() # File offset.
            $null = $reader.ReadUInt64() # File size.
            if ($reader.ReadBytes(16).Length -ne 16) { throw 'Godot PCK directory ended inside an MD5 hash.' }
            $null = $reader.ReadUInt32() # Entry flags.
        }
    }
    finally {
        $stream.Dispose()
    }
}

$pckEntries = @(Get-GodotPckEntries -Path $pck)
$forbiddenPckEntries = @($pckEntries | Where-Object {
    $_ -match '(^|/)addons/(godot_ai|tactics_tooling)/' -or $_ -match '(^|/)tests/'
})
if ($forbiddenPckEntries.Count -gt 0) {
    throw "Forbidden Editor/test content is embedded in Tactics.pck: $($forbiddenPckEntries -join ', ')"
}

$stream = [IO.File]::OpenRead($exe)
try {
    $reader = [IO.BinaryReader]::new($stream)
    if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'Tactics.exe is not a PE executable (missing MZ header).' }
    $stream.Position = 0x3c
    $peOffset = $reader.ReadInt32()
    $stream.Position = $peOffset
    if ($reader.ReadUInt32() -ne 0x00004550) { throw 'Tactics.exe is not a PE executable (missing PE signature).' }
    $machine = $reader.ReadUInt16()
    if ($machine -ne 0x8664) { throw ("Expected x86_64 PE machine 0x8664, found 0x{0:x4}." -f $machine) }
} finally { $stream.Dispose() }

$files = @(Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object FullName)
$looseManagedAssemblies = @($files | Where-Object Extension -eq '.dll')
$resolvedManagedPayloadMode = if ($ManagedPayloadMode -eq 'Auto') {
    if ($looseManagedAssemblies.Count -gt 0) { 'LooseAssemblies' } else { 'PckEmbedded' }
} else { $ManagedPayloadMode }
if ($resolvedManagedPayloadMode -eq 'LooseAssemblies' -and $looseManagedAssemblies.Count -eq 0) {
    throw 'RC package declares loose managed assemblies but contains no DLLs.'
}
$forbidden = @($files | Where-Object {
    $_.Name -match '^(GdUnit|Microsoft\.TestPlatform|testhost)' -or
    $_.Name -match '\.meta$' -or
    $_.FullName -match '[\\/](tests|addons[\\/]godot_ai|Assets|Packages|ProjectSettings)[\\/]' -or
    $_.Name -match '^(UnityEngine|UnityEditor).*\.dll$' -or
    $_.Name -match '(?i)(save|backup|quarantine).*\.json$'
})
if ($ExportMode -eq 'Release') {
    $forbidden += @($files | Where-Object Extension -eq '.pdb')
}
if ($forbidden.Count -gt 0) {
    throw "Forbidden RC payload detected: $($forbidden.FullName -join ', ')"
}

$allowedRootFiles = @('Tactics.exe', 'Tactics.pck', 'SHA256SUMS.txt')
$allowedRootExtensions = @('.dll', '.json')
if ($ExportMode -eq 'Debug') { $allowedRootExtensions += '.pdb' }
$unexpectedRootFiles = @(Get-ChildItem -LiteralPath $root -File | Where-Object {
    $_.Name -notin $allowedRootFiles -and $_.Extension -notin $allowedRootExtensions
})
if ($unexpectedRootFiles.Count -gt 0) {
    throw "Unexpected RC root files: $($unexpectedRootFiles.Name -join ', ')"
}
$unexpectedRootDirectories = @(Get-ChildItem -LiteralPath $root -Directory | Where-Object {
    $_.Name -notmatch '^(data_|Tactics_Data$)'
})
if ($unexpectedRootDirectories.Count -gt 0) {
    throw "Unexpected RC root directories: $($unexpectedRootDirectories.Name -join ', ')"
}

$sourceManifestHash = (Get-FileHash -LiteralPath $SourceManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$payloadFiles = @($files | Where-Object { $_.Name -notin @('rc-manifest.json', 'rc-semantic-manifest.json', 'SHA256SUMS.txt') })
$entries = @($payloadFiles | ForEach-Object {
    [ordered]@{
        path = $_.FullName.Substring($root.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1).Replace('\', '/')
        size = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$semantic = [ordered]@{
    schemaVersion = 1
    sourceCommit = $SourceCommit
    sourceBoundary = 'godot-owned-without-unity-v1'
    sourceManifestSha256 = $sourceManifestHash
    godotVersion = $GodotVersion
    dotnetSdk = $DotnetSdk
    exportMode = $ExportMode.ToLowerInvariant()
    configuration = $Configuration
    architecture = 'windows-x86_64'
    managedPayloadMode = $resolvedManagedPayloadMode
    audio = 'deferred_no_payload'
    files = $entries
}
$semanticPath = Join-Path $root 'rc-semantic-manifest.json'
$semantic | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $semanticPath -Encoding utf8
$semanticHash = (Get-FileHash -LiteralPath $semanticPath -Algorithm SHA256).Hash.ToLowerInvariant()
$provenance = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    workflowRunId = $WorkflowRunId
    workflowRef = $WorkflowRef
    semanticManifestSha256 = $semanticHash
}
$provenance | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $root 'rc-manifest.json') -Encoding utf8

$sumFiles = @(Get-ChildItem -LiteralPath $root -File -Recurse |
    Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object FullName)
$sumLines = @($sumFiles | ForEach-Object {
    $relative = $_.FullName.Substring($root.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1).Replace('\', '/')
    "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
})
[IO.File]::WriteAllLines((Join-Path $root 'SHA256SUMS.txt'), $sumLines, [Text.UTF8Encoding]::new($false))

[pscustomobject]@{ SemanticManifestSha256 = $semanticHash; FileCount = $sumFiles.Count }
