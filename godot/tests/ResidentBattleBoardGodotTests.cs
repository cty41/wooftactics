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
public sealed class ResidentBattleBoardGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task BorrowedTerrainKeepsItsIdentityAndAllCellsUseTheSameCrossParentTransform()
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node2D();
        tree.Root.AddChild(root);
        try
        {
            GodotActWorldHost host = CreateHost();
            root.AddChild(host);
            host.Transform = new Transform2D(.13f, new Vector2(81, -37));
            var page = new Node2D { Position = new Vector2(-15, 28), Scale = new Vector2(.8f, 1.1f) };
            var uiLayer = new CanvasLayer { Offset = new Vector2(33, 12), Rotation = -.07f };
            root.AddChild(uiLayer);
            uiLayer.AddChild(page);
            var board = new GodotIsometricBattleBoard { Size = new Vector2(1200, 900), Position = new Vector2(19, 31) };
            page.AddChild(board);
            GodotNodeActivationLease lease = host.ActivateNode("fight");
            GodotIsometricTileMapSurface surface = host.Nodes["fight"].Surface;
            ulong surfaceId = surface.GetInstanceId(), terrainId = surface.TerrainLayer.GetInstanceId();
            Transform2D terrainTransform = surface.TerrainLayer.Transform;
            Vector2I[] cells = surface.TerrainLayer.GetUsedCells().ToArray();
            board.BindResidentSurface(host, lease);
            var template = surface.Template!.ToCoreDefinition();
            board.SetVisuals(new Dictionary<GridPoint, Color> { [new(2, 3)] = Colors.Red }, template.Board.BlockedCells);
            for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
            {
                GridPoint cell = new(x, y);
                Vector2 center = board.CellCenter(cell);
                AssertThat((board.GetGlobalTransformWithCanvas() * center).IsEqualApprox(surface.GetGlobalTransformWithCanvas() * surface.CellCenter(cell))).IsTrue();
                AssertThat(board.TryPointToCell(center, out GridPoint picked)).IsTrue();
                AssertThat(picked).IsEqual(cell);
            }
            GridPoint? clicked = null;
            board.CellPressed += cell => clicked = cell;
            board._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, Position = board.CellCenter(new(2, 3)) });
            AssertThat(clicked).IsEqual(new GridPoint(2, 3));
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(board.UsesResidentSurface).IsTrue();
            AssertThat(board.ResidentSurface!.GetInstanceId()).IsEqual(surfaceId);
            AssertThat(surface.TerrainLayer.GetInstanceId()).IsEqual(terrainId);
            AssertThat(surface.TerrainLayer.Transform).IsEqual(terrainTransform);
            AssertThat(surface.TerrainLayer.GetUsedCells().ToArray()).IsEqual(cells);
            AssertThat(surface.TerrainLayer.GetCellAtlasCoords(new(2, 3))).IsEqual(new Vector2I(0, 0));
        }
        finally { root.QueueFree(); await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ExpiredLeaseRejectsInputAndOldViewExitCannotReleaseTheReplacementMap()
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node2D();
        tree.Root.AddChild(root);
        try
        {
            GodotActWorldHost host = CreateHost();
            root.AddChild(host);
            var old = new GodotIsometricBattleBoard();
            root.AddChild(old);
            GodotNodeActivationLease oldLease = host.ActivateNode("fight");
            old.BindResidentSurface(host, oldLease);
            Vector2 point = old.CellCenter(new(2, 3));
            int clicks = 0;
            old.PointerPressed += _ => clicks++;
            int exits = 0;
            old.HoverCleared += () => exits++;
            old.PointerExited += () => exits++;
            ulong surfaceId = old.ResidentSurface!.GetInstanceId();
            host.DeactivateCurrent();
            GodotNodeActivationLease nextLease = host.ActivateNode("fight");
            var next = new GodotIsometricBattleBoard();
            root.AddChild(next);
            next.BindResidentSurface(host, nextLease);
            AssertThat(old.TryPointToCell(point, out _)).IsFalse();
            old._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, Position = point });
            AssertThat(clicks).IsEqual(0);
            old._Notification((int)Control.NotificationMouseExit);
            AssertThat(exits).IsEqual(0);
            AssertThrown(() => old.BindResidentSurface(host, oldLease)).IsInstanceOf<InvalidOperationException>();
            old.QueueFree();
            for (int i = 0; i < 4 && GodotObject.IsInstanceValid(old); i++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(GodotObject.IsInstanceValid(old)).IsFalse();
            AssertThat(host.IsLeaseCurrent(nextLease)).IsTrue();
            AssertThat(next.ResidentSurface!.GetInstanceId()).IsEqual(surfaceId);
            AssertThat(next.TryPointToCell(next.CellCenter(new(2, 3)), out _)).IsTrue();
            host.Release();
            AssertThat(next.TryPointToCell(point, out _)).IsFalse();
        }
        finally { root.QueueFree(); await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task MismatchedBattleTerrainIsRejectedInsteadOfPaintingOverTheFormalMap()
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node2D();
        tree.Root.AddChild(root);
        try
        {
            GodotActWorldHost host = CreateHost();
            root.AddChild(host);
            var board = new GodotIsometricBattleBoard();
            root.AddChild(board);
            board.BindResidentSurface(host, host.ActivateNode("fight"));
            var template = board.ResidentSurface!.Template!.ToCoreDefinition();
            board.SetVisuals(new Dictionary<GridPoint, Color>(), template.Board.BlockedCells);
            AssertThrown(() => board.SetVisuals(new Dictionary<GridPoint, Color>(), Array.Empty<GridPoint>()))
                .IsInstanceOf<InvalidOperationException>();
            AssertThrown(() => board.SetVisuals(new Dictionary<GridPoint, Color>(), template.Board.BlockedCells, [new GridPoint(2, 3)]))
                .IsInstanceOf<InvalidOperationException>();
            AssertThat(board.ResidentSurface.TerrainLayer.GetUsedCells().Count).IsEqual(100);
        }
        finally { root.QueueFree(); await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task HoverCallbacksCannotPublishMotionAfterRebindingOrRevokingTheLease()
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node2D();
        tree.Root.AddChild(root);
        try
        {
            GodotActWorldHost host = CreateHost();
            root.AddChild(host);
            var board = new GodotIsometricBattleBoard();
            root.AddChild(board);
            board.BindResidentSurface(host, host.ActivateNode("fight"));
            Vector2 point = board.CellCenter(new(2, 3));
            int motions = 0;
            board.PointerMoved += _ => motions++;
            board.CellHovered += _ =>
            {
                host.DeactivateCurrent();
                board.BindResidentSurface(host, host.ActivateNode("fight"));
            };
            board._GuiInput(new InputEventMouseMotion { Position = point });
            AssertThat(motions).IsEqual(0);
            int exits = 0;
            board.HoverCleared += host.DeactivateCurrent;
            board.PointerExited += () => exits++;
            board._Notification((int)Control.NotificationMouseExit);
            AssertThat(exits).IsEqual(0);
        }
        finally { root.QueueFree(); await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame); }
    }

    private static GodotActWorldHost CreateHost()
    {
        var map = new PureRunMapDefinition(new ContentId("run-map.resident-battle-test"), 2,
            [new("start", 0, PureRunNodeKind.Battle, new ContentId("run.test")),
             new("fight", 1, PureRunNodeKind.Boss, new ContentId("encounter.test"))], [new("start", "fight")]);
        var definition = new PureRunActDefinition(new ContentId("act-01"), 1, map, "start", "fight",
            [new("start", new ContentId(AdventureMapAssetFactory.StartCampContentId), PureRunNodeEntryStrategy.Exploration),
             new("fight", new ContentId(AdventureMapAssetFactory.BasicBattleContentId), PureRunNodeEntryStrategy.ImmediateCombat)]);
        var camp = AdventureMapAssetFactory.CreateStartCampTemplate();
        camp.ExitsValue = "exit-main@9,5@fight@entry-main";
        var battle = AdventureMapAssetFactory.CreateBasicBattleTemplate();
        battle.IsTerminal = true;
        battle.ExitsValue = string.Empty;
        battle.ConnectionAnchorsValue = string.Empty;
        var host = new GodotActWorldHost();
        host.BeginPreparation("resident-battle-test", definition);
        host.PrepareNode("start", camp);
        host.PrepareNode("fight", battle);
        host.CompletePreparation();
        return host;
    }
}
