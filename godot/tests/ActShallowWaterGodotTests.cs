using GdUnit4;
using Godot;
using Tactics.Core.Board;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Editor;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public sealed class ActShallowWaterGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void N2WaterRoundTripsAndRendersWithoutChangingWalkabilityOrProjection()
    {
        var layout = ResourceLoader.Load<BattleLayoutResource>("res://content/ai_encounters/BattleLayoutPureRunN2ShallowWater.tres");
        AssertThat(layout.SchemaVersion).IsEqual(2);
        using AdventureMapTemplateResource template = AdventureMapAssetFactory.CreateBasicBattleTemplate();
        // Use the exact canonical N2 terrain data, not an invented water fixture.
        template.BlockedCellsValue = layout.BlockedCellsValue;
        template.ShallowWaterCellsValue = layout.ShallowWaterCellsValue;
        AdventureMapTemplateDefinition core = template.ToCoreDefinition();
        AssertThat(core.ShallowWaterCells!.ToArray()).ContainsExactly(layout.ToCoreDefinition().ShallowWater.ToArray());
        AssertThat(core.Board.BlockedCells.ToArray()).ContainsExactly(layout.ToCoreDefinition().BlockedCells.ToArray());
        var surface = new GodotIsometricTileMapSurface();
        try
        {
            surface.Configure(template);
            foreach (GridPoint cell in core.ShallowWaterCells!)
            {
                AssertThat(core.Board.IsWalkable(cell)).IsTrue();
                AssertThat(surface.TerrainLayer.GetCellAtlasCoords(new Vector2I(cell.X, cell.Y))).IsEqual(new Vector2I(5, 0));
                AssertThat(surface.CellCenter(cell)).IsEqual(IsometricGridProjection.GridToScreen(cell));
                AssertThat(surface.TryPointToCell(surface.CellCenter(cell), out GridPoint picked)).IsTrue();
                AssertThat(picked).IsEqual(cell);
            }
            AssertThat(surface.TerrainLayer.GetUsedCells().Count).IsEqual(100);
            AssertThat(surface.TerrainLayer.GetCellAtlasCoords(new Vector2I(0, 0))).IsEqual(new Vector2I(0, 0));
            // Water does not alter the independent Adventure uniform-cost movement policy.
            GridPoint[] route = AdventureBoardPathfinder.FindPath(core.Board, new GridPoint(3, 2), new GridPoint(3, 3)).ToArray();
            AssertThat(route).ContainsExactly(new GridPoint(3, 2), new GridPoint(3, 3));
        }
        finally { surface.Free(); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WaterRequiresItsAtlasTileButLegacyDryMapsRemainSupported()
    {
        using AdventureMapTemplateResource template = AdventureMapAssetFactory.CreateBasicBattleTemplate();
        // Modify only this new in-memory TileSet, never the shared generated resource on disk.
        var source = (TileSetAtlasSource)template.TileSet!.GetSource(0);
        if (source.HasTile(new Vector2I(5, 0))) source.RemoveTile(new Vector2I(5, 0));
        var surface = new GodotIsometricTileMapSurface();
        try
        {
            surface.Configure(template);
            AssertThat(surface.TerrainLayer.GetUsedCells().Count).IsEqual(100);
            Vector2I before = surface.TerrainLayer.GetCellAtlasCoords(new Vector2I(3, 3));
            template.ShallowWaterCellsValue = "3,3";
            AssertThrown(() => surface.Configure(template)).IsInstanceOf<InvalidOperationException>()
                .HasMessage("adventure.shallow_water_atlas_missing:source=0,atlas=5,0");
            AssertThat(surface.TerrainLayer.GetCellAtlasCoords(new Vector2I(3, 3))).IsEqual(before);
            AssertThat(surface.TerrainLayer.GetUsedCells().Count).IsEqual(100);
        }
        finally { surface.Free(); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CoreRejectsDuplicateOutOfBoundsAndBlockedWaterAndKeepsOptionalCompatibility()
    {
        using AdventureMapTemplateResource resource = AdventureMapAssetFactory.CreateBasicBattleTemplate();
        AdventureMapTemplateDefinition core = resource.ToCoreDefinition();
        foreach (IReadOnlyList<GridPoint> cells in new IReadOnlyList<GridPoint>[]
        {
            new[] { new GridPoint(3, 3), new GridPoint(3, 3) },
            new[] { new GridPoint(10, 3) },
            new[] { core.Board.BlockedCells.First() }
        })
            AssertThrown(() => (core with { ShallowWaterCells = cells }).Validate()).IsInstanceOf<ArgumentException>();
        (core with { ShallowWaterCells = null }).Validate();
        (core with { IsTerminal = true, Exits = [], ConnectionAnchors = [], ShallowWaterCells = [new(3, 3)] }).Validate();
    }
}
