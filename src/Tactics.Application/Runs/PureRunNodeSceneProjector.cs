using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

public sealed record PureRunNodeSceneProjectionResult(
    bool Succeeded, string? ErrorCode, RunNodeSceneIdentity? Identity, bool RequiresMigration, ContentId? BoardContentId);

/// <summary>
/// Read-only identity boundary for the current Demo's PendingBattle snapshots. A successful
/// legacy projection is a migration candidate, not permission to save, activate a scene or rebuild a checkpoint.
/// </summary>
public static class PureRunNodeSceneProjector
{
    public static PureRunNodeSceneProjectionResult ResolvePendingBattle(PureRunState run, PureRunActBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(blueprint);
        if (run.Phase != PureRunPhase.PendingBattle) return Fail("scene.pending_battle_required");
        if (run.Checkpoint is not { } checkpoint) return Fail("scene.checkpoint_missing");
        if (run.AdventureState is not { } adventure) return Fail("scene.adventure_missing");
        if (checkpoint.Revision <= 0 || checkpoint.Revision > run.Revision || adventure.Revision < 0 ||
            checkpoint.EncounterContentId != run.EncounterContentId)
            return Fail("scene.checkpoint_conflict");

        RunNodeSceneIdentity? current = adventure.SceneIdentity;
        RunNodeSceneIdentity? saved = checkpoint.SceneIdentity;
        if ((current is null) != (saved is null)) return Fail("scene.partial_identity");
        if (current != saved) return Fail("scene.identity_mismatch");
        bool legacy = current is null;
        PureRunActDefinition definition = blueprint.ActDefinition;
        // Blueprint content choices must belong to this seed, even if this particular encounter is shared.
        PureRunActBlueprint defaults = PureRunActBlueprint.Resolve(definition.Map, run.Seed);
        PureRunActBlueprint expectedBlueprint = PureRunActBlueprint.Restore(definition.Map, run.Seed,
            blueprint.PersistedSceneIdentities);
        if (blueprint.Bindings.Count != expectedBlueprint.Bindings.Count ||
            blueprint.Bindings.Any(pair => !expectedBlueprint.Bindings.TryGetValue(pair.Key, out var value) || pair.Value != value))
            return Fail("scene.blueprint_seed_mismatch");

        string? nodeId = checkpoint.EncounterIndex switch
        {
            0 => "layer_01_battle", 1 => "layer_02_battle", 2 => "layer_03_battle",
            4 => "layer_05_battle", 6 => "layer_07_battle",
            3 or 5 => run.NodeTransaction?.NodeId,
            _ => null
        };
        if (nodeId is null || !blueprint.Bindings.TryGetValue(nodeId, out PureRunNodeContentBinding? binding))
            return Fail("scene.node_unresolved");
        PureRunMapNodeDefinition node = definition.Map.Nodes.Single(value => value.NodeId == nodeId);
        if (node.Layer != checkpoint.EncounterIndex + 1 || binding.EncounterId != checkpoint.EncounterContentId)
            return Fail("scene.encounter_binding_mismatch");
        // This boundary proves only the current Pending node, never unrecorded history or future node overrides.
        if (blueprint.PersistedSceneIdentities.Any(identity => identity.NodeId != nodeId) ||
            (legacy && blueprint.PersistedSceneIdentities.Count != 0 &&
             (node.Kind != PureRunNodeKind.Mystery || checkpoint.EncounterIndex is not (3 or 5))))
            return Fail("scene.override_unproven");
        var expected = new RunNodeSceneIdentity(definition.ActId, definition.DefinitionVersion, nodeId, binding.TemplateId);
        if (definition.Nodes.Single(value => value.NodeId == nodeId).TemplateId != binding.TemplateId ||
            (current is not null && current != expected))
            return Fail("scene.definition_binding_mismatch");

        bool IsPredecessor(string id) => definition.Map.Connections.Any(edge => edge.FromNodeId == id && edge.ToNodeId == nodeId);
        PureRunMapState? map = run.MapState;
        if (map is not null &&
            ((map.MapContentId is { } mapId && mapId != definition.Map.ContentId) ||
             (map.MapLayoutVersion != 0 && map.MapLayoutVersion != definition.Map.LayoutVersion) ||
             (map.CurrentNodeId != nodeId && !IsPredecessor(map.CurrentNodeId))))
            return Fail("scene.map_conflict");

        if (checkpoint.EncounterIndex is 3 or 5)
        {
            RunNodeTransaction? transaction = run.NodeTransaction;
            if (transaction is null || transaction.Committed || string.IsNullOrWhiteSpace(transaction.TransactionKey) ||
                run.AppliedTransactionKeys.Contains(transaction.TransactionKey, StringComparer.Ordinal) ||
                transaction.Kind != node.Kind || node.Kind is not (PureRunNodeKind.Battle or PureRunNodeKind.Mystery) ||
                map is null || map.Phase != PureRunMapPhase.ResolvingNode || map.NodeLifecycle != RunNodeLifecycle.Pending ||
                map.SelectedNodeId != nodeId || map.PendingNodeId != nodeId || map.PendingTransactionKey != transaction.TransactionKey)
                return Fail("scene.transaction_conflict");
            // The legacy branch service deliberately retains run.EncounterIndex; checkpoint.Index is authoritative here.
        }
        else
        {
            if (run.EncounterIndex != checkpoint.EncounterIndex) return Fail("scene.encounter_index_conflict");
            RunNodeTransaction? prior = run.NodeTransaction;
            if (prior is not null && (!prior.Committed || !IsPredecessor(prior.NodeId) ||
                string.IsNullOrWhiteSpace(prior.TransactionKey) ||
                prior.Kind != definition.Map.Nodes.Single(value => value.NodeId == prior.NodeId).Kind ||
                (map is not null && map.CurrentNodeId != prior.NodeId)))
                return Fail("scene.transaction_conflict");
            if (map is not null && (map.PendingNodeId is not null || map.PendingTransactionKey is not null ||
                (map.SelectedNodeId is not null && (prior is null || map.SelectedNodeId != prior.NodeId))))
                return Fail("scene.transaction_conflict");
        }

        if (node.Kind == PureRunNodeKind.Mystery)
        {
            if (adventure.PendingEventContext == RunAdventureEventContextKind.None &&
                adventure.PendingEventNodeId is null && adventure.PendingEventObjectId is null)
                return Fail("scene.event_state_incomplete");
            if (binding.EventContext == RunAdventureEventContextKind.None ||
                adventure.PendingEventContext == RunAdventureEventContextKind.None ||
                adventure.PendingEventNodeId != nodeId || string.IsNullOrWhiteSpace(adventure.PendingEventObjectId))
                return Fail("scene.event_conflict");
            // Old UI triggers used hard-coded variants, not necessarily the seed-selected template.
            // Never rewrite that persisted context or manufacture escort state to fit today's blueprint.
            if (adventure.PendingEventContext != binding.EventContext)
                return Fail("scene.event_variant_incompatible");
            string expectedObjectId = binding.EventContext switch
            {
                RunAdventureEventContextKind.CursedChestMimic => "cursed-chest",
                RunAdventureEventContextKind.FallenAltarGuardian => "fallen-altar",
                RunAdventureEventContextKind.LostVillagerEscort => "lost-villager",
                _ => string.Empty
            };
            if (!string.Equals(adventure.PendingEventObjectId, expectedObjectId, StringComparison.Ordinal))
                return Fail("scene.event_conflict");
            if (binding.EventContext == RunAdventureEventContextKind.LostVillagerEscort &&
                (run.EscortState is not { Lifecycle: RunEscortLifecycle.BattlePending, ProtectedNpcAlive: true } escort ||
                 escort.DestinationNodeId != nodeId))
                return Fail("scene.escort_state_conflict");
            // Assignments remain the default seed history, independent of the actually triggered variant.
            string eventId = defaults.Bindings[nodeId].EventContext switch
            {
                RunAdventureEventContextKind.CursedChestMimic => "cursed_chest_001",
                RunAdventureEventContextKind.FallenAltarGuardian => "fallen_altar_001",
                RunAdventureEventContextKind.LostVillagerEscort => "lost_villager_001",
                _ => string.Empty
            };
            if (map is null || !map.MysteryEventAssignments.TryGetValue(nodeId, out string? assigned) || assigned != eventId)
                return Fail("scene.event_conflict");
        }
        else if (adventure.PendingEventContext != RunAdventureEventContextKind.None ||
                 adventure.PendingEventNodeId is not null || adventure.PendingEventObjectId is not null)
            return Fail("scene.event_conflict");

        // The runtime spawns the protected NPC from EscortState, while settlement dispatches by context.
        // Both authorities must describe the same live escort battle; historical non-pending quests remain valid.
        if (run.EscortState is { Lifecycle: RunEscortLifecycle.BattlePending } pendingEscort &&
            (!pendingEscort.ProtectedNpcAlive || pendingEscort.DestinationNodeId != nodeId ||
             node.Kind != PureRunNodeKind.Mystery ||
             adventure.PendingEventContext != RunAdventureEventContextKind.LostVillagerEscort))
            return Fail("scene.escort_state_conflict");

        if (adventure.Lifecycle == RunAdventureLifecycle.InitialExploration)
        {
            // Old direct battle APIs can retain Initial across the entire run. For an existing
            // checkpoint it means missing scene metadata, not first-battle authority. Ready migration
            // (which would have to create a checkpoint) is deliberately outside this projector.
            if (!legacy || adventure.BoardContentId != RunAdventureTransitionService.InitialBoardContentId)
                return Fail("scene.initial_conflict");
        }
        else if (adventure.Lifecycle != RunAdventureLifecycle.MapActive || !BoardMatches())
            return Fail("scene.board_conflict");
        return new(true, null, current ?? expected, legacy, new ContentId(BoardAlias(nodeId)));

        bool BoardMatches()
        {
            string board = adventure.BoardContentId.Value;
            // Explicit aliases emitted by Main/AdventureMapAssetFactory; never parse a suffix into an unknown node.
            if (board == BoardAlias(nodeId)) return true;
            if (node.Kind == PureRunNodeKind.Mystery &&
                board == $"adventure-board.pure-run.event.{binding.EventContext.ToString().ToLowerInvariant()}") return true;
            if (!legacy) return false;
            // Old BeginEncounter/BeginLayerFive/BeginBoss/branch APIs retain the previous AdventureState.
            // Only an explicitly enumerated direct predecessor is accepted, never an arbitrary stale Board.
            return definition.Map.Connections.Where(edge => edge.ToNodeId == nodeId).Any(edge =>
            {
                string priorId = edge.FromNodeId;
                if (map is not null && map.CurrentNodeId != priorId) return false;
                if (board == BoardAlias(priorId) || board == BoardAlias(priorId) + ".resolved") return true;
                // Main's resolved event board lacks a NodeId; require the committed predecessor transaction too.
                return run.NodeTransaction is { Committed: true, Kind: PureRunNodeKind.Mystery } prior &&
                    prior.NodeId == priorId && PureRunActBlueprint.EnumerateContentVariants(definition.Map)
                        .Where(value => value.NodeId == priorId && value.EventContext != RunAdventureEventContextKind.None)
                        .Any(value => board == $"adventure-board.pure-run.event.{value.EventContext.ToString().ToLowerInvariant()}.resolved");
            });
        }
    }

    // Called only for nodes already proven to exist in the current Demo definition/edge set.
    private static string BoardAlias(string nodeId) => $"adventure-board.pure-run.node.{nodeId.Replace('_', '-')}";
    private static PureRunNodeSceneProjectionResult Fail(string code) => new(false, code, null, false, null);
}
