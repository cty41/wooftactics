using Godot;

namespace Tactics.Godot.Adapter.Runtime;

public enum AdventureMapInstanceMode { Preview, Active }

[GlobalClass]
public partial class GodotAdventureMapInstance : Node2D
{
    public AdventureMapInstanceMode Mode { get; private set; } = AdventureMapInstanceMode.Preview;
    public GodotIsometricTileMapSurface Surface { get; private set; } = null!;

    public override void _Ready()
    {
        if (Surface is not null) return;
        Surface = new GodotIsometricTileMapSurface { Name = "IsometricTileMapSurface" };
        AddChild(Surface);
    }

    public void Configure(AdventureMapTemplateResource template)
    {
        if (Surface is null)
        {
            Surface = new GodotIsometricTileMapSurface { Name = "IsometricTileMapSurface" };
            AddChild(Surface);
        }
        Surface.Configure(template);
        Mode = AdventureMapInstanceMode.Preview;
    }

    public void Activate() => Mode = AdventureMapInstanceMode.Active;
    public void Deactivate() => Mode = AdventureMapInstanceMode.Preview;
}
