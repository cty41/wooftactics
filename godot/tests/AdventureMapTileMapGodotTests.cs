using GdUnit4;
using Godot;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Editor;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public sealed class AdventureMapTileMapGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task SharedSurfaceUsesRealTileMapLayersAndMatchesAllProjectionCenters()
    {
        AdventureMapTemplateResource template = AdventureMapAssetFactory.CreateStartCampTemplate();
        var surface = new GodotIsometricTileMapSurface();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(surface);
        await surface.ToSignal(surface.GetTree(), SceneTree.SignalName.ProcessFrame);
        surface.Configure(template);

        AssertThat(surface.TerrainLayer).IsInstanceOf<TileMapLayer>();
        AssertThat(surface.TerrainLayer.GetUsedCells().Count).IsEqual(100);
        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
        {
            GridPoint cell = new(x, y);
            AssertThat(surface.CellCenter(cell)).IsEqual(IsometricGridProjection.GridToScreen(cell));
            AssertThat(surface.TryPointToCell(surface.CellCenter(cell), out GridPoint picked)).IsTrue();
            AssertThat(picked).IsEqual(cell);
        }
        Vector2 edge = surface.CellCenter(new GridPoint(2, 2)) + new Vector2(48, 0);
        AssertThat(surface.TryPointToCell(edge, out GridPoint selected)).IsTrue();
        AssertThat(selected).IsEqual(new GridPoint(2, 1));
        surface.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedTemplatesRoundTripAllRequiredAnchorsAndLayers()
    {
        AdventureMapTemplateResource camp = AdventureMapAssetFactory.CreateStartCampTemplate();
        AdventureMapTemplateResource battle = AdventureMapAssetFactory.CreateBasicBattleTemplate();
        AssertThat(camp.ToCoreDefinition().CandidateSlots.Count).IsGreater(3);
        AssertThat(camp.ToCoreDefinition().StateLayerIds.ToArray()).ContainsExactly("planning", "tactical-preview", "current", "completed");
        AssertThat(battle.ToCoreDefinition().PlayerBattleSlots.Count).IsGreater(0);
        AssertThat(battle.ToCoreDefinition().EnemyBattleSlots.Count).IsGreater(0);
        AssertThat(camp.TileSet).IsNotNull();
        AssertThat(AdventureMapAssetFactory.SemanticFingerprint(camp))
            .IsEqual(AdventureMapAssetFactory.SemanticFingerprint(AdventureMapAssetFactory.CreateStartCampTemplate()));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedCatalogContainsStableTemplateUidsExactlyOnce()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(AdventureMapAssetFactory.CatalogPath);
        AssertThat(catalog).IsNotNull();
        AssertThat(catalog!.Entries.Count(entry => entry.ContentIdValue == AdventureMapAssetFactory.StartCampContentId)).IsEqual(1);
        AssertThat(catalog.Entries.Count(entry => entry.ContentIdValue == AdventureMapAssetFactory.BasicBattleContentId)).IsEqual(1);
        foreach (GodotResourceEntry entry in catalog.Entries.Where(entry => entry.ResourceTypeIdValue == "adventure-map-template"))
        {
            AssertThat(entry.ResourceUidValue.StartsWith("uid://", StringComparison.Ordinal)).IsTrue();
            AssertThat(ResourceUid.GetIdPath(ResourceUid.TextToId(entry.ResourceUidValue))).IsEqual(entry.DiagnosticPathValue);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MapInstanceHasMinimalPreviewAndActiveLifecycleSkeleton()
    {
        var instance = new GodotAdventureMapInstance();
        instance.Configure(AdventureMapAssetFactory.CreateBasicBattleTemplate());
        AssertThat(instance.Mode).IsEqual(AdventureMapInstanceMode.Preview);
        instance.Activate();
        AssertThat(instance.Mode).IsEqual(AdventureMapInstanceMode.Active);
        instance.Deactivate();
        AssertThat(instance.Mode).IsEqual(AdventureMapInstanceMode.Preview);
        instance.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task StartCampUsesTypedObjectsAndOneRealActorPerCandidateSlot()
    {
        GodotStartCampCandidate[] candidates = LoadUnitDefinitions(5).Select((definition, index) =>
            new GodotStartCampCandidate($"candidate-{index + 1}", definition)).ToArray();
        var view = new GodotStartCampView { Name = "StartCampUnderTest" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(view);
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);
        view.Configure(AdventureMapAssetFactory.CreateStartCampTemplate(), candidates);

        AssertThat(view.MapInstance.Surface.TerrainLayer).IsInstanceOf<TileMapLayer>();
        AssertThat(view.MapInstance.Surface.TerrainLayer.GetUsedCells().Count).IsEqual(100);
        AssertThat(view.CandidateActors.Count).IsEqual(candidates.Length);
        AssertThat(view.CandidateCells.Values.Distinct().Count()).IsEqual(candidates.Length);
        AssertThat(view.CandidateActors.Select(value => value.Value.DefinitionId).ToArray())
            .ContainsExactly(candidates.Select(value => value.Definition.ContentIdValue).ToArray());
        AssertThat(view.CandidateActors.Values.All(actor => actor is GodotUnitActor)).IsTrue();
        AssertThat(Descendants<Label>(view).Any(label => candidates.Any(candidate =>
            label.Text.Contains(candidate.CharacterId, StringComparison.Ordinal)))).IsFalse();
        AssertThat(view.Campfire).IsInstanceOf<GodotStartCampfireActor>();
        AssertThat(view.Exit).IsInstanceOf<GodotStartCampExitActor>();
        AssertThat(GodotStartCampView.SafeMapArea.Encloses(view.FittedMapBounds)).IsTrue();

        view.SetSelection(candidates.Take(3).Select(value => value.CharacterId).ToArray(), true);
        AssertThat(view.Exit.IsUnlocked).IsTrue();
        view.SetSelection(candidates.Take(2).Select(value => value.CharacterId).ToArray(), false);
        AssertThat(view.Exit.IsUnlocked).IsFalse();
        view.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartCampReportsCandidateCapacityInsteadOfIndexingPastSlots()
    {
        AdventureMapTemplateResource template = AdventureMapAssetFactory.CreateStartCampTemplate();
        GodotStartCampCandidate[] candidates = LoadUnitDefinitions(7).Select((definition, index) =>
            new GodotStartCampCandidate($"candidate-{index + 1}", definition)).ToArray();
        var view = new GodotStartCampView();

        AssertThrown(() => view.Configure(template, candidates))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("Start camp template has 6 candidate slots for 7 candidates.");
        view.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task StartCampKeepsOneActiveMapAndBuildsAllRouteNodesAsPlanningPreviews()
    {
        GodotStartCampCandidate[] candidates = LoadUnitDefinitions(4).Select((definition, index) =>
            new GodotStartCampCandidate($"candidate-{index + 1}", definition)).ToArray();
        var view = new GodotStartCampView { Name = "StartCampAtlasUnderTest" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(view);
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);
        view.Configure(AdventureMapAssetFactory.CreateStartCampTemplate(), candidates);
        var map = new PureRunMapDefinition(new ContentId("run-map.test"), 2,
        [
            new PureRunMapNodeDefinition("battle", 4, PureRunNodeKind.Battle, new ContentId("encounter.test"), Lane: -1),
            new PureRunMapNodeDefinition("rest", 4, PureRunNodeKind.Rest, new ContentId("rest.test"), Lane: 1),
            new PureRunMapNodeDefinition("boss", 5, PureRunNodeKind.Boss, new ContentId("boss.test"))
        ],
        [
            new PureRunMapConnectionDefinition("battle", "boss"),
            new PureRunMapConnectionDefinition("rest", "boss")
        ]);

        view.ConfigureRoutePreviews(AdventureMapAssetFactory.CreateBasicBattleTemplate(), map);
        view.SetSelection([candidates[0].CharacterId], false);

        AssertThat(view.MapInstance.Mode).IsEqual(AdventureMapInstanceMode.Active);
        AssertThat(view.RoutePreviews.Count).IsEqual(map.Nodes.Count);
        AssertThat(view.RoutePreviews.All(value => value.Mode == AdventureMapInstanceMode.Preview)).IsTrue();
        AssertThat(view.LeaderId).IsEqual(candidates[0].CharacterId);
        view.GrabFocus();
        view._UnhandledKeyInput(new InputEventKey { Keycode = Key.M, Pressed = true });
        AssertThat(view.IsAtlasOverview).IsTrue();
        AssertThat(view.AtlasZoom).IsEqual(.78f);
        view._UnhandledKeyInput(new InputEventKey { Keycode = Key.M, Pressed = true });
        AssertThat(view.IsAtlasOverview).IsFalse();
        AssertThat(view.AtlasZoom).IsEqual(1f);
        view._UnhandledKeyInput(new InputEventKey { Keycode = Key.F, Pressed = true });
        AssertThat(view.AtlasPan).IsNotEqual(Vector2.Zero);
        view.QueueFree();
    }

    private static UnitDefinitionResource[] LoadUnitDefinitions(int count)
    {
        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres")!;
        return catalog.Entries.Select(entry => ResourceLoader.Load(entry.ResourceLocator))
            .OfType<UnitDefinitionResource>().Take(count).ToArray();
    }

    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (T nested in Descendants<T>(child)) yield return nested;
        }
    }
}
