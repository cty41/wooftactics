using System.Text.Json;

namespace Tactics.Godot.Tests.GameplaySpec;

public sealed record GodotGameplayScenarioPlan(
    int SchemaVersion,
    string Runtime,
    string ScenarioName,
    string[] RequiredAdapters,
    string[] RequiredCapabilities,
    GodotGameplayPlanStep[] SetupActions,
    GodotGameplayPlanStep[] RuntimeActions,
    GodotGameplayPlanAssertion[] AssertionPlans,
    GodotGameplayProbeRequest[] ProbeRequests,
    GodotGameplaySaveIsolation SaveIsolation,
    GodotGameplayWatchdog Watchdog,
    GodotGameplayCheckpoint? Checkpoint)
{
    public static GodotGameplayScenarioPlan Parse(string json)
    {
        GodotGameplayScenarioPlan? plan = JsonSerializer.Deserialize<GodotGameplayScenarioPlan>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (plan is null || plan.SchemaVersion is not (2 or 3) || !string.Equals(plan.Runtime, "Godot", StringComparison.Ordinal))
            throw new InvalidDataException("A Godot ExecutableScenarioPlan v2 or v3 is required.");
        plan.ValidateContract();
        return plan;
    }

    public void ValidateContract()
    {
        if (SaveIsolation.Root != "user://qa-runner" || !SaveIsolation.ProtectProductionSave)
            throw new InvalidDataException("Godot gameplay plans must protect the production save under user://qa-runner.");
        if (Checkpoint is not null && (Checkpoint.Source != "validated_checkpoint" ||
            Checkpoint.SemanticHash.Length != 64 || Checkpoint.SemanticHash.Any(value => value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))))
            throw new InvalidDataException("Checkpoint provenance is invalid.");
        int loadCheckpointCount = SetupActions.Count(step => step.Kind == "loadValidatedCheckpoint");
        if (Checkpoint is null && loadCheckpointCount != 0 || Checkpoint is not null && loadCheckpointCount != 1)
            throw new InvalidDataException("Checkpoint metadata and loadValidatedCheckpoint must occur together exactly once.");
        if (Checkpoint is not null)
        {
            GodotGameplayPlanStep load = SetupActions.Single(step => step.Kind == "loadValidatedCheckpoint");
            if (!MatchesCheckpointParameter(load, "id", Checkpoint.Id) ||
                !MatchesCheckpointParameter(load, "path", Checkpoint.Path) ||
                !MatchesCheckpointParameter(load, "semanticHash", Checkpoint.SemanticHash))
                throw new InvalidDataException("loadValidatedCheckpoint parameters do not match checkpoint metadata.");
        }
        if (Watchdog.StepTimeoutMs <= 0 || Watchdog.BattleRoundLimit is <= 0 or > 80 ||
            Watchdog.ScenarioTimeoutMs <= 0 || Watchdog.NoProgressLimit <= 0)
            throw new InvalidDataException("Watchdog limits are invalid.");
        var expectedCapabilities = new List<string>();
        var expectedAdapters = new HashSet<string>(StringComparer.Ordinal);
        ValidateSteps("setup", SetupActions, expectedCapabilities, expectedAdapters);
        ValidateSteps("action", RuntimeActions, expectedCapabilities, expectedAdapters);
        ValidateSteps("assertion", AssertionPlans.Select(value => new GodotGameplayPlanStep(value.Kind, value.Adapter, value.Target, value.Parameters)), expectedCapabilities, expectedAdapters);
        string[] expected = expectedCapabilities.Distinct().Order(StringComparer.Ordinal).ToArray();
        string[] actual = RequiredCapabilities.Distinct().Order(StringComparer.Ordinal).ToArray();
        if (!expected.SequenceEqual(actual)) throw new InvalidDataException("requiredCapabilities does not match executable steps.");
        if (!expectedAdapters.Order(StringComparer.Ordinal).SequenceEqual(RequiredAdapters.Distinct().Order(StringComparer.Ordinal)))
            throw new InvalidDataException("requiredAdapters does not match executable steps.");
        foreach (GodotGameplayPlanStep step in SetupActions.Concat(RuntimeActions)) ValidateParameters(step);
        foreach (GodotGameplayPlanAssertion assertion in AssertionPlans) ValidateAssertion(assertion);
        if (ProbeRequests.Length != AssertionPlans.Length || ProbeRequests.Where((probe, index) =>
                probe.Kind != AssertionPlans[index].Kind || probe.Adapter != AssertionPlans[index].Adapter || probe.Target != AssertionPlans[index].Target ||
                JsonSerializer.Serialize(probe.Parameters) != JsonSerializer.Serialize(AssertionPlans[index].Parameters)).Any())
            throw new InvalidDataException("Probe requests do not correspond to assertions.");
    }

    private static bool MatchesCheckpointParameter(GodotGameplayPlanStep step, string key, string expected) =>
        step.Parameters.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private void ValidateSteps(string phase, IEnumerable<GodotGameplayPlanStep> steps, List<string> capabilities,
        HashSet<string> adapters)
    {
        foreach (GodotGameplayPlanStep step in steps)
        {
            string expectedAdapter = GodotGameplayCapabilities.AdapterFor(phase, step.Kind)
                ?? throw new InvalidDataException($"Unsupported Godot {phase} '{step.Kind}'.");
            if (step.Adapter != expectedAdapter || !RequiredAdapters.Contains(expectedAdapter, StringComparer.Ordinal))
                throw new InvalidDataException($"{step.Kind} requires declared adapter {expectedAdapter}.");
            capabilities.Add($"{phase}:{step.Kind}");
            adapters.Add(expectedAdapter);
        }
    }

    private static void ValidateParameters(GodotGameplayPlanStep step)
    {
        bool HasString(string key) => step.Parameters.TryGetValue(key, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString());
        bool HasNumber(string key) => step.Parameters.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.Number;
        switch (step.Kind)
        {
            case "movePointerToTarget" or "clickPointerTarget" or "rightClickPointerTarget":
                string kind = HasString("targetKind") ? step.Parameters["targetKind"].GetString()! : "UiElement";
                if (kind is not ("UiElement" or "MapNode" or "BattleUnit" or "BattleCell" or
                    "AdventureCell" or "AdventureActor" or "AdventureObject" or "AdventureExitApproach" or "RouteNode") ||
                    string.IsNullOrWhiteSpace(step.Target) && !new[] { "elementName", "nodeId", "unitId", "actorId", "objectId", "routeNodeId", "cell" }.Any(HasString))
                    throw new InvalidDataException($"{step.Kind} has an invalid pointer target.");
                break;
            case "pressInputKey" when !HasString("key"):
                throw new InvalidDataException("pressInputKey requires key.");
            case "waitForPlayerObservable":
                if (!HasString("observable") || step.Parameters["observable"].GetString() is not
                    ("uiElement" or "uiVisible" or "uiHidden" or "mapReady" or "battleReady" or "humanTurn" or "battleEnded" or
                     "adventureBoardReady" or "adventureLeaderChanged" or "adventureInteractionResolved" or "exitCommitted" or
                     "eventBattleReady" or "adventureSceneChanged"))
                    throw new InvalidDataException("waitForPlayerObservable has an invalid observable.");
                string observable = step.Parameters["observable"].GetString()!;
                if (observable == "uiElement" && string.IsNullOrWhiteSpace(step.Target) && !HasString("elementName") ||
                    observable is "uiVisible" or "uiHidden" && string.IsNullOrWhiteSpace(step.Target) && !HasString("uiId"))
                    throw new InvalidDataException("waitForPlayerObservable requires a UI locator.");
                if (step.Parameters.TryGetValue("maximumFrames", out JsonElement frames) &&
                    (frames.ValueKind != JsonValueKind.Number || frames.GetInt32() <= 0))
                    throw new InvalidDataException("waitForPlayerObservable maximumFrames is invalid.");
                break;
            case "waitForFrames" when !HasNumber("count") || step.Parameters["count"].GetInt32() <= 0:
                throw new InvalidDataException("waitForFrames requires a positive count.");
            case "playBattleThroughInput" when step.Parameters.TryGetValue("maximumActions", out JsonElement maximum) &&
                (maximum.ValueKind != JsonValueKind.Number || maximum.GetInt32() is < 1 or > 100):
                throw new InvalidDataException("playBattleThroughInput maximumActions is invalid.");
            case "useBattleSkillThroughInput":
                if (!HasString("actorId") || !HasString("skillId") ||
                    step.Parameters.TryGetValue("maximumActions", out JsonElement skillMaximum) &&
                    (skillMaximum.ValueKind != JsonValueKind.Number || skillMaximum.GetInt32() is < 1 or > 100))
                    throw new InvalidDataException("useBattleSkillThroughInput parameters are invalid.");
                break;
            case "endTurnUntilPresentationNumber":
                if (!HasString("kind") || step.Parameters["kind"].GetString() is not
                    ("Normal" or "Critical" or "Heal" or "Mana" or "Miss") ||
                    !step.Parameters.TryGetValue("maximumActions", out JsonElement numberActions) ||
                    numberActions.ValueKind != JsonValueKind.Number || numberActions.GetInt32() is < 1 or > 100)
                    throw new InvalidDataException("endTurnUntilPresentationNumber parameters are invalid.");
                break;
            case "setPresentationPaused" when !step.Parameters.TryGetValue("paused", out JsonElement paused) ||
                paused.ValueKind is not (JsonValueKind.True or JsonValueKind.False):
                throw new InvalidDataException("setPresentationPaused requires paused.");
            case "setPresentationSpeed" when !HasNumber("speed") ||
                step.Parameters["speed"].GetDouble() is not (0.5 or 1 or 2 or 4):
                throw new InvalidDataException("setPresentationSpeed has an unsupported speed.");
        }
    }

    private static void ValidateAssertion(GodotGameplayPlanAssertion assertion)
    {
        JsonValueKind kind = assertion.Expected.ValueKind;
        bool valid = assertion.Kind switch
        {
            "inventoryProjectionEnteredBattle" or "activeRunExistsEquals" or "runtimeHasNoErrors" or
                "productionSaveUnchanged" => kind is JsonValueKind.True or JsonValueKind.False,
            "presentationNodeCountEquals" or "checkpointRevisionEquals" or "storeOfferCountEquals" or
                "storeSoldOfferCountEquals" or "runSaveSchemaVersionEquals" => kind == JsonValueKind.Number,
            "demonboundCorruptionEquals" => kind == JsonValueKind.Number && !string.IsNullOrWhiteSpace(assertion.Target),
            "demonboundPossessedEquals" => kind is JsonValueKind.True or JsonValueKind.False && !string.IsNullOrWhiteSpace(assertion.Target),
            "battleSkillReceiptEquals" => kind == JsonValueKind.Object,
            "terminalSummaryOutcomeEquals" or "presentationNumberEquals" or "runtimeStateHashEquals" or
                "adventureActorCellEquals" or "activeAdventureLeaderEquals" or "runNodeLifecycleEquals" or
                "adventureObjectStateEquals" or "eventResolutionEquals" or "pendingBattleContextKindEquals" or
                "escortStateEquals" => kind == JsonValueKind.String,
            "immediateSuccessorNodeIdsEqual" or "pendingPartyOrderEquals" or "activePartyStartingSkillIdsEqual" or "partyResourceSummaryEquals" => kind == JsonValueKind.Array &&
                assertion.Expected.EnumerateArray().All(value => value.ValueKind == JsonValueKind.String),
            "backpackContainsContentId" or "protectedNpcAliveEquals" or "partyAllLivingAtFullResourcesEquals" => kind is JsonValueKind.True or JsonValueKind.False,
            _ => false
        };
        if (!valid) throw new InvalidDataException($"Assertion '{assertion.Kind}' has an invalid expected value.");
    }
}

public sealed record GodotGameplayPlanStep(string Kind, string Adapter, string? Target, Dictionary<string, JsonElement> Parameters);
public sealed record GodotGameplayPlanAssertion(string Kind, string Adapter, string? Target, JsonElement Expected, Dictionary<string, JsonElement> Parameters);
public sealed record GodotGameplayProbeRequest(string Kind, string Adapter, string? Target, Dictionary<string, JsonElement> Parameters);
public sealed record GodotGameplaySaveIsolation(string Root, bool ProtectProductionSave);
public sealed record GodotGameplayWatchdog(int StepTimeoutMs, int BattleRoundLimit, int ScenarioTimeoutMs, int NoProgressLimit);
public sealed record GodotGameplayCheckpoint(string Id, string Source, string SemanticHash, string Path);

internal static class GodotGameplayCapabilities
{
    private static readonly Dictionary<string, string> Adapters = new(StringComparer.Ordinal)
    {
        ["setup:loadValidatedCheckpoint"] = "Map", ["setup:initializePlayerInput"] = "PlayerInput",
        ["action:movePointerToTarget"] = "PlayerInput", ["action:clickPointerTarget"] = "PlayerInput",
        ["action:rightClickPointerTarget"] = "PlayerInput", ["action:pressInputKey"] = "PlayerInput",
        ["action:waitForPlayerObservable"] = "PlayerInput", ["action:waitForFrames"] = "PlayerInput",
        ["action:playBattleThroughInput"] = "PlayerInput", ["action:endTurnOnlyUntilTerminal"] = "Battle",
        ["action:useBattleSkillThroughInput"] = "PlayerInput",
        ["action:endTurnUntilPresentationNumber"] = "Battle",
        ["action:restartGodotMain"] = "UI", ["action:setPresentationPaused"] = "UI", ["action:setPresentationSpeed"] = "UI",
        ["assertion:inventoryProjectionEnteredBattle"] = "Battle", ["assertion:terminalSummaryOutcomeEquals"] = "Map",
        ["assertion:activeRunExistsEquals"] = "Map", ["assertion:presentationNumberEquals"] = "UI",
        ["assertion:presentationNodeCountEquals"] = "UI", ["assertion:productionSaveUnchanged"] = "Map",
        ["assertion:checkpointRevisionEquals"] = "Map", ["assertion:runtimeStateHashEquals"] = "UI",
        ["assertion:demonboundCorruptionEquals"] = "Battle", ["assertion:demonboundPossessedEquals"] = "Battle",
        ["assertion:battleSkillReceiptEquals"] = "Battle",
        ["assertion:adventureActorCellEquals"] = "Map", ["assertion:activeAdventureLeaderEquals"] = "Map",
        ["assertion:runNodeLifecycleEquals"] = "Map", ["assertion:immediateSuccessorNodeIdsEqual"] = "Map",
        ["assertion:adventureObjectStateEquals"] = "Map", ["assertion:storeOfferCountEquals"] = "Map",
        ["assertion:storeSoldOfferCountEquals"] = "Map", ["assertion:backpackContainsContentId"] = "Map",
        ["assertion:eventResolutionEquals"] = "Map", ["assertion:pendingBattleContextKindEquals"] = "Map",
        ["assertion:escortStateEquals"] = "Map", ["assertion:protectedNpcAliveEquals"] = "Map",
        ["assertion:runSaveSchemaVersionEquals"] = "Map",
        ["assertion:pendingPartyOrderEquals"] = "Map",
        ["assertion:activePartyStartingSkillIdsEqual"] = "Map",
        ["assertion:partyAllLivingAtFullResourcesEquals"] = "Map",
        ["assertion:partyResourceSummaryEquals"] = "Map",
        ["assertion:runtimeHasNoErrors"] = "UI"
    };
    public static string? AdapterFor(string phase, string kind) => Adapters.GetValueOrDefault($"{phase}:{kind}");
}

public enum GodotGameplayFailureKind { Contract, Timeout, NoProgress, Action, Assertion, Cleanup }
public sealed record GodotGameplayTraceEntry(int Ordinal, string Phase, string Kind, bool Succeeded, string StateHash, long ElapsedMs, string? Diagnostic);
public sealed record GodotGameplayScenarioResult(string ScenarioName, bool Succeeded, GodotGameplayFailureKind? FailureKind,
    string? ErrorCode, IReadOnlyList<GodotGameplayTraceEntry> Trace, bool ProductionSaveUnchanged,
    int RemainingTemporaryNodes, string ProductionSaveBefore, string ProductionSaveAfter,
    GodotDemonboundRunMetrics? DemonboundMetrics = null);
