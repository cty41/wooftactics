using Godot;

namespace Tactics.Godot.Adapter.Runtime;

public enum AdventureMapInstanceMode { Preview, Active }

[GlobalClass]
public partial class GodotAdventureMapInstance : Node2D
{
    private GodotActWorldHost? _owner;
    public string? NodeId { get; private set; }
    public AdventureMapInstanceMode Mode { get; private set; } = AdventureMapInstanceMode.Preview;
    public GodotIsometricTileMapSurface Surface { get; private set; } = null!;

    public GodotAdventureMapInstance() => ProcessMode = ProcessModeEnum.Disabled;

    public override void _Ready()
    {
        if (Surface is not null) return;
        Surface = new GodotIsometricTileMapSurface { Name = "IsometricTileMapSurface" };
        AddChild(Surface);
    }

    public void Configure(AdventureMapTemplateResource template)
    {
        if (_owner is not null) throw new InvalidOperationException("act.owned_map_cannot_reconfigure");
        if (Surface is null)
        {
            Surface = new GodotIsometricTileMapSurface { Name = "IsometricTileMapSurface" };
            AddChild(Surface);
        }
        Surface.Configure(template);
        ApplyMode(AdventureMapInstanceMode.Preview);
    }

    // Standalone legacy views remain supported until they migrate to the resident Act host.
    public void Activate()
    {
        if (_owner is not null) throw new InvalidOperationException("act.activation_requires_owner");
        ApplyMode(AdventureMapInstanceMode.Active);
    }

    public void Deactivate()
    {
        if (_owner is not null) throw new InvalidOperationException("act.activation_requires_owner");
        ApplyMode(AdventureMapInstanceMode.Preview);
    }

    internal void BindOwner(GodotActWorldHost owner, string nodeId)
    {
        if (_owner is not null) throw new InvalidOperationException("act.map_already_owned");
        _owner = owner;
        NodeId = nodeId;
        ApplyMode(AdventureMapInstanceMode.Preview);
    }

    internal void SetOwnedMode(GodotActWorldHost owner, AdventureMapInstanceMode mode)
    {
        if (!ReferenceEquals(_owner, owner)) throw new InvalidOperationException("act.map_owner_mismatch");
        ApplyMode(mode);
    }

    private void ApplyMode(AdventureMapInstanceMode mode)
    {
        Mode = mode;
        ProcessMode = mode == AdventureMapInstanceMode.Active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        if (Surface is not null) Surface.MaskLayer.Visible = mode == AdventureMapInstanceMode.Preview;
    }
}
