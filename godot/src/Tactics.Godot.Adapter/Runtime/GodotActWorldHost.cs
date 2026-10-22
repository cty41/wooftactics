using System.Collections.ObjectModel;
using Godot;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>A generation-fenced lease on the only playable map in a resident Act.</summary>
public readonly record struct GodotNodeActivationLease(ulong HostInstanceId, string RunScope, string NodeId, long Generation);

/// <summary>
/// Owns formal map identity independently of page/UI lifetime. Preparation is incremental;
/// no map may become playable until the entire Act has been prepared and validated.
/// </summary>
public partial class GodotActWorldHost : Node2D
{
    private readonly Dictionary<string, GodotAdventureMapInstance> _nodes = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, GodotAdventureMapInstance> _view;
    private PureRunActDefinition? _definition;
    private long _generation;
    private bool _released;

    public GodotActWorldHost() => _view = new ReadOnlyDictionary<string, GodotAdventureMapInstance>(_nodes);

    public string RunScope { get; private set; } = string.Empty;
    public IReadOnlyDictionary<string, GodotAdventureMapInstance> Nodes => _view;
    public PureRunActDefinition Definition => _definition ?? throw new InvalidOperationException("act.not_preparing");
    public bool IsPrepared { get; private set; }
    public string? ActiveNodeId { get; private set; }
    public long Generation => _generation;
    public int PreparedCount => _nodes.Count;

    public void BeginPreparation(string runScope, PureRunActDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(runScope)) throw new ArgumentException("Run scope is required.", nameof(runScope));
        ArgumentNullException.ThrowIfNull(definition);
        if (_definition is not null || _released) throw new InvalidOperationException("act.already_initialized");
        RunScope = runScope;
        _definition = definition;
    }

    /// <summary>Called by the loader once per map on the Godot thread; shared templates remain immutable.</summary>
    public GodotAdventureMapInstance PrepareNode(string nodeId, AdventureMapTemplateResource template)
    {
        if (_released || IsPrepared) throw new InvalidOperationException("act.preparation_closed");
        ArgumentNullException.ThrowIfNull(template);
        PureRunNodeSceneDefinition scene = Definition.Nodes.SingleOrDefault(node => node.NodeId == nodeId)
            ?? throw new ArgumentException($"act.unknown_node:{nodeId}", nameof(nodeId));
        if (_nodes.ContainsKey(nodeId)) throw new InvalidOperationException($"act.duplicate_node:{nodeId}");
        if (scene.TemplateId != new ContentId(template.ContentIdValue))
            throw new InvalidOperationException($"act.template_mismatch:{nodeId}:{template.ContentIdValue}");
        // Validate before allocating a Node so malformed content cannot leak a partially registered map.
        template.ToCoreDefinition();
        var instance = new GodotAdventureMapInstance { Name = $"Map_{nodeId}" };
        try
        {
            instance.Configure(template);
            instance.BindOwner(this, nodeId);
            AddChild(instance);
            _nodes.Add(nodeId, instance);
            return instance;
        }
        catch
        {
            instance.Free();
            throw;
        }
    }

    public void CompletePreparation()
    {
        if (_released || IsPrepared) throw new InvalidOperationException("act.preparation_closed");
        if (_nodes.Count != Definition.Nodes.Count) throw new InvalidOperationException("act.maps_incomplete");
        var maps = _nodes.ToDictionary(pair => pair.Key, pair => pair.Value.Surface.Template!.ToCoreDefinition(), StringComparer.Ordinal);
        foreach ((string nodeId, AdventureMapTemplateDefinition map) in maps)
        {
            if (map.IsTerminal != (nodeId == Definition.TerminalNodeId))
                throw new InvalidOperationException($"act.terminal_mismatch:{nodeId}");
            string[] expected = Definition.Map.Connections.Where(edge => edge.FromNodeId == nodeId)
                .Select(edge => edge.ToNodeId).Order(StringComparer.Ordinal).ToArray();
            string[] actual = map.Exits.Select(exit => exit.TargetNodeId).Order(StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
                throw new InvalidOperationException($"act.exit_topology_mismatch:{nodeId}");
            foreach (AdventureMapExitAnchor exit in map.Exits)
                if (!maps[exit.TargetNodeId].Entries.Any(entry => entry.AnchorId == exit.TargetEntryId))
                    throw new InvalidOperationException($"act.target_entry_missing:{nodeId}:{exit.ExitId}");
        }
        IsPrepared = true;
    }

    /// <summary>Transfer only presentation/input ownership; callers must commit gameplay before calling this.</summary>
    public GodotNodeActivationLease ActivateNode(string nodeId)
    {
        if (!IsPrepared || _released) throw new InvalidOperationException("act.not_ready");
        if (!_nodes.TryGetValue(nodeId, out GodotAdventureMapInstance? target))
            throw new ArgumentException($"act.unknown_node:{nodeId}", nameof(nodeId));
        if (ActiveNodeId == nodeId) return new(GetInstanceId(), RunScope, nodeId, _generation);
        if (ActiveNodeId is string previous) _nodes[previous].SetOwnedMode(this, AdventureMapInstanceMode.Preview);
        ActiveNodeId = null;
        _generation++;
        target.SetOwnedMode(this, AdventureMapInstanceMode.Active);
        ActiveNodeId = nodeId;
        return new(GetInstanceId(), RunScope, nodeId, _generation);
    }

    public bool IsLeaseCurrent(GodotNodeActivationLease lease) => !_released && IsPrepared &&
        lease.HostInstanceId == GetInstanceId() && lease.RunScope == RunScope &&
        lease.NodeId == ActiveNodeId && lease.Generation == _generation;

    public void DeactivateCurrent()
    {
        _generation++;
        if (ActiveNodeId is string previous && _nodes.TryGetValue(previous, out GodotAdventureMapInstance? instance))
            instance.SetOwnedMode(this, AdventureMapInstanceMode.Preview);
        ActiveNodeId = null;
    }

    /// <summary>Invalidate work immediately, then free maps at Godot's safe deletion boundary.</summary>
    public void Release()
    {
        if (_released) return;
        DeactivateCurrent();
        _released = true;
        IsPrepared = false;
        foreach (GodotAdventureMapInstance instance in _nodes.Values)
        {
            instance.Visible = false;
            instance.SetOwnedMode(this, AdventureMapInstanceMode.Preview);
            instance.QueueFree();
        }
        _nodes.Clear();
    }

    public override void _ExitTree() => Release();
}
