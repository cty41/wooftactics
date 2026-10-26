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
public sealed class StartCampResidentWorldGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task ReplacingAViewKeepsResidentMapsAndDoesNotDeleteTheNextViewsActors()
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var stage = new Node2D();
        tree.Root.AddChild(stage);
        GodotActWorldHost host = CreateHost();
        stage.AddChild(host);
        host.ActivateNode("start");
        var page = new Control { Size = new Vector2(1600, 900) };
        stage.AddChild(page);
        GodotStartCampCandidate[] candidates = Candidates();
        var first = new GodotStartCampView();
        page.AddChild(first);
        try
        {
            first.ConfigureResidentWorld(host, candidates, host.Definition.Map);
            ulong mapId = first.MapInstance.GetInstanceId(), surfaceId = first.MapInstance.Surface.GetInstanceId();
            ulong otherMapId = host.Nodes["fight"].GetInstanceId();
            Transform2D firstMapTransform = first.MapInstance.Transform;
            AssertThat(first.RoutePreviews.Single().GetInstanceId()).IsEqual(otherMapId);
            AssertThat(host.PreparedCount).IsEqual(2);
            AssertThat(Descendants<GodotAdventureMapInstance>(host).Count()).IsEqual(2);
            AssertThat(Descendants<GodotAdventureMapInstance>(first).Count()).IsEqual(0);
            AssertThat(first.AtlasNodeTransforms["start"]).IsEqual(host.Nodes["start"].Transform);
            first.QueueFree();
            var second = new GodotStartCampView();
            page.AddChild(second);
            second.ConfigureResidentWorld(host, candidates, host.Definition.Map);
            GodotStartCampfireActor nextCampfire = second.Campfire;
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(GodotObject.IsInstanceValid(host)).IsTrue();
            AssertThat(host.IsPrepared).IsTrue();
            AssertThat(second.MapInstance.GetInstanceId()).IsEqual(mapId);
            AssertThat(second.MapInstance.Surface.GetInstanceId()).IsEqual(surfaceId);
            AssertThat(second.MapInstance.Transform).IsEqual(firstMapTransform);
            AssertThat(host.Nodes["fight"].GetInstanceId()).IsEqual(otherMapId);
            AssertThat(host.PreparedCount).IsEqual(2);
            AssertThat(GodotObject.IsInstanceValid(nextCampfire)).IsTrue();
            AssertThat(second.CandidateActors.Values.All(GodotObject.IsInstanceValid)).IsTrue();
            AssertThrown(() => second.ConfigureRoutePreviews(AdventureMapAssetFactory.CreateBasicBattleTemplate(), host.Definition.Map))
                .IsInstanceOf<InvalidOperationException>();
            second.QueueFree();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(host.IsPrepared).IsTrue();
            AssertThat(host.Nodes["start"].Surface.GetInstanceId()).IsEqual(surfaceId);
        }
        finally
        {
            stage.QueueFree();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task BorrowedWorldUsesTheSameTransformForFeetCameraAndPickingAcrossDifferentParents()
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var stage = new Node2D { Position = new Vector2(23, 17), Scale = new Vector2(.9f, .9f) };
        tree.Root.AddChild(stage);
        GodotActWorldHost host = CreateHost();
        stage.AddChild(host);
        host.ActivateNode("start");
        var page = new Control { Position = new Vector2(45, 30), Scale = new Vector2(.8f, .8f), Size = new Vector2(1600, 900) };
        stage.AddChild(page);
        var view = new GodotStartCampView();
        page.AddChild(view);
        try
        {
            GodotStartCampCandidate[] candidates = Candidates();
            view.ConfigureResidentWorld(host, candidates, host.Definition.Map);
            GodotUnitActor actor = view.CandidateActors[candidates[0].CharacterId];
            GridPoint cell = view.CandidateCells[candidates[0].CharacterId];
            Vector2 expectedFoot = view.MapInstance.GlobalTransform * view.MapInstance.Surface.CellCenter(cell);
            AssertThat(actor.GlobalPosition.IsEqualApprox(expectedFoot)).IsTrue();
            AssertThat(view.TryResolveTarget("AdventureActor", candidates[0].CharacterId, out Vector2 target)).IsTrue();
            string? clicked = null;
            view.CandidatePressed += id => clicked = id;
            view.HandleAtlasInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true },
                view.GetGlobalTransform().AffineInverse() * target);
            AssertThat(clicked).IsEqual(candidates[0].CharacterId);
            AssertThat(view.TryResolveTarget("AdventureCell", "2,5", out Vector2 worldCell)).IsTrue();
            AssertThat(worldCell.IsEqualApprox(view.MapInstance.GlobalTransform * view.MapInstance.Surface.CellCenter(new GridPoint(2, 5)))).IsTrue();
            view.HandleAtlasKey(new InputEventKey { Keycode = Key.M, Pressed = true });
            AssertThat(view.IsAtlasOverview).IsTrue();
            view.HandleAtlasKey(new InputEventKey { Keycode = Key.F, Pressed = true });
            AssertThat(view.IsAtlasOverview).IsFalse();
            AssertThat(host.Nodes["start"].Transform).IsEqual(view.AtlasNodeTransforms["start"]);
            AssertThat(host.ActiveNodeId).IsEqual("start");
        }
        finally
        {
            stage.QueueFree();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static GodotActWorldHost CreateHost()
    {
        var map = new PureRunMapDefinition(new ContentId("run-map.resident-test"), 2,
            [new("start", 0, PureRunNodeKind.Battle, new ContentId("run.test")),
             new("fight", 1, PureRunNodeKind.Boss, new ContentId("encounter.test"))], [new("start", "fight")]);
        var definition = new PureRunActDefinition(new ContentId("act-01"), 1, map, "start", "fight",
            [new("start", new ContentId(AdventureMapAssetFactory.StartCampContentId), PureRunNodeEntryStrategy.Exploration),
             new("fight", new ContentId(AdventureMapAssetFactory.BasicBattleContentId), PureRunNodeEntryStrategy.ImmediateCombat)]);
        AdventureMapTemplateResource camp = AdventureMapAssetFactory.CreateStartCampTemplate();
        camp.ExitsValue = "exit-main@9,5@fight@entry-main";
        AdventureMapTemplateResource battle = AdventureMapAssetFactory.CreateBasicBattleTemplate();
        battle.IsTerminal = true;
        battle.ExitsValue = string.Empty;
        battle.ConnectionAnchorsValue = string.Empty;
        var host = new GodotActWorldHost();
        host.BeginPreparation("resident-view-test", definition);
        host.PrepareNode("start", camp);
        host.PrepareNode("fight", battle);
        host.CompletePreparation();
        return host;
    }

    private static GodotStartCampCandidate[] Candidates()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres");
        return catalog.Entries.Select(entry => ResourceLoader.Load(entry.ResourceLocator)).OfType<UnitDefinitionResource>()
            .Take(3).Select((resource, index) => new GodotStartCampCandidate($"candidate-{index + 1}", resource)).ToArray();
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
