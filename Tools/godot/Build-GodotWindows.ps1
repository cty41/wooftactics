[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GodotExecutable,

    [string]$OutputDirectory = 'Build/Godot',

    [ValidateSet('Debug', 'Release', 'Both')]
    [string]$BuildFlavor = 'Both',

    [string]$SourceManifestPath = '',

    [string]$SourceCommit = '',

    [string]$WorkflowRunId = '',

    [string]$WorkflowRef = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectRoot = Join-Path $repoRoot 'godot'
$projectFile = Join-Path $projectRoot 'project.godot'
$exportPreset = Join-Path $projectRoot 'export_presets.cfg'
$solution = Join-Path $repoRoot 'Tactics.Godot.slnx'
$runSettings = Join-Path $repoRoot 'Tactics.Godot.runsettings'
$adapterProject = Join-Path $projectRoot 'Tactics.Godot.Adapter.csproj'
$toolingManifest = Join-Path $repoRoot 'Tools\migration\manifest\godot-tooling.json'
$packageValidator = Join-Path $repoRoot 'Tools\godot\Test-GodotWindowsPackage.ps1'
$sessionModule = Join-Path $PSScriptRoot 'GodotDevSession.psm1'
Import-Module $sessionModule -Force
$editorLaunchArgument = Get-TacticsGodotLaunchArgument

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    Write-Host "== $Description =="
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

foreach ($requiredFile in @(
    $GodotExecutable,
    $projectFile,
    $exportPreset,
    $solution,
    $runSettings,
    $adapterProject,
    $toolingManifest,
    $packageValidator)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required build file not found: $requiredFile"
    }
}

$trackedProjects = @(git -C $repoRoot ls-files -- 'project.godot' '*/project.godot')
if ($LASTEXITCODE -ne 0 -or $trackedProjects.Count -ne 1 -or $trackedProjects[0] -ne 'godot/project.godot') {
    throw "Expected exactly one tracked Godot project at godot/project.godot; found: $($trackedProjects -join ', ')"
}

$tooling = Get-Content -LiteralPath $toolingManifest -Raw | ConvertFrom-Json
$expectedGodotVersion = [string]$tooling.godotVersion
$expectedDotnetSdk = [string]$tooling.dotnetSdk
$actualGodotVersion = (& $GodotExecutable --version).Trim()
$normalizedExpectedGodotVersion = $expectedGodotVersion.Replace('-', '.')
if ($LASTEXITCODE -ne 0 -or $actualGodotVersion -notlike "$normalizedExpectedGodotVersion*") {
    throw "Expected Godot '$expectedGodotVersion', found '$actualGodotVersion'."
}
$actualDotnetSdk = (dotnet --version).Trim()
$expectedDotnetVersion = [Version]$expectedDotnetSdk
$actualDotnetVersion = [Version]$actualDotnetSdk
$sameFeatureBand = $actualDotnetVersion.Major -eq $expectedDotnetVersion.Major -and
    $actualDotnetVersion.Minor -eq $expectedDotnetVersion.Minor -and
    [Math]::Floor($actualDotnetVersion.Build / 100) -eq [Math]::Floor($expectedDotnetVersion.Build / 100)
if ($LASTEXITCODE -ne 0 -or -not $sameFeatureBand -or $actualDotnetVersion -lt $expectedDotnetVersion) {
    throw "Expected .NET SDK '$expectedDotnetSdk' or a newer patch in the same feature band, found '$actualDotnetSdk'."
}

$resolvedSourceCommit = if ([string]::IsNullOrWhiteSpace($SourceCommit)) {
    (git -C $repoRoot rev-parse HEAD).Trim()
} else { $SourceCommit }
if ([string]::IsNullOrWhiteSpace($resolvedSourceCommit)) { throw 'SourceCommit is required.' }

$outputRoot = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
}
$buildRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'Build'))
$buildPrefix = $buildRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $outputRoot.StartsWith($buildPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must resolve below '$buildRoot'; found '$outputRoot'."
}
$selectedFlavors = if ($BuildFlavor -eq 'Both') { @('Debug', 'Release') } else { @($BuildFlavor) }

$statusBefore = @(git -C $repoRoot status --porcelain=v1 --untracked-files=no)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to capture the initial tracked worktree state.'
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
foreach ($flavor in $selectedFlavors) {
    $selectedOutput = Join-Path $outputRoot ("Windows-" + $flavor)
    if (Test-Path -LiteralPath $selectedOutput) {
        Remove-Item -LiteralPath $selectedOutput -Recurse -Force
    }
}
$releaseVerificationDirectory = Join-Path $env:TEMP ("tactics-godot-ci-release-" + [Guid]::NewGuid().ToString('N'))
$exportArtifactsDirectory = Join-Path $env:TEMP ("tactics-godot-ci-artifacts-" + [Guid]::NewGuid().ToString('N'))
$exportGodotProjectDirectory = Join-Path $exportArtifactsDirectory 'godot-project'
$editorAssetsPath = Join-Path $projectRoot '.godot\mono\temp\obj\project.assets.json'

Push-Location $repoRoot
try {
    $env:GODOT_BIN = $GodotExecutable

    Invoke-Checked 'Restore locked Godot dependencies' {
        dotnet restore $solution --locked-mode
        if ($LASTEXITCODE -eq 0) { dotnet restore $adapterProject --locked-mode -r win-x64 }
    }
    Invoke-Checked 'Build Godot projects with one MSBuild node' {
        dotnet build $solution -c Debug --no-restore -m:1
    }
    Invoke-Checked 'Run Tactics.Core tests' {
        dotnet test 'src/Tactics.Core.Tests/Tactics.Core.Tests.csproj' -c Debug --no-restore --no-build --settings $runSettings --logger 'console;verbosity=minimal'
    }
    Invoke-Checked 'Run Tactics.Application tests' {
        dotnet test 'src/Tactics.Application.Tests/Tactics.Application.Tests.csproj' -c Debug --no-restore --no-build --settings $runSettings --logger 'console;verbosity=minimal'
    }
    Invoke-Checked 'Scan Godot project in headless Editor' {
        & $GodotExecutable --headless --editor --path $projectRoot --quit-after 6000 -- $editorLaunchArgument
    }

    if ($selectedFlavors -contains 'Debug') {
        Invoke-Checked 'Build production Godot adapter ExportDebug' {
            dotnet build $adapterProject -c ExportDebug --no-restore -m:1
        }
    }
    if ($selectedFlavors -contains 'Release') {
        New-Item -ItemType Directory -Path $releaseVerificationDirectory -Force | Out-Null
        Invoke-Checked 'Build production Godot adapter ExportRelease' {
            dotnet build $adapterProject -c ExportRelease --no-restore -m:1 --output $releaseVerificationDirectory
        }
        if (-not (Test-Path -LiteralPath $editorAssetsPath -PathType Leaf)) {
            throw "Godot Editor dependency graph is missing: $editorAssetsPath"
        }
        $editorAssetsText = Get-Content -LiteralPath $editorAssetsPath -Raw
        if ($editorAssetsText -notmatch 'GodotSharpEditor/4\.7\.1') {
            throw 'Godot Editor dependency graph does not contain GodotSharpEditor/4.7.1 before ExportRelease publish.'
        }
        $editorAssetsHashBeforeExport = (Get-FileHash -LiteralPath $editorAssetsPath -Algorithm SHA256).Hash
        Invoke-Checked 'Publish production Godot adapter for Windows ExportRelease' {
            dotnet publish $adapterProject -c ExportRelease -r win-x64 --self-contained true -p:GodotTargetPlatform=windows `
                -p:GodotProjectDir=$exportGodotProjectDirectory --artifacts-path $exportArtifactsDirectory `
                -m:1 --output $releaseVerificationDirectory
        }
        $editorAssetsHashAfterExport = (Get-FileHash -LiteralPath $editorAssetsPath -Algorithm SHA256).Hash
        if ($editorAssetsHashAfterExport -ne $editorAssetsHashBeforeExport) {
            throw 'ExportRelease publish changed the Godot Editor dependency graph despite isolated artifacts.'
        }
        $forbiddenReleaseFiles = @(
            Get-ChildItem -LiteralPath $releaseVerificationDirectory -File -Recurse |
                Where-Object { $_.Name -match '^(GdUnit|Microsoft\.TestPlatform|testhost)' }
        )
        if ($forbiddenReleaseFiles.Count -gt 0) {
            throw "Release verification output contains test dependencies: $($forbiddenReleaseFiles.Name -join ', ')"
        }
        $releaseDependencyFile = Join-Path $releaseVerificationDirectory 'Tactics.Godot.Adapter.deps.json'
        $releaseDependencies = Get-Content -LiteralPath $releaseDependencyFile -Raw
        if ($releaseDependencies -match 'gdUnit4|Microsoft\.TestPlatform|testhost') {
            throw 'Release dependency manifest contains test dependencies.'
        }
    }

    Invoke-Checked 'Validate playable run UI in Compatibility mode' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --validate-playable-run-ui --quit-after 6000
    }
    Invoke-Checked 'Validate canonical catalog in Compatibility mode' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --validate-buffs-items --quit-after 6000
    }

    foreach ($flavor in $selectedFlavors) {
        $configuration = if ($flavor -eq 'Debug') { 'ExportDebug' } else { 'ExportRelease' }
        $exportSwitch = if ($flavor -eq 'Debug') { '--export-debug' } else { '--export-release' }
        $outputPath = Join-Path $outputRoot ("Windows-" + $flavor)
        New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
        $exportExecutable = Join-Path $outputPath 'Tactics.exe'
        Write-Host "== Export Windows Desktop $flavor =="
        $exportOutput = @(& $GodotExecutable --verbose --headless --path $projectRoot $exportSwitch 'Windows Desktop' $exportExecutable 2>&1)
        $exportExitCode = $LASTEXITCODE
        $exportOutput | ForEach-Object { Write-Output $_ }
        if ($exportExitCode -ne 0) {
            throw "Export Windows Desktop $flavor failed with exit code $exportExitCode."
        }
        $exportErrors = @($exportOutput | Where-Object {
            $line = [string]$_
            $line -match '^ERROR:' -and
                $line -notmatch "^ERROR: [0-9]+ RID allocations of type '.+' were leaked at exit\.$"
        })
        if ($exportErrors.Count -gt 0) {
            throw "Godot reported $flavor export errors despite exit code 0: $($exportErrors -join ' | ')"
        }
        foreach ($requiredOutput in @($exportExecutable, [IO.Path]::ChangeExtension($exportExecutable, '.pck'))) {
            if (-not (Test-Path -LiteralPath $requiredOutput -PathType Leaf)) {
                throw "Expected exported file is missing: $requiredOutput"
            }
        }
        $managedAssemblies = @(Get-ChildItem -LiteralPath $outputPath -Filter '*.dll' -File -Recurse)
        $managedPayloadMode = if ($managedAssemblies.Count -gt 0) { 'LooseAssemblies' } else { 'PckEmbedded' }
        $forbiddenExportFiles = @(
            Get-ChildItem -LiteralPath $outputPath -File -Recurse |
                Where-Object { $_.Name -match '^(GdUnit|Microsoft\.TestPlatform|testhost)' }
        )
        if ($forbiddenExportFiles.Count -gt 0) {
            throw "Exported package contains test dependencies: $($forbiddenExportFiles.Name -join ', ')"
        }

        $files = @(Get-ChildItem -LiteralPath $outputPath -File -Recurse | Sort-Object FullName)
        $outputPrefixLength = $outputPath.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1
        $manifest = [ordered]@{
            commit = $resolvedSourceCommit
            godotVersion = $actualGodotVersion
            dotnetSdk = $actualDotnetSdk
            exportMode = $flavor.ToLowerInvariant()
            configuration = $configuration
            managedPayloadMode = $managedPayloadMode
            files = @($files | ForEach-Object {
                [ordered]@{
                    path = $_.FullName.Substring($outputPrefixLength).Replace('\', '/')
                    size = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            })
        }
        $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputPath 'build-manifest.json') -Encoding utf8

        $resolvedSourceManifest = if ([string]::IsNullOrWhiteSpace($SourceManifestPath)) {
            $fallback = Join-Path $outputPath 'rc-source-manifest.json'
            [ordered]@{
                schemaVersion = 1
                sourceCommit = $resolvedSourceCommit
                boundary = 'current-tracked-worktree-v1'
                fileCount = 0
                files = @()
            } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $fallback -Encoding utf8
            $fallback
        } else {
            $candidate = if ([IO.Path]::IsPathRooted($SourceManifestPath)) {
                [IO.Path]::GetFullPath($SourceManifestPath)
            } else { [IO.Path]::GetFullPath((Join-Path $repoRoot $SourceManifestPath)) }
            if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
                throw "Source manifest not found: $candidate"
            }
            $destination = Join-Path $outputPath 'rc-source-manifest.json'
            Copy-Item -LiteralPath $candidate -Destination $destination -Force
            $destination
        }
        & $packageValidator -PackageRoot $outputPath -SourceManifestPath $resolvedSourceManifest `
            -SourceCommit $resolvedSourceCommit -GodotVersion $actualGodotVersion -DotnetSdk $actualDotnetSdk `
            -ExportMode $flavor -Configuration $configuration -WorkflowRunId $WorkflowRunId -WorkflowRef $WorkflowRef `
            -ManagedPayloadMode $managedPayloadMode
        if ($LASTEXITCODE -ne 0) { throw "Windows $flavor package audit failed with exit code $LASTEXITCODE." }
        Write-Host "Windows $flavor export passed: $exportExecutable"
    }

    $statusAfter = @(git -C $repoRoot status --porcelain=v1 --untracked-files=no)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to capture the final tracked worktree state.'
    }
    if ((Compare-Object $statusBefore $statusAfter).Count -ne 0) {
        throw "The CI build changed tracked files.`nBefore:`n$($statusBefore -join "`n")`nAfter:`n$($statusAfter -join "`n")"
    }
}
finally {
    if (Test-Path -LiteralPath $releaseVerificationDirectory -PathType Container) {
        [IO.Directory]::Delete($releaseVerificationDirectory, $true)
    }
    if (Test-Path -LiteralPath $exportArtifactsDirectory -PathType Container) {
        [IO.Directory]::Delete($exportArtifactsDirectory, $true)
    }
    Pop-Location
}
