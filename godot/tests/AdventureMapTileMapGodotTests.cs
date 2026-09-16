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
            new PureRunMapNodeDefinition("start", 0, PureRunNodeKind.Rest, new ContentId("start.test")),
            new PureRunMapNodeDefinition("battle", 1, PureRunNodeKind.Battle, new ContentId("encounter.test")),
            new PureRunMapNodeDefinition("left", 4, PureRunNodeKind.Rest, new ContentId("rest.test"), Lane: -1),
            new PureRunMapNodeDefinition("right", 4, PureRunNodeKind.Store, new ContentId("store.test"), Lane: 1)
        ],
        [
            new PureRunMapConnectionDefinition("start", "battle"),
            new PureRunMapConnectionDefinition("battle", "left"),
            new PureRunMapConnectionDefinition("battle", "right")
        ]);

        view.ConfigureRoutePreviews(AdventureMapAssetFactory.CreateBasicBattleTemplate(), map);
        view.SetSelection([candidates[0].CharacterId], false);

        AssertThat(view.MapInstance.Mode).IsEqual(AdventureMapInstanceMode.Active);
        AssertThat(view.RoutePreviews.Count).IsEqual(map.Nodes.Count - 1);
        AssertThat(view.RoutePreviews.All(value => value.Mode == AdventureMapInstanceMode.Preview)).IsTrue();
        AssertThat(view.RoutePreviews.Any(value => value.Name == "PlanningPreview_start")).IsFalse();
        Rect2 start = view.AtlasNodeBounds["start"];
        Rect2 battle = view.AtlasNodeBounds["battle"];
        Rect2 left = view.AtlasNodeBounds["left"];
        Rect2 right = view.AtlasNodeBounds["right"];
        AssertThat(view.AtlasNodeBounds.Values.All(value => value.Size == start.Size)).IsTrue();
        AssertThat(battle.Position.X).IsGreater(start.Position.X);
        AssertThat(battle.Position.Y).IsLess(start.Position.Y);
        AssertThat(left.Position.Y).IsEqual(right.Position.Y);
        AssertThat(left.Position.X).IsLess(right.Position.X);
        AdventureMapTemplateDefinition campDefinition = AdventureMapAssetFactory.CreateStartCampTemplate().ToCoreDefinition();
        AdventureMapTemplateDefinition previewDefinition = AdventureMapAssetFactory.CreateBasicBattleTemplate().ToCoreDefinition();
        (Vector2 from, Vector2 to) = view.ConnectionEndpoints["start->battle"];
        AssertThat(from).IsEqual(view.AtlasNodeTransforms["start"] *
            view.MapInstance.Surface.CellCenter(campDefinition.Exits.Single().Cell));
        AssertThat(to).IsEqual(view.AtlasNodeTransforms["battle"] *
            view.MapInstance.Surface.CellCenter(previewDefinition.Entries.Single().Cell));
        float currentMapZoom = GodotBattleBoardFitter.Fit(start, GodotStartCampView.AtlasViewport).X.Length();
        AssertThat(view.IsAtlasOverview).IsFalse();
        AssertThat(view.AtlasZoom).IsEqual(currentMapZoom);
        AssertThat(GodotStartCampView.AtlasViewport.Encloses(view.FittedMapBounds)).IsTrue();
        AssertThat(view.FittedMapBounds.Size.Y).IsGreater(GodotStartCampView.AtlasViewport.Size.Y * .95f);
        GodotUnitActor candidateActor = view.CandidateActors[candidates[^1].CharacterId];
        Rect2 candidateBounds = candidateActor.VisualBoundsInParent();
        Vector2 bodyPoint = new(candidateBounds.GetCenter().X, candidateBounds.Position.Y + 4f);
        AssertThat(bodyPoint.DistanceTo(candidateActor.Position)).IsGreater(34f);
        Node2D candidateParent = (Node2D)candidateActor.GetParent();
        Vector2 candidateViewPoint = view.GetGlobalTransform().AffineInverse() * (candidateParent.GetGlobalTransform() * bodyPoint);
        AssertThat(view.ResolveCandidateAt(candidateViewPoint)).IsEqual(candidates[^1].CharacterId);
        string? pressedCandidate = null;
        view.CandidatePressed += id => pressedCandidate = id;
        view.HandleAtlasInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true }, candidateViewPoint);
        AssertThat(pressedCandidate).IsEqual(candidates[^1].CharacterId);
        GodotUnitActor rearActor = view.CandidateActors[candidates[0].CharacterId];
        Rect2 rearBounds = rearActor.VisualBoundsInParent();
        Vector2 overlapPoint = new(rearBounds.GetCenter().X, rearBounds.Position.Y + 4f);
        Vector2 overlapViewPoint = view.GetGlobalTransform().AffineInverse() *
            (((Node2D)rearActor.GetParent()).GetGlobalTransform() * overlapPoint);
        AssertThat(view.ResolveCandidateAt(overlapViewPoint)).IsEqual(candidates[1].CharacterId);
        AssertThat(GodotPlayableRunMain.StartPageUiZIndex).IsGreater(GodotStartCampView.AtlasWorldMaxZIndex);
        AssertThat(GodotPlayableRunMain.PauseOverlayZIndex).IsGreater(GodotPlayableRunMain.StartPageUiZIndex);
        Label battleBadge = Descendants<Label>(view).Single(label => label.Name == "PreviewBadge_battle");
        AssertThat(Descendants<Label>(view).Count(label => label.Name.ToString().StartsWith("PreviewBadge_", StringComparison.Ordinal)))
            .IsEqual(map.Nodes.Count);
        AssertThat(battleBadge.Position.Y).IsLess(battle.Position.Y);
        AssertThat(battleBadge.Position.X + battleBadge.Size.X * .5f).IsEqual(battle.GetCenter().X);
        AssertThat(view.LeaderId).IsEqual(candidates[0].CharacterId);
        view.HandleAtlasKey(new InputEventKey { Keycode = Key.M, Pressed = true });
        AssertThat(view.IsAtlasOverview).IsTrue();
        float overviewZoom = view.AtlasZoom;
        AssertThat(overviewZoom).IsLess(1f);
        float fullRouteFitZoom = GodotBattleBoardFitter.Fit(
            GodotAdventureAtlasLayout.Union(view.AtlasNodeBounds.Values), GodotStartCampView.AtlasViewport).X.Length();
        AssertThat(overviewZoom).IsGreater(fullRouteFitZoom);
        pressedCandidate = null;
        view.HandleAtlasInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true }, candidateViewPoint);
        AssertThat(pressedCandidate).IsNull();
        Vector2 overviewPan = view.AtlasPan;
        view.HandleAtlasInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true }, candidateViewPoint);
        AssertThat(view.AtlasZoom).IsEqual(overviewZoom);
        AssertThat(view.AtlasPan).IsEqual(overviewPan);
        view.HandleAtlasKey(new InputEventKey { Keycode = Key.M, Pressed = true });
        AssertThat(view.IsAtlasOverview).IsFalse();
        AssertThat(view.AtlasZoom).IsEqual(currentMapZoom);
        view.HandleAtlasKey(new InputEventKey { Keycode = Key.M, Pressed = true });
        AssertThat(view.IsAtlasOverview).IsTrue();
        view.HandleAtlasKey(new InputEventKey { Keycode = Key.F, Pressed = true });
        AssertThat(view.IsAtlasOverview).IsFalse();
        AssertThat(view.AtlasZoom).IsGreater(overviewZoom);
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
