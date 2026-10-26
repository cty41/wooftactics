namespace Tactics.Core.Runs;

/// <summary>
/// Deterministic node lifecycle for one forward-only Act. Rejections are returned as codes instead of
/// exceptions so Application commands can preserve the previous save unchanged.
/// Contract: ROGUELIKE-NODE-SAME-SCENE-001.
/// </summary>
public sealed class PureRunNodeSceneTransitionService
{
    public PureRunActState Start(PureRunActDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new PureRunActState(definition.ActId, definition.DefinitionVersion, definition.StartNodeId,
            definition.Nodes.Select(node => new PureRunNodeSceneState(node.NodeId,
                node.NodeId == definition.StartNodeId ? PureRunNodeScenePhase.Exploring : PureRunNodeScenePhase.Unentered)));
    }

    public PureRunActTransitionResult Enter(PureRunActState state, PureRunActDefinition definition, string targetNodeId)
    {
        if (Invalid(state, definition) is { } invalid) return invalid;
        if (state.CurrentNodeId == targetNodeId)
            return state.Current.Phase is PureRunNodeScenePhase.Exploring or PureRunNodeScenePhase.InCombat &&
                   targetNodeId != definition.StartNodeId
                ? new(true, null, state, true)
                : Reject("act_state.target_already_current", state);
        if (!CanExit(state, definition)) return Reject("act_state.exit_locked", state);
        if (!definition.Map.Connections.Any(edge => edge.FromNodeId == state.CurrentNodeId && edge.ToNodeId == targetNodeId))
            return Reject("act_state.not_immediate_successor", state);
        if (state.Node(targetNodeId).Phase != PureRunNodeScenePhase.Unentered)
            return Reject("act_state.target_already_entered", state);

        PureRunNodeEntryStrategy strategy = definition.Nodes.Single(node => node.NodeId == targetNodeId).EntryStrategy;
        PureRunNodeScenePhase entered = strategy == PureRunNodeEntryStrategy.ImmediateCombat
            ? PureRunNodeScenePhase.InCombat
            : PureRunNodeScenePhase.Exploring;
        return new(true, null, state.With(targetNodeId,
            new PureRunNodeSceneState(state.CurrentNodeId, PureRunNodeScenePhase.Resolved, departed: true),
            new PureRunNodeSceneState(targetNodeId, entered)));
    }

    /// <summary>Starts an in-place event battle; immediate-combat nodes and the camp cannot use it.</summary>
    public PureRunActTransitionResult BeginCombat(PureRunActState state, PureRunActDefinition definition)
    {
        if (Invalid(state, definition) is { } invalid) return invalid;
        if (state.CurrentNodeId == definition.StartNodeId ||
            definition.Nodes.Single(node => node.NodeId == state.CurrentNodeId).EntryStrategy != PureRunNodeEntryStrategy.Exploration)
            return Reject("act_state.combat_not_triggerable", state);
        return state.Current.Phase switch
        {
            PureRunNodeScenePhase.InCombat => new(true, null, state, true),
            PureRunNodeScenePhase.Exploring => Replace(state, PureRunNodeScenePhase.InCombat),
            _ => Reject("act_state.phase_invalid", state)
        };
    }

    public PureRunActTransitionResult Resolve(PureRunActState state, PureRunActDefinition definition)
    {
        if (Invalid(state, definition) is { } invalid) return invalid;
        if (state.CurrentNodeId == definition.StartNodeId) return Reject("act_state.start_not_resolvable", state);
        return state.Current.Phase switch
        {
            PureRunNodeScenePhase.Resolved => new(true, null, state, true),
            PureRunNodeScenePhase.InCombat or PureRunNodeScenePhase.Exploring => Replace(state, PureRunNodeScenePhase.Resolved),
            _ => Reject("act_state.phase_invalid", state)
        };
    }

    public PureRunActTransitionResult Fail(PureRunActState state, PureRunActDefinition definition)
    {
        if (Invalid(state, definition) is { } invalid) return invalid;
        return state.Current.Phase switch
        {
            PureRunNodeScenePhase.Failed => new(true, null, state, true),
            PureRunNodeScenePhase.InCombat => Replace(state, PureRunNodeScenePhase.Failed),
            _ => Reject("act_state.phase_invalid", state)
        };
    }

    /// <summary>The camp may always be left; other nodes need a committed result, and the terminal node has no exit.</summary>
    public static bool CanExit(PureRunActState state, PureRunActDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        if (state.CurrentNodeId == definition.TerminalNodeId) return false;
        if (state.CurrentNodeId == definition.StartNodeId) return state.Current.Phase == PureRunNodeScenePhase.Exploring;
        return state.Current.Phase == PureRunNodeScenePhase.Resolved;
    }

    public static IReadOnlyList<PureRunMapNodeDefinition> ImmediateSuccessors(PureRunActState state, PureRunActDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Map.Connections.Where(edge => edge.FromNodeId == state.CurrentNodeId)
            .Select(edge => definition.Map.Nodes.Single(node => node.NodeId == edge.ToNodeId))
            .Where(node => state.Node(node.NodeId).Phase == PureRunNodeScenePhase.Unentered)
            .OrderBy(node => node.Lane).ThenBy(node => node.NodeId, StringComparer.Ordinal).ToArray();
    }

    private static PureRunActTransitionResult? Invalid(PureRunActState state, PureRunActDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        return state.Validate(definition) is { } code ? Reject(code, state) : null;
    }

    private static PureRunActTransitionResult Replace(PureRunActState state, PureRunNodeScenePhase phase) =>
        new(true, null, state.With(state.CurrentNodeId, new PureRunNodeSceneState(state.CurrentNodeId, phase)));

    private static PureRunActTransitionResult Reject(string code, PureRunActState state) => new(false, code, state);
}
