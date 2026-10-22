using Tactics.Core.Content;

namespace Tactics.Core.Runs;

/// <summary>
/// Validates the complete formal-template mapping for one Act without owning runtime scenes.
/// Contract: ROGUELIKE-ACT-RESIDENCY-001; this definition alone does not establish runtime residency.
/// </summary>
public sealed record PureRunActDefinition
{
    public PureRunActDefinition(ContentId actId, int definitionVersion, PureRunMapDefinition map,
        string startNodeId, string terminalNodeId, IEnumerable<PureRunNodeSceneDefinition> nodes)
    {
        if (string.IsNullOrWhiteSpace(actId.Value))
            throw new ArgumentException("Act identity cannot be default or empty.", nameof(actId));
        if (definitionVersion <= 0) throw new ArgumentOutOfRangeException(nameof(definitionVersion));
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(nodes);
        if (string.IsNullOrWhiteSpace(startNodeId))
            throw new ArgumentException("Start identity cannot be empty.", nameof(startNodeId));
        if (string.IsNullOrWhiteSpace(terminalNodeId))
            throw new ArgumentException("Terminal identity cannot be empty.", nameof(terminalNodeId));
        if (string.IsNullOrWhiteSpace(map.ContentId.Value) || map.Nodes.Any(node =>
                string.IsNullOrWhiteSpace(node.NodeId) || string.IsNullOrWhiteSpace(node.ContentId.Value)))
            throw new ArgumentException("Map and topology nodes must have valid identities.", nameof(map));

        PureRunNodeSceneDefinition[] scenes = nodes.ToArray();
        if (scenes.Any(node => node is null))
            throw new ArgumentException("Scene mappings cannot contain null entries.", nameof(nodes));
        HashSet<string> ids = map.Nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        if (!ids.Contains(startNodeId))
            throw new ArgumentException("Start node must belong to the map.", nameof(startNodeId));
        if (!ids.Contains(terminalNodeId))
            throw new ArgumentException("Terminal node must belong to the map.", nameof(terminalNodeId));
        if (scenes.Length != ids.Count || scenes.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count() != scenes.Length ||
            scenes.Any(node => !ids.Contains(node.NodeId)))
            throw new ArgumentException("Scene mappings must cover every topology node exactly once.", nameof(nodes));

        Dictionary<string, PureRunNodeSceneDefinition> byId = scenes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        foreach (PureRunMapNodeDefinition node in map.Nodes)
        {
            // Legacy topology labels the Layer-0 camp Battle. Only the explicit start is exempt.
            PureRunNodeEntryStrategy expected = node.NodeId == startNodeId
                ? PureRunNodeEntryStrategy.Exploration
                : node.Kind is PureRunNodeKind.Battle or PureRunNodeKind.Elite or PureRunNodeKind.Boss
                    ? PureRunNodeEntryStrategy.ImmediateCombat
                    : PureRunNodeEntryStrategy.Exploration;
            if (byId[node.NodeId].EntryStrategy != expected)
                throw new ArgumentException($"Node '{node.NodeId}' requires entry strategy {expected}.", nameof(nodes));
        }

        if (Reachable(startNodeId, map.Connections, reverse: false).Count != ids.Count)
            throw new ArgumentException("Every node must be reachable from the start.", nameof(map));
        if (Reachable(terminalNodeId, map.Connections, reverse: true).Count != ids.Count)
            throw new ArgumentException("Every node must have a path to the terminal.", nameof(map));

        // The current Demo only advances and forbids revisiting departed nodes. Reachability
        // alone admits routes that require revisiting a node, so this content must be a DAG.
        if (map.Connections.Any(edge => edge.ToNodeId == startNodeId))
            throw new ArgumentException("The Demo start node cannot have incoming edges.", nameof(map));
        if (map.Connections.Any(edge => edge.FromNodeId == terminalNodeId))
            throw new ArgumentException("The Demo terminal node cannot have outgoing edges.", nameof(map));
        if (!IsAcyclic(ids, map.Connections))
            throw new ArgumentException("The forward-only Demo topology must be acyclic.", nameof(map));

        ActId = actId;
        DefinitionVersion = definitionVersion;
        Map = map;
        StartNodeId = startNodeId;
        TerminalNodeId = terminalNodeId;
        Nodes = Array.AsReadOnly(scenes.OrderBy(node => node.NodeId, StringComparer.Ordinal).ToArray());
    }

    public ContentId ActId { get; }
    public int DefinitionVersion { get; }
    public PureRunMapDefinition Map { get; }
    public string StartNodeId { get; }
    public string TerminalNodeId { get; }
    public IReadOnlyList<PureRunNodeSceneDefinition> Nodes { get; }

    private static bool IsAcyclic(HashSet<string> nodeIds,
        IReadOnlyList<PureRunMapConnectionDefinition> connections)
    {
        Dictionary<string, int> incoming = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        ILookup<string, string> outgoing = connections.ToLookup(edge => edge.FromNodeId,
            edge => edge.ToNodeId, StringComparer.Ordinal);
        foreach (PureRunMapConnectionDefinition edge in connections) incoming[edge.ToNodeId]++;
        Queue<string> ready = new(incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        int removed = 0;
        while (ready.TryDequeue(out string? current))
        {
            removed++;
            foreach (string next in outgoing[current])
                if (--incoming[next] == 0) ready.Enqueue(next);
        }
        return removed == nodeIds.Count;
    }

    private static HashSet<string> Reachable(string origin,
        IReadOnlyList<PureRunMapConnectionDefinition> connections, bool reverse)
    {
        ILookup<string, string> adjacency = connections.ToLookup(
            edge => reverse ? edge.ToNodeId : edge.FromNodeId,
            edge => reverse ? edge.FromNodeId : edge.ToNodeId, StringComparer.Ordinal);
        HashSet<string> visited = new(StringComparer.Ordinal) { origin };
        Queue<string> pending = new();
        pending.Enqueue(origin);
        while (pending.TryDequeue(out string? current))
            foreach (string next in adjacency[current])
                if (visited.Add(next)) pending.Enqueue(next);
        return visited;
    }
}
