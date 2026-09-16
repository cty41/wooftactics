using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Application.Battle;
using Tactics.Application.Runs;
using Tactics.Godot.Adapter.Runtime;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Godot.Tests.GameplaySpec;

public interface IGodotGameplayDecisionSource
{
    Task ExecuteAsync(GodotGameplayRuntimeContext context, CancellationToken cancellationToken);
}

public sealed class ScriptedDecisionSource(IEnumerable<string> buttonTexts) : IGodotGameplayDecisionSource
{
    public async Task ExecuteAsync(GodotGameplayRuntimeContext context, CancellationToken cancellationToken)
    {
        foreach (string text in buttonTexts) await context.ClickButtonAsync(text, cancellationToken);
    }
}

public sealed class EndTurnOnlyDecisionSource(int? maximumActions = null) : IGodotGameplayDecisionSource
{
    public async Task ExecuteAsync(GodotGameplayRuntimeContext context, CancellationToken cancellationToken)
    {
        int actions = 0;
        int limit = Math.Min(maximumActions ?? context.Plan.Watchdog.BattleRoundLimit, context.Plan.Watchdog.BattleRoundLimit);
        while (!context.IsTerminal && actions < limit)
        {
            if (context.IsTerminalPending || !context.CanSubmitPlayerInput)
            {
                await context.WaitForAutomaticProgressAsync(cancellationToken);
                continue;
            }
            await context.PressKeyAsync(Key.Enter, cancellationToken);
            actions++;
        }
        if (!context.IsTerminal) throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress, "battle_round_limit");
    }
}

public sealed class ProductionInputDecisionSource(int maximumActions) : IGodotGameplayDecisionSource
{
    public async Task ExecuteAsync(GodotGameplayRuntimeContext context, CancellationToken cancellationToken)
    {
        int actions = 0;
        while (context.HasActiveBattle && !context.IsTerminal && !context.IsAdventureEventResolved && actions < maximumActions)
        {
            if (context.IsTerminalPending || !context.CanSubmitPlayerInput)
            {
                await context.WaitForAutomaticProgressAsync(cancellationToken);
                continue;
            }
            BattleUiSnapshot snapshot = context.Main.CaptureVisibleBattleSnapshot()!;
            if (await TryUseFirstSkillAsync(context, snapshot, cancellationToken))
            {
                actions++;
                await context.WaitUntilPlayerReadyOrTerminalAsync(cancellationToken);
                continue;
            }
            if (await TryMoveTowardEnemyAsync(context, snapshot, cancellationToken))
            {
                actions++;
                await context.WaitUntilPlayerReadyOrTerminalAsync(cancellationToken);
                continue;
            }
            await context.PressKeyAsync(Key.Enter, cancellationToken);
            actions++;
        }
        if (context.HasActiveBattle && !context.IsTerminal && !context.IsAdventureEventResolved)
        {
            BattleUiSnapshot? final = context.Main.CaptureTestProbe().BattleSnapshot;
            string units = final is null ? "none" : string.Join(",", final.Units.Where(value => value.IsAlive)
                .Select(value => $"{value.UnitId.Value}@{value.Cell}:{value.CurrentHealth}/{value.MaxHealth}:p{value.PlayerNumber}"));
            string skills = final is null ? "none" : string.Join(",", final.SkillAvailability?.Select(value =>
                $"{value.SkillId.Value}:{value.IsAvailable}:{value.FailureCode ?? "ok"}") ?? []);
            string targets = final is null ? "none" : string.Join(",", final.LegalTargets.Select(value =>
                $"{value.SkillId.Value}@{value.Cell}:{value.UnitId?.Value ?? "cell"}"));
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
                $"battle_action_limit:round={final?.Round.ToString() ?? "none"}:active={final?.ActiveUnitId.Value ?? "none"}:units={units}:skills={skills}:targets={targets}");
        }
    }

    private static async Task<bool> TryUseFirstSkillAsync(GodotGameplayRuntimeContext context,
        BattleUiSnapshot snapshot, CancellationToken cancellationToken)
    {
        BattleUiUnitSnapshot active = snapshot.Units.Single(value => value.UnitId == snapshot.ActiveUnitId);
        HashSet<UnitInstanceId> enemyIds = snapshot.Units.Where(value => value.IsAlive && value.PlayerNumber != active.PlayerNumber)
            .Select(value => value.UnitId).ToHashSet();
        BattleUiTarget? selectedTarget = snapshot.TargetingMode == BattleTargetingMode.Skill
            ? snapshot.LegalTargets.FirstOrDefault(value => value.SkillId == snapshot.SelectedSkillId &&
                (value.UnitId is UnitInstanceId id && enemyIds.Contains(id) ||
                 snapshot.Units.Any(unit => unit.IsAlive && enemyIds.Contains(unit.UnitId) && unit.Cell == value.Cell)))
            : null;
        if (selectedTarget is not null)
        {
            await context.ClickPointerAsync($"{selectedTarget.Cell.X},{selectedTarget.Cell.Y}",
                Parameters(("targetKind", "BattleCell")), cancellationToken);
            return true;
        }
        foreach (BattleUiSkillAvailability availability in snapshot.SkillAvailability?.Where(value => value.IsAvailable &&
                     snapshot.ActiveSkills.Any(skill => skill.ContentId == value.SkillId && !skill.Hidden && !skill.IsPassive &&
                         skill.ExecutionKind != SkillExecutionKind.Meditation) &&
                     snapshot.LegalTargets.Any(target => target.SkillId == value.SkillId &&
                         (target.UnitId is UnitInstanceId targetId && enemyIds.Contains(targetId) ||
                          snapshot.Units.Any(unit => unit.IsAlive && enemyIds.Contains(unit.UnitId) && unit.Cell == target.Cell)))) ?? [])
        {
            await context.ClickPointerAsync("SkillAction_" + availability.SkillId.Value.Replace('.', '_'),
                Parameters(("targetKind", "UiElement")), cancellationToken);
            BattleUiSnapshot? targeted = await context.WaitForSkillTargetingAsync(availability.SkillId, cancellationToken);
            if (targeted is null)
            {
                await context.CancelBattleTargetingAsync(cancellationToken);
                continue;
            }
            BattleUiTarget? target = targeted.LegalTargets.FirstOrDefault(value => value.SkillId == availability.SkillId &&
                (value.UnitId is UnitInstanceId id && enemyIds.Contains(id) ||
                 targeted.Units.Any(unit => unit.IsAlive && enemyIds.Contains(unit.UnitId) && unit.Cell == value.Cell)));
            if (target is not null)
            {
                BattleAuthorityStamp beforeCommit = context.CaptureBattleAuthorityStamp();
                await context.ClickPointerAsync($"{target.Cell.X},{target.Cell.Y}",
                    Parameters(("targetKind", "BattleCell")), cancellationToken);
                await context.WaitForBattleCommitAsync(beforeCommit, cancellationToken);
                return true;
            }
            await context.PressKeyAsync(Key.Escape, cancellationToken);
        }
        return false;
    }

    private static async Task<bool> TryMoveTowardEnemyAsync(GodotGameplayRuntimeContext context,
        BattleUiSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.MoveAvailability?.IsAvailable != true || snapshot.LegalMoveCells.Count == 0) return false;
        BattleUiUnitSnapshot active = snapshot.Units.Single(value => value.UnitId == snapshot.ActiveUnitId);
        BattleUiUnitSnapshot[] enemies = snapshot.Units
            .Where(value => value.IsAlive && value.PlayerNumber != active.PlayerNumber).ToArray();
        if (enemies.Length == 0) return false;
        static int Distance(GridPoint left, GridPoint right) => Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
        int currentDistance = enemies.Min(enemy => Distance(active.Cell, enemy.Cell));
        GridPoint? destination = snapshot.LegalMoveCells
            .Select(cell => new { Cell = cell, Distance = enemies.Min(enemy => Distance(cell, enemy.Cell)) })
            .Where(value => value.Distance < currentDistance)
            .OrderBy(value => value.Distance).ThenBy(value => value.Cell.X).ThenBy(value => value.Cell.Y)
            .Select(value => (GridPoint?)value.Cell).FirstOrDefault();
        if (destination is not GridPoint target) return false;

        await context.ClickPointerAsync("MoveAction", Parameters(("targetKind", "UiElement")), cancellationToken);
        BattleAuthorityStamp beforeCommit = context.CaptureBattleAuthorityStamp();
        await context.ClickPointerAsync($"{target.X},{target.Y}", Parameters(("targetKind", "BattleCell")), cancellationToken);
        await context.WaitForBattleCommitAsync(beforeCommit, cancellationToken);
        return true;
    }

    private static Dictionary<string, JsonElement> Parameters(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value), StringComparer.Ordinal);
}

public sealed record GodotGameplayRunOptions(int FixedSeed = 7, string? AttemptLabel = null)
{
    public static readonly GodotGameplayRunOptions Default = new();
}

public sealed record GodotDemonboundRunMetrics(
    int Seed,
    string? Outcome,
    int BattlesCompleted,
    int EncountersObserved,
    int CorruptionPeak,
    int Meditations,
    int? FirstPossessionRound,
    int FriendlyDamage,
    int Downs,
    int PermanentDeaths,
    int BaneUses,
    int BaneDoubleTargetUses,
    IReadOnlyDictionary<string, int> SkillUses,
    IReadOnlyDictionary<string, int> DamageBySkill,
    IReadOnlyDictionary<string, int> StatusApplications,
    IReadOnlyDictionary<string, int> FailureReasons);

public readonly record struct BattleAuthorityStamp(int Round, string ActiveUnit, PlayableBattlePhase Phase,
    BattleTargetingMode TargetingMode, int EventCount)
{
    public static BattleAuthorityStamp From(BattleUiSnapshot? snapshot) => snapshot is null
        ? new BattleAuthorityStamp(-1, string.Empty, PlayableBattlePhase.Faulted, BattleTargetingMode.None, -1)
        : new BattleAuthorityStamp(snapshot.Round, snapshot.ActiveUnitId.Value, snapshot.Phase,
            snapshot.TargetingMode, snapshot.RecentEvents.Count);
}

public static class GodotGameplayEventWindow
{
    public static int NewEventOffset(IReadOnlyList<string> previous, IReadOnlyList<string> current)
    {
        int maximum = Math.Min(previous.Count, current.Count);
        for (int length = maximum; length > 0; length--)
        {
            bool matches = true;
            for (int index = 0; index < length; index++)
            {
                if (string.Equals(previous[previous.Count - length + index], current[index], StringComparison.Ordinal)) continue;
                matches = false;
                break;
            }
            if (matches) return length;
        }
        return 0;
    }
}

public sealed class GodotGameplayRuntimeRunner
{
    public async Task<GodotGameplayScenarioResult> ExecuteAsync(GodotGameplayScenarioPlan plan,
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(plan, GodotGameplayRunOptions.Default, cancellationToken);

    public async Task<GodotGameplayScenarioResult> ExecuteAsync(GodotGameplayScenarioPlan plan,
        GodotGameplayRunOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);
        plan.ValidateContract();
        ValidatedGodotRunCheckpoint? checkpoint = plan.Checkpoint is null
            ? null
            : GodotGameplayCheckpointCatalog.Create(plan.Checkpoint.Id);
        if (plan.Checkpoint is not null &&
            (plan.Checkpoint.Id != checkpoint!.Id || plan.Checkpoint.Path != checkpoint.Path ||
             !checkpoint.Verify() || !string.Equals(plan.Checkpoint.SemanticHash, checkpoint.SemanticHash, StringComparison.Ordinal)))
            throw new InvalidDataException("The catalog checkpoint does not match the compiled plan.");
        var trace = new List<GodotGameplayTraceEntry>();
        string before = ProductionSaveEvidence();
        string attemptId = string.IsNullOrWhiteSpace(options.AttemptLabel)
            ? Guid.NewGuid().ToString("N")
            : options.AttemptLabel + "-" + Guid.NewGuid().ToString("N");
        var isolatedStore = new GodotGameplayIsolatedRunStore(plan.ScenarioName, attemptId, checkpoint?.Snapshot);
        TacticsMigrationRoot? activeRoot = null;
        GodotGameplayRuntimeContext? context = null;
        GodotDemonboundRunMetrics? demonboundMetrics = null;
        GodotGameplayFailureKind? failure = null;
        string? error = null;
        int remainingTemporaryNodes = 0;
        using var scenarioTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        scenarioTimeout.CancelAfter(plan.Watchdog.ScenarioTimeoutMs);
        try
        {
            PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/Main.tscn")
                ?? throw new InvalidOperationException("Main.tscn is missing.");
            activeRoot = scene.Instantiate<TacticsMigrationRoot>();
            activeRoot.ConfigureTestContext(new GodotPlayableRunTestContext(isolatedStore, options.FixedSeed,
                plan.Checkpoint?.Id ?? "no-checkpoint", true, 4f));
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(activeRoot);
            await activeRoot.ToSignal(activeRoot.GetTree(), SceneTree.SignalName.ProcessFrame);
            GodotPlayableRunMain main = activeRoot.PlayableRun ?? throw new InvalidOperationException("Main did not create the playable run UI.");
            context = new GodotGameplayRuntimeContext(plan, activeRoot, main, isolatedStore, before, options.FixedSeed);
            int ordinal = 0;
            foreach (GodotGameplayPlanStep step in plan.SetupActions)
                await ExecuteStepAsync(context, step, "setup", ++ordinal, trace, scenarioTimeout.Token);
            foreach (GodotGameplayPlanStep step in plan.RuntimeActions)
            {
                if (context.IsTerminal) break;
                await ExecuteStepAsync(context, step, "action", ++ordinal, trace, scenarioTimeout.Token);
            }
            foreach (GodotGameplayPlanAssertion assertion in plan.AssertionPlans)
                await ExecuteAssertionAsync(context, assertion, ++ordinal, trace, scenarioTimeout.Token);
        }
        catch (OperationCanceledException)
        {
            failure = GodotGameplayFailureKind.Timeout;
            error = "scenario.timeout";
        }
        catch (GodotGameplayScenarioException exception)
        {
            failure = exception.Kind;
            error = exception.Message;
        }
        catch (Exception exception)
        {
            failure = GodotGameplayFailureKind.Action;
            error = exception.GetType().Name + ":" + exception.Message;
        }
        finally
        {
            if (context is not null)
                demonboundMetrics = context.BuildDemonboundMetrics();
            activeRoot = context?.Root ?? activeRoot;
            if (activeRoot is not null && GodotObject.IsInstanceValid(activeRoot))
            {
                SceneTree tree = (SceneTree)Engine.GetMainLoop();
                activeRoot.QueueFree();
                for (int frame = 0; frame < 5 && GodotObject.IsInstanceValid(activeRoot); frame++)
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                remainingTemporaryNodes = GodotObject.IsInstanceValid(activeRoot) ? 1 : 0;
                if (remainingTemporaryNodes > 0) { failure ??= GodotGameplayFailureKind.Cleanup; error ??= "scene_cleanup_incomplete"; }
            }
            try { isolatedStore.Cleanup(); }
            catch (Exception cleanupError) { failure ??= GodotGameplayFailureKind.Cleanup; error ??= "isolation_cleanup:" + cleanupError.Message; }
        }
        string after = ProductionSaveEvidence();
        bool productionUnchanged = before == after;
        if (!productionUnchanged && failure is null) { failure = GodotGameplayFailureKind.Cleanup; error = "production_save_changed"; }
        return new GodotGameplayScenarioResult(plan.ScenarioName, failure is null, failure, error, trace,
            productionUnchanged, remainingTemporaryNodes, before, after, demonboundMetrics);
    }

    private static async Task ExecuteStepAsync(GodotGameplayRuntimeContext context, GodotGameplayPlanStep step,
        string phase, int ordinal, List<GodotGameplayTraceEntry> trace, CancellationToken scenarioToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(scenarioToken);
        int stepTimeout = step.Kind == "playBattleThroughInput"
            ? Math.Max(context.Plan.Watchdog.StepTimeoutMs, 120_000)
            : context.Plan.Watchdog.StepTimeoutMs;
        timeout.CancelAfter(stepTimeout);
        ulong started = Time.GetTicksMsec();
        try
        {
            switch (step.Kind)
            {
                case "loadValidatedCheckpoint": await context.ValidateCheckpointAsync(timeout.Token); break;
                case "initializePlayerInput": await context.WaitFramesAsync(1, timeout.Token); break;
                case "movePointerToTarget": await context.MovePointerAsync(PointerLocator(step), step.Parameters, timeout.Token); break;
                case "clickPointerTarget": await context.ClickPointerAsync(PointerLocator(step), step.Parameters, timeout.Token); break;
                case "rightClickPointerTarget": await context.RightClickAsync(PointerLocator(step), step.Parameters, timeout.Token); break;
                case "pressInputKey": await context.PressKeyAsync(ParseKey(RequiredString(step.Parameters, "key")), timeout.Token); break;
                case "waitForPlayerObservable": await context.WaitObservableAsync(RequiredString(step.Parameters, "observable"), step.Target, step.Parameters, timeout.Token); break;
                case "waitForFrames": await context.WaitFramesAsync(RequiredInt(step.Parameters, "count", 1), timeout.Token); break;
                case "playBattleThroughInput": await new ProductionInputDecisionSource(RequiredInt(step.Parameters, "maximumActions", 100)).ExecuteAsync(context, timeout.Token); break;
                case "useBattleSkillThroughInput": await context.UseBattleSkillThroughInputAsync(
                    RequiredString(step.Parameters, "actorId"), RequiredString(step.Parameters, "skillId"),
                    RequiredInt(step.Parameters, "maximumActions", 100), timeout.Token); break;
                case "restartGodotMain": await context.RestartMainAsync(timeout.Token); break;
                case "setPresentationPaused": await context.SetPausedAsync(step.Parameters["paused"].GetBoolean(), timeout.Token); break;
                case "setPresentationSpeed": await context.SetSpeedAsync((float)step.Parameters["speed"].GetDouble(), timeout.Token); break;
                case "endTurnOnlyUntilTerminal": await new EndTurnOnlyDecisionSource().ExecuteAsync(context, timeout.Token); break;
                case "endTurnUntilPresentationNumber": await context.EndTurnUntilPresentationNumberAsync(
                    RequiredString(step.Parameters, "kind"), RequiredInt(step.Parameters, "maximumActions", 100), timeout.Token); break;
                default: throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Contract, "unsupported_action:" + step.Kind);
            }
            trace.Add(new GodotGameplayTraceEntry(ordinal, phase, step.Kind, true, context.StateHash(), (long)(Time.GetTicksMsec() - started), null));
        }
        catch (OperationCanceledException) when (!scenarioToken.IsCancellationRequested)
        {
            GodotPlayableRunProbe probe = context.Main.CaptureTestProbe();
            BattleUiSnapshot? current = probe.BattleSnapshot;
            BattleUiSnapshot? visible = context.Main.CaptureVisibleBattleSnapshot();
            string diagnostic = $"step.timeout:{ordinal}:{step.Kind}:page={probe.PageTitle}:round={current?.Round.ToString() ?? "none"}" +
                $":active={current?.ActiveUnitId.Value ?? "none"}:phase={current?.Phase.ToString() ?? "none"}" +
                $":targeting={current?.TargetingMode.ToString() ?? "none"}:visibleActive={visible?.ActiveUnitId.Value ?? "none"}" +
                $":visiblePhase={visible?.Phase.ToString() ?? "none"}:visibleTargeting={visible?.TargetingMode.ToString() ?? "none"}" +
                $":locked={probe.PresentationLocked}:playing={probe.PresentationPlaying}:automatic={probe.AutomaticFramesPending}";
            trace.Add(new GodotGameplayTraceEntry(ordinal, phase, step.Kind, false, context.StateHash(), (long)(Time.GetTicksMsec() - started), diagnostic));
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Timeout, diagnostic);
        }
        catch (Exception exception)
        {
            trace.Add(new GodotGameplayTraceEntry(ordinal, phase, step.Kind, false, context.StateHash(), (long)(Time.GetTicksMsec() - started), exception.Message));
            throw;
        }
    }

    private static Task ExecuteAssertionAsync(GodotGameplayRuntimeContext context, GodotGameplayPlanAssertion assertion,
        int ordinal, List<GodotGameplayTraceEntry> trace, CancellationToken token)
    {
        GodotPlayableRunProbe probe = context.Main.CaptureTestProbe();
        bool passed = assertion.Kind switch
        {
            "inventoryProjectionEnteredBattle" => context.InventoryProjectionEnteredBattle() == assertion.Expected.GetBoolean(),
            "activeRunExistsEquals" => (probe.SaveSnapshot?.ActiveRun is not null) == assertion.Expected.GetBoolean(),
            "terminalSummaryOutcomeEquals" => string.Equals(probe.SaveSnapshot?.TerminalSummary?.Outcome.ToString(), assertion.Expected.GetString(), StringComparison.Ordinal),
            "presentationNumberEquals" => probe.PresentationNumbers.Any(number =>
                string.Equals(number.Kind.ToString(), assertion.Expected.GetString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(number.Text, assertion.Expected.GetString(), StringComparison.Ordinal)),
            "presentationNodeCountEquals" => probe.PresentationNumberCount == assertion.Expected.GetInt32(),
            "checkpointRevisionEquals" => probe.SaveSnapshot?.ActiveRun?.Checkpoint?.Revision == assertion.Expected.GetInt64(),
            "runtimeStateHashEquals" => context.StateHash() == assertion.Expected.GetString(),
            "demonboundCorruptionEquals" => probe.BattleSnapshot?.Units.SingleOrDefault(unit =>
                unit.UnitId.Value == assertion.Target)?.Corruption == assertion.Expected.GetInt32(),
            "demonboundPossessedEquals" => probe.BattleSnapshot?.Units.SingleOrDefault(unit =>
                unit.UnitId.Value == assertion.Target)?.IsPossessed == assertion.Expected.GetBoolean(),
            "battleSkillReceiptEquals" => context.LastBattleSkillReceipt is JsonElement receipt &&
                ExpectedReceiptMatches(receipt, assertion.Expected),
            "adventureActorCellEquals" => probe.Adventure?.ActorCells.TryGetValue(assertion.Target ?? string.Empty, out string? actorCell) == true &&
                actorCell == assertion.Expected.GetString(),
            "activeAdventureLeaderEquals" => probe.Adventure?.LeaderId == assertion.Expected.GetString(),
            "runNodeLifecycleEquals" => probe.Adventure?.NodeLifecycle == assertion.Expected.GetString(),
            "immediateSuccessorNodeIdsEqual" => probe.Adventure?.ImmediateSuccessorNodeIds.SequenceEqual(
                assertion.Expected.EnumerateArray().Select(value => value.GetString()!), StringComparer.Ordinal) == true,
            "adventureObjectStateEquals" => probe.Adventure?.ObjectStates.TryGetValue(assertion.Target ?? string.Empty, out string? objectState) == true &&
                objectState == assertion.Expected.GetString(),
            "storeOfferCountEquals" => probe.Adventure?.StoreOfferCount == assertion.Expected.GetInt32(),
            "storeSoldOfferCountEquals" => probe.Adventure?.StoreSoldOfferCount == assertion.Expected.GetInt32(),
            "backpackContainsContentId" => ((probe.SaveSnapshot?.ActiveRun?.BackpackConsumables.Any(value => value.DefinitionId.Value == assertion.Target) ?? false) ||
                (probe.SaveSnapshot?.ActiveRun?.BackpackEquipment.Any(value => value.DefinitionId.Value == assertion.Target) ?? false)) == assertion.Expected.GetBoolean(),
            "eventResolutionEquals" => probe.Adventure?.EventResolution == assertion.Expected.GetString(),
            "pendingBattleContextKindEquals" => probe.Adventure?.PendingBattleContextKind == assertion.Expected.GetString(),
            "escortStateEquals" => probe.Adventure?.EscortState == assertion.Expected.GetString(),
            "protectedNpcAliveEquals" => probe.Adventure?.ProtectedNpcAlive == assertion.Expected.GetBoolean(),
            "runSaveSchemaVersionEquals" => RunSaveDocumentV10.SchemaVersion == assertion.Expected.GetInt32(),
            "pendingPartyOrderEquals" => probe.SaveSnapshot?.PendingRunSetup?.SelectedCharacterIds.SequenceEqual(
                assertion.Expected.EnumerateArray().Select(value => value.GetString()!), StringComparer.Ordinal) == true,
            "activePartyStartingSkillIdsEqual" => probe.SaveSnapshot?.ActiveRun?.Party.Select(value => value.StartingSkillContentId?.Value)
                .SequenceEqual(assertion.Expected.EnumerateArray().Select(value => value.GetString()), StringComparer.Ordinal) == true,
            "partyAllLivingAtFullResourcesEquals" => (probe.SaveSnapshot?.ActiveRun?.Party.Where(value => !value.IsDead)
                .All(value => value.CurrentHealth == value.MaxHealth && value.CurrentMana == value.MaxMana) == true) == assertion.Expected.GetBoolean(),
            "partyResourceSummaryEquals" => probe.SaveSnapshot?.ActiveRun?.Party.Select(value =>
                    $"{value.CharacterId}:{value.CurrentHealth}/{value.MaxHealth}:{value.CurrentMana}/{value.MaxMana}")
                .SequenceEqual(assertion.Expected.EnumerateArray().Select(value => value.GetString()), StringComparer.Ordinal) == true,
            "runtimeHasNoErrors" => (probe.RuntimeErrorCount == 0) == assertion.Expected.GetBoolean(),
            "productionSaveUnchanged" => context.ProductionSaveIsUnchanged() == assertion.Expected.GetBoolean(),
            _ => throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Contract, "unsupported_assertion:" + assertion.Kind)
        };
        if (!passed)
        {
            string diagnostic = assertion.Kind switch
            {
                "inventoryProjectionEnteredBattle" => "assertion_failed:" + string.Join(";", context.Main.CaptureInventoryBattleProjectionEvidence().Select(value =>
                    $"{value.CharacterId}:equipment={value.EquipmentCount},hp={value.BaseMaxHealth}->{value.ProjectedMaxHealth}/{value.BattleMaxHealth},mp={value.BaseMaxMana}->{value.ProjectedMaxMana}/{value.BattleMaxMana},match={value.Matches}")),
                "activePartyStartingSkillIdsEqual" => "assertion_failed:actual=" + string.Join(",",
                    probe.SaveSnapshot?.ActiveRun?.Party.Select(value => value.StartingSkillContentId?.Value ?? "null") ?? []),
                "partyResourceSummaryEquals" => "assertion_failed:actual=" + string.Join(",",
                    probe.SaveSnapshot?.ActiveRun?.Party.Select(value => $"{value.CharacterId}:{value.CurrentHealth}/{value.MaxHealth}:{value.CurrentMana}/{value.MaxMana}") ?? []),
                "storeOfferCountEquals" => $"assertion_failed:actual={probe.Adventure?.StoreOfferCount.ToString() ?? "null"}",
                "storeSoldOfferCountEquals" => $"assertion_failed:actual={probe.Adventure?.StoreSoldOfferCount.ToString() ?? "null"}",
                "escortStateEquals" => $"assertion_failed:actual={probe.Adventure?.EscortState ?? "null"}:npc={probe.Adventure?.ProtectedNpcAlive?.ToString() ?? "null"}",
                "adventureObjectStateEquals" => $"assertion_failed:actual={string.Join(',', probe.Adventure?.ObjectStates.Select(value => $"{value.Key}={value.Value}") ?? [])}",
                "runtimeHasNoErrors" => "assertion_failed:" + string.Join(";", context.Main.CaptureRejectedBattleLogEntries()
                    .Select(value => $"{value.EventType}:{value.Message}")),
                _ => "assertion_failed"
            };
            trace.Add(new GodotGameplayTraceEntry(ordinal, "assertion", assertion.Kind, false, context.StateHash(), 0, diagnostic));
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Assertion, diagnostic);
        }
        trace.Add(new GodotGameplayTraceEntry(ordinal, "assertion", assertion.Kind, true, context.StateHash(), 0, null));
        return Task.CompletedTask;
    }

    private static bool ExpectedReceiptMatches(JsonElement actual, JsonElement expected) =>
        expected.EnumerateObject().All(property => actual.TryGetProperty(property.Name, out JsonElement value) &&
            JsonElement.DeepEquals(value, property.Value));

    private static string RequiredString(Dictionary<string, JsonElement> parameters, string key) =>
        parameters.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()! : throw new InvalidDataException($"Missing string parameter '{key}'.");
    private static int RequiredInt(Dictionary<string, JsonElement> parameters, string key, int fallback) =>
        parameters.TryGetValue(key, out JsonElement value) ? value.GetInt32() : fallback;
    private static string PointerLocator(GodotGameplayPlanStep step) => step.Target ??
        new[] { "elementName", "nodeId", "unitId", "actorId", "objectId", "routeNodeId", "cell" }
            .Select(key => step.Parameters.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ??
        throw new InvalidDataException("Pointer action requires a semantic target locator.");
    private static Key ParseKey(string key) => Enum.TryParse(key, true, out Key parsed) ? parsed : throw new InvalidDataException("Unknown key: " + key);

    private static string ProductionSaveEvidence()
    {
        static string Evidence(string path)
        {
            string absolute = ProjectSettings.GlobalizePath(path);
            if (!File.Exists(absolute)) return "missing";
            var info = new FileInfo(absolute);
            return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolute)))}";
        }
        return Evidence(GodotRunSaveStore.DefaultPath) + "|" + Evidence(GodotRunSaveStore.DefaultPath + ".bak");
    }
}

public sealed class GodotGameplayRuntimeContext(GodotGameplayScenarioPlan plan, TacticsMigrationRoot root,
    GodotPlayableRunMain main, GodotGameplayIsolatedRunStore saveStore, string productionSaveEvidence, int fixedSeed)
{
    public GodotGameplayScenarioPlan Plan { get; } = plan;
    public TacticsMigrationRoot Root { get; private set; } = root;
    public GodotPlayableRunMain Main { get; private set; } = main;
    private GodotGameplayIsolatedRunStore SaveStore { get; } = saveStore;
    private string ProductionSaveEvidence { get; } = productionSaveEvidence;
    private string _lastHash = string.Empty;
    private int _sameHashCount;
    private AdventureRevisionBaseline _lastActionAdventure;
    private readonly DemonboundTelemetry _telemetry = new(fixedSeed);
    public JsonElement? LastBattleSkillReceipt { get; private set; }

    public async Task UseBattleSkillThroughInputAsync(string actorId, string skillId, int maximumActions,
        CancellationToken token)
    {
        var requestedSkill = new ContentId(skillId);
        for (int action = 0; action < maximumActions && HasActiveBattle && !IsTerminal; action++)
        {
            if (IsTerminalPending || !CanSubmitPlayerInput)
            {
                await WaitForAutomaticProgressAsync(token);
                action--;
                continue;
            }
            BattleUiSnapshot snapshot = Main.CaptureVisibleBattleSnapshot()!;
            if (!string.Equals(snapshot.ActiveUnitId.Value, actorId, StringComparison.Ordinal))
            {
                await PressKeyAsync(Key.Enter, token);
                continue;
            }
            BattleUiSkillAvailability? availability = snapshot.SkillAvailability?.SingleOrDefault(value => value.SkillId == requestedSkill);
            BattleUiTarget[] legalTargets = snapshot.LegalTargets.Where(value => value.SkillId == requestedSkill)
                .OrderBy(value => value.Cell.X).ThenBy(value => value.Cell.Y).ThenBy(value => value.UnitId?.Value, StringComparer.Ordinal).ToArray();
            if (availability?.IsAvailable != true || legalTargets.Length == 0)
            {
                await PressKeyAsync(Key.Enter, token);
                continue;
            }
            await ClickPointerAsync("SkillAction_" + skillId.Replace('.', '_'),
                Parameters(("targetKind", "UiElement")), token);
            BattleUiSnapshot targeted = Main.CaptureVisibleBattleSnapshot()!;
            BattleUiTarget target = targeted.LegalTargets.Where(value => value.SkillId == requestedSkill)
                .OrderBy(value => value.Cell.X).ThenBy(value => value.Cell.Y).ThenBy(value => value.UnitId?.Value, StringComparer.Ordinal).First();
            await ClickPointerAsync($"{target.Cell.X},{target.Cell.Y}", Parameters(("targetKind", "BattleCell")), token);
            BattleUiSnapshot after = Main.CaptureTestProbe().BattleSnapshot!;
            var damages = after.RecentEvents.OfType<DamageAppliedEvent>().Where(value => value.SkillId == requestedSkill)
                .Select(value => new { targetId = value.TargetId.Value, amount = value.Amount }).ToArray();
            var statuses = after.RecentEvents.OfType<StatusAppliedEvent>()
                .Where(value => value.SourceId.Value == actorId)
                .Select(value => new { targetId = value.TargetId.Value, statusId = value.StatusId.Value, remainingTurns = value.RemainingTurns }).ToArray();
            int? corruption = after.Units.Single(value => value.UnitId.Value == actorId).Corruption;
            LastBattleSkillReceipt = JsonSerializer.SerializeToElement(new { actorId, skillId, damages, statuses, corruption });
            return;
        }
        throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
            $"battle_skill_not_resolved:{actorId}:{skillId}");
    }

    private static Dictionary<string, JsonElement> Parameters(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value), StringComparer.Ordinal);

    public async Task ClickButtonAsync(string text, CancellationToken token)
    {
        CaptureAdventureBaseline();
        Control expected;
        Vector2 logicalPoint;
        Button[] candidates = Descendants<Button>(Main).Where(value => value.IsVisibleInTree() && !value.Disabled).ToArray();
        Button? button = candidates.FirstOrDefault(value => string.Equals(value.Name, text, StringComparison.Ordinal)) ??
            candidates.FirstOrDefault(value => string.Equals(value.Text, text, StringComparison.OrdinalIgnoreCase)) ??
            candidates.FirstOrDefault(value => value.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
        GodotRogueMapView? map = null;
        string? mapNodeId = null;
        if (button is not null) { expected = button; logicalPoint = button.GetGlobalRect().GetCenter(); }
        else if (text.StartsWith("map:", StringComparison.Ordinal))
        {
            map = Descendants<GodotRogueMapView>(Main).Single();
            mapNodeId = text[4..];
            expected = map; logicalPoint = map.NodeCenter(mapNodeId);
        }
        else throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action,
            "pointer_target_not_available:" + text + ":buttons=" + string.Join(",", Descendants<Button>(Main)
                .Select(value => $"{value.Name}:{value.Text}[{value.Disabled},{value.Visible}]")));
        (Viewport viewport, Vector2 point, bool localCoordinates) = map is null
            ? await ResolvePointerAsync(logicalPoint, expected, text, token)
            : await ResolveMapPointerAsync(map, mapNodeId!, token);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action buttonHandler = completion.SetResult;
        Action<string> mapHandler = nodeId => { if (nodeId == mapNodeId) completion.TrySetResult(); };
        if (button is not null) button.Pressed += buttonHandler;
        else map!.NodePressed += mapHandler;
        using CancellationTokenRegistration registration = token.Register(() => completion.TrySetCanceled(token));
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, localCoordinates);
        await WaitFramesAsync(1, token);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, localCoordinates);
        await completion.Task;
        if (button is not null && GodotObject.IsInstanceValid(button)) button.Pressed -= buttonHandler;
        else if (map is not null && GodotObject.IsInstanceValid(map)) map.NodePressed -= mapHandler;
        await WaitFramesAsync(1, token);
    }

    public async Task MovePointerAsync(string target, CancellationToken token) =>
        await MovePointerAsync(target, [], token);

    public async Task MovePointerAsync(string target, Dictionary<string, JsonElement> parameters, CancellationToken token)
    {
        string targetKind = OptionalString(parameters, "targetKind") ?? "UiElement";
        Button? button = string.Equals(targetKind, "UiElement", StringComparison.Ordinal)
            ? Descendants<Button>(Main).FirstOrDefault(value => value.IsVisibleInTree() &&
                (value.Text.Contains(target, StringComparison.OrdinalIgnoreCase) || string.Equals(value.Name, target, StringComparison.Ordinal)))
            : null;
        if (button is not null) { await ResolvePointerAsync(button.GetGlobalRect().GetCenter(), button, target, token); return; }
        if (string.Equals(targetKind, "MapNode", StringComparison.Ordinal) || target.StartsWith("map:", StringComparison.Ordinal))
        {
            GodotRogueMapView map = Descendants<GodotRogueMapView>(Main).Single();
            string nodeId = target.StartsWith("map:", StringComparison.Ordinal) ? target[4..] : target;
            await ResolveMapPointerAsync(map, nodeId, token);
            return;
        }
        if (Main.TryResolveTestBattlePointerTarget(targetKind, target, out Control? surface, out Vector2 battlePoint) && surface is not null)
        {
            await ResolvePointerAsync(battlePoint, surface, target, token);
            return;
        }
        if (Main.TryResolveTestAdventurePointerTarget(targetKind, target, out Control? adventureSurface, out Vector2 adventurePoint) && adventureSurface is not null)
        {
            await ResolvePointerAsync(adventurePoint, adventureSurface, target, token);
            return;
        }
        throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action, "pointer_target_not_found:" + target);
    }

    public async Task ClickPointerAsync(string target, Dictionary<string, JsonElement> parameters, CancellationToken token)
    {
        CaptureAdventureBaseline();
        string targetKind = OptionalString(parameters, "targetKind") ?? "UiElement";
        if (string.Equals(targetKind, "UiElement", StringComparison.Ordinal))
        {
            await ClickButtonAsync(target, token);
            return;
        }
        if (string.Equals(targetKind, "MapNode", StringComparison.Ordinal))
        {
            await ClickButtonAsync(target.StartsWith("map:", StringComparison.Ordinal) ? target : "map:" + target, token);
            return;
        }
        if (Main.TryResolveTestAdventurePointerTarget(targetKind, target, out Control? adventureSurface, out Vector2 adventurePoint) &&
            adventureSurface is not null)
        {
            string before = StateHash();
            (Viewport adventureViewport, Vector2 adventureClickPoint, bool adventureLocal) =
                await ResolvePointerAsync(adventurePoint, adventureSurface, target, token);
            adventureViewport.PushInput(new InputEventMouseButton { Position = adventureClickPoint, GlobalPosition = adventureClickPoint, ButtonIndex = MouseButton.Left, Pressed = true }, adventureLocal);
            await WaitFramesAsync(1, token);
            adventureViewport.PushInput(new InputEventMouseButton { Position = adventureClickPoint, GlobalPosition = adventureClickPoint, ButtonIndex = MouseButton.Left, Pressed = false }, adventureLocal);
            await WaitForStateDifferentAsync(before, token);
            return;
        }
        if (!Main.TryResolveTestBattlePointerTarget(targetKind, target, out Control? surface, out Vector2 logicalPoint) ||
            surface is null)
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action,
                "pointer_target_not_found:" + target);
        (Viewport viewport, Vector2 point, bool localCoordinates) =
            await ResolvePointerAsync(logicalPoint, surface, target, token);
        GodotIsometricBattleBoard board = Descendants<GodotIsometricBattleBoard>(Main).Single();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(Vector2 _) => completion.TrySetResult();
        board.PointerPressed += Handler;
        using CancellationTokenRegistration registration = token.Register(() => completion.TrySetCanceled(token));
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, localCoordinates);
        await WaitFramesAsync(1, token);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, localCoordinates);
        await completion.Task;
        if (GodotObject.IsInstanceValid(board)) board.PointerPressed -= Handler;
    }

    private async Task<(Viewport Viewport, Vector2 Point, bool Local)> ResolvePointerAsync(Vector2 logicalPoint,
        Control expectedControl, string identity, CancellationToken token)
    {
        Viewport viewport = Main.GetViewport();
        Vector2[] logicalCandidates = expectedControl is Button
            ? ButtonPointerCandidates(expectedControl.GetGlobalRect())
            : [logicalPoint];
        (Vector2 Point, bool Local)? resolved = null;
        Vector2? observedLocalPoint = null;
        void ObserveLocalPoint(InputEvent input)
        {
            if (input is InputEventMouseMotion motion) observedLocalPoint = motion.Position;
        }
        expectedControl.GuiInput += ObserveLocalPoint;
        try
        {
            foreach (Vector2 logicalCandidate in logicalCandidates)
            {
                (Vector2 Point, bool Local)[] candidates =
                [
                    (viewport.GetScreenTransform() * viewport.GetCanvasTransform() * logicalCandidate, false),
                    (viewport.GetCanvasTransform() * logicalCandidate, true),
                    (logicalCandidate, false)
                ];
                foreach ((Vector2 candidatePoint, bool local) in candidates)
                {
                    observedLocalPoint = null;
                    viewport.PushInput(new InputEventMouseMotion { Position = new Vector2(-100, -100), GlobalPosition = new Vector2(-100, -100) }, local);
                    await WaitFramesAsync(1, token);
                    viewport.PushInput(new InputEventMouseMotion { Position = candidatePoint, GlobalPosition = candidatePoint }, local);
                    await WaitFramesAsync(1, token);
                    Control? hovered = viewport.GuiGetHoveredControl();
                    Vector2 expectedLocal = expectedControl.GetGlobalTransform().AffineInverse() * logicalCandidate;
                    bool exactSurfacePoint = expectedControl is Button ||
                        observedLocalPoint is Vector2 observed && observed.DistanceTo(expectedLocal) <= 1f;
                    if (exactSurfacePoint &&
                        (hovered == expectedControl || hovered is not null && expectedControl.IsAncestorOf(hovered)))
                    { resolved = (candidatePoint, local); break; }
                }
                if (resolved is not null) break;
            }
        }
        finally { if (GodotObject.IsInstanceValid(expectedControl)) expectedControl.GuiInput -= ObserveLocalPoint; }
        if (resolved is null) throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action,
            $"pointer_target_not_hovered:{identity}:logical={logicalPoint}:observedLocal={observedLocalPoint?.ToString() ?? "none"}:hovered={viewport.GuiGetHoveredControl()?.Name ?? "none"}:viewport={viewport.GetVisibleRect()}");
        return (viewport, resolved.Value.Point, resolved.Value.Local);
    }

    private static Vector2[] ButtonPointerCandidates(Rect2 rect) =>
    [
        rect.GetCenter(),
        new Vector2(rect.Position.X + rect.Size.X * .25f, rect.Position.Y + rect.Size.Y * .5f),
        new Vector2(rect.Position.X + rect.Size.X * .75f, rect.Position.Y + rect.Size.Y * .5f),
        new Vector2(rect.Position.X + rect.Size.X * .5f, rect.Position.Y + rect.Size.Y * .3f),
        new Vector2(rect.Position.X + rect.Size.X * .5f, rect.Position.Y + rect.Size.Y * .7f)
    ];

    private async Task<(Viewport Viewport, Vector2 Point, bool Local)> ResolveMapPointerAsync(
        GodotRogueMapView map, string nodeId, CancellationToken token)
    {
        Viewport viewport = Main.GetViewport();
        Vector2 localPoint = map.NodeCenter(nodeId);
        (Vector2 Point, bool Local)[] candidates =
        [
            (viewport.GetScreenTransform() * map.GetGlobalTransformWithCanvas() * localPoint, false),
            (map.GetGlobalTransformWithCanvas() * localPoint, true),
            (map.GetGlobalTransform() * localPoint, false)
        ];
        string? hoveredNode = null;
        void Hovered(PureRunMapNodeSnapshot? node) => hoveredNode = node?.NodeId;
        map.NodeHovered += Hovered;
        try
        {
            foreach ((Vector2 candidatePoint, bool local) in candidates)
            {
                hoveredNode = null;
                viewport.PushInput(new InputEventMouseMotion
                {
                    Position = new Vector2(-100, -100),
                    GlobalPosition = new Vector2(-100, -100)
                }, local);
                await WaitFramesAsync(1, token);
                viewport.PushInput(new InputEventMouseMotion
                {
                    Position = candidatePoint,
                    GlobalPosition = candidatePoint
                }, local);
                await WaitFramesAsync(1, token);
                if (hoveredNode == nodeId && viewport.GuiGetHoveredControl() == map)
                    return (viewport, candidatePoint, local);
            }
        }
        finally
        {
            if (GodotObject.IsInstanceValid(map)) map.NodeHovered -= Hovered;
        }
        throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action,
            $"map_node_not_hovered:{nodeId}:viewport={viewport.GetVisibleRect()}");
    }

    public async Task RightClickAsync(string target, Dictionary<string, JsonElement> parameters, CancellationToken token)
    {
        await MovePointerAsync(target, parameters, token);
        BattleTargetingMode before = Main.CaptureTestProbe().BattleSnapshot?.TargetingMode ?? BattleTargetingMode.None;
        Viewport viewport = Main.GetViewport();
        Vector2 point = viewport.GetMousePosition();
        foreach (bool pressed in new[] { true, false })
        {
            viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Right, Pressed = pressed }, false);
            await WaitFramesAsync(1, token);
        }
        if (before != BattleTargetingMode.None)
            await WaitUntilAsync(() => Main.CaptureTestProbe().BattleSnapshot?.TargetingMode == BattleTargetingMode.None, token);
    }

    public async Task WaitObservableAsync(string observable, string? target,
        Dictionary<string, JsonElement> parameters, CancellationToken token)
    {
        string? locator = target ?? OptionalString(parameters, "elementName") ?? OptionalString(parameters, "uiId");
        int maximumFrames = RequiredPositiveInt(parameters, "maximumFrames", int.MaxValue);
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            bool ready = observable switch
            {
                "mapReady" => probe.PageTitle.Contains("MAP", StringComparison.OrdinalIgnoreCase),
                "battleReady" => probe.BattleSnapshot is not null,
                "humanTurn" => probe.BattleSnapshot?.Phase == Tactics.Application.Battle.PlayableBattlePhase.PlayerTurn,
                "battleEnded" => IsTerminal,
                "adventureBoardReady" => probe.Adventure is not null,
                "adventureLeaderChanged" => probe.Adventure is not null && probe.Adventure.LeaderRevision > _lastActionAdventure.Leader,
                "adventureInteractionResolved" => probe.Adventure is not null && probe.Adventure.InteractionRevision > _lastActionAdventure.Interaction,
                "exitCommitted" => probe.Adventure is not null && probe.Adventure.ExitRevision > _lastActionAdventure.Route,
                "eventBattleReady" => probe.Adventure?.PendingBattleContextKind is not null and not "None" && probe.BattleSnapshot is not null,
                "adventureSceneChanged" => probe.Adventure is not null && probe.Adventure.SceneRevision > _lastActionAdventure.Scene,
                "uiVisible" or "uiElement" => locator is not null && IsUiVisible(locator),
                "uiHidden" => locator is not null && !IsUiVisible(locator),
                _ => false
            };
            if (ready) return;
            await WaitFramesAsync(1, token);
        }
        GodotPlayableRunProbe finalProbe = Main.CaptureTestProbe();
        throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
            $"observable_not_reached:{observable}:{locator ?? "none"}:page={finalProbe.PageTitle}:status={finalProbe.StatusText ?? "none"}:" +
            $"event_context={finalProbe.Adventure?.PendingBattleContextKind ?? "null"}:battle={(finalProbe.BattleSnapshot is null ? "null" : finalProbe.BattleSnapshot.Phase.ToString())}");
    }

    public async Task RestartMainAsync(CancellationToken token)
    {
        SceneTree tree = Main.GetTree();
        TacticsMigrationRoot previousRoot = Root;
        previousRoot.QueueFree();
        for (int frame = 0; frame < 120 && GodotObject.IsInstanceValid(previousRoot); frame++)
        {
            token.ThrowIfCancellationRequested();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        if (GodotObject.IsInstanceValid(previousRoot))
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Cleanup, "previous_main_cleanup_failed");
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/Main.tscn")!;
        Root = scene.Instantiate<TacticsMigrationRoot>();
        Root.ConfigureTestContext(new GodotPlayableRunTestContext(SaveStore, 7,
            Plan.Checkpoint?.Id ?? "no-checkpoint", true, 4f));
        tree.Root.AddChild(Root);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Main = Root.PlayableRun ?? throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action, "main_restart_failed");
        token.ThrowIfCancellationRequested();
    }

    public async Task PressKeyAsync(Key key, CancellationToken token)
    {
        GodotPlayableRunProbe before = Main.CaptureTestProbe();
        if (key is Key.Enter or Key.KpEnter && (before.PresentationLocked || before.PresentationPlaying))
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action, "input_locked:presentation");
        if (key is Key.Enter or Key.KpEnter)
        {
            BattleAuthorityStamp stamp = BattleAuthorityStamp.From(before.BattleSnapshot);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                PushKeyInput(key);
                for (int frame = 0; frame < 3; frame++)
                {
                    await WaitFramesAsync(1, token);
                    if (BattleAuthorityStamp.From(Main.CaptureTestProbe().BattleSnapshot) != stamp) return;
                }
            }
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
                $"key_input_not_committed:{key}:round={stamp.Round}:active={stamp.ActiveUnit}");
        }
        PushKeyInput(key);
        if (key == Key.Escape)
        {
            await WaitUntilAsync(() =>
            {
                GodotPlayableRunProbe after = Main.CaptureTestProbe();
                return before.CheatConsoleVisible && !after.CheatConsoleVisible ||
                    before.BattleSnapshot?.TargetingMode != BattleTargetingMode.None && after.BattleSnapshot?.TargetingMode == BattleTargetingMode.None ||
                    !before.PauseMenuVisible && after.PauseMenuVisible;
            }, token);
            await WaitFramesAsync(1, token);
            return;
        }
        await WaitForStateDifferentAsync(StateHash(before), token);
    }

    private void PushKeyInput(Key key)
    {
        Main.GetViewport().GuiReleaseFocus();
        Main.GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }, true);
        Main.GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }, true);
    }

    public async Task SetPausedAsync(bool paused, CancellationToken token)
    {
        if (Main.CaptureTestProbe().PlaybackPaused == paused) return;
        await ClickButtonAsync("Pause/Resume", token);
        await WaitUntilAsync(() => Main.CaptureTestProbe().PlaybackPaused == paused, token);
    }

    public async Task SetSpeedAsync(float speed, CancellationToken token)
    {
        for (int index = 0; index < 4; index++)
        {
            if (Math.Abs(Main.CaptureTestProbe().PlaybackSpeed - speed) < .001f) return;
            await ClickButtonAsync("Speed ", token);
        }
        throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action, "speed_not_reached");
    }

    public async Task WaitForChangeAsync(CancellationToken token)
    {
        string before = StateHash();
        await WaitForStateDifferentAsync(before, token);
    }

    public async Task WaitForAutomaticProgressAsync(CancellationToken token)
    {
        string before = StateHash();
        await WaitFramesAsync(1, token);
        GodotPlayableRunProbe probe = Main.CaptureTestProbe();
        string after = StateHash(probe);
            if (after != before || probe.PresentationPlaying || probe.PresentationLocked || probe.AutomaticFramesPending || IsTerminalPending)
        {
            _sameHashCount = 0;
            _lastHash = after;
            return;
        }
        if (after == _lastHash) _sameHashCount++; else { _lastHash = after; _sameHashCount = 1; }
        if (_sameHashCount >= Plan.Watchdog.NoProgressLimit)
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
                $"no_progress:page={probe.PageTitle}:round={probe.BattleSnapshot?.Round.ToString() ?? "none"}:" +
                $"active={probe.BattleSnapshot?.ActiveUnitId.Value ?? "none"}:phase={probe.BattleSnapshot?.Phase.ToString() ?? "none"}:" +
                $"terminalPending={IsTerminalPending}:presentation={probe.PresentationPlaying}:locked={probe.PresentationLocked}:" +
                $"automatic={probe.AutomaticFramesPending}:status={probe.StatusText ?? "none"}");
    }

    public async Task WaitUntilPlayerReadyOrTerminalAsync(CancellationToken token)
    {
        // The production click can enqueue presentation work deferred to the next frame.
        // Observe at least one frame before deciding that the player is ready again.
        await WaitFramesAsync(1, token);
        while (HasActiveBattle && !IsTerminal && !IsAdventureEventResolved && !IsTerminalPending && !CanSubmitPlayerInput)
            await WaitForAutomaticProgressAsync(token);
    }

    public async Task EndTurnUntilPresentationNumberAsync(string kind, int maximumActions, CancellationToken token)
    {
        for (int action = 0; action < maximumActions;)
        {
            if (Main.CaptureTestProbe().PresentationNumbers.Any(number =>
                    string.Equals(number.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase)))
                return;
            if (IsTerminal)
                throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
                    "presentation_number_not_observed:" + kind);
            if (!CanSubmitPlayerInput)
            {
                await WaitForAutomaticProgressAsync(token);
                continue;
            }
            await PressKeyAsync(Key.Enter, token);
            action++;
        }
        if (!Main.CaptureTestProbe().PresentationNumbers.Any(number =>
                string.Equals(number.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase)))
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
                "presentation_number_action_limit:" + kind);
    }

    private async Task WaitForStateDifferentAsync(string before, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await WaitFramesAsync(1, token);
            string after = StateHash();
            if (after != before) return;
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            if (probe.PresentationPlaying || probe.PresentationLocked) { _sameHashCount = 0; continue; }
            if (after == _lastHash) _sameHashCount++; else { _lastHash = after; _sameHashCount = 0; }
            if (_sameHashCount >= Plan.Watchdog.NoProgressLimit)
                throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress, "no_progress:" + DescribeProbe());
        }
        token.ThrowIfCancellationRequested();
    }

    public async Task WaitFramesAsync(int count, CancellationToken token)
    {
        for (int index = 0; index < count; index++)
        {
            token.ThrowIfCancellationRequested();
            await Main.ToSignal(Main.GetTree(), SceneTree.SignalName.ProcessFrame);
            _telemetry.Observe(Main.CaptureTestProbe());
        }
    }

    public GodotDemonboundRunMetrics BuildDemonboundMetrics()
    {
        GodotPlayableRunProbe probe = Main.CaptureTestProbe();
        _telemetry.Observe(probe);
        return _telemetry.Build();
    }

    public string DescribeProbe()
    {
        GodotPlayableRunProbe probe = Main.CaptureTestProbe();
        BattleUiSnapshot? battle = probe.BattleSnapshot;
        string units = battle is null ? "none" : string.Join(',', battle.Units.Where(value => value.IsAlive)
            .OrderBy(value => value.UnitId.Value, StringComparer.Ordinal)
            .Select(value => $"{value.UnitId.Value}@{value.Cell}:{value.CurrentHealth}/{value.MaxHealth}:p{value.PlayerNumber}"));
        string adventure = probe.Adventure is null ? "none" :
            $"{probe.Adventure.BoardContentId}:leader={probe.Adventure.LeaderId ?? "none"}:actors={string.Join(',', probe.Adventure.ActorCells.Select(value => $"{value.Key}@{value.Value}"))}";
        return $"page={probe.PageTitle}:battle={battle?.Phase.ToString() ?? "none"}:round={battle?.Round.ToString() ?? "none"}:" +
            $"active={battle?.ActiveUnitId.Value ?? "none"}:targeting={battle?.TargetingMode.ToString() ?? "none"}:" +
            $"locked={probe.PresentationLocked}:playing={probe.PresentationPlaying}:automatic={probe.AutomaticFramesPending}:" +
            $"status={probe.StatusText ?? "none"}:adventure={adventure}:units={units}";
    }

    public BattleAuthorityStamp CaptureBattleAuthorityStamp() =>
        BattleAuthorityStamp.From(Main.CaptureTestProbe().BattleSnapshot);

    public async Task<BattleUiSnapshot?> WaitForSkillTargetingAsync(ContentId skillId, CancellationToken token)
    {
        for (int frame = 0; frame < 8; frame++)
        {
            BattleUiSnapshot? snapshot = Main.CaptureVisibleBattleSnapshot();
            if (snapshot?.TargetingMode == BattleTargetingMode.Skill && snapshot.SelectedSkillId == skillId) return snapshot;
            await WaitFramesAsync(1, token);
        }
        return null;
    }

    public async Task CancelBattleTargetingAsync(CancellationToken token)
    {
        if (Main.CaptureVisibleBattleSnapshot()?.TargetingMode != BattleTargetingMode.None)
            await PressKeyAsync(Key.Escape, token);
    }

    public async Task WaitForBattleCommitAsync(BattleAuthorityStamp before, CancellationToken token)
    {
        for (int frame = 0; frame < 12; frame++)
        {
            await WaitFramesAsync(1, token);
            if (CaptureBattleAuthorityStamp() != before) return;
        }
        throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
            "battle_pointer_input_not_committed:" + DescribeProbe());
    }

    public string StateHash()
    {
        return StateHash(Main.CaptureTestProbe());
    }

    private static string StateHash(GodotPlayableRunProbe probe)
    {
        GodotAdventureRuntimeProbe? adventure = probe.Adventure;
        string actors = adventure is null ? "" : string.Join(';', adventure.ActorCells.OrderBy(value => value.Key, StringComparer.Ordinal).Select(value => $"{value.Key}={value.Value}"));
        string candidates = adventure is null ? "" : string.Join(',', adventure.ImmediateSuccessorNodeIds);
        string value = $"{probe.PageTitle}|{probe.SaveSnapshot?.Revision}|{probe.SaveSnapshot?.ActiveRun?.Revision}|{probe.BattleSnapshot?.Round}|{probe.BattleSnapshot?.ActiveUnitId.Value}|{probe.BattleSnapshot?.Phase}|{probe.PresentationLocked}|{probe.PresentationPlaying}|{probe.AutomaticFramesPending}|{probe.PresentationNumberCount}|{probe.PlaybackPaused}|{probe.PlaybackSpeed}|{probe.QuitRequested}|{adventure?.BoardContentId}|{adventure?.NodeLifecycle}|{adventure?.LeaderId}|{actors}|{candidates}|{adventure?.PendingBattleContextKind}|{adventure?.LeaderRevision}|{adventure?.InteractionRevision}|{adventure?.ExitRevision}|{adventure?.SceneRevision}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private void CaptureAdventureBaseline()
    {
        GodotAdventureRuntimeProbe? adventure = Main.CaptureTestProbe().Adventure;
        _lastActionAdventure = adventure is null ? default : new AdventureRevisionBaseline(
            adventure.LeaderRevision, adventure.InteractionRevision, adventure.ExitRevision, adventure.SceneRevision);
    }

    private readonly record struct AdventureRevisionBaseline(int Leader, int Interaction, int Route, int Scene);

    private sealed class DemonboundTelemetry(int seed)
    {
        private readonly Dictionary<string, int> _skillUses = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _damageBySkill = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _statusApplications = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _failureReasons = new(StringComparer.Ordinal);
        private bool _battleActive;
        private int _encountersObserved;
        private int _corruptionPeak;
        private int _meditations;
        private int? _firstPossessionRound;
        private int _friendlyDamage;
        private int _downs;
        private int _permanentDeaths;
        private int _baneUses;
        private int _baneDoubleTargetUses;
        private string[] _previousEventWindow = [];
        private string? _outcome;
        private int _battlesCompleted;

        public void Observe(GodotPlayableRunProbe probe)
        {
            BattleUiSnapshot? battle = probe.BattleSnapshot;
            if (probe.BattleActive && !_battleActive) _encountersObserved++;
            _battleActive = probe.BattleActive;
            if (probe.SaveSnapshot?.TerminalSummary is { } terminal)
            {
                _outcome = terminal.Outcome.ToString();
                _battlesCompleted = terminal.BattlesCompleted;
            }
            if (battle is null) return;
            BattleUiUnitSnapshot? demonbound = battle.Units.SingleOrDefault(unit =>
                unit.UnitId.Value == "party-pure_run_demonbound" || unit.DefinitionId.Value == "unit.pure-run.demonbound");
            if (demonbound is not null)
            {
                _corruptionPeak = Math.Max(_corruptionPeak, demonbound.Corruption ?? 0);
                if (demonbound.IsPossessed && _firstPossessionRound is null) _firstPossessionRound = battle.Round;
            }
            if (battle.RecentEvents.Count == 0)
            {
                _previousEventWindow = [];
                return;
            }
            string[] currentWindow = battle.RecentEvents.Select(value => value.ToString()).ToArray();
            int overlap = GodotGameplayEventWindow.NewEventOffset(_previousEventWindow, currentWindow);
            _previousEventWindow = currentWindow;
            int baneTargets = 0;
            foreach (BattleEvent battleEvent in battle.RecentEvents.Skip(overlap))
            {
                switch (battleEvent)
                {
                    case SkillUsedEvent skill when skill.ActorId.Value == "party-pure_run_demonbound":
                        Increment(_skillUses, skill.SkillId.Value);
                        if (skill.SkillId.Value.Contains("demonbound.bane", StringComparison.Ordinal)) _baneUses++;
                        break;
                    case DamageAppliedEvent damage when damage.SourceId.Value == "party-pure_run_demonbound":
                        Increment(_damageBySkill, damage.SkillId.Value, damage.Amount);
                        if (damage.SkillId.Value.Contains("demonbound.bane", StringComparison.Ordinal)) baneTargets++;
                        BattleUiUnitSnapshot? target = battle.Units.SingleOrDefault(unit => unit.UnitId == damage.TargetId);
                        BattleUiUnitSnapshot? source = battle.Units.SingleOrDefault(unit => unit.UnitId == damage.SourceId);
                        if (target is not null && source is not null && target.PlayerNumber == source.PlayerNumber) _friendlyDamage += damage.Amount;
                        break;
                    case StatusAppliedEvent status when status.SourceId.Value == "party-pure_run_demonbound":
                        Increment(_statusApplications, status.StatusId.Value);
                        break;
                    case MeditationUsedEvent meditation when meditation.UnitId.Value == "party-pure_run_demonbound":
                        _meditations++;
                        break;
                    case UnitDefeatedEvent defeated when battle.Units.SingleOrDefault(unit => unit.UnitId == defeated.UnitId)?.PlayerNumber == 0:
                        _downs++;
                        break;
                    case RunPermanentDeathRolledEvent death when death.PermanentDeath:
                        _permanentDeaths++;
                        break;
                    case CommandRejectedEvent rejected when rejected.ActorId.Value == "party-pure_run_demonbound":
                        Increment(_failureReasons, rejected.Reason);
                        break;
                }
            }
            if (baneTargets >= 2) _baneDoubleTargetUses++;
        }

        public GodotDemonboundRunMetrics Build() => new(seed, _outcome, _battlesCompleted, _encountersObserved,
            _corruptionPeak, _meditations, _firstPossessionRound, _friendlyDamage, _downs, _permanentDeaths,
            _baneUses, _baneDoubleTargetUses, Copy(_skillUses), Copy(_damageBySkill), Copy(_statusApplications), Copy(_failureReasons));

        private static void Increment(Dictionary<string, int> values, string key, int amount = 1) =>
            values[key] = values.GetValueOrDefault(key) + amount;

        private static IReadOnlyDictionary<string, int> Copy(Dictionary<string, int> values) =>
            new Dictionary<string, int>(values, StringComparer.Ordinal);
    }

    public bool IsTerminal
    {
        get
        {
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            return probe.SaveSnapshot?.TerminalSummary is not null;
        }
    }

    public bool HasActiveBattle => Main.CaptureTestProbe().BattleActive;

    public bool IsAdventureEventResolved
    {
        get
        {
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            return !probe.BattleActive && probe.Adventure?.EventResolution is not null;
        }
    }

    public bool IsTerminalPending
    {
        get
        {
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            return probe.BattleSnapshot?.TerminalPending == true ||
                probe.BattleSnapshot?.Phase is PlayableBattlePhase.Victory or PlayableBattlePhase.Defeat;
        }
    }

    public bool CanSubmitPlayerInput
    {
        get
        {
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            BattleUiSnapshot? visible = Main.CaptureVisibleBattleSnapshot();
            return probe.BattleSnapshot?.Phase == PlayableBattlePhase.PlayerTurn &&
                visible?.Phase == PlayableBattlePhase.PlayerTurn &&
                visible.ActiveUnitId == probe.BattleSnapshot.ActiveUnitId &&
                !probe.PresentationLocked && !probe.PresentationPlaying && !probe.AutomaticFramesPending && !probe.PlaybackPaused;
        }
    }

    public bool ProductionSaveIsUnchanged() => ProductionSaveEvidence == CaptureProductionSaveEvidence();

    public bool InventoryProjectionEnteredBattle()
        => Main.ValidateInventoryProjectionEnteredBattle();

    public Task ValidateCheckpointAsync(CancellationToken token)
    {
        RunStoreResult loaded = SaveStore.Load();
        if (!loaded.Succeeded || loaded.Snapshot is null || Plan.Checkpoint is null)
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Contract, "validated_checkpoint_not_loaded");
        ValidatedGodotRunCheckpoint actual = ValidatedGodotRunCheckpoint.Create(Plan.Checkpoint.Id, Plan.Checkpoint.Path, loaded.Snapshot);
        if (!string.Equals(actual.SemanticHash, Plan.Checkpoint.SemanticHash, StringComparison.Ordinal))
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Contract, "validated_checkpoint_hash_mismatch");
        token.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static string CaptureProductionSaveEvidence()
    {
        static string Evidence(string path)
        {
            string absolute = ProjectSettings.GlobalizePath(path);
            if (!File.Exists(absolute)) return "missing";
            var info = new FileInfo(absolute);
            return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolute)))}";
        }
        return Evidence(GodotRunSaveStore.DefaultPath) + "|" + Evidence(GodotRunSaveStore.DefaultPath + ".bak");
    }

    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (T nested in Descendants<T>(child)) yield return nested;
        }
    }

    private bool IsUiVisible(string locator) =>
        Main.CaptureTestProbe().PageTitle.Contains(locator, StringComparison.OrdinalIgnoreCase) ||
        Descendants<Control>(Main).Any(value => value.IsVisibleInTree() &&
            (string.Equals(value.Name, locator, StringComparison.OrdinalIgnoreCase) ||
             value is Button button && button.Text.Contains(locator, StringComparison.OrdinalIgnoreCase) ||
             value is Label label && label.Text.Contains(locator, StringComparison.OrdinalIgnoreCase)));

    public bool HasVisibleUiElement(string locator) => IsUiVisible(locator);

    private async Task WaitUntilAsync(Func<bool> predicate, CancellationToken token)
    {
        while (!predicate())
        {
            token.ThrowIfCancellationRequested();
            await WaitFramesAsync(1, token);
        }
    }

    private static string? OptionalString(Dictionary<string, JsonElement> parameters, string key) =>
        parameters.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int RequiredPositiveInt(Dictionary<string, JsonElement> parameters, string key, int fallback) =>
        parameters.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.GetInt32() > 0
            ? value.GetInt32()
            : fallback;

}

public sealed class GodotGameplayScenarioException(GodotGameplayFailureKind kind, string message) : Exception(message)
{
    public GodotGameplayFailureKind Kind { get; } = kind;
}
