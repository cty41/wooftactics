using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

public enum PureRunFlowPage
{
    Home,
    Map,
    Battle,
    Progression,
    Rest,
    Store,
    Mystery,
    Treasure,
    NewRunSetup,
    Inventory,
    Summary
}

public enum PureRunFlowAction
{
    OpenMap,
    OpenInventory,
    CompleteProgression,
    BeginAvailableNode,
    ResumeBattle,
    ResolveNode,
    ChooseStartingSkill,
    ReturnHome
}

public enum PureRunMapNodeState
{
    Locked,
    Available,
    Current,
    Selected,
    Pending,
    Completed
}

public sealed record PureRunFlowPartyMemberSnapshot(
    string CharacterId,
    int Level,
    int CurrentHealth,
    int MaxHealth,
    int CurrentMana,
    int MaxMana,
    bool IsDead);

public sealed record PureRunMapNodeSnapshot(
    string NodeId,
    int Layer,
    PureRunNodeKind Kind,
    string Title,
    ContentId? ContentId,
    float Lane,
    PureRunMapNodeState State,
    string? UnavailableReason = null,
    PureRunNodeIntelState IntelState = PureRunNodeIntelState.Planning);

public sealed record PureRunMapConnectionSnapshot(
    string FromNodeId,
    string ToNodeId,
    bool Revealed,
    bool Traversed);

public sealed record PureRunMapSnapshot(
    IReadOnlyList<PureRunMapNodeSnapshot> Nodes,
    IReadOnlyList<PureRunMapConnectionSnapshot> Connections,
    string FocusNodeId);

public sealed record PureRunFlowSnapshot(
    PureRunFlowPage Page,
    long Revision,
    int Gold,
    int InventoryCount,
    IReadOnlyList<PureRunFlowPartyMemberSnapshot> Party,
    IReadOnlyList<PureRunFlowAction> Actions,
    PureRunMapSnapshot? Map,
    string? BlockingReason = null);

/// <summary>Projects committed Pure Run state into engine-neutral flow and seven-layer map UI facts.</summary>
public sealed class PureRunFlowProjector
{
    public PureRunFlowSnapshot Project(PureRunState run, PureRunDefinition definition,
        PureRunMapDefinition branchMap)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(branchMap);

        PureRunFlowPage page = Page(run);
        var actions = new List<PureRunFlowAction> { PureRunFlowAction.OpenMap, PureRunFlowAction.OpenInventory };
        if (run.PendingProgression.Count > 0) actions.Add(PureRunFlowAction.CompleteProgression);
        if (page == PureRunFlowPage.Battle) actions.Add(PureRunFlowAction.ResumeBattle);
        if (page == PureRunFlowPage.Map && run.PendingProgression.Count == 0) actions.Add(PureRunFlowAction.BeginAvailableNode);
        if (page is PureRunFlowPage.Rest or PureRunFlowPage.Store or PureRunFlowPage.Mystery or PureRunFlowPage.Treasure)
            actions.Add(PureRunFlowAction.ResolveNode);

        return new PureRunFlowSnapshot(page, run.Revision, run.Gold,
            run.BackpackConsumables.Count + run.BackpackEquipment.Count,
            run.Party.Select(value => new PureRunFlowPartyMemberSnapshot(value.CharacterId, value.Level,
                value.CurrentHealth, value.MaxHealth, value.CurrentMana, value.MaxMana, value.IsDead)).ToArray(),
            actions.Distinct().ToArray(), ProjectMap(run, definition, branchMap),
            run.PendingProgression.Count > 0 ? "progression.required" : null);
    }

    public PureRunFlowSnapshot ProjectTerminal(PureRunSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new PureRunFlowSnapshot(PureRunFlowPage.Summary, 0, summary.TotalGoldEarned,
            summary.AcquiredItems.Count, Array.Empty<PureRunFlowPartyMemberSnapshot>(),
            new[] { PureRunFlowAction.ReturnHome }, null);
    }

    public PureRunMapSnapshot ProjectMap(PureRunState run, PureRunDefinition definition,
        PureRunMapDefinition branchMap)
    {
        var definitions = StaticNodes(definition, branchMap);
        PureRunMapNodeSnapshot[] nodes = definitions.Select(value => value with
        {
            State = State(run, value.NodeId),
            UnavailableReason = UnavailableReason(run, value.NodeId),
            IntelState = IntelState(run, value.NodeId)
        }).ToArray();
        var states = nodes.ToDictionary(value => value.NodeId, value => value.State, StringComparer.Ordinal);
        PureRunMapConnectionSnapshot[] connections = branchMap.Connections.Select(edge =>
        {
            PureRunMapNodeState from = states[edge.FromNodeId];
            PureRunMapNodeState to = states[edge.ToNodeId];
            bool traversed = from == PureRunMapNodeState.Completed &&
                to is PureRunMapNodeState.Completed or PureRunMapNodeState.Current or PureRunMapNodeState.Selected or PureRunMapNodeState.Pending;
            bool revealed = traversed || from != PureRunMapNodeState.Locked || to != PureRunMapNodeState.Locked;
        return new PureRunMapConnectionSnapshot(edge.FromNodeId, edge.ToNodeId, true, traversed);
        }).ToArray();
        string focus = nodes.FirstOrDefault(value => value.State is PureRunMapNodeState.Pending or PureRunMapNodeState.Selected)?.NodeId
            ?? nodes.FirstOrDefault(value => value.State == PureRunMapNodeState.Current)?.NodeId
            ?? nodes.FirstOrDefault(value => value.State == PureRunMapNodeState.Available)?.NodeId
            ?? nodes.Last(value => value.State == PureRunMapNodeState.Completed).NodeId;
        return new PureRunMapSnapshot(nodes, connections, focus);
    }

    private static PureRunFlowPage Page(PureRunState run)
    {
        if (run.Phase == PureRunPhase.PendingBattle) return PureRunFlowPage.Battle;
        if (run.PendingProgression.Count > 0) return PureRunFlowPage.Progression;
        if (run.Phase is PureRunPhase.AwaitingLayerFourChoice or PureRunPhase.ReadyForLayerFive or
            PureRunPhase.ReadyForLayerSix or PureRunPhase.AwaitingLayerSixChoice or PureRunPhase.ReadyForBoss or PureRunPhase.Ready)
            return PureRunFlowPage.Map;
        if (run.Phase is PureRunPhase.ResolvingLayerFourNode or PureRunPhase.ResolvingLayerSixNode)
            return run.NodeTransaction?.Kind switch
            {
                PureRunNodeKind.Rest => PureRunFlowPage.Rest,
                PureRunNodeKind.Store => PureRunFlowPage.Store,
                PureRunNodeKind.Mystery => PureRunFlowPage.Mystery,
                PureRunNodeKind.Treasure => PureRunFlowPage.Treasure,
                _ => PureRunFlowPage.Battle
            };
        return PureRunFlowPage.Map;
    }

    private static PureRunMapNodeState State(PureRunState run, string nodeId)
    {
        if (nodeId == "start") return PureRunMapNodeState.Completed;
        if (nodeId.StartsWith("layer_0", StringComparison.Ordinal) && nodeId.EndsWith("_battle", StringComparison.Ordinal))
        {
            int layer = int.Parse(nodeId.AsSpan(6, 2), System.Globalization.CultureInfo.InvariantCulture);
            if (layer <= 3)
            {
                if (run.BattlesCompleted >= layer) return PureRunMapNodeState.Completed;
                if (run.EncounterIndex == layer - 1 && run.Phase == PureRunPhase.PendingBattle) return PureRunMapNodeState.Pending;
                if (run.EncounterIndex == layer - 1 && run.Phase == PureRunPhase.Ready && run.PendingProgression.Count == 0)
                    return PureRunMapNodeState.Current;
                return PureRunMapNodeState.Locked;
            }
        }
        if (nodeId.StartsWith("layer_04_", StringComparison.Ordinal) || nodeId.StartsWith("layer_06_", StringComparison.Ordinal))
        {
            PureRunMapState? map = run.MapState;
            if (map?.VisitedNodeIds.Contains(nodeId, StringComparer.Ordinal) == true) return PureRunMapNodeState.Completed;
            if (map?.SelectedNodeId == nodeId)
                return map.NodeLifecycle is RunNodeLifecycle.Pending or RunNodeLifecycle.Resolved
                    ? PureRunMapNodeState.Pending : PureRunMapNodeState.Selected;
            if (map?.ReachableNodeIds.Contains(nodeId, StringComparer.Ordinal) == true && run.PendingProgression.Count == 0)
                return PureRunMapNodeState.Available;
            if (nodeId.StartsWith("layer_04_", StringComparison.Ordinal) && map is null &&
                run.Phase == PureRunPhase.AwaitingLayerFourChoice && run.BattlesCompleted == 3 &&
                run.PendingProgression.Count == 0)
                return PureRunMapNodeState.Available;
            if (nodeId.StartsWith("layer_06_", StringComparison.Ordinal) && run.Phase == PureRunPhase.ReadyForLayerSix &&
                run.PendingProgression.Count == 0)
                return PureRunMapNodeState.Available;
            return PureRunMapNodeState.Locked;
        }
        if (nodeId == "layer_05_battle")
        {
            if (run.Phase is PureRunPhase.ReadyForLayerSix or PureRunPhase.AwaitingLayerSixChoice or
                PureRunPhase.ResolvingLayerSixNode or PureRunPhase.ReadyForBoss) return PureRunMapNodeState.Completed;
            if (run.EncounterIndex == 4 && run.Phase == PureRunPhase.PendingBattle) return PureRunMapNodeState.Pending;
            if (run.Phase == PureRunPhase.ReadyForLayerFive && run.PendingProgression.Count == 0)
                return PureRunMapNodeState.Current;
            return PureRunMapNodeState.Locked;
        }
        if (nodeId == "layer_07_battle")
        {
            if (run.EncounterIndex == 6 && run.Phase == PureRunPhase.PendingBattle) return PureRunMapNodeState.Pending;
            return run.Phase == PureRunPhase.ReadyForBoss && run.PendingProgression.Count == 0
                ? PureRunMapNodeState.Current : PureRunMapNodeState.Locked;
        }
        return PureRunMapNodeState.Locked;
    }

    private static string? UnavailableReason(PureRunState run, string nodeId)
    {
        PureRunMapNodeState state = State(run, nodeId);
        if (state is PureRunMapNodeState.Available or PureRunMapNodeState.Current) return null;
        if (run.PendingProgression.Count > 0) return "progression.required";
        if (state == PureRunMapNodeState.Completed) return "map.node_completed";
        if ((nodeId.StartsWith("layer_04_", StringComparison.Ordinal) || nodeId.StartsWith("layer_06_", StringComparison.Ordinal)) &&
            run.MapState?.SelectedNodeId is not null) return "map.route_locked";
        return "map.node_locked";
    }

    private static PureRunNodeIntelState IntelState(PureRunState run, string nodeId)
    {
        if (run.MapState?.NodeIntelStates?.TryGetValue(nodeId, out PureRunNodeIntelState persisted) == true)
            return persisted;
        PureRunMapNodeState state = State(run, nodeId);
        if (state == PureRunMapNodeState.Completed) return PureRunNodeIntelState.Completed;
        if (state is PureRunMapNodeState.Current or PureRunMapNodeState.Selected or PureRunMapNodeState.Pending)
            return PureRunNodeIntelState.Current;
        if (state == PureRunMapNodeState.Available ||
            run.MapState?.ReachableNodeIds.Contains(nodeId, StringComparer.Ordinal) == true)
            return PureRunNodeIntelState.TacticalPreview;
        return state switch
        {
            _ => PureRunNodeIntelState.Planning
        };
    }

    private static IReadOnlyList<PureRunMapNodeSnapshot> StaticNodes(PureRunDefinition definition,
        PureRunMapDefinition map)
    {
        return map.Nodes.OrderBy(value => value.Layer).ThenBy(value => value.Lane).ThenBy(value => value.NodeId)
            .Select(value => Node(value.NodeId, value.Layer, value.Kind,
                string.IsNullOrWhiteSpace(value.Title) ? value.Kind.ToString() : value.Title,
                value.ContentId, value.Lane)).ToArray();
    }

    private static PureRunMapNodeSnapshot Node(string id, int layer, PureRunNodeKind kind, string title,
        ContentId? contentId, float lane) => new(id, layer, kind, title, contentId, lane,
        PureRunMapNodeState.Locked);

}
