using GdUnit4;
using Godot;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public sealed class AdventureMovementGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task ActiveAdventureUsesRealActorsAndRepathsAtTheNextCellCenter()
    {
        GodotAdventureBoardView view = await CreateView();
        GodotUnitActor leader = view.ActorNodes["pure_run_mage"];
        UnitDefinitionResource mage = Load("res://content/units/PureRunMage.tres");
        GridPoint origin = new(2, 5);
        GridPoint firstTarget = new(7, 5);
        GridPoint redirectedTarget = new(4, 7);
        int completions = 0;
        int actorPresses = 0;
        string? rejection = null;
        AssertThrown(() => view.MoveActor("pure_run_necromancer", origin))
            .IsInstanceOf<ArgumentException>();
        view.MovementCompleted += _ => completions++;
        view.ActorPressed += _ => actorPresses++;
        view.MovementRejected += value => rejection = value;

        await Click(view, view.CellCenter(firstTarget));
        AssertThat(view.IsMovementLocked).IsTrue();
        AssertThrown(() => view.MoveActor("pure_run_necromancer", new GridPoint(3, 6)))
            .IsInstanceOf<InvalidOperationException>();
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);
        AssertThat(leader.PresentationFacing).IsEqual(GodotUnitFacing.East);
        AssertThat(leader.Body!.Texture).IsEqual(mage.UpLeftTexture);
        AssertThat(leader.Body.FlipH).IsTrue();
        AssertThat(view.ActorCells["pure_run_mage"]).IsEqual(origin);
        AssertThat(leader.Position).IsNotEqual(view.CellCenter(firstTarget));

        PressNow(view, view.CellCenter(redirectedTarget));
        AssertThat(view.LastMovementDiagnostic).IsNotNull();
        AssertThat(view.LastMovementDiagnostic!.DesiredDestination).IsEqual(redirectedTarget);
        AssertThat(view.LastMovementDiagnostic.ReplanCount).IsEqual(1);
        AssertThat(view.LastMovementDiagnostic.CurrentCell).IsEqual(origin);
        AssertThat(view.ActorCells["pure_run_mage"]).IsEqual(origin);
        ReleaseNow(view, view.CellCenter(redirectedTarget));
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);

        await Click(view, view.CellCenter(new GridPoint(1, 4)));
        AssertThat(actorPresses).IsEqual(0);
        AssertThat(rejection).IsEqual("adventure.movement_in_progress");

        await WaitUntil(view, () => !view.IsMovementLocked, 240);
        AssertThat(completions).IsEqual(1);
        AssertThat(view.ActorCells["pure_run_mage"]).IsEqual(redirectedTarget);
        AssertThat(leader.Position).IsEqual(view.CellCenter(redirectedTarget));
        AssertThat(leader.Body!.Position).IsEqual(Vector2.Zero);
        SceneTree tree = view.GetTree();
        view.QueueFree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureBoardFitsAndCentersInTheFullscreenGameplayArea()
    {
        GodotAdventureBoardView view = await CreateView();
        Rect2 safeArea = GodotAdventureBoardView.GameplaySafeArea(view.Size);
        Rect2 fitted = view.FittedBoardBounds;
        AssertThat(safeArea.Encloses(fitted)).IsTrue();
        AssertThat(fitted.GetCenter().DistanceTo(safeArea.GetCenter())).IsLess(0.1f);
        AssertThat(view.BoardScale).IsGreater(1f);
        SceneTree tree = view.GetTree();
        view.QueueFree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ViewportResizeInterruptsAtTheLastCenteredAuthoritativeCell()
    {
        GodotAdventureBoardView view = await CreateView();
        GridPoint origin = new(2, 5);
        GridPoint? interrupted = null;
        view.MovementInterruptedAtCell += cell => interrupted = cell;
        await Click(view, view.CellCenter(new GridPoint(7, 5)));
        await WaitUntil(view, () => view.LastMovementDiagnostic is { CurrentCell: var cell } && cell != origin, 240);
        GridPoint centered = view.LastMovementDiagnostic!.CurrentCell;

        view._Notification((int)Control.NotificationResized);
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);

        AssertThat(view.IsMovementLocked).IsFalse();
        AssertThat(interrupted).IsEqual(centered);
        AssertThat(view.ActorCells["pure_run_mage"]).IsEqual(centered);
        AssertThat(view.ActorNodes["pure_run_mage"].Position).IsEqual(view.CellCenter(centered));
        SceneTree tree = view.GetTree();
        view.QueueFree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PauseFreezesMovementAndExitTreeCancelsWithoutLateCompletion()
    {
        GodotAdventureBoardView view = await CreateView();
        int completions = 0;
        view.MovementCompleted += _ => completions++;
        await Click(view, view.CellCenter(new GridPoint(7, 5)));
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);
        view.SetMovementPaused(true);
        Vector2 paused = view.ActorNodes["pure_run_mage"].Position;
        for (int index = 0; index < 8; index++)
            await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);
        AssertThat(view.ActorNodes["pure_run_mage"].Position).IsEqual(paused);
        view.SetMovementPaused(false);
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);
        AssertThat(view.ActorNodes["pure_run_mage"].Position).IsNotEqual(paused);

        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.RemoveChild(view);
        for (int index = 0; index < 20; index++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(completions).IsEqual(0);
        AssertThat(view.IsMovementLocked).IsFalse();
        view.Free();
    }

    private static async Task<GodotAdventureBoardView> CreateView()
    {
        UnitDefinitionResource mage = Load("res://content/units/PureRunMage.tres");
        UnitDefinitionResource amazon = Load("res://content/units/PureRunAmazon.tres");
        UnitDefinitionResource necromancer = Load("res://content/units/PureRunNecromancer.tres");
        var profile = new StandardUnitPresentationResource { MoveCycleDuration = .12f, MoveSettleDuration = .03f };
        var view = new GodotAdventureBoardView
        {
            Name = "AdventureMovementUnderTest",
            Position = new Vector2(17, 23),
            Scale = new Vector2(.9f, .9f)
        };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(view);
        await view.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        view.ConfigureActors(new Dictionary<string, UnitDefinitionResource>(StringComparer.Ordinal)
        {
            ["pure_run_mage"] = mage,
            ["pure_run_amazon"] = amazon,
            ["pure_run_necromancer"] = necromancer
        }, "pure_run_mage", profile);
        view.SetBoard(Board());
        await view.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        return view;
    }

    private static AdventureBoardDefinition Board()
    {
        GridPoint[] perimeter = Enumerable.Range(0, 10).SelectMany(value =>
                new[] { new GridPoint(value, 0), new GridPoint(value, 9) })
            .Concat(Enumerable.Range(1, 8).SelectMany(value => new[] { new GridPoint(0, value), new GridPoint(9, value) }))
            .Distinct().ToArray();
        return new AdventureBoardDefinition(new ContentId("adventure-board.movement-test"), 10, 10, perimeter,
            [new AdventureBoardObject("exit", AdventureObjectKind.Exit, new GridPoint(8, 5), false, false)],
            [
                new AdventureActorPlacement("pure_run_mage", new GridPoint(2, 5)),
                new AdventureActorPlacement("pure_run_amazon", new GridPoint(1, 4)),
                new AdventureActorPlacement("pure_run_necromancer", new GridPoint(1, 6))
            ], new GridPoint(1, 5), new GridPoint(8, 5));
    }

    private static UnitDefinitionResource Load(string path) => ResourceLoader.Load<UnitDefinitionResource>(path)
        ?? throw new InvalidOperationException($"Unit resource is missing: {path}");

    private static void PressNow(GodotAdventureBoardView view, Vector2 localPoint)
    {
        Viewport viewport = view.GetViewport();
        Vector2 point = view.GetGlobalTransformWithCanvas() * localPoint;
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, false);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            Pressed = true
        }, false);
    }

    private static void ReleaseNow(GodotAdventureBoardView view, Vector2 localPoint)
    {
        Vector2 point = view.GetGlobalTransformWithCanvas() * localPoint;
        view.GetViewport().PushInput(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            Pressed = false
        }, false);
    }

    private static async Task Click(GodotAdventureBoardView view, Vector2 localPoint)
    {
        Viewport viewport = view.GetViewport();
        Vector2 point = view.GetGlobalTransformWithCanvas() * localPoint;
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, false);
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            Pressed = true
        }, false);
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            Pressed = false
        }, false);
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static async Task WaitUntil(Node node, Func<bool> predicate, int maximumFrames)
    {
        for (int frame = 0; frame < maximumFrames && !predicate(); frame++)
            await node.ToSignal(node.GetTree(), SceneTree.SignalName.ProcessFrame);
        AssertThat(predicate()).IsTrue();
    }
}
