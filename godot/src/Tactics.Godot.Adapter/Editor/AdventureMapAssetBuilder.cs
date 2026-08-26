#if TOOLS
using Godot;
using Tactics.Core.Board;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class AdventureMapAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try
        {
            AdventureMapAssetBuildResult result = AdventureMapAssetFactory.Build();
            ValidateGeneratedSurface();
            GD.Print($"Adventure map templates generated through ResourceSaver: catalog={result.CatalogCount}.");
            Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            Quit(1);
        }
    }

    private static void ValidateGeneratedSurface()
    {
        AdventureMapTemplateResource template = ResourceLoader.Load<AdventureMapTemplateResource>(
            AdventureMapAssetFactory.StartCampPath, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Generated start-camp template cannot be loaded.");
        template.ToCoreDefinition();
        var surface = new GodotIsometricTileMapSurface();
        surface.Configure(template);
        for (int y = 0; y < IsometricGridProjection.GridSize; y++)
        for (int x = 0; x < IsometricGridProjection.GridSize; x++)
        {
            GridPoint cell = new(x, y);
            Vector2 actual = surface.CellCenter(cell), expected = IsometricGridProjection.GridToScreen(cell);
            if (!actual.IsEqualApprox(expected))
                throw new InvalidOperationException($"TileMap projection mismatch at {cell}: actual={actual}, expected={expected}.");
            if (!surface.TryPointToCell(surface.CellCenter(cell), out GridPoint picked) || picked != cell)
                throw new InvalidOperationException($"TileMap picking mismatch at {cell}.");
        }
        Vector2 edge = surface.CellCenter(new GridPoint(2, 2)) + new Vector2(48f, 0f);
        if (!surface.TryPointToCell(edge, out GridPoint edgeCell) || edgeCell != new GridPoint(2, 1))
            throw new InvalidOperationException("TileMap shared-edge picking is not deterministic.");
        surface.Free();
    }
}
#endif
