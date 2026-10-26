using Tactics.Core.Content;

namespace Tactics.Core.Runs;

public enum PureRunNodeScenePhase { Unentered, Exploring, InCombat, Resolved, Failed }

/// <summary>Business phase of one Act node; presentation residency and visibility are never stored here.</summary>
public sealed record PureRunNodeSceneState
{
    public PureRunNodeSceneState(string nodeId, PureRunNodeScenePhase phase, bool departed = false)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || nodeId != nodeId.Trim())
            throw new ArgumentException("A canonical node identity is required.", nameof(nodeId));
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        // Leaving is only legal after a committed result; failed or unfinished nodes cannot be departed.
        if (departed && phase != PureRunNodeScenePhase.Resolved)
            throw new ArgumentException($"Node '{nodeId}' can only be departed after it is resolved.", nameof(departed));
        NodeId = nodeId;
        Phase = phase;
        Departed = departed;
    }

    public string NodeId { get; }
    public PureRunNodeScenePhase Phase { get; }
    public bool Departed { get; }
}

/// <summary>
/// Single authority for where the party is inside one Act and what each node has become.
/// Contract: ROGUELIKE-NODE-SAME-SCENE-001. Structural invariants are enforced on construction;
/// coverage and route legality against a definition are checked by <see cref="Validate"/>.
/// </summary>
public sealed record PureRunActState
{
    public PureRunActState(ContentId actId, int definitionVersion, string currentNodeId,
        IEnumerable<PureRunNodeSceneState> nodes)
    {
        if (string.IsNullOrWhiteSpace(actId.Value))
            throw new ArgumentException("Act identity cannot be default or empty.", nameof(actId));
        if (definitionVersion < 1) throw new ArgumentOutOfRangeException(nameof(definitionVersion));
        ArgumentNullException.ThrowIfNull(nodes);
        PureRunNodeSceneState[] states = nodes.ToArray();
        if (states.Any(node => node is null))
            throw new ArgumentException("Node states cannot contain null entries.", nameof(nodes));
        if (states.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count() != states.Length)
            throw new ArgumentException("Node states must be unique per node.", nameof(nodes));
        PureRunNodeSceneState current = states.SingleOrDefault(node => node.NodeId == currentNodeId)
            ?? throw new ArgumentException($"Current node '{currentNodeId}' has no state.", nameof(currentNodeId));
        if (current.Departed)
            throw new ArgumentException($"Current node '{currentNodeId}' cannot be departed.", nameof(currentNodeId));
        if (current.Phase == PureRunNodeScenePhase.Unentered)
            throw new ArgumentException($"Current node '{currentNodeId}' must have been entered.", nameof(currentNodeId));
        foreach (PureRunNodeSceneState node in states.Where(node => node.NodeId != currentNodeId))
        {
            if (node.Phase is not (PureRunNodeScenePhase.Unentered or PureRunNodeScenePhase.Resolved) ||
                node.Phase == PureRunNodeScenePhase.Resolved && !node.Departed)
                throw new ArgumentException(
                    $"Only the current node may be {node.Phase}{(node.Departed ? string.Empty : " without departing")}: '{node.NodeId}'.",
                    nameof(nodes));
        }

        ActId = actId;
        DefinitionVersion = definitionVersion;
        CurrentNodeId = currentNodeId;
        Nodes = Array.AsReadOnly(states.OrderBy(node => node.NodeId, StringComparer.Ordinal).ToArray());
    }

    public ContentId ActId { get; }
    public int DefinitionVersion { get; }
    public string CurrentNodeId { get; }
    public IReadOnlyList<PureRunNodeSceneState> Nodes { get; }

    public PureRunNodeSceneState Current => Node(CurrentNodeId);

    public PureRunNodeSceneState Node(string nodeId) =>
        Nodes.SingleOrDefault(node => node.NodeId == nodeId)
        ?? throw new ArgumentException($"Node '{nodeId}' has no state.", nameof(nodeId));

    /// <summary>Returns null when this state belongs to the definition and its departed route is a forward path.</summary>
    public string? Validate(PureRunActDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (ActId != definition.ActId || DefinitionVersion != definition.DefinitionVersion)
            return "act_state.definition_mismatch";
        HashSet<string> expected = definition.Nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        if (Nodes.Count != expected.Count || Nodes.Any(node => !expected.Contains(node.NodeId)))
            return "act_state.coverage_mismatch";

        // Walk from start through exactly the departed nodes; each step must have one successor in the route.
        HashSet<string> route = Nodes.Where(node => node.Departed).Select(node => node.NodeId)
            .Append(CurrentNodeId).ToHashSet(StringComparer.Ordinal);
        if (!route.Contains(definition.StartNodeId)) return "act_state.route_invalid";
        ILookup<string, string> successors = definition.Map.Connections
            .ToLookup(edge => edge.FromNodeId, edge => edge.ToNodeId, StringComparer.Ordinal);
        string cursor = definition.StartNodeId;
        int walked = 1;
        while (cursor != CurrentNodeId)
        {
            string[] next = successors[cursor].Where(route.Contains).ToArray();
            if (next.Length != 1) return "act_state.route_invalid";
            cursor = next[0];
            walked++;
        }
        return walked == route.Count ? null : "act_state.route_invalid";
    }

    internal PureRunActState With(string currentNodeId, params PureRunNodeSceneState[] replacements)
    {
        Dictionary<string, PureRunNodeSceneState> byId = Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        foreach (PureRunNodeSceneState replacement in replacements) byId[replacement.NodeId] = replacement;
        return new PureRunActState(ActId, DefinitionVersion, currentNodeId, byId.Values);
    }
}

public sealed record PureRunActTransitionResult(
    bool Succeeded,
    string? RejectionCode,
    PureRunActState? State,
    bool WasDuplicate = false);
