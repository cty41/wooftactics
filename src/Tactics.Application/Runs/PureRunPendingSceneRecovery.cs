using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

public sealed record PureRunPendingSceneRecoveryResult(bool Succeeded, string? ErrorCode,
    PureRunSaveSnapshot Snapshot, bool WasMigrated);

/// <summary>
/// In-memory representation migration of an already committed battle. No store, CAS, encounter creation,
/// reward application or revision advancement occurs here. Ready/Resolved migration uses separate policies.
/// </summary>
public static class PureRunPendingSceneRecovery
{
    public static PureRunPendingSceneRecoveryResult Recover(PureRunSaveSnapshot snapshot, PureRunMapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(map);
        if (snapshot.PendingRunSetup is not null) return Fail("run_setup.pending", snapshot);
        if (snapshot.ActiveRun is not { Phase: PureRunPhase.PendingBattle } run)
            return Fail("run.pending_battle_required", snapshot);
        if (snapshot.Revision != run.Revision) return Fail("save.revision_mismatch", snapshot);

        PureRunActBlueprint blueprint;
        try { blueprint = ResolveBlueprint(run, map); }
        catch (ArgumentException) { return Fail("run.scene_definition_invalid", snapshot); }
        catch (NotSupportedException) { return Fail("run.scene_definition_invalid", snapshot); }

        PureRunNodeSceneProjectionResult projection = PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint);
        if (!projection.Succeeded) return Fail(projection.ErrorCode ?? "run.scene_identity_invalid", snapshot);
        if (!projection.RequiresMigration) return new(true, null, snapshot, false);

        // The projector has established the target using checkpoint/transaction evidence, not Board name guessing.
        // Old positions are process-local and belong to a potentially different legacy board.
        RunAdventureState adventure = run.AdventureState! with
        {
            Lifecycle = RunAdventureLifecycle.MapActive,
            BoardContentId = projection.BoardContentId!.Value,
            SceneIdentity = projection.Identity,
            ActorCells = Array.Empty<RunAdventureActorCell>()
        };
        RunEncounterCheckpoint checkpoint = run.Checkpoint! with { SceneIdentity = projection.Identity };
        var migrated = new PureRunState(run.RunId, run.Seed, run.Revision, run.Phase,
            run.EncounterIndex, run.EncounterContentId, run.Party, run.BackpackConsumables,
            run.BackpackEquipment, run.PendingProgression, run.AppliedTransactionKeys, run.Gold,
            run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems, checkpoint,
            run.MapState, run.NodeTransaction, run.EscortState, adventure);
        return new(true, null, snapshot with { ActiveRun = migrated }, true);
    }

    /// <summary>Builds a candidate only; callers must still run the projector before saving or activating anything.</summary>
    public static PureRunActBlueprint ResolveBlueprint(PureRunState run, PureRunMapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(map);
        PureRunActBlueprint defaults = PureRunActBlueprint.Resolve(map, run.Seed);
        RunNodeSceneIdentity? current = run.AdventureState?.SceneIdentity;
        RunNodeSceneIdentity? saved = run.Checkpoint?.SceneIdentity;
        if (current is not null || saved is not null)
        {
            if (current is null || saved is null || current != saved)
                throw new ArgumentException("Scene identity evidence is partial or conflicting.", nameof(run));
            return PureRunActBlueprint.Restore(map, run.Seed, [current]);
        }
        if (run.Phase == PureRunPhase.PendingBattle && run.Checkpoint is { EncounterIndex: 3 or 5 } checkpoint &&
            run.NodeTransaction is { Kind: PureRunNodeKind.Mystery, Committed: false } transaction &&
            run.AdventureState is { } adventure && adventure.PendingEventNodeId == transaction.NodeId &&
            adventure.PendingEventContext != RunAdventureEventContextKind.None &&
            !string.IsNullOrWhiteSpace(adventure.PendingEventObjectId))
        {
            PureRunNodeContentBinding? actual = PureRunActBlueprint.EnumerateContentVariants(map).SingleOrDefault(value =>
                value.NodeId == transaction.NodeId && value.EventContext == adventure.PendingEventContext &&
                value.EncounterId == checkpoint.EncounterContentId);
            if (actual is not null)
                return PureRunActBlueprint.Restore(map, run.Seed,
                    [new RunNodeSceneIdentity(defaults.ActDefinition.ActId, defaults.ActDefinition.DefinitionVersion,
                        actual.NodeId, actual.TemplateId)]);
        }
        return defaults;
    }

    private static PureRunPendingSceneRecoveryResult Fail(string error, PureRunSaveSnapshot source) =>
        new(false, error, source, false);
}
