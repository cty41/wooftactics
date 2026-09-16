using System.Text.Json;
using GdUnit4;
using Godot;
using Tactics.Application.Runs;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests.GameplaySpec;

[TestSuite]
public class GodotGameplayRuntimeRunnerTests
{
    [TestCase]
    public void EventWindowCountsOnlyTheNewSuffix()
    {
        AssertThat(GodotGameplayEventWindow.NewEventOffset(["a", "b", "c"], ["b", "c", "d"])).IsEqual(2);
        AssertThat(GodotGameplayEventWindow.NewEventOffset(["a", "b"], ["a", "b"])).IsEqual(2);
        AssertThat(GodotGameplayEventWindow.NewEventOffset(["a", "b"], ["c"])).IsEqual(0);
        AssertThat(GodotGameplayEventWindow.NewEventOffset([], ["a"])).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DemonboundProductionPartyUsesTheProductionStartingSkillButtonName()
    {
        GodotGameplayScenarioPlan source = LoadCompiledPlan("adventure-fixed-seed-full-run");
        GodotGameplayScenarioPlan plan = WithParty(source,
            ["pure_run_mage", "pure_run_necromancer", "pure_run_demonbound"], "Runner.NecromancerParty");

        AssertThat(plan.RuntimeActions.Any(action =>
            action.Target == "starting_skill__skill_necromancer_summon-skeleton_lv1")).IsTrue();
        AssertThat(plan.RuntimeActions.Any(action =>
            action.Target == "starting_skill__skill_necromancer_summon_skeleton_lv1")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ValidatedCheckpointCatalogProducesStableCanonicalV11Hashes()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["inventory-store-ready-v1"] = "acf7098eb5833e018afd5c35cb82eb1d0f2e3b88d89375f1ec059dce37f6c6e0",
            ["defeat-no-summon-v1"] = "57f955cffb989e9a44aac824568b88509c0756f19279652a965beb74e58ccc8b",
            ["numbers-mana-v1"] = "0600ff730fc74de5e9ef3c81b59db38f6ddf64dbec9e45a8e8ef82a14fb65b75",
            ["numbers-miss-v1"] = "9a6718898e311e99470ff6f55ad67ad76baf22a715ab0d5e4108af613ab3e0e4",
            ["reload-pending-battle-v1"] = "13c911dd9ed54586e3dc160a86e03bdde1a9301b8bbc6c0702962f4a3038e6a0",
            ["demonbound-ready-v1"] = "6a6cd1044c7a51332edc0d347970565df0b4f5dd22e08dd4ede2e316aef9c5df",
            ["layer4-choice-ready-v1"] = "8876d2ebebbd451ac2ae5b3be2d018b26c24eba29c1cc03bcd2a5bea848bdf19",
            ["layer4-event-ready-v1"] = "a3fb1f889423aae1e4c53a8fba80d4c0bcfc966ec6b5872f80f7e7e7b3407c29",
            ["layer6-event-ready-v1"] = "35d535d83668385d2e5466824acc29a6b0e17639bf83bbf1ee3ed984ba135099",
            ["layer6-escort-ready-v1"] = "db5796777319f9278eafaa394efaa98e153b97e009680b413c22fa9811ce5339"
        };
        var mismatches = new List<string>();
        foreach ((string id, string hash) in expected)
        {
            ValidatedGodotRunCheckpoint checkpoint = GodotGameplayCheckpointCatalog.Create(id);
            System.Console.WriteLine($"checkpoint:{id}:{checkpoint.SemanticHash}");
            AssertThat(checkpoint.Verify()).IsTrue();
            if (checkpoint.SemanticHash != hash) mismatches.Add($"{id}={checkpoint.SemanticHash}");
        }
        AssertThat(string.Join(";", mismatches)).IsEqual(string.Empty);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AcceptanceSpecsWriteStructuredBatchReport()
    {
        (string Plan, string? Checkpoint)[] scenarios =
        [
            ("inventory-battle-projection", "inventory-store-ready-v1"),
            ("defeated-terminal", "defeat-no-summon-v1"),
            ("presentation-numbers", "numbers-mana-v1"),
            ("presentation-miss", "numbers-miss-v1"),
            ("reload-cleanup", "reload-pending-battle-v1"),
            ("demonbound-runtime", "demonbound-ready-v1"),
            ("adventure-start-camp", null),
            ("adventure-starting-skills", null),
            ("adventure-exploration-controls", null),
            ("adventure-immediate-exit-selection", "layer4-choice-ready-v1"),
            ("adventure-immediate-exit-reload", "layer4-choice-ready-v1"),
            ("adventure-rest-node", "layer4-choice-ready-v1"),
            ("adventure-store-node", "layer4-choice-ready-v1"),
            ("adventure-treasure-node", "layer4-choice-ready-v1"),
            ("adventure-cursed-chest-battle", "layer4-event-ready-v1"),
            ("adventure-event-battle-reload", "layer4-event-ready-v1"),
            ("adventure-post-event-normal-battle", "layer4-event-ready-v1"),
            ("adventure-altar-guardian-battle", "layer6-event-ready-v1"),
            ("adventure-escort-battle", "layer6-escort-ready-v1"),
            ("adventure-fixed-seed-full-run", null)
        ];
        var executions = new List<GodotGameplayReportScenario>();
        foreach ((string planName, string? _) in scenarios)
        {
            GodotGameplayScenarioPlan plan = LoadCompiledPlan(planName);
            GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);
            executions.Add(GodotGameplayReportScenario.From(plan, result));
        }
        GodotGameplaySpecReport report = GodotGameplaySpecReport.Create(executions);
        string output = GodotGameplaySpecReportWriter.Write(report);

        if (report.Failed > 0)
        {
            string diagnostics = string.Join(System.Environment.NewLine, report.Scenarios.Select(value =>
                $"{value.ScenarioName}: passed={value.Succeeded}, error={value.ErrorCode ?? "none"}, " +
                $"checkpoint={value.CheckpointId}, cleanup={value.RemainingTemporaryNodes}"));
            throw new InvalidOperationException($"Gameplay acceptance batch failed:{System.Environment.NewLine}{diagnostics}");
        }

        AssertThat(File.Exists(output)).IsTrue();
        AssertThat(report.Scenarios.Count).IsEqual(20);
        AssertThat(report.Passed).IsEqual(20);
        AssertThat(report.Failed).IsEqual(0);
        AssertThat(report.Scenarios.Select(value => value.ScenarioName).Distinct().Count()).IsEqual(20);
        var expectedIdentities = scenarios.ToDictionary(value => LoadCompiledPlan(value.Plan).ScenarioName,
            value => value.Checkpoint, StringComparer.Ordinal);
        AssertThat(report.Scenarios.All(value => expectedIdentities.TryGetValue(value.ScenarioName, out string? checkpointId) &&
            checkpointId == value.CheckpointId)).IsTrue();
        AssertThat(report.Scenarios.Select(value => value.CheckpointId).Order().ToArray())
            .ContainsExactly(scenarios.Select(value => value.Checkpoint).Order().ToArray());
        AssertThat(report.Scenarios.All(value => value.ProductionSaveUnchanged &&
            value.ProductionSaveBefore == value.ProductionSaveAfter && value.RemainingTemporaryNodes == 0)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task MainSceneReceivesIsolatedStoreAndProductionQuitInput()
    {
        var assertion = new GodotGameplayPlanAssertion("runtimeHasNoErrors", "UI", null, Json(true), []);
        var plan = new GodotGameplayScenarioPlan(2, "Godot", "Runner.MainQuit", ["PlayerInput", "UI"],
            ["setup:initializePlayerInput", "action:pressInputKey", "action:clickPointerTarget", "assertion:runtimeHasNoErrors"],
            [new GodotGameplayPlanStep("initializePlayerInput", "PlayerInput", null, [])],
            [
                new GodotGameplayPlanStep("pressInputKey", "PlayerInput", null,
                    new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement("Escape") }),
                new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "SAVE AND QUIT", [])
            ],
            [assertion], [new GodotGameplayProbeRequest(assertion.Kind, assertion.Adapter, assertion.Target, assertion.Parameters)],
            new GodotGameplaySaveIsolation("user://qa-runner", true),
            new GodotGameplayWatchdog(30000, 80, 300000, 4), null);

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");

        AssertThat(result.ErrorCode).IsNull();
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.Trace.Count).IsEqual(4);
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureStartCampUsesProductionInputAndPreservesClickOrder()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-start-camp");

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureStartingSkillsUseProductionInputAndEnterRunMap()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-starting-skills");

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureLeaderSwitchMovesOnlyTheLeaderThroughProductionInput()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-exploration-controls");
        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureExitSelectsOnlyAnImmediateSuccessor()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-immediate-exit-selection");
        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureRestUsesCampfireBeforeFormalResolution()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-rest-node");
        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureStoreUsesMerchantAndPersistsPurchase()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-store-node");
        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureTreasureUsesChestAndPersistsReward()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-treasure-node");
        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureCursedChestBattleReturnsToChangedScene()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-cursed-chest-battle");
        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureFallenAltarBattleReturnsToPurifiedScene()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-altar-guardian-battle");
        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureEscortSurvivesReloadAndCompletesProtectedBattle()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-escort-battle");
        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureFixedSeedCompleteRunUsesProductionInputToBossVictory()
    {
        GodotGameplayScenarioPlan plan = LoadCompiledPlan("adventure-fixed-seed-full-run");
        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    public void PlanParserRejectsUnityOrUnknownSchema()
    {
        AssertThrown(() => GodotGameplayScenarioPlan.Parse("{\"schemaVersion\":1,\"runtime\":\"Unity\"}"))
            .IsInstanceOf<InvalidDataException>();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PlanParserAcceptsTheTypeScriptCompiledGodotFixture()
    {
        string path = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "Tests",
            "gameplay-specs", "godot", "runner-home-quit.plan.json"));
        GodotGameplayScenarioPlan plan = GodotGameplayScenarioPlan.Parse(File.ReadAllText(path));

        AssertThat(plan.Runtime).IsEqual("Godot");
        AssertThat(plan.ScenarioName).IsEqual("GodotGameplayRunner.RunnerHomeQuit");
        AssertThat(plan.RequiredCapabilities.Length).IsEqual(4);
    }

    [TestCase]
    public void PlanContractRejectsCapabilityAdapterAndProbeTampering()
    {
        GodotGameplayScenarioPlan canonical = HomeQuitPlan("Contract.Canonical");
        canonical.ValidateContract();

        AssertThrown(() => (canonical with { RequiredCapabilities = ["action:clickPointerTarget"] }).ValidateContract())
            .IsInstanceOf<InvalidDataException>();
        AssertThrown(() => (canonical with { RequiredAdapters = ["PlayerInput"] }).ValidateContract())
            .IsInstanceOf<InvalidDataException>();
        AssertThrown(() => (canonical with
            {
                ProbeRequests = [new GodotGameplayProbeRequest("runtimeStateHashEquals", "UI", null, [])]
            }).ValidateContract()).IsInstanceOf<InvalidDataException>();
    }

    [TestCase]
    public void PlanContractAcceptsAdventureTargetsObservablesAndAssertions()
    {
        var actions = new[]
        {
            new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "4,4", Parameters(("targetKind", "AdventureCell"))),
            new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "party-mage", Parameters(("targetKind", "AdventureActor"))),
            new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "campfire", Parameters(("targetKind", "AdventureObject"))),
            new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "layer04-rest", Parameters(("targetKind", "RouteNode"))),
            new GodotGameplayPlanStep("waitForPlayerObservable", "PlayerInput", null, Parameters(("observable", "adventureBoardReady")))
        };
        var assertions = new[]
        {
            new GodotGameplayPlanAssertion("adventureActorCellEquals", "Map", "party-mage", JsonSerializer.SerializeToElement("4,4"), []),
            new GodotGameplayPlanAssertion("activeAdventureLeaderEquals", "Map", null, JsonSerializer.SerializeToElement("party-mage"), []),
            new GodotGameplayPlanAssertion("immediateSuccessorNodeIdsEqual", "Map", null,
                JsonSerializer.SerializeToElement(new[] { "layer04-rest", "layer04-store", "layer04-event" }), []),
            new GodotGameplayPlanAssertion("runSaveSchemaVersionEquals", "Map", null, JsonSerializer.SerializeToElement(10), [])
        };
        GodotGameplayScenarioPlan plan = Plan("Runner.AdventureContract", [], actions, assertions) with { SchemaVersion = 3 };

        plan.ValidateContract();
    }

    [TestCase]
    public async Task CheckpointMismatchIsRejectedBeforeSceneExecution()
    {
        ValidatedGodotRunCheckpoint actual = GodotGameplayCheckpointCatalog.Create("reload-pending-battle-v1");
        GodotGameplayScenarioPlan plan = CheckpointPlan(actual) with
        {
            Checkpoint = new GodotGameplayCheckpoint(actual.Id, "validated_checkpoint", new string('0', 64), actual.Path)
        };
        try
        {
            await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);
            AssertThat(false).IsTrue();
        }
        catch (InvalidDataException exception)
        {
            AssertThat(exception.Message).Contains("checkpoint metadata");
        }
    }

    [TestCase]
    public void PlanContractRejectsCheckpointSetupMetadataTampering()
    {
        ValidatedGodotRunCheckpoint checkpoint = GodotGameplayCheckpointCatalog.Create("reload-pending-battle-v1");
        GodotGameplayScenarioPlan canonical = CheckpointPlan(checkpoint);
        canonical.ValidateContract();
        GodotGameplayPlanStep tampered = canonical.SetupActions.Single(step => step.Kind == "loadValidatedCheckpoint") with
        {
            Parameters = Parameters(("id", "defeat-no-summon-v1"), ("path", checkpoint.Path),
                ("semanticHash", checkpoint.SemanticHash))
        };

        AssertThrown(() => (canonical with { SetupActions = [tampered] }).ValidateContract())
            .IsInstanceOf<InvalidDataException>();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ValidatedCheckpointRunsInIsolationAndCleansTheScene()
    {
        ValidatedGodotRunCheckpoint checkpoint = GodotGameplayCheckpointCatalog.Create("reload-pending-battle-v1");

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(
            CheckpointPlan(checkpoint));

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");

        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
        AssertThat(result.Trace.Count).IsEqual(2);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task FailedPointerActionWritesTraceAndStillCleansTheScene()
    {
        var step = new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "missing-control",
            Parameters(("targetKind", "UiElement")));
        GodotGameplayScenarioPlan plan = Plan("Runner.BadPointer", [], [step], []);

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        AssertThat(result.Succeeded).IsFalse();
        AssertThat(result.FailureKind).IsEqual(GodotGameplayFailureKind.Action);
        AssertThat(result.Trace.Count).IsEqual(1);
        AssertThat(result.Trace[0].Succeeded).IsFalse();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task MainRestartReusesTheIsolatedStoreAndProductionInput()
    {
        var restart = new GodotGameplayPlanStep("restartGodotMain", "UI", null, []);
        var pause = new GodotGameplayPlanStep("pressInputKey", "PlayerInput", null,
            Parameters(("key", "Escape")));
        var quit = new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "SAVE AND QUIT",
            Parameters(("targetKind", "UiElement")));
        var assertion = BoolAssertion("runtimeHasNoErrors", "UI", true);
        GodotGameplayScenarioPlan plan = Plan("Runner.Restart", [], [restart, pause, quit], [assertion]);

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.Trace.Count).IsEqual(4);
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ObservableFrameLimitProducesNoProgressTrace()
    {
        var wait = new GodotGameplayPlanStep("waitForPlayerObservable", "PlayerInput", null,
            Parameters(("observable", "battleReady"), ("maximumFrames", 1)));
        GodotGameplayScenarioPlan plan = Plan("Runner.NoProgress", [], [wait], []);

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        AssertThat(result.Succeeded).IsFalse();
        AssertThat(result.FailureKind).IsEqual(GodotGameplayFailureKind.NoProgress);
        AssertThat(result.Trace.Count).IsEqual(1);
        AssertThat(result.Trace[0].Diagnostic).Contains("observable_not_reached");
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PauseSpeedAndRightClickUseTheirRequestedProductionSemantics()
    {
        ValidatedGodotRunCheckpoint checkpoint = GodotGameplayCheckpointCatalog.Create("reload-pending-battle-v1");
        var pause = new GodotGameplayPlanStep("setPresentationPaused", "UI", null,
            Parameters(("paused", true)));
        var speed = new GodotGameplayPlanStep("setPresentationSpeed", "UI", null,
            Parameters(("speed", 1f)));
        var rightClick = new GodotGameplayPlanStep("rightClickPointerTarget", "PlayerInput", "CurrentPlayer",
            Parameters(("targetKind", "BattleUnit")));
        var resume = new GodotGameplayPlanStep("setPresentationPaused", "UI", null,
            Parameters(("paused", false)));
        GodotGameplayScenarioPlan plan = Plan("Runner.BattleControls",
            [LoadCheckpointStep(checkpoint)],
            [pause, speed, rightClick, resume], [BoolAssertion("productionSaveUnchanged", "Map", true)]) with
        {
            Checkpoint = new GodotGameplayCheckpoint(checkpoint.Id, "validated_checkpoint", checkpoint.SemanticHash, checkpoint.Path)
        };

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            Console.WriteLine($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine, result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");

        AssertThat(result.ErrorCode).IsNull();
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.Trace.Count).IsEqual(6);
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PlayBattleThroughInputHonorsMaximumActionsAndUsesBattleUi()
    {
        ValidatedGodotRunCheckpoint checkpoint = GodotGameplayCheckpointCatalog.Create("reload-pending-battle-v1");
        var play = new GodotGameplayPlanStep("playBattleThroughInput", "PlayerInput", null,
            Parameters(("maximumActions", 1)));
        GodotGameplayScenarioPlan plan = Plan("Runner.BattleActionLimit",
            [LoadCheckpointStep(checkpoint)], [play], []) with
        {
            Checkpoint = new GodotGameplayCheckpoint(checkpoint.Id, "validated_checkpoint", checkpoint.SemanticHash, checkpoint.Path)
        };

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        AssertThat(result.Succeeded).IsFalse();
        AssertThat(result.ErrorCode).StartsWith("battle_action_limit:");
        AssertThat(result.FailureKind).IsEqual(GodotGameplayFailureKind.NoProgress);
        AssertThat(result.Trace.Last().Succeeded).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task StepAndScenarioTimeoutsAreClassifiedSeparately()
    {
        var wait = new GodotGameplayPlanStep("waitForPlayerObservable", "PlayerInput", null,
            Parameters(("observable", "battleReady"), ("maximumFrames", 100000)));
        GodotGameplayScenarioPlan stepPlan = Plan("Runner.StepTimeout", [], [wait], []) with
        {
            Watchdog = new GodotGameplayWatchdog(20, 80, 300000, 4)
        };
        GodotGameplayScenarioPlan scenarioPlan = stepPlan with
        {
            ScenarioName = "Runner.ScenarioTimeout",
            Watchdog = new GodotGameplayWatchdog(30000, 80, 20, 4)
        };

        GodotGameplayScenarioResult step = await new GodotGameplayRuntimeRunner().ExecuteAsync(stepPlan);
        GodotGameplayScenarioResult scenario = await new GodotGameplayRuntimeRunner().ExecuteAsync(scenarioPlan);

        AssertThat(step.ErrorCode).StartsWith("step.timeout:1:waitForPlayerObservable:");
        AssertThat(step.Trace.Single().Succeeded).IsFalse();
        AssertThat(scenario.ErrorCode).IsEqual("scenario.timeout");
        AssertThat(scenario.Trace.Single().Succeeded).IsFalse();
    }

    private static GodotGameplayScenarioPlan HomeQuitPlan(string name)
    {
        var setup = new GodotGameplayPlanStep("initializePlayerInput", "PlayerInput", null, []);
        var quit = new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "Quit",
            Parameters(("targetKind", "UiElement")));
        return Plan(name, [setup], [quit], [BoolAssertion("runtimeHasNoErrors", "UI", true)]);
    }

    internal static GodotGameplayScenarioPlan WithParty(GodotGameplayScenarioPlan plan, IReadOnlyList<string> party,
        string scenarioName)
    {
        if (party.Count != 3 || !party.Contains("pure_run_demonbound", StringComparer.Ordinal))
            throw new ArgumentException("A Demonbound production party must contain exactly three actors including Demonbound.", nameof(party));
        int firstSelection = Array.FindIndex(plan.RuntimeActions, action => action.Parameters.TryGetValue("targetKind", out JsonElement kind) &&
            kind.GetString() == "AdventureActor");
        int exit = Array.FindIndex(plan.RuntimeActions, action => action.Target == "start-exit");
        if (firstSelection < 0 || exit <= firstSelection) throw new InvalidDataException("The source full-run plan has no party-selection segment.");
        GodotGameplayPlanStep[] selection = party.Select(actor => new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", actor,
            Parameters(("targetKind", "AdventureActor")))).ToArray();
        var actions = new List<GodotGameplayPlanStep>();
        actions.AddRange(plan.RuntimeActions.Take(firstSelection));
        actions.AddRange(selection);
        actions.Add(plan.RuntimeActions[exit]);
        actions.AddRange(party.Where(actor => actor != "pure_run_demonbound").Select(StartingSkillAction));
        actions.AddRange(plan.RuntimeActions.Skip(exit + 1).SkipWhile(action => action.Target?.StartsWith("starting_skill__", StringComparison.Ordinal) == true));
        GodotGameplayPlanAssertion[] assertions = plan.AssertionPlans.Where(value =>
            value.Kind != "terminalSummaryOutcomeEquals").ToArray();
        GodotGameplayProbeRequest[] probes = assertions.Select(value =>
            new GodotGameplayProbeRequest(value.Kind, value.Adapter, value.Target, value.Parameters)).ToArray();
        string[] adapters = plan.SetupActions.Concat(actions).Select(value => value.Adapter)
            .Concat(assertions.Select(value => value.Adapter)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string[] capabilities = plan.SetupActions.Select(value => "setup:" + value.Kind)
            .Concat(actions.Select(value => "action:" + value.Kind))
            .Concat(assertions.Select(value => "assertion:" + value.Kind)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return plan with { ScenarioName = scenarioName, RequiredAdapters = adapters, RequiredCapabilities = capabilities,
            RuntimeActions = actions.ToArray(), AssertionPlans = assertions, ProbeRequests = probes };
    }

    private static GodotGameplayPlanStep StartingSkillAction(string actor) => actor switch
    {
        "pure_run_amazon" => new("clickPointerTarget", "PlayerInput", "starting_skill__skill_amazon_thrust_lv1", Parameters(("targetKind", "UiElement"))),
        "pure_run_mage" => new("clickPointerTarget", "PlayerInput", "starting_skill__skill_mage_fireball_lv1", Parameters(("targetKind", "UiElement"))),
        "pure_run_necromancer" => new("clickPointerTarget", "PlayerInput", "starting_skill__skill_necromancer_summon-skeleton_lv1", Parameters(("targetKind", "UiElement"))),
        _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, "Unknown production party actor.")
    };

    internal static GodotGameplayScenarioPlan LoadCompiledPlan(string planName)
    {
        string path = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "Tests",
            "gameplay-specs", "godot", planName + ".plan.json"));
        return GodotGameplayScenarioPlan.Parse(File.ReadAllText(path));
    }

    private static GodotGameplayScenarioPlan CheckpointPlan(ValidatedGodotRunCheckpoint checkpoint)
    {
        GodotGameplayPlanStep load = LoadCheckpointStep(checkpoint);
        return Plan("Runner.Checkpoint", [load], [], [BoolAssertion("productionSaveUnchanged", "Map", true)]) with
        {
            Checkpoint = new GodotGameplayCheckpoint(checkpoint.Id, "validated_checkpoint", checkpoint.SemanticHash, checkpoint.Path)
        };
    }

    private static GodotGameplayPlanStep LoadCheckpointStep(ValidatedGodotRunCheckpoint checkpoint) =>
        new("loadValidatedCheckpoint", "Map", null,
            Parameters(("id", checkpoint.Id), ("path", checkpoint.Path), ("semanticHash", checkpoint.SemanticHash)));

    private static GodotGameplayScenarioPlan Plan(string name, GodotGameplayPlanStep[] setup,
        GodotGameplayPlanStep[] actions, GodotGameplayPlanAssertion[] assertions)
    {
        string[] adapters = setup.Concat(actions).Select(step => step.Adapter)
            .Concat(assertions.Select(assertion => assertion.Adapter)).Distinct().Order().ToArray();
        string[] capabilities = setup.Select(step => "setup:" + step.Kind)
            .Concat(actions.Select(step => "action:" + step.Kind))
            .Concat(assertions.Select(assertion => "assertion:" + assertion.Kind)).Distinct().Order().ToArray();
        GodotGameplayProbeRequest[] probes = assertions.Select(assertion =>
            new GodotGameplayProbeRequest(assertion.Kind, assertion.Adapter, assertion.Target, assertion.Parameters)).ToArray();
        return new GodotGameplayScenarioPlan(2, "Godot", name, adapters, capabilities, setup, actions,
            assertions, probes, new GodotGameplaySaveIsolation("user://qa-runner", true),
            new GodotGameplayWatchdog(30000, 80, 300000, 4), null);
    }

    private static GodotGameplayPlanAssertion BoolAssertion(string kind, string adapter, bool value) =>
        new(kind, adapter, null, Json(value), []);

    private static Dictionary<string, JsonElement> Parameters(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value), StringComparer.Ordinal);

    private static JsonElement Json(bool value) => JsonDocument.Parse(value ? "true" : "false").RootElement.Clone();

}
