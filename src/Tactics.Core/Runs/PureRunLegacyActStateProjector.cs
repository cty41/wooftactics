namespace Tactics.Core.Runs;

/// <summary>
/// Read-only migration from pre-Act run fields to <see cref="PureRunActState"/>. This is the only place
/// allowed to interpret legacy Board content strings; contradictory evidence is rejected, never guessed.
/// </summary>
public static class PureRunLegacyActStateProjector
{
    private const string NodeMarker = ".node.";
    private const string ResolvedSuffix = ".resolved";

    public static PureRunActTransitionResult Project(PureRunState run, PureRunActDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(definition);
        if (run.Phase is PureRunPhase.SliceCompleted or PureRunPhase.Abandoned)
            return Reject("act_state.terminal_phase");

        (string? node, PureRunNodeScenePhase phase, string? error) = PhaseEvidence(run, definition);
        if (error is not null) return Reject(error);

        (string? boardNode, bool boardResolved) = ParseBoard(run.AdventureState, definition);
        if (boardNode is not null && boardNode != node)
        {
            // Legacy exits stored the next immediate-combat board before the encounter was persisted.
            bool leavable = phase == PureRunNodeScenePhase.Resolved ||
                            node == definition.StartNodeId && phase == PureRunNodeScenePhase.Exploring;
            bool enteredAhead = !boardResolved && leavable && node is not null &&
                definition.Map.Connections.Any(edge => edge.FromNodeId == node && edge.ToNodeId == boardNode) &&
                Strategy(definition, boardNode) == PureRunNodeEntryStrategy.ImmediateCombat;
            if (!enteredAhead) return Reject("act_state.ambiguous_current");
            node = boardNode;
            phase = PureRunNodeScenePhase.InCombat;
        }
        else if (boardNode is not null && boardResolved != (phase == PureRunNodeScenePhase.Resolved))
            // Includes the legacy gap between settlement and ResolveBoard; that save is not recoverable.
            return Reject("act_state.inconsistent_resolution");

        if (node is null) return Reject("act_state.ambiguous_current");
        if (run.AdventureState?.SceneIdentity is { } identity &&
            (identity.NodeId != node || identity.ActId != definition.ActId || identity.DefinitionVersion != definition.DefinitionVersion))
            return Reject("act_state.ambiguous_current");
        if (run.AdventureState?.PendingEventContext is { } context && context != RunAdventureEventContextKind.None &&
            (run.AdventureState.PendingEventNodeId != node || phase != PureRunNodeScenePhase.InCombat))
            return Reject("act_state.inconsistent_resolution");

        string[]? route = Route(run, definition, node);
        if (route is null) return Reject("act_state.route_invalid");
        HashSet<string> departed = route.Where(id => id != node).ToHashSet(StringComparer.Ordinal);
        var state = new PureRunActState(definition.ActId, definition.DefinitionVersion, node,
            definition.Nodes.Select(scene => scene.NodeId == node
                ? new PureRunNodeSceneState(scene.NodeId, phase)
                : departed.Contains(scene.NodeId)
                    ? new PureRunNodeSceneState(scene.NodeId, PureRunNodeScenePhase.Resolved, departed: true)
                    : new PureRunNodeSceneState(scene.NodeId, PureRunNodeScenePhase.Unentered)));
        return state.Validate(definition) is { } code ? Reject(code) : new(true, null, state);
    }

    private static (string? Node, PureRunNodeScenePhase Phase, string? Error) PhaseEvidence(
        PureRunState run, PureRunActDefinition definition)
    {
        PureRunMapState? map = run.MapState;
        string? selected = run.NodeTransaction?.NodeId ?? map?.PendingNodeId ?? map?.SelectedNodeId;
        // Event battles keep the preceding fixed EncounterIndex; the pending event node is the combat site.
        if (run.Phase is PureRunPhase.PendingBattle or PureRunPhase.Defeated &&
            run.AdventureState is { PendingEventContext: not RunAdventureEventContextKind.None, PendingEventNodeId: { } eventNode })
        {
            int eventLayer = definition.Map.Nodes.SingleOrDefault(node => node.NodeId == eventNode)?.Layer ?? -1;
            return eventLayer is 4 or 6 && LayerNode(definition, eventLayer, eventNode) == eventNode
                ? (eventNode, run.Phase == PureRunPhase.Defeated ? PureRunNodeScenePhase.Failed : PureRunNodeScenePhase.InCombat, null)
                : (null, PureRunNodeScenePhase.Unentered, "act_state.ambiguous_current");
        }
        switch (run.Phase)
        {
            case PureRunPhase.Ready when run.AdventureState?.Lifecycle == RunAdventureLifecycle.InitialExploration ||
                                         run.EncounterIndex == 0 && run.BattlesCompleted == 0:
                return (definition.StartNodeId, PureRunNodeScenePhase.Exploring, null);
            case PureRunPhase.Ready:
                return (LayerNode(definition, run.EncounterIndex, selected), PureRunNodeScenePhase.Resolved, null);
            case PureRunPhase.PendingBattle:
                return (LayerNode(definition, run.EncounterIndex + 1, selected), PureRunNodeScenePhase.InCombat, null);
            case PureRunPhase.Defeated:
                return (LayerNode(definition, run.EncounterIndex + 1, selected), PureRunNodeScenePhase.Failed, null);
            case PureRunPhase.AwaitingLayerFourChoice:
                return (LayerNode(definition, 3, null), PureRunNodeScenePhase.Resolved, null);
            case PureRunPhase.ResolvingLayerFourNode or PureRunPhase.ResolvingLayerSixNode:
            {
                int layer = run.Phase == PureRunPhase.ResolvingLayerSixNode ? 6 : 4;
                string? node = LayerNode(definition, layer, selected);
                bool committed = run.NodeTransaction?.Committed == true ||
                                 map?.NodeLifecycle is RunNodeLifecycle.Resolved or RunNodeLifecycle.Committed;
                return (node, committed ? PureRunNodeScenePhase.Resolved : PureRunNodeScenePhase.Exploring, null);
            }
            case PureRunPhase.ReadyForLayerFive:
                return (LayerNode(definition, 4, map?.CurrentNodeId), PureRunNodeScenePhase.Resolved, null);
            case PureRunPhase.ReadyForLayerSix or PureRunPhase.AwaitingLayerSixChoice:
                return (LayerNode(definition, 5, null), PureRunNodeScenePhase.Resolved, null);
            case PureRunPhase.ReadyForBoss:
                return (LayerNode(definition, 6, map?.CurrentNodeId), PureRunNodeScenePhase.Resolved, null);
            default:
                return (null, PureRunNodeScenePhase.Unentered, "act_state.phase_unsupported");
        }
    }

    /// <summary>Fixed layers have one node; choice layers need explicit selection evidence on that layer.</summary>
    private static string? LayerNode(PureRunActDefinition definition, int layer, string? evidence)
    {
        PureRunMapNodeDefinition[] candidates = definition.Map.Nodes.Where(node => node.Layer == layer).ToArray();
        if (candidates.Length == 1) return candidates[0].NodeId;
        return candidates.Any(node => node.NodeId == evidence) ? evidence : null;
    }

    private static string[]? Route(PureRunState run, PureRunActDefinition definition, string current)
    {
        int currentLayer = definition.Map.Nodes.Single(node => node.NodeId == current).Layer;
        HashSet<string> visited = run.MapState?.VisitedNodeIds.ToHashSet(StringComparer.Ordinal) ?? [];
        var route = new List<string>();
        for (int layer = 0; layer < currentLayer; layer++)
        {
            PureRunMapNodeDefinition[] candidates = definition.Map.Nodes.Where(node => node.Layer == layer).ToArray();
            PureRunMapNodeDefinition[] chosen = candidates.Length == 1
                ? candidates
                : candidates.Where(node => visited.Contains(node.NodeId)).ToArray();
            if (chosen.Length != 1) return null;
            route.Add(chosen[0].NodeId);
        }
        // Visited evidence beyond the current node would mean the save already moved past it.
        if (visited.Any(id => definition.Map.Nodes.SingleOrDefault(node => node.NodeId == id) is not { } node ||
                              node.Layer > currentLayer || node.Layer == currentLayer && id != current))
            return null;
        route.Add(current);
        return route.ToArray();
    }

    private static (string? Node, bool Resolved) ParseBoard(RunAdventureState? adventure, PureRunActDefinition definition)
    {
        if (adventure is null || adventure.Lifecycle == RunAdventureLifecycle.InitialExploration) return (null, false);
        string value = adventure.BoardContentId.Value;
        int marker = value.IndexOf(NodeMarker, StringComparison.Ordinal);
        if (marker < 0) return (null, false);
        bool resolved = value.EndsWith(ResolvedSuffix, StringComparison.Ordinal);
        string raw = value[(marker + NodeMarker.Length)..];
        if (resolved) raw = raw[..^ResolvedSuffix.Length];
        // Variant boards append ".<variant>" after the node; the node segment itself never contains dots.
        string nodeId = raw.Split('.')[0].Replace('-', '_');
        return definition.Nodes.Any(node => node.NodeId == nodeId) ? (nodeId, resolved) : (null, false);
    }

    private static PureRunNodeEntryStrategy Strategy(PureRunActDefinition definition, string nodeId) =>
        definition.Nodes.Single(node => node.NodeId == nodeId).EntryStrategy;

    private static PureRunActTransitionResult Reject(string code) => new(false, code, null);
}

/// <summary>Migration-period check that a node state and the legacy run phase describe the same moment.</summary>
public static class PureRunPhaseConsistency
{
    public static string? Check(PureRunActState state, PureRunState run, PureRunActDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(definition);
        if (state.Validate(definition) is { } invalid) return invalid;
        int layer = definition.Map.Nodes.Single(node => node.NodeId == state.CurrentNodeId).Layer;
        PureRunPhase[] allowed = (state.Current.Phase, layer) switch
        {
            (PureRunNodeScenePhase.Exploring, 0) => [PureRunPhase.Ready],
            (PureRunNodeScenePhase.Exploring, 4) => [PureRunPhase.ResolvingLayerFourNode],
            (PureRunNodeScenePhase.Exploring, 6) => [PureRunPhase.ResolvingLayerSixNode],
            (PureRunNodeScenePhase.InCombat, 1 or 2 or 3) => [PureRunPhase.PendingBattle, PureRunPhase.Ready],
            (PureRunNodeScenePhase.InCombat, 5) => [PureRunPhase.PendingBattle, PureRunPhase.ReadyForLayerFive],
            (PureRunNodeScenePhase.InCombat, 7) => [PureRunPhase.PendingBattle, PureRunPhase.ReadyForBoss],
            (PureRunNodeScenePhase.InCombat, 4 or 6) => [PureRunPhase.PendingBattle],
            (PureRunNodeScenePhase.Resolved, 1 or 2) => [PureRunPhase.Ready],
            (PureRunNodeScenePhase.Resolved, 3) => [PureRunPhase.AwaitingLayerFourChoice],
            (PureRunNodeScenePhase.Resolved, 4) => [PureRunPhase.ReadyForLayerFive, PureRunPhase.ResolvingLayerFourNode],
            (PureRunNodeScenePhase.Resolved, 5) => [PureRunPhase.ReadyForLayerSix, PureRunPhase.AwaitingLayerSixChoice],
            (PureRunNodeScenePhase.Resolved, 6) => [PureRunPhase.ReadyForBoss, PureRunPhase.ResolvingLayerSixNode],
            (PureRunNodeScenePhase.Failed, _) => [PureRunPhase.Defeated],
            _ => []
        };
        return allowed.Contains(run.Phase) ? null : "act_state.phase_mismatch";
    }
}
