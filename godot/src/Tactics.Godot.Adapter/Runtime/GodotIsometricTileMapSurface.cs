using Godot;
using Tactics.Core.Board;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Shared real TileMapLayer surface for battle and Adventure maps.</summary>
[GlobalClass]
public partial class GodotIsometricTileMapSurface : Node2D
{
    public TileMapLayer TerrainLayer { get; private set; } = null!;
    public TileMapLayer DecorationLayer { get; private set; } = null!;
    public TileMapLayer MaskLayer { get; private set; } = null!;
    public TileMapLayer OverlayLayer { get; private set; } = null!;
    public AdventureMapTemplateResource? Template { get; private set; }

    public override void _Ready()
    {
        EnsureLayers();
    }

    private void EnsureLayers()
    {
        if (TerrainLayer is not null) return;
        TerrainLayer = Layer("TerrainTileMapLayer", 0);
        DecorationLayer = Layer("DecorationTileMapLayer", 1);
        MaskLayer = Layer("MaskTileMapLayer", 2);
        OverlayLayer = Layer("OverlayTileMapLayer", 3);
    }

    public void Configure(AdventureMapTemplateResource template)
    {
        ArgumentNullException.ThrowIfNull(template);
        template.ToCoreDefinition();
        EnsureLayers();
        Template = template;
        foreach (TileMapLayer layer in Layers()) { layer.Clear(); layer.TileSet = template.TileSet; }
        Fill(TerrainLayer, template.TerrainCells, 0);
        Fill(DecorationLayer, template.DecorationCells, 2);
        Fill(MaskLayer, template.MaskCells, 3);
    }

    public Vector2 CellCenter(GridPoint cell)
    {
        EnsureLayers();
        return TerrainLayer.Transform * TerrainLayer.MapToLocal(new Vector2I(cell.X, cell.Y));
    }

    public bool TryPointToCell(Vector2 localPoint, out GridPoint cell) =>
        IsometricGridProjection.TryScreenToGrid(localPoint, out cell);

    public void SetOverlay(IEnumerable<GridPoint> cells)
    {
        EnsureLayers();
        OverlayLayer.Clear();
        Fill(OverlayLayer, cells, 4);
    }

    private TileMapLayer Layer(string name, int zIndex)
    {
        var layer = new TileMapLayer
        {
            Name = name, ZIndex = zIndex, YSortEnabled = false,
            // DiamondDown MapToLocal(0,0) is the tile-region center (48,24).
            // Offset plus the vertical flip preserves the established 96x48 local projection contract.
            Position = IsometricGridProjection.FirstCellCenter -
                new Vector2(IsometricGridProjection.TileWidth * .5f, -IsometricGridProjection.TileHeight * .5f),
            Scale = new Vector2(1f, -1f)
        };
        AddChild(layer);
        return layer;
    }

    private IEnumerable<TileMapLayer> Layers() => [TerrainLayer, DecorationLayer, MaskLayer, OverlayLayer];
    private static void Fill(TileMapLayer layer, IEnumerable<GridPoint> cells, int atlasX)
    {
        foreach (GridPoint cell in cells) layer.SetCell(new Vector2I(cell.X, cell.Y), 0, new Vector2I(atlasX, 0));
    }
}
