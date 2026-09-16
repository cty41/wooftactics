[CmdletBinding()]
param(
    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe'
)

$ErrorActionPreference = 'Stop'
$GodotOwned = $true
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectRoot = (Resolve-Path (Join-Path $repoRoot 'godot')).Path
$projectRootWithSeparator = $projectRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$projectFile = Join-Path $projectRoot 'project.godot'
$adapterProject = Join-Path $projectRoot 'Tactics.Godot.Adapter.csproj'
$testHostProject = Join-Path $projectRoot 'tests\Tactics.Godot.TestHost.csproj'
$testHostAssembly = Join-Path $projectRoot '.godot\mono\temp\bin\Debug\Tactics.Godot.Adapter.dll'
$solution = Join-Path $repoRoot 'Tactics.Godot.slnx'
$runSettings = Join-Path $repoRoot 'Tactics.Godot.runsettings'
$gdUnitRunnerTemplate = Join-Path $projectRoot 'tests\GdUnit4TestRunnerScene.cs.txt'
$gdUnitRunnerSource = Join-Path $projectRoot 'gdunit4_testadapter_v5\GdUnit4TestRunnerScene.cs'
$createdGdUnitRunnerSource = $false
$operationLock = $null
$testHostMayOwnGodotAssembly = $false
$poisonExport = Join-Path $repoRoot 'Tools\migration\out\poison-spear-lv1.unity.json'
$poisonDraft = Join-Path $repoRoot 'Tools\migration\out\poison-spear-lv1.draft.json'
$poisonSpecification = Join-Path $repoRoot 'Tools\migration\manifest\export-batches\poison-spear-lv1.json'
$poisonExportReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\poison-spear-lv1-export.json'
$poisonGenerationLedger = Join-Path $repoRoot 'Tools\migration\manifest\state\poison-spear-lv1-real.json'
$poisonGenerationReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\poison-spear-lv1-generation.json'
$unitExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-units-v1.unity.json'
$unitDraft = Join-Path $repoRoot 'Tools\migration\out\pure-run-units-v1.draft.json'
$unitGolden = Join-Path $repoRoot 'Tests\golden\unit-batch-v1.json'
$unitSpecification = Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-units-v1.json'
$unitExportReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-units-v1-export.json'
$unitGenerationLedger = Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-units-v1.json'
$unitTextureLedger = Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-unit-textures-v1.json'
$unitGenerationReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-units-v1-generation.json'
$unitGalleryCapture = Join-Path $repoRoot 'Tools\migration\out\pure-run-units-v1-gallery.png'
$unitSpawnCapture = Join-Path $repoRoot 'Tools\migration\out\pure-run-units-v1-spawn.png'
$unitGoatTintShader = Join-Path $projectRoot 'src\Tactics.Godot.Adapter\Runtime\Shaders\GoatBodyTint.gdshader'
$actionPoseManifest = Join-Path $repoRoot 'Tools\migration\manifest\action-poses\pure-run-player-action-poses-v1.json'
$actionPoseReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-player-action-poses-v1-generation.json'
$buffItemExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-buffs-items-v1.unity.json'
$buffItemDraft = Join-Path $repoRoot 'Tools\migration\out\pure-run-buffs-items-v1.draft.json'
$buffItemGolden = Join-Path $repoRoot 'Tests\golden\buff-item-batch-v1.json'
$buffItemSpecification = Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-buffs-items-v1.json'
$buffItemExportReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-buffs-items-v1-export.json'
$buffItemGenerationLedger = Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-buffs-items-v1.json'
$buffItemGenerationReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-buffs-items-v1-generation.json'
$startingSkillExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-starting-skills-v1.unity.json'
$startingSkillDraft = Join-Path $repoRoot 'Tools\migration\out\pure-run-starting-skills-v1.draft.json'
$startingSkillSpecification = Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-starting-skills-v1.json'
$startingSkillExportReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-starting-skills-v1-export.json'
$startingSkillGenerationLedger = Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-starting-skills-v1.json'
$startingSkillGenerationReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-starting-skills-v1-generation.json'
$aiEncounterExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-ai-encounter-v1.unity.json'
$aiEncounterDraft = Join-Path $repoRoot 'Tools\migration\out\pure-run-ai-encounter-v1.draft.json'
$aiEncounterSpecification = Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-ai-encounter-v1.json'
$aiEncounterGenerationLedger = Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-ai-encounter-v1.json'
$runPersistenceExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-persistence-v1.unity.json'
$runPersistenceDraft = Join-Path $repoRoot 'Tools\migration\out\pure-run-persistence-v1.draft.json'
$runPersistenceSpecification = Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-persistence-v1.json'
$runPersistenceExportReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-persistence-v1-export.json'
$runPersistenceGenerationLedger = Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-persistence-v1.json'
$runPersistenceGenerationReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-persistence-v1-generation.json'
$inventoryProgressionExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-inventory-progression-v1.unity.json'
$inventoryProgressionDraft = Join-Path $repoRoot 'Tools\migration\out\pure-run-inventory-progression-v1.draft.json'
$inventoryProgressionSpecification = Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-inventory-progression-v1.json'
$inventoryProgressionGenerationReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-inventory-progression-v1-generation.json'
$ownershipClosureExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-ownership-closure-v1.unity.json'
$ownershipClosureDraft = Join-Path $repoRoot 'Tools\migration\out\pure-run-ownership-closure-v1.draft.json'
$ownershipClosureSpecification = Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-ownership-closure-v1.json'
$consumablesJson = Join-Path $repoRoot 'Assets\Tactics\GameData\Consumables.json'
$equipmentJson = Join-Path $repoRoot 'Assets\Tactics\GameData\Equipment.json'
$systemTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$releaseVerificationDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $systemTempRoot ("tactics-godot-release-" + [Guid]::NewGuid().ToString('N'))))
$requiredTempPrefix = $systemTempRoot + [System.IO.Path]::DirectorySeparatorChar
if (-not $releaseVerificationDirectory.StartsWith($requiredTempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Release verification directory escaped the system temp root: $releaseVerificationDirectory"
}

foreach ($retiredRoot in @('Assets', 'Packages', 'ProjectSettings', 'UIElementsSchema')) {
    if (Test-Path -LiteralPath (Join-Path $repoRoot $retiredRoot)) {
        throw "Godot mainline verification requires retired Unity root to be absent: $retiredRoot"
    }
}

foreach ($requiredFile in @($GodotExecutable, $projectFile, $adapterProject, $testHostProject,
        $solution, $runSettings, $gdUnitRunnerTemplate)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required migration file not found: $requiredFile"
    }
}

$hasGitMetadata = Test-Path -LiteralPath (Join-Path $repoRoot '.git')
if ($hasGitMetadata) {
    $trackedProjects = @(git -C $repoRoot ls-files -- 'project.godot' '*/project.godot')
    if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate tracked Godot projects.' }
    if ($trackedProjects.Count -ne 1 -or $trackedProjects[0] -ne 'godot/project.godot') {
        throw "Expected exactly one tracked Godot project at godot/project.godot; found: $($trackedProjects -join ', ')"
    }
}
else {
    $projectFiles = @(Get-ChildItem -LiteralPath $repoRoot -Filter 'project.godot' -File -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/]\.godot[\\/]' })
    if (-not $GodotOwned -or $projectFiles.Count -ne 1 -or $projectFiles[0].FullName -ne $projectFile) {
        throw "Godot-owned copy must contain exactly the canonical godot/project.godot."
    }
}

function Invoke-Checked {
    param(
        [string]$Description,
        [scriptblock]$Command
    )

    Write-Host "== $Description =="
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE"
    }
}

function Assert-GodotEditorDependencyGraph {
    $assetsPath = Join-Path $projectRoot '.godot\mono\temp\obj\project.assets.json'
    if (-not (Test-Path -LiteralPath $assetsPath -PathType Leaf)) {
        throw "Godot Editor dependency graph is missing after locked restore: $assetsPath"
    }
    $assetsText = Get-Content -LiteralPath $assetsPath -Raw
    if ($assetsText -notmatch 'GodotSharpEditor/4\.7\.1') {
        throw 'Godot Editor dependency graph is missing GodotSharpEditor/4.7.1. Restore the Debug/Editor lock graph before building.'
    }
}

function Invoke-IsolatedGdUnitSuite {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [string]$Filter
    )

    for ($attempt = 1; $attempt -le 2; $attempt++) {
        # A native GdUnit host can remove Godot's temp assembly when it exits. Rebuild only when
        # that isolated output disappeared so the next suite never consumes a missing/stale host.
        if (-not (Test-Path -LiteralPath $testHostAssembly -PathType Leaf)) {
            Invoke-Checked "Rebuild isolated GdUnit4Net test host for $Description" {
                dotnet build $testHostProject -c Debug --no-restore --no-incremental -m:1 `
                    -p:GodotProjectDir=$projectRootWithSeparator
            }
        }
        Write-Host "== $Description (attempt $attempt/2) =="
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = @(
                dotnet test $testHostProject -c Debug --no-restore --no-build --settings $runSettings `
                    -p:GodotProjectDir=$projectRootWithSeparator --filter $Filter `
                    --logger 'console;verbosity=minimal' 2>&1 |
                    ForEach-Object { [string]$_ }
            )
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        $output | ForEach-Object { Write-Output $_ }
        if ($exitCode -eq 0) { return }

        $text = $output -join "`n"
        $knownNativeCrash =
            $text -match 'GodotRuntimeTestRunner ends with exit code: -107374(?:1795|1819)' -or
            $text -match 'GodotRuntimeTestRunner ends with exit code: (?:-1|100)' -or
            $text -match "Value cannot be null\. \(Parameter 'resource'\)"
        $reportedAssertionFailure = $text -match 'Failed:\s+[1-9][0-9]*'
        if ($attempt -eq 1 -and $knownNativeCrash -and -not $reportedAssertionFailure) {
            Write-Warning "$Description lost its native Godot host; retrying once in a fresh process."
            continue
        }
        throw "$Description failed with exit code $exitCode on attempt $attempt."
    }
}

$sessionModule = Join-Path $PSScriptRoot 'GodotDevSession.psm1'
Import-Module $sessionModule -Force
$operationLock = Enter-TacticsGodotOperationLock -RepoRoot $repoRoot
$openEditors = @(Get-TacticsGodotEditorProcess -ProjectRoot $projectRoot)
if ($openEditors.Count -gt 0) {
    Exit-TacticsGodotOperationLock -Lock $operationLock
    $operationLock = $null
    throw "Close the Godot Editor for this worktree before verification. PIDs: $($openEditors.ProcessId -join ', ')"
}

Push-Location $repoRoot
try {
    $env:GODOT_BIN = $GodotExecutable

    $codexProjectConfig = Join-Path $repoRoot '.codex\config.toml'
    if (Test-Path -LiteralPath $codexProjectConfig -PathType Leaf) {
        Invoke-Checked 'Validate project-scoped godot-ai Codex configuration' {
            powershell -NoProfile -ExecutionPolicy Bypass -File 'Tools/godot/Sync-GodotAiCodexConfig.ps1' -Check
        }
    }
    else {
        Write-Host '== Skip project-scoped godot-ai Codex configuration: local config is not present =='
    }

    Invoke-Checked 'Validate pinned Godot AI vendor tree' {
        python 'Tools/migration/godot_ai_vendor.py' --root $repoRoot
    }

    if ($GodotOwned) {
        Invoke-Checked 'Restore locked Godot-owned dependencies' {
            dotnet restore 'src/Tactics.Core.Tests/Tactics.Core.Tests.csproj' --locked-mode
            if ($LASTEXITCODE -eq 0) { dotnet restore 'src/Tactics.Application.Tests/Tactics.Application.Tests.csproj' --locked-mode }
            if ($LASTEXITCODE -eq 0) { dotnet restore 'src/Tactics.FrozenOracle.Tests/Tactics.FrozenOracle.Tests.csproj' --locked-mode }
            if ($LASTEXITCODE -eq 0) { dotnet restore $adapterProject --locked-mode }
            if ($LASTEXITCODE -eq 0) { dotnet restore $testHostProject --locked-mode }
        }
    }
    else {
        Invoke-Checked 'Restore locked migration dependencies' {
            dotnet restore $solution --locked-mode
        }
    }

    Assert-GodotEditorDependencyGraph

    # Build/test steps are intentionally sequential. Separate processes have
    # previously contended for Tactics.Core/obj and produced intermittent locks.
    if ($GodotOwned) {
        Invoke-Checked 'Build Godot-owned projects without Unity Oracle sources' {
            dotnet build 'src/Tactics.Core.Tests/Tactics.Core.Tests.csproj' -c Debug --no-restore -m:1
            if ($LASTEXITCODE -eq 0) { dotnet build 'src/Tactics.Application.Tests/Tactics.Application.Tests.csproj' -c Debug --no-restore -m:1 }
            if ($LASTEXITCODE -eq 0) { dotnet build 'src/Tactics.FrozenOracle.Tests/Tactics.FrozenOracle.Tests.csproj' -c Debug --no-restore -m:1 }
            if ($LASTEXITCODE -eq 0) { dotnet build $adapterProject -c Debug --no-restore -m:1 }
        }
    }
    else {
        Invoke-Checked 'Build migration solution (single MSBuild node)' {
            dotnet build $solution -c Debug --no-restore -m:1
        }
    }

    if ($GodotOwned) {
        Invoke-Checked 'Prime fresh Godot resource UID cache' {
            & $GodotExecutable --headless --editor --path $projectRoot --import
        }
    }

    Invoke-Checked 'Run Tactics.Core NUnit' {
        dotnet test 'src/Tactics.Core.Tests/Tactics.Core.Tests.csproj' -c Debug --no-restore --no-build --settings $runSettings --logger 'console;verbosity=minimal'
    }

    Invoke-Checked 'Run Tactics.Application NUnit' {
        dotnet test 'src/Tactics.Application.Tests/Tactics.Application.Tests.csproj' -c Debug --no-restore --no-build --settings $runSettings --logger 'console;verbosity=minimal'
    }

    Invoke-Checked 'Run repository-owned frozen source Oracle NUnit' {
        dotnet test 'src/Tactics.FrozenOracle.Tests/Tactics.FrozenOracle.Tests.csproj' -c Debug --no-restore --no-build --settings $runSettings --logger 'console;verbosity=minimal'
    }

    Invoke-Checked 'Build development-only Tactics Authoring MCP' {
        dotnet build 'Tools/tactics-authoring-mcp/Tactics.Authoring.Mcp.csproj' -c Release -m:1
    }

    Invoke-Checked 'Run Tactics Authoring MCP protocol tests' {
        python -m unittest Tools.tactics-authoring-mcp.tests.test_protocol
    }

    Invoke-Checked 'Validate canonical Godot content ownership' {
        python -m unittest Tools.migration.tests.test_godot_content_ownership
    }

    Invoke-Checked 'Run agent policy unittest' {
        python -m unittest discover -s 'Tools/agent-policy' -p 'test_*.py'
    }

    Invoke-Checked 'Validate Godot Incidents' {
        python 'Tools/agent-policy/validate_godot_incidents.py'
    }

    foreach ($skill in @(
        'godot-workflow',
        'godot-csharp-development',
        'godot-content-migration',
        'godot-editor-tooling',
        'godot-editor-lifecycle',
        'godot-testing-diagnostics',
        'godot-ai-workflow',
        'manual-qa-handoff')) {
        Invoke-Checked "Validate Codex/OpenCode skill: $skill" {
            powershell -NoProfile -ExecutionPolicy Bypass -File '.agents/scripts/validate-skills.ps1' -SkillsRoot ".agents/skills/$skill"
        }
    }

    Invoke-Checked 'Validate manual QA handoff policy' {
        python Tools/agent-policy/validate_manual_qa_handoff.py
    }

    if ($GodotOwned -and -not (Test-Path -LiteralPath 'Tools/gameplay-test-spec/node_modules/.bin/tsc.cmd')) {
        Invoke-Checked 'Restore gameplay spec compiler dependencies' {
            npm --prefix Tools/gameplay-test-spec ci --ignore-scripts
        }
    }

    Invoke-Checked 'Run gameplay spec compiler tests' {
        try {
            if ($GodotOwned) { $env:GODOT_OWNED_VERIFY = '1' }
            npm --prefix Tools/gameplay-test-spec test
        }
        finally { Remove-Item Env:GODOT_OWNED_VERIFY -ErrorAction SilentlyContinue }
    }

    $godotGameplaySpecSource = Join-Path $repoRoot 'Tests\gameplay-specs\godot'
    $godotGameplaySpecOutput = Join-Path $repoRoot 'artifacts\gameplay-specs\godot'
    $godotGameplayReport = Join-Path $godotGameplaySpecOutput 'godot-gameplay-spec-result-v1.json'
    New-Item -ItemType Directory -Path $godotGameplaySpecOutput -Force | Out-Null
    if (Test-Path -LiteralPath $godotGameplayReport -PathType Leaf) {
        Remove-Item -LiteralPath $godotGameplayReport -Force
    }
    Invoke-Checked 'Batch compile Godot gameplay specs' {
        node Tools/gameplay-test-spec/dist/src/cli.js batch-compile `
            -d $godotGameplaySpecSource -o $godotGameplaySpecOutput --runtime godot
    }
    foreach ($spec in Get-ChildItem -LiteralPath $godotGameplaySpecSource -Filter '*.gameplay-test.md' -File) {
        $name = $spec.Name.Substring(0, $spec.Name.Length - '.gameplay-test.md'.Length)
        $generated = Join-Path $godotGameplaySpecOutput ($name + '.plan.json')
        $tracked = Join-Path $godotGameplaySpecSource ($name + '.plan.json')
        if (-not (Test-Path -LiteralPath $generated -PathType Leaf) -or
            -not (Test-Path -LiteralPath $tracked -PathType Leaf) -or
            (Get-FileHash -LiteralPath $generated -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $tracked -Algorithm SHA256).Hash) {
            throw "Godot gameplay plan is stale or missing: $name"
        }
    }

    Invoke-Checked 'Restore isolated GdUnit4Net test host dependencies' {
        dotnet restore $testHostProject --locked-mode -p:GodotProjectDir=$projectRootWithSeparator
    }

    if (-not (Test-Path -LiteralPath $gdUnitRunnerSource -PathType Leaf)) {
        $runnerDirectory = Split-Path -Parent $gdUnitRunnerSource
        if (-not (Test-Path -LiteralPath $runnerDirectory -PathType Container)) {
            New-Item -ItemType Directory -Path $runnerDirectory | Out-Null
        }
        Copy-Item -LiteralPath $gdUnitRunnerTemplate -Destination $gdUnitRunnerSource
        $createdGdUnitRunnerSource = $true
    }

    Invoke-Checked 'Build isolated GdUnit4Net test host non-incrementally' {
        dotnet build $testHostProject -c Debug --no-restore --no-incremental -m:1 `
            -p:GodotProjectDir=$projectRootWithSeparator
    }
    $testHostMayOwnGodotAssembly = $true

    # GdUnit4Net owns one native Godot runtime per dotnet test invocation. Discover the test suites from
    # their version-controlled declarations and run each in a fresh host; a single long-lived Windows host can retain
    # ResourceLoader/SceneTree state and fail a later, otherwise independent suite nondeterministically.
    $suiteNames = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -Filter '*.cs' -File -Recurse |
        ForEach-Object {
            $sourceText = Get-Content -Raw -LiteralPath $_.FullName
            if ($sourceText -notmatch '\[TestSuite\]') { return }
            $namespaceMatch = [Regex]::Match($sourceText, '(?m)^namespace\s+([^;{]+)[;{]')
            $classMatch = [Regex]::Match($sourceText,
                '(?ms)\[TestSuite\]\s*(?:\[[^\]]+\]\s*)*(?:public|internal)\s+(?:sealed\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)')
            if (-not $namespaceMatch.Success -or -not $classMatch.Success) {
                throw "Unable to parse GdUnit4Net suite declaration: $($_.FullName)"
            }
            "$($namespaceMatch.Groups[1].Value).$($classMatch.Groups[1].Value)"
        } | Where-Object { $_ -notin @(
            'Tactics.Godot.Tests.GameplaySpec.GodotGameplayRuntimeRunnerTests',
            'Tactics.Godot.Tests.GameplaySpec.GodotDemonboundLongRunTests'
        ) } |
        Sort-Object -Unique)
    if ($suiteNames.Count -eq 0) { throw 'No non-gameplay GdUnit4Net suites were discovered.' }
    foreach ($suiteName in $suiteNames) {
        $suiteFilter = "FullyQualifiedName~$suiteName"
        if ($suiteName -eq 'Tactics.Godot.Tests.PlayableRunUiGodotTests') {
            $suiteFilter += '&FullyQualifiedName!~ReplacingAPageDoesNotRetainDisposedUnitMeters'
        }
        Invoke-IsolatedGdUnitSuite "Run isolated GdUnit4Net suite $suiteName" $suiteFilter
    }
    # This test intentionally creates and tears down the complete Main page, so it remains isolated even
    # from the other PlayableRunUi tests.
    Invoke-IsolatedGdUnitSuite 'Run isolated GdUnit4Net page replacement cleanup' `
        'FullyQualifiedName~ReplacingAPageDoesNotRetainDisposedUnitMeters'
    Invoke-IsolatedGdUnitSuite 'Run isolated GdUnit4Net gameplay-spec journeys' `
        'FullyQualifiedName~GodotGameplayRuntimeRunnerTests'

    if (-not (Test-Path -LiteralPath $godotGameplayReport -PathType Leaf)) {
        throw 'Godot gameplay spec report was not generated.'
    }
    $godotGameplayResult = Get-Content -Raw -LiteralPath $godotGameplayReport | ConvertFrom-Json
    $expectedGodotGameplayScenarios = @{
        'GodotPendingAcceptance.InventoryBattleProjection' = 'inventory-store-ready-v1'
        'GodotPendingAcceptance.DefeatedTerminal' = 'defeat-no-summon-v1'
        'GodotPendingAcceptance.PresentationNumbers' = 'numbers-mana-v1'
        'GodotPendingAcceptance.PresentationMiss' = 'numbers-miss-v1'
        'GodotPendingAcceptance.ReloadCleanup' = 'reload-pending-battle-v1'
        'Demonbound.RuntimeState' = 'demonbound-ready-v1'
        'AdventureBoard.StartCampPartyOrder' = $null
        'AdventureBoard.StartingSkillsEnterAdventureBoard' = $null
        'AdventureBoard.LeaderSwitchAndFreePath' = $null
        'AdventureBoard.ImmediateSuccessorExitSelection' = 'layer4-choice-ready-v1'
        'AdventureBoard.ImmediateExitReload' = 'layer4-choice-ready-v1'
        'AdventureBoard.RestCampfireResolution' = 'layer4-choice-ready-v1'
        'AdventureBoard.StoreMerchantPurchase' = 'layer4-choice-ready-v1'
        'AdventureBoard.StandardTreasureChest' = 'layer4-choice-ready-v1'
        'AdventureBoard.CursedChestMimicBattle' = 'layer4-event-ready-v1'
        'AdventureBoard.EventBattleContextSurvivesReload' = 'layer4-event-ready-v1'
        'AdventureBoard.PostEventNormalBattleClearsContext' = 'layer4-event-ready-v1'
        'AdventureBoard.FallenAltarGuardianBattle' = 'layer6-event-ready-v1'
        'AdventureBoard.LostVillagerEscortBattle' = 'layer6-escort-ready-v1'
        'AdventureBoard.FixedSeedCompleteRun' = $null
    }
    $actualScenarioNames = @($godotGameplayResult.scenarios | ForEach-Object { [string]$_.scenarioName })
    $scenarioIdentityMismatch = $actualScenarioNames.Count -ne $expectedGodotGameplayScenarios.Count -or
        @($actualScenarioNames | Select-Object -Unique).Count -ne $expectedGodotGameplayScenarios.Count -or
        @($godotGameplayResult.scenarios | Where-Object {
            -not $expectedGodotGameplayScenarios.ContainsKey([string]$_.scenarioName) -or
            [string]$_.checkpointId -ne [string]$expectedGodotGameplayScenarios[[string]$_.scenarioName]
        }).Count -ne 0
    if ($godotGameplayResult.schema -ne 'godot-gameplay-spec-result-v1' -or
        $godotGameplayResult.runtime -ne 'Godot' -or $godotGameplayResult.total -ne 20 -or
        $godotGameplayResult.passed -ne 20 -or $godotGameplayResult.failed -ne 0 -or
        $scenarioIdentityMismatch -or
        @($godotGameplayResult.scenarios | Where-Object {
            -not $_.productionSaveUnchanged -or $_.productionSaveBefore -ne $_.productionSaveAfter -or
            $_.remainingTemporaryNodes -ne 0
        }).Count -ne 0) {
        throw 'Godot gameplay spec report failed its isolation or result contract.'
    }

    Invoke-Checked 'Restore production Godot Debug assembly after GdUnit' {
        dotnet build $adapterProject -c Debug --no-restore --no-incremental -m:1
    }
    $testHostMayOwnGodotAssembly = $false

    if (-not $GodotOwned) {
    if (Test-Path -LiteralPath $poisonExport -PathType Leaf) {
        Invoke-Checked 'Compile real Poison Spear typed migration draft' {
            python -m Tools.migration.poison_spear_converter `
                --export $poisonExport `
                --specification $poisonSpecification `
                --output $poisonDraft
        }

        $generatedTargets = @(
            'godot/content/poison_spear/PoisonBuff.tres',
            'godot/content/poison_spear/PoisonSpearSkillLv1.tres',
            'godot/content/poison_spear/PoisonSpearPresentationLv1.tres',
            'godot/content/poison_spear/PoisonSpear10x10Fixture.tres',
            'godot/content/poison_spear/PoisonSpearProjectile.tscn',
            'godot/content/poison_spear/PoisonSpearImpact.tscn',
            'godot/content/poison_spear/ContentCatalog.tres',
            'Tools/migration/manifest/state/poison-spear-lv1-real.json'
        )
        Invoke-Checked 'Generate real Poison Spear Godot assets through ResourceSaver' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/PoisonSpearAssetBuilder.cs'
        }
        $firstGenerationHashes = @{}
        foreach ($target in $generatedTargets) {
            $firstGenerationHashes[$target] = (Get-FileHash -LiteralPath (Join-Path $repoRoot $target) -Algorithm SHA256).Hash
        }
        Invoke-Checked 'Repeat real Poison Spear generation for idempotency' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/PoisonSpearAssetBuilder.cs'
        }
        foreach ($target in $generatedTargets) {
            $secondHash = (Get-FileHash -LiteralPath (Join-Path $repoRoot $target) -Algorithm SHA256).Hash
            if ($firstGenerationHashes[$target] -ne $secondHash) {
                throw "Poison Spear generation is not byte-idempotent: $target"
            }
        }

    }
    else {
        Write-Host '== Skip real Poison Spear regeneration: disposable Unity DTO is not present =='
    }

    if (Test-Path -LiteralPath $unitExport -PathType Leaf) {
        Invoke-Checked 'Generate approved Pure Run player action poses' {
            python Tools/migration/action_pose_converter.py --root $repoRoot --manifest $actionPoseManifest --receipt $actionPoseReceipt
        }
        $firstActionPoseHashes = @{}
        foreach ($artifact in (Get-Content -LiteralPath $actionPoseReceipt -Raw | ConvertFrom-Json).artifacts) {
            $target = Join-Path $projectRoot $artifact.resourcePath.Substring('res://'.Length)
            $firstActionPoseHashes[$target] = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        }
        Invoke-Checked 'Repeat Pure Run player action pose generation for idempotency' {
            python Tools/migration/action_pose_converter.py --root $repoRoot --manifest $actionPoseManifest --receipt $actionPoseReceipt | Out-Null
        }
        foreach ($target in $firstActionPoseHashes.Keys) {
            if ($firstActionPoseHashes[$target] -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
                throw "Pure Run action pose generation is not byte-idempotent: $target"
            }
        }
        Invoke-Checked 'Compile real Pure Run Unit typed migration draft' {
            python -m Tools.migration.unit_converter `
                --export $unitExport `
                --specification $unitSpecification `
                --golden $unitGolden `
                --output $unitDraft
        }

        Invoke-Checked 'Copy approved project-owned Unit PNG payload transactionally' {
            python -m Tools.migration.unit_texture_migration --root $repoRoot --draft $unitDraft
        }
        $firstTextureHashes = @{}
        foreach ($artifact in (Get-Content -LiteralPath $unitTextureLedger -Raw | ConvertFrom-Json).artifacts) {
            $firstTextureHashes[$artifact.relativePath] = (
                Get-FileHash -LiteralPath (Join-Path $repoRoot $artifact.relativePath) -Algorithm SHA256).Hash
        }
        Invoke-Checked 'Repeat Unit PNG migration for idempotency' {
            python -m Tools.migration.unit_texture_migration --root $repoRoot --draft $unitDraft
        }
        foreach ($target in $firstTextureHashes.Keys) {
            $secondHash = (Get-FileHash -LiteralPath (Join-Path $repoRoot $target) -Algorithm SHA256).Hash
            if ($firstTextureHashes[$target] -ne $secondHash) {
                throw "Pure Run Unit PNG migration is not byte-idempotent: $target"
            }
        }

        # ResourceSaver must resolve imported Texture2D resources on a clean checkout.
        Invoke-Checked 'Import Pure Run Unit PNG payload in headless Editor' {
            & $GodotExecutable --headless --editor --path $projectRoot --quit-after 6000
        }

        $unitGeneratedTargets = @(
            'godot/content/units/ContentCatalog.tres',
            'godot/content/units/PureRunAmazon.tres',
            'godot/content/units/PureRunFireDemon.tres',
            'godot/content/units/PureRunGoatAoe.tres',
            'godot/content/units/PureRunGoatCharger.tres',
            'godot/content/units/PureRunGoatEliteCharger.tres',
            'godot/content/units/PureRunGoatElitePoisonCaster.tres',
            'godot/content/units/PureRunGoatRanged.tres',
            'godot/content/units/PureRunGoatSupport.tres',
            'godot/content/units/PureRunMage.tres',
            'godot/content/units/PureRunNecromancer.tres',
            'godot/content/units/PureRunSkeletonMage.tres',
            'godot/content/units/PureRunSkeletonWarrior.tres',
            'godot/content/units/UnitActor.tscn',
            'godot/content/units/UnitGallery.tscn',
            'godot/content/units/UnitSpawnFixture.tscn',
            'Tools/migration/manifest/state/pure-run-units-v1.json'
        )
        Invoke-Checked 'Generate Pure Run Unit Godot assets through ResourceSaver' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/UnitAssetBuilder.cs'
        }
        $firstUnitGenerationHashes = @{}
        foreach ($target in $unitGeneratedTargets) {
            $firstUnitGenerationHashes[$target] = (
                Get-FileHash -LiteralPath (Join-Path $repoRoot $target) -Algorithm SHA256).Hash
        }
        Invoke-Checked 'Repeat Pure Run Unit generation for idempotency' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/UnitAssetBuilder.cs'
        }
        foreach ($target in $unitGeneratedTargets) {
            $secondHash = (Get-FileHash -LiteralPath (Join-Path $repoRoot $target) -Algorithm SHA256).Hash
            if ($firstUnitGenerationHashes[$target] -ne $secondHash) {
                throw "Pure Run Unit generation is not byte-idempotent: $target"
            }
        }
    }
    else {
        Write-Host '== Skip real Pure Run Unit regeneration: disposable Unity DTO is not present =='
        Invoke-Checked 'Upgrade committed Pure Run Unit attributes through ResourceSaver' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/UnitAttributeBalanceBuilder.cs'
        }
    }

    if (Test-Path -LiteralPath $buffItemExport -PathType Leaf) {
        Invoke-Checked 'Compile real Pure Run Buff/Item typed migration draft' {
            python -m Tools.migration.buff_item_converter `
                --export $buffItemExport `
                --specification $buffItemSpecification `
                --golden $buffItemGolden `
                --consumables $consumablesJson `
                --equipment $equipmentJson `
                --output $buffItemDraft
        }

        Invoke-Checked 'Refresh Pure Run Buff/Item export receipt' {
            python -m Tools.migration.buff_item_receipt `
                --export $buffItemExport `
                --specification $buffItemSpecification `
                --draft $buffItemDraft `
                --output $buffItemExportReceipt
        }

        Invoke-Checked 'Generate Pure Run Buff/Item Godot assets through ResourceSaver' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/BuffItemAssetBuilder.cs'
        }
        $buffItemGeneratedTargets = @(
            (Get-Content -LiteralPath $buffItemGenerationLedger -Raw | ConvertFrom-Json).artifacts |
                ForEach-Object { Join-Path $projectRoot $_.resourcePath.Substring('res://'.Length) }
        ) + @($buffItemGenerationLedger)
        $firstBuffItemGenerationHashes = @{}
        foreach ($target in $buffItemGeneratedTargets) {
            $firstBuffItemGenerationHashes[$target] = (
                Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        }
        Invoke-Checked 'Repeat Pure Run Buff/Item generation for idempotency' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/BuffItemAssetBuilder.cs'
        }
        foreach ($target in $buffItemGeneratedTargets) {
            $secondHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
            if ($firstBuffItemGenerationHashes[$target] -ne $secondHash) {
                throw "Pure Run Buff/Item generation is not byte-idempotent: $target"
            }
        }
    }
    else {
        Write-Host '== Skip real Pure Run Buff/Item draft: disposable Unity DTO is not present =='
    }

    if (Test-Path -LiteralPath $startingSkillExport -PathType Leaf) {
        Invoke-Checked 'Compile real Pure Run starting-skill typed migration draft' {
            python -m Tools.migration.starting_skill_converter `
                --export $startingSkillExport `
                --specification $startingSkillSpecification `
                --output $startingSkillDraft
        }
        Invoke-Checked 'Generate Pure Run starting-skill Godot assets through ResourceSaver' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/StartingSkillAssetBuilder.cs'
        }
        $startingSkillTargets = @(
            (Get-Content -LiteralPath $startingSkillGenerationLedger -Raw | ConvertFrom-Json).artifacts |
                ForEach-Object { Join-Path $projectRoot $_.resourcePath.Substring('res://'.Length) }
        ) + @($startingSkillGenerationLedger)
        $firstStartingSkillHashes = @{}
        foreach ($target in $startingSkillTargets) {
            $firstStartingSkillHashes[$target] = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        }
        Invoke-Checked 'Repeat Pure Run starting-skill generation for idempotency' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/StartingSkillAssetBuilder.cs'
        }
        foreach ($target in $startingSkillTargets) {
            if ($firstStartingSkillHashes[$target] -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
                throw "Pure Run starting-skill generation is not byte-idempotent: $target"
            }
        }
    }
    else {
        Write-Host '== Skip real Pure Run starting-skill draft: disposable Unity DTO is not present =='
    }

    if (Test-Path -LiteralPath $aiEncounterExport -PathType Leaf) {
        Invoke-Checked 'Compile real Pure Run AI/Encounter typed migration draft' {
            python -m Tools.migration.ai_encounter_converter `
                --export $aiEncounterExport `
                --specification $aiEncounterSpecification `
                --output $aiEncounterDraft
        }
        Invoke-Checked 'Generate Pure Run AI/Encounter Godot assets through ResourceSaver' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/AiEncounterAssetBuilder.cs'
        }
        $aiEncounterTargets = @(
            (Get-Content -LiteralPath $aiEncounterGenerationLedger -Raw | ConvertFrom-Json).artifacts |
                ForEach-Object { Join-Path $projectRoot $_.resourcePath.Substring('res://'.Length) }
        ) + @($aiEncounterGenerationLedger)
        $firstAiEncounterHashes = @{}
        foreach ($target in $aiEncounterTargets) {
            $firstAiEncounterHashes[$target] = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        }
        Invoke-Checked 'Repeat Pure Run AI/Encounter generation for idempotency' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/AiEncounterAssetBuilder.cs'
        }
        foreach ($target in $aiEncounterTargets) {
            if ($firstAiEncounterHashes[$target] -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
                throw "Pure Run AI/Encounter generation is not byte-idempotent: $target"
            }
        }
    }
    else {
        Write-Host '== Skip real Pure Run AI/Encounter draft: disposable Unity DTO is not present =='
    }

    if (Test-Path -LiteralPath $runPersistenceExport -PathType Leaf) {
        Invoke-Checked 'Compile real Pure Run persistence typed migration draft' {
            python -m Tools.migration.pure_run_persistence_converter `
                --export $runPersistenceExport `
                --specification $runPersistenceSpecification `
                --output $runPersistenceDraft
        }
        Invoke-Checked 'Generate Pure Run persistence Godot assets through ResourceSaver' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/RunPersistenceAssetBuilder.cs'
        }
        $runPersistenceTargets = @(
            (Get-Content -LiteralPath $runPersistenceGenerationLedger -Raw | ConvertFrom-Json).artifacts |
                ForEach-Object { Join-Path $projectRoot $_.resourcePath.Substring('res://'.Length) }
        ) + @($runPersistenceGenerationLedger)
        $firstRunPersistenceHashes = @{}
        foreach ($target in $runPersistenceTargets) { $firstRunPersistenceHashes[$target] = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash }
        Invoke-Checked 'Repeat Pure Run persistence generation for idempotency' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/RunPersistenceAssetBuilder.cs'
        }
        foreach ($target in $runPersistenceTargets) {
            if ($firstRunPersistenceHashes[$target] -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
                throw "Pure Run persistence generation is not byte-idempotent: $target"
            }
        }
    }

    if (Test-Path -LiteralPath $inventoryProgressionExport -PathType Leaf) {
        Invoke-Checked 'Compile Pure Run inventory/progression typed draft' {
            python -m Tools.migration.inventory_progression_converter --export $inventoryProgressionExport --specification $inventoryProgressionSpecification --output $inventoryProgressionDraft
        }
        Invoke-Checked 'Generate Pure Run inventory/progression resources first pass' {
            & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/InventoryProgressionAssetBuilder.cs'
        }
        $inventoryProgressionTargets = @(
            Get-ChildItem -LiteralPath (Join-Path $projectRoot 'content\skills') -Filter '*.tres' |
                Where-Object { $_.Name -eq 'InventoryProgressionCatalog.tres' -or $_.Name -notin @(
                    'ContentCatalog.tres','BasicMagic.tres','BasicMelee.tres','MageFireballLv1.tres','MageIceBoltLv1.tres','MageLightningLv1.tres',
                    'NecromancerSummonSkeletonLv1.tres','NecromancerAmplifyDamageLv1.tres','NecromancerBoneSpearLv1.tres','AmazonThrustLv1.tres','AmazonCombatTechniquesLv1.tres','AmazonPickupSpearLv1.tres') }
        )
        $firstInventoryProgressionHashes = @{}
        foreach ($target in $inventoryProgressionTargets) { $firstInventoryProgressionHashes[$target.FullName] = (Get-FileHash -LiteralPath $target.FullName -Algorithm SHA256).Hash }
        Invoke-Checked 'Generate Pure Run inventory/progression resources second pass' {
            & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/InventoryProgressionAssetBuilder.cs'
        }
        foreach ($target in $inventoryProgressionTargets) {
            if ($firstInventoryProgressionHashes[$target.FullName] -ne (Get-FileHash -LiteralPath $target.FullName -Algorithm SHA256).Hash) {
                throw "Pure Run inventory/progression generation is not byte-idempotent: $($target.FullName)"
            }
        }
        Invoke-Checked 'Refresh Pure Run inventory/progression generation evidence' {
            python -m Tools.migration.inventory_progression_generation_receipt --draft $inventoryProgressionDraft --output $inventoryProgressionGenerationReceipt --idempotent
        }
    }
    else { Write-Host '== Skip real Pure Run persistence draft: disposable Unity DTO is not present ==' }

    if (Test-Path -LiteralPath $ownershipClosureExport -PathType Leaf) {
        Invoke-Checked 'Compile final Unity ownership-closure typed draft' {
            python -m Tools.migration.ownership_closure_converter --export $ownershipClosureExport --specification $ownershipClosureSpecification --output $ownershipClosureDraft
        }
        Invoke-Checked 'Validate final Unity ownership-closure export evidence' {
            python -m unittest Tools.migration.tests.test_ownership_closure_converter
        }
    }
    else { Write-Host '== Skip final Unity ownership-closure draft: disposable Unity DTO is not present ==' }

    $layerFourExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-layer4-map-nodes-v1.unity.json'
    if (Test-Path -LiteralPath $layerFourExport -PathType Leaf) {
        Invoke-Checked 'Compile Pure Run layer four typed draft' {
            python -m Tools.migration.layer4_map_nodes_converter --export $layerFourExport --specification (Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-layer4-map-nodes-v1.json') --output (Join-Path $repoRoot 'Tools\migration\out\pure-run-layer4-map-nodes-v1.draft.json')
        }
        Invoke-Checked 'Generate Pure Run layer four resources first pass' {
            & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/LayerFourAssetBuilder.cs'
        }
        Invoke-Checked 'Generate Pure Run layer four resources second pass' {
            & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/LayerFourAssetBuilder.cs'
        }
    }

    $fullRunExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-full-seven-layer-v1.unity.json'
    if (Test-Path -LiteralPath $fullRunExport -PathType Leaf) {
        Invoke-Checked 'Compile Pure Run full seven-layer typed draft' {
            python -m Tools.migration.full_run_converter --export $fullRunExport --specification (Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-full-seven-layer-v1.json') --output (Join-Path $repoRoot 'Tools\migration\out\pure-run-full-seven-layer-v1.draft.json')
        }
        Invoke-Checked 'Generate Pure Run full seven-layer resources first pass' {
            & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/FullRunAssetBuilder.cs'
        }
        $fullRunCatalogHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\ContentCatalog.tres') -Algorithm SHA256).Hash
        Invoke-Checked 'Generate Pure Run full seven-layer resources second pass' {
            & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/FullRunAssetBuilder.cs'
        }
        if ($fullRunCatalogHash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\ContentCatalog.tres') -Algorithm SHA256).Hash) {
            throw 'Pure Run full seven-layer Catalog generation is not byte-idempotent.'
        }
    }


    Invoke-Checked 'Generate authored split-flank layout closure first pass' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/SplitFlankLayoutClosureBuilder.cs'
    }
    $splitFlankCatalogHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\ContentCatalog.tres') -Algorithm SHA256).Hash
    $splitFlankLayoutHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\full_run\BattleLayoutPureRunSplitFlank.tres') -Algorithm SHA256).Hash
    Invoke-Checked 'Generate authored split-flank layout closure second pass' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/SplitFlankLayoutClosureBuilder.cs'
    }
    if ($splitFlankCatalogHash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\ContentCatalog.tres') -Algorithm SHA256).Hash -or
        $splitFlankLayoutHash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\full_run\BattleLayoutPureRunSplitFlank.tres') -Algorithm SHA256).Hash) {
        throw 'Split-flank layout closure is not byte-idempotent.'
    }

    Invoke-Checked 'Generate isometric battle board resource first pass' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/IsometricPresentationAssetBuilder.cs'
    }
    $isometricCatalogHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\ContentCatalog.tres') -Algorithm SHA256).Hash
    $isometricBoardHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\presentation\BattleBoardPureRunIsometricV1.tres') -Algorithm SHA256).Hash
    $unitPresentationHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\presentation\StandardUnitPresentationV1.tres') -Algorithm SHA256).Hash
    $skillPresentationHashes = @('FireballPresentation.tres','BoneSpearPresentation.tres','ThrustPresentation.tres') | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $projectRoot "content\presentation\$_") -Algorithm SHA256).Hash }
    Invoke-Checked 'Generate isometric battle board resource second pass' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/IsometricPresentationAssetBuilder.cs'
    }
    if ($isometricCatalogHash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\ContentCatalog.tres') -Algorithm SHA256).Hash -or
        $isometricBoardHash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\presentation\BattleBoardPureRunIsometricV1.tres') -Algorithm SHA256).Hash -or
        $unitPresentationHash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'content\presentation\StandardUnitPresentationV1.tres') -Algorithm SHA256).Hash -or
        (Compare-Object $skillPresentationHashes (@('FireballPresentation.tres','BoneSpearPresentation.tres','ThrustPresentation.tres') | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $projectRoot "content\presentation\$_") -Algorithm SHA256).Hash }))) {
        throw 'Isometric presentation generation is not byte-idempotent.'
    }

    $ownershipDraft = Join-Path $repoRoot 'Tools\migration\out\pure-run-ownership-closure-v1.draft.json'
    if (Test-Path -LiteralPath $ownershipDraft -PathType Leaf) {
        Invoke-Checked 'Generate ownership-closure Lv3 resources first pass' {
            & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/OwnershipClosureAssetBuilder.cs'
        }
        $ownershipPaths = @(
            (Join-Path $projectRoot 'content\ContentCatalog.tres'),
            (Join-Path $projectRoot 'content\skills\OwnershipClosureCatalog.tres')
        ) + @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'content\skills') -Filter '*Lv3.tres' -File | Select-Object -ExpandProperty FullName)
        $ownershipHashes = $ownershipPaths | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
        Invoke-Checked 'Generate ownership-closure Lv3 resources second pass' {
            & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/OwnershipClosureAssetBuilder.cs'
        }
        $repeatedOwnershipHashes = $ownershipPaths | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
        if (Compare-Object $ownershipHashes $repeatedOwnershipHashes) {
            throw 'Ownership-closure Lv3 generation is not byte-idempotent.'
        }
        Invoke-Checked 'Refresh ownership-closure Lv3 generation evidence' {
            python -m Tools.migration.ownership_closure_generation `
                --draft $ownershipDraft --project $projectRoot `
                --state (Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-ownership-closure-v1.json') `
                --receipt (Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-ownership-closure-v1-generation.json')
        }
    }

    Invoke-Checked 'Generate authoritative Map and Treasure resources first pass' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/MapTreasureAssetBuilder.cs'
    }
    $mapTreasurePaths = @(
        (Join-Path $projectRoot 'content\ContentCatalog.tres'),
        (Join-Path $projectRoot 'content\map\PureRunDefaultMap.tres'),
        (Join-Path $projectRoot 'content\map\PureRunStandardTreasure.tres')
    )
    $mapTreasureHashes = $mapTreasurePaths | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    Invoke-Checked 'Generate authoritative Map and Treasure resources second pass' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility --script 'res://src/Tactics.Godot.Adapter/Editor/MapTreasureAssetBuilder.cs'
    }
    if (Compare-Object $mapTreasureHashes ($mapTreasurePaths | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash })) {
        throw 'Authoritative Map and Treasure generation is not byte-idempotent.'
    }
    Invoke-Checked 'Refresh authoritative Map and Treasure generation evidence' {
        python -m Tools.migration.map_treasure_generation --project $projectRoot `
            --state (Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-map-treasure-v1.json') `
            --receipt (Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-map-treasure-v1-generation.json')
    }

    $uiExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-ui-input-v1.unity.json'
    $uiDraft = Join-Path $repoRoot 'Tools\migration\out\pure-run-ui-input-v1.draft.json'
    if (Test-Path -LiteralPath $uiExport -PathType Leaf) {
        Invoke-Checked 'Compile Pure Run UI/Input typed audit draft' {
            python -m Tools.migration.pure_run_ui_input_converter `
                --export $uiExport `
                --specification (Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-ui-input-v1.json') `
                --output $uiDraft
        }
        Invoke-Checked 'Generate playable Run Main PackedScene through ResourceSaver' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/PlayableRunSceneBuilder.cs'
        }
        $mainScene = Join-Path $projectRoot 'scenes\Main.tscn'
        $playableBalance = Join-Path $projectRoot 'content\ui\PlayableLv1BalanceProfile.tres'
        $enemySpeedProfile = Join-Path $projectRoot 'content\ui\PlayableEnemySpeedProfile.tres'
        $firstMainHash = (Get-FileHash -LiteralPath $mainScene -Algorithm SHA256).Hash
        $firstPlayableBalanceHash = (Get-FileHash -LiteralPath $playableBalance -Algorithm SHA256).Hash
        $firstEnemySpeedHash = (Get-FileHash -LiteralPath $enemySpeedProfile -Algorithm SHA256).Hash
        Invoke-Checked 'Repeat playable Run Main generation for idempotency' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/PlayableRunSceneBuilder.cs'
        }
        if ($firstMainHash -ne (Get-FileHash -LiteralPath $mainScene -Algorithm SHA256).Hash) {
            throw 'Playable Run Main generation is not byte-idempotent.'
        }
        if ($firstPlayableBalanceHash -ne (Get-FileHash -LiteralPath $playableBalance -Algorithm SHA256).Hash) {
            throw 'Playable Lv1 balance generation is not byte-idempotent.'
        }
        if ($firstEnemySpeedHash -ne (Get-FileHash -LiteralPath $enemySpeedProfile -Algorithm SHA256).Hash) {
            throw 'Playable enemy speed generation is not byte-idempotent.'
        }
        Invoke-Checked 'Refresh playable Run UI generation evidence' {
            python -m Tools.migration.pure_run_ui_input_generation `
                --draft $uiDraft --scene $mainScene --balance $playableBalance --enemy-speed $enemySpeedProfile `
                --state (Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-ui-input-v1.json') `
                --receipt (Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-ui-input-v1-generation.json')
        }
    }
    }

    Invoke-Checked 'Import approved Poet Idle PNG payload in headless Editor' {
        & $GodotExecutable --headless --editor --path $projectRoot --quit-after 6000
    }
    $poetGeneratedTargets = @(
        Get-ChildItem -LiteralPath (Join-Path $projectRoot 'content\poet') -Filter '*.tres' -File |
            Select-Object -ExpandProperty FullName
    ) + @(
        (Join-Path $projectRoot 'content\ContentCatalog.tres')
        (Join-Path $projectRoot 'content\runs\PureRunThreeEncounterV1.tres')
        (Join-Path $projectRoot 'content\ui\PlayableLv1BalanceProfile.tres')
    )
    Invoke-Checked 'Generate approved Poet content through ResourceSaver' {
        & $GodotExecutable --headless --path $projectRoot `
            --script 'res://src/Tactics.Godot.Adapter/Editor/PoetAssetBuilder.cs'
    }
    $firstPoetGenerationHashes = @{}
    foreach ($target in $poetGeneratedTargets) {
        $firstPoetGenerationHashes[$target] = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    }
    Invoke-Checked 'Repeat approved Poet generation for idempotency' {
        & $GodotExecutable --headless --path $projectRoot `
            --script 'res://src/Tactics.Godot.Adapter/Editor/PoetAssetBuilder.cs'
    }
    foreach ($target in $poetGeneratedTargets) {
        if ($firstPoetGenerationHashes[$target] -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
            throw "Poet generation is not byte-idempotent: $target"
        }
    }

    # A ResourceSaver script can register a newly created UID only in its current process.
    # The headless Editor filesystem scan persists the project UID cache before Runtime validation.
    Invoke-Checked 'Godot editor filesystem scan and plugin initialization' {
        & $GodotExecutable --headless --editor --path $projectRoot --quit-after 120
    }

    Invoke-Checked 'Build Release without test sources or dev packages' {
        New-Item -ItemType Directory -Path $releaseVerificationDirectory -ErrorAction Stop | Out-Null
        dotnet build $adapterProject -c Release --no-restore -m:1 --output $releaseVerificationDirectory
    }

    $forbiddenReleaseAssemblies = @(
        Get-ChildItem -LiteralPath $releaseVerificationDirectory -File -ErrorAction Stop |
            Where-Object { $_.Name -match '^(GdUnit|Microsoft\.TestPlatform|testhost)' }
    )
    if ($forbiddenReleaseAssemblies.Count -gt 0) {
        throw "Release contains test assemblies: $($forbiddenReleaseAssemblies.Name -join ', ')"
    }
    $releaseDependencyFile = Join-Path $releaseVerificationDirectory 'Tactics.Godot.Adapter.deps.json'
    $releaseDependencies = Get-Content -LiteralPath $releaseDependencyFile -Raw -ErrorAction Stop
    if ($releaseDependencies -match 'gdUnit4|Microsoft\.TestPlatform|testhost') {
        throw 'Release dependency manifest contains GdUnit or TestPlatform entries.'
    }

    Invoke-Checked 'Poison Spear catalog and Core validation (Compatibility)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            --validate-poison-spear --quit-after 6000
    }

    Invoke-Checked 'Poison Spear catalog and Core validation (Forward+)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method forward_plus `
            --validate-poison-spear --quit-after 6000
    }

    Invoke-Checked 'Poison Spear Tween and Scope validation' {
        & $GodotExecutable --headless --path $projectRoot --play-poison-spear --quit-after 6000
    }

    Invoke-Checked 'Pure Run Unit catalog, factory, and fixture validation (Compatibility)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            --validate-units --quit-after 6000
    }

    Invoke-Checked 'Pure Run Unit catalog, factory, and fixture validation (Forward+)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method forward_plus `
            --validate-units --quit-after 6000
    }

    Invoke-Checked 'Pure Run Buff/Item and canonical catalog validation (Compatibility)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            --validate-buffs-items --quit-after 6000
    }

    Invoke-Checked 'Pure Run Buff/Item and canonical catalog validation (Forward+)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method forward_plus `
            --validate-buffs-items --quit-after 6000
    }

    Invoke-Checked 'Pure Run starting-skill catalog and fixture validation (Compatibility)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            --validate-starting-skills --quit-after 6000
    }

    Invoke-Checked 'Pure Run starting-skill catalog and fixture validation (Forward+)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method forward_plus `
            --validate-starting-skills --quit-after 6000
    }

    Invoke-Checked 'Pure Run AI/Encounter catalog and fixture validation (Compatibility)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            --validate-ai-encounters --quit-after 6000
    }

    Invoke-Checked 'Pure Run AI/Encounter catalog and fixture validation (Forward+)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method forward_plus `
            --validate-ai-encounters --quit-after 6000
    }

    Invoke-Checked 'Pure Run persistence catalog and fixture validation (Compatibility)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            --validate-run-persistence --quit-after 6000
    }

    Invoke-Checked 'Pure Run persistence catalog and fixture validation (Forward+)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method forward_plus `
            --validate-run-persistence --quit-after 6000
    }

    Invoke-Checked 'Playable Pure Run UI validation (Compatibility)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            --validate-playable-run-ui --quit-after 6000
    }

    Invoke-Checked 'Playable Pure Run UI validation (Forward+)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method forward_plus `
            --validate-playable-run-ui --quit-after 6000
    }

    Invoke-Checked 'Capture deterministic Pure Run Unit programmatic gallery' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            -- --capture-unit-gallery
    }

    Invoke-Checked 'Capture deterministic Pure Run Unit 10x10 spawn fixture' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            -- --capture-unit-spawn
    }

    if (-not $GodotOwned) {
    if (Test-Path -LiteralPath $poisonExport -PathType Leaf) {
        # Only write a "passed" generation receipt after UID scan, both renderer paths,
        # runtime semantics, Tween, and scope validation have actually succeeded.
        Invoke-Checked 'Refresh real Poison Spear generation receipt' {
            python -m Tools.migration.poison_spear_receipt `
                --export-receipt $poisonExportReceipt `
                --draft $poisonDraft `
                --ledger $poisonGenerationLedger `
                --output $poisonGenerationReceipt
        }

        Invoke-Checked 'Validate regenerated Poison Spear ledger and receipt' {
            python -m unittest Tools.migration.tests.test_poison_spear_generation
        }
    }

    if (Test-Path -LiteralPath $unitExport -PathType Leaf) {
        # Visual acceptance remains manual even after the deterministic gallery is captured.
        Invoke-Checked 'Refresh Pure Run Unit generation receipt' {
            python -m Tools.migration.unit_generation_receipt `
                --export-receipt $unitExportReceipt `
                --draft $unitDraft `
                --generation-ledger $unitGenerationLedger `
                --texture-ledger $unitTextureLedger `
                --gallery-capture $unitGalleryCapture `
                --spawn-capture $unitSpawnCapture `
                --goat-tint-shader $unitGoatTintShader `
                --output $unitGenerationReceipt
        }

        Invoke-Checked 'Validate regenerated Pure Run Unit ledger and receipt' {
            python -m unittest Tools.migration.tests.test_unit_generation
        }
    }

    if (Test-Path -LiteralPath $buffItemExport -PathType Leaf) {
        # This batch carries no visual payload, so successful deterministic generation,
        # both renderer paths, and typed runtime validation complete its acceptance gate.
        Invoke-Checked 'Refresh Pure Run Buff/Item generation receipt' {
            python -m Tools.migration.buff_item_generation_receipt `
                --export-receipt $buffItemExportReceipt `
                --draft $buffItemDraft `
                --ledger $buffItemGenerationLedger `
                --output $buffItemGenerationReceipt
        }

        Invoke-Checked 'Validate regenerated Pure Run Buff/Item ledger and receipt' {
            python -m unittest Tools.migration.tests.test_buff_item_generation
        }
    }


    if (Test-Path -LiteralPath $startingSkillExport -PathType Leaf) {
        Invoke-Checked 'Refresh Pure Run starting-skill generation receipt' {
            python -m Tools.migration.starting_skill_generation_receipt `
                --export-receipt $startingSkillExportReceipt `
                --draft $startingSkillDraft `
                --ledger $startingSkillGenerationLedger `
                --output $startingSkillGenerationReceipt
        }
        Invoke-Checked 'Validate regenerated Pure Run starting-skill ledger and receipt' {
            python -m unittest Tools.migration.tests.test_starting_skill_generation
        }
    }

    if (Test-Path -LiteralPath $runPersistenceExport -PathType Leaf) {
        Invoke-Checked 'Refresh Pure Run persistence generation receipt' {
            python -m Tools.migration.pure_run_persistence_generation_receipt `
                --export-receipt $runPersistenceExportReceipt `
                --draft $runPersistenceDraft `
                --ledger $runPersistenceGenerationLedger `
                --output $runPersistenceGenerationReceipt
        }
        Invoke-Checked 'Validate Pure Run persistence ledger and receipt' {
            python -m unittest Tools.migration.tests.test_pure_run_persistence_generation
        }
    }

    Invoke-Checked 'Run migration Python unittest against refreshed generation evidence' {
        python -m unittest discover -s 'Tools/migration/tests' -p 'test_*.py'
    }
    }

    Invoke-Checked 'Run OKF unittest' {
        python -m unittest discover -s (Join-Path $repoRoot 'Tools/okf') -p 'test_*.py'
    }

    Invoke-Checked 'Run public release policy unittest' {
        python -m unittest discover -s (Join-Path $repoRoot 'Tools/public-release/tests') -p 'test_*.py'
    }

    Invoke-Checked 'Run artwork pipeline unittest' {
        python -m unittest discover -s (Join-Path $repoRoot '.agents/skills/pure-run-artwork-pipeline/tests') -p 'test_*.py'
    }

    Invoke-Checked 'Run Godot tooling Python unittest' {
        python -m unittest discover -s (Join-Path $repoRoot 'Tools/godot/tests') -p 'test_*.py'
    }

    Invoke-Checked 'Validate Pure Run artwork state registry' {
        python '.agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py' --root $repoRoot check --strict
    }

    Invoke-Checked 'Validate public source candidate' {
        python 'Tools/public-release/validate_public_candidate.py' --root $repoRoot --candidate
    }

    Invoke-Checked 'Validate public dependency inventory' {
        python 'Tools/public-release/generate_dependency_report.py' --root $repoRoot --check
    }

    Invoke-Checked 'Validate OKF bundle' {
        if ($GodotOwned) {
            python 'Tools/okf/validate_bundle.py' `
                --allow-missing-repo-prefix Assets `
                --allow-missing-repo-prefix Packages `
                --allow-missing-repo-prefix ProjectSettings `
                --allow-missing-repo-prefix UIElementsSchema
        }
        else {
            python 'Tools/okf/validate_bundle.py'
        }
    }

    if ($hasGitMetadata) {
        Invoke-Checked 'Report OKF worktree impact' {
            python 'Tools/okf/catalog_impact.py' report --worktree
        }

        Invoke-Checked 'Validate patch whitespace' {
            git diff --check
        }
    }

    Write-Host "Godot project verification passed. Canonical project: $projectRoot"
}
finally {
    if ($testHostMayOwnGodotAssembly) {
        Write-Host '== Restore production Godot Debug assembly after interrupted GdUnit =='
        dotnet build $adapterProject -c Debug --no-restore --no-incremental -m:1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Production Adapter recovery failed with exit code $LASTEXITCODE; run Tools/godot/Open-GodotDev.ps1 -NoLaunch before opening the Editor."
        }
    }
    if ($createdGdUnitRunnerSource -and (Test-Path -LiteralPath $gdUnitRunnerSource -PathType Leaf)) {
        Remove-Item -LiteralPath $gdUnitRunnerSource -Force
    }
    if (Test-Path -LiteralPath $releaseVerificationDirectory -PathType Container) {
        [System.IO.Directory]::Delete($releaseVerificationDirectory, $true)
    }
    Pop-Location
    Exit-TacticsGodotOperationLock -Lock $operationLock
}
