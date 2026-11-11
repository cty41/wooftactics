using GdUnit4;
using Godot;
using System.Reflection;
using Tactics.Application.Runs;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

/// <summary>
/// Every tile-exploration page must borrow its node's formal resident map and draw the party with the same
/// battle sprites; a walled placeholder board may only exist for headless contexts.
/// </summary>
[TestSuite]
public sealed class ResidentExplorationPagesGodotTests
{
    private const int Seed = 37;

    [TestCase]
    [RequireGodotRuntime]
    public async Task InitialAdventureUsesTheStartCampFormalMapAndBattleSprites() => await WithRun(async (ui, host, setup, _) =>
    {
        InvokePrivate(ui, "ShowInitialAdventure", setup.Snapshot!.ActiveRun!);
        AssertResidentPage(ui, host, "start");
        AssertThat(Field<GodotAdventureBoardView>(ui, "_adventureBoard").Definition.Objects
            .Any(value => value.Kind == AdventureObjectKind.Campfire)).IsTrue();
        await Task.CompletedTask;
    });

    /// <summary>Only a freshly committed setup leaves the start map by itself; resuming an old InitialExploration save keeps the page.</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task ResumedInitialExplorationSaveKeepsTheStartPageInsteadOfStartingBattle() => await WithRun(async (ui, host, setup, store) =>
    {
        // WithRun committed the setup, which already went straight to battle. Routing the stored InitialExploration
        // snapshot again is exactly what a cold resume of an old save does: it must show the page, not exit again.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        long revision = store.Load().Snapshot!.Revision;
        InvokePrivate(ui, "RouteRunState", setup.Snapshot!);
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(Field<GodotAdventureBoardView?>(ui, "_adventureBoard")?.Name.ToString()).IsEqual("InitialAdventureBoard");
        AssertThat(store.Load().Snapshot!.Revision).IsEqual(revision);
    });

    [TestCase]
    [RequireGodotRuntime]
    public async Task BattleEntryStartsTheBattleOnceWithoutAnExplorationBoard() => await WithRun(async (ui, host, setup, store) =>
    {
        int begun = 0;
        InvokePrivate(ui, "ShowAdventureBattleEntry", setup.Snapshot!.ActiveRun!, "layer_01_battle", (Action)(() => begun++));
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(begun).IsEqual(1);
        AssertThat(Field<GodotAdventureBoardView?>(ui, "_adventureBoard")).IsNull();
        // Re-rendering the same node revision must not start a second battle.
        InvokePrivate(ui, "ShowAdventureBattleEntry", store.Load().Snapshot!.ActiveRun!, "layer_01_battle", (Action)(() => begun++));
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(begun).IsEqual(1);
    });

    [TestCase]
    [RequireGodotRuntime]
    public async Task FailedAutomaticBattleStartKeepsItsReasonOrSaysItFailedAndKeepsTheRetryButton() => await WithRun(async (ui, host, setup, store) =>
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        // The start reports its own rejection: that text must survive.
        InvokePrivate(ui, "ShowAdventureBattleEntry", setup.Snapshot!.ActiveRun!, "layer_01_battle",
            (Action)(() => Field<Label>(ui, "_status").Text = "battle.rejected_for_test"));
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(Field<Label>(ui, "_status").Text).IsEqual("battle.rejected_for_test");

        // A silent failure (page untouched) gets a generic message; the manual button stays available.
        store.RestoreFixture(setup.Snapshot!);
        typeof(GodotPlayableRunMain).GetField("_autoBattleKey", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ui, null);
        InvokePrivate(ui, "ShowAdventureBattleEntry", store.Load().Snapshot!.ActiveRun!, "layer_01_battle", (Action)(() => { }));
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(Field<Label>(ui, "_status").Text).Contains("battle.start_failed");
        AssertThat(ui.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.Text == "Begin battle")).IsTrue();
    });

    [TestCase]
    [RequireGodotRuntime]
    public async Task StaleDirectExitFlagNeverSkipsTheFallbackPageOfAnotherRun() => await WithRun(async (ui, host, setup, store) =>
    {
        store.RestoreFixture(setup.Snapshot!);
        typeof(GodotPlayableRunMain).GetField("_directExitRunId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(ui, "some-other-run");
        Invoke(ui, "RouteRunState", setup.Snapshot!);
        GodotAdventureBoardView board = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
        AssertThat(board.Name.ToString()).IsEqual("InitialAdventureBoard");
        AssertThat(store.Load().Snapshot!.ActiveRun!.AdventureState!.Lifecycle).IsEqual(RunAdventureLifecycle.InitialExploration);
        AssertThat(typeof(GodotPlayableRunMain).GetField("_directExitRunId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(ui)).IsNull();
        await Task.CompletedTask;
    });

    [TestCase]
    [RequireGodotRuntime]
    public async Task ResolvedBattleNodeShowsTheSameSpritesOnTheFormalMap() => await WithRun(async (ui, host, setup, _) =>
    {
        InvokePrivate(ui, "ShowResolvedAdventureNode", setup.Snapshot!.ActiveRun!);
        AssertResidentPage(ui, host, RunAdventureTransitionService.CurrentNodeId(setup.Snapshot!.ActiveRun!));
        await Task.CompletedTask;
    });

    /// <summary>The post-battle camera stays on the fought map; Overview (button or M) toggles the route view.</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task PostBattleCameraStartsOnTheMapAndOverviewToggles() => await WithRun(async (ui, host, setup, _) =>
    {
        InvokePrivate(ui, "ShowResolvedAdventureNode", setup.Snapshot!.ActiveRun!);
        GodotAdventureBoardView board = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
        AssertThat(board.FrameExtraPoints.Count).IsEqual(0);
        float mapScale = board.BoardScale;
        var button = (LinkButton)ui.FindChild("PostBattleOverviewButton", true, false)!;
        AssertThat(button.Text).IsEqual(GodotPlayableRunMain.PostBattleOverviewLabel);

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(board.FrameExtraPoints.Count).IsGreater(0);
        AssertThat(board.BoardScale).IsLess(mapScale);
        AssertThat(button.Text).IsEqual(GodotPlayableRunMain.PostBattleCurrentMapLabel);

        bool handled = (bool)typeof(GodotPlayableRunMain)
            .GetMethod("TryHandlePostBattleOverviewKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(ui, [new InputEventKey { Pressed = true, Keycode = Key.M }])!;
        AssertThat(handled).IsTrue();
        AssertThat(board.FrameExtraPoints.Count).IsEqual(0);
        AssertThat(board.BoardScale).IsEqualApprox(mapScale, .001f);
        await Task.CompletedTask;
    });

    [TestCase]
    [RequireGodotRuntime]
    public async Task RestStoreAndTreasureUseTheirFormalNodeMaps() => await WithRun(async (ui, host, setup, store) =>
    {
        PureRunMapDefinition map = LoadMap();
        foreach ((PureRunNodeKind kind, string nodeId, string objectId, AdventureObjectKind objectKind) in new[]
        {
            (PureRunNodeKind.Rest, "layer_04_rest", "rest-campfire", AdventureObjectKind.Campfire),
            (PureRunNodeKind.Store, "layer_04_store", "store-merchant", AdventureObjectKind.Merchant),
            (PureRunNodeKind.Treasure, "layer_04_treasure", "treasure-chest", AdventureObjectKind.Chest)
        })
        {
            PureRunSaveSnapshot fixture = NodeFixture(setup, map, nodeId, kind);
            store.RestoreFixture(fixture);
            InvokePrivate(ui, "ShowAdventureNodeEntry", fixture.ActiveRun!, kind);
            GodotAdventureBoardView view = AssertResidentPage(ui, host, nodeId);
            AdventureBoardObject interaction = view.Definition.Objects.Single(value => value.ObjectId == objectId);
            AssertThat(interaction.Kind).IsEqual(objectKind);
            // The formal template owns the terrain: its blocked cells are not the placeholder's perimeter wall.
            AssertThat(view.Definition.BlockedCells.Count < 10).IsTrue();
        }
        await Task.CompletedTask;
    });

    [TestCase]
    [RequireGodotRuntime]
    public async Task LeaderMovementPersistsAcrossRefreshOnTheFormalMap() => await WithRun(async (ui, host, setup, store) =>
    {
        InvokePrivate(ui, "ShowInitialAdventure", setup.Snapshot!.ActiveRun!);
        GodotAdventureBoardView first = AssertResidentPage(ui, host, "start");
        RunAdventureState state = store.Load().Snapshot!.ActiveRun!.AdventureState!;
        GridPoint leader = state.ActorCells.Single(value => value.ActorId == state.LeaderId).Cell;
        GridPoint[] occupied = first.Definition.Actors.Select(value => value.Cell)
            .Concat(first.Definition.Objects.Select(value => value.Cell)).ToArray();
        GridPoint destination = new[] { new GridPoint(leader.X, leader.Y + 1), new GridPoint(leader.X + 1, leader.Y),
                new GridPoint(leader.X, leader.Y - 1), new GridPoint(leader.X - 1, leader.Y) }
            .First(cell => first.Definition.IsWalkable(cell) && !occupied.Contains(cell) &&
                cell != first.Definition.EntryCell && cell != first.Definition.ExitCell);

        var click = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = true, Position = first.CellCenter(destination)
        };
        InvokePrivate(first, "OnGuiInput", click);
        AssertThat(first.IsWalking).IsTrue();
        await WaitWalk(ui);

        GodotAdventureBoardView refreshed = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
        AssertThat(ReferenceEquals(refreshed, first)).IsFalse();
        AssertThat(refreshed.Definition.Actors.Single(value => value.ActorId == state.LeaderId).Cell).IsEqual(destination);
        AssertThat(refreshed.ActorCells[state.LeaderId]).IsEqual(destination);
        await Task.CompletedTask;
    });

    /// <summary>A page that cannot borrow its resident map says so on screen, once, and never on a later page.</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task MismatchedResidentTemplateFallsBackAndShowsTheReasonOnThePage() => await WithRun(async (ui, host, setup, _) =>
    {
        FieldInfo world = typeof(GodotPlayableRunMain).GetField("_actWorld", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object? original = world.GetValue(ui);
        var unprepared = new GodotActWorldHost();
        world.SetValue(ui, unprepared);
        try
        {
            InvokePrivate(ui, "ShowInitialAdventure", setup.Snapshot!.ActiveRun!);
            AssertThat(Field<Label>(ui, "_status").Text).Contains("exploration.resident_template_mismatch:start");
            AssertThat(Field<GodotAdventureBoardView>(ui, "_adventureBoard").UsesResidentSurface).IsFalse();
        }
        finally
        {
            world.SetValue(ui, original);
            unprepared.Free();
        }
        // The reason was consumed: the next page, now on the real resident map, does not repeat it.
        InvokePrivate(ui, "ShowInitialAdventure", setup.Snapshot!.ActiveRun!);
        AssertThat(Field<Label>(ui, "_status").Text.Contains("exploration.", StringComparison.Ordinal)).IsFalse();
        AssertThat(Field<GodotAdventureBoardView>(ui, "_adventureBoard").UsesResidentSurface).IsTrue();
        await Task.CompletedTask;
    });

    /// <summary>A still-invalid party placement keeps the resident map off and puts its diagnostic on the status bar.</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task InvalidPlacementFallbackShowsItsReasonOnThePage() => await WithRun(async (ui, host, setup, _) =>
    {
        PureRunState run = setup.Snapshot!.ActiveRun!;
        string nodeId = RunAdventureTransitionService.CurrentNodeId(run);
        AdventureMapTemplateDefinition template = host.Nodes[nodeId].Surface.Template!.ToCoreDefinition();
        AdventureBoardObject blocker = template.Board.Objects.First(value =>
            value.Cell.X is > 0 and < 8 && value.Cell.Y is > 0 and < 9 && value.Cell != new GridPoint(8, 4));
        RunAdventureActorCell[] broken = run.AdventureState!.ActorCells
            .Select((cell, index) => index == 0 ? cell with { Cell = blocker.Cell } : cell).ToArray();
        PureRunState invalid = new RunAdventureTransitionService().PlaceParty(run, broken);

        InvokePrivate(ui, "ShowResolvedAdventureNode", invalid);
        AssertThat(Field<Label>(ui, "_status").Text).Contains($"exploration.party_placement_invalid:{nodeId}");
        AssertThat(Field<GodotAdventureBoardView>(ui, "_adventureBoard").UsesResidentSurface).IsFalse();
        await Task.CompletedTask;
    });

    /// <summary>Entering a node reseats a party that stands on objects or blocked cells; a valid placement is left untouched.</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task InvalidPartyPlacementIsReseatedOnTheTemplateAndValidOnesAreLeftAlone() => await WithRun(async (ui, host, setup, _) =>
    {
        var service = new RunAdventureTransitionService();
        MethodInfo seat = typeof(GodotPlayableRunMain).GetMethod("SeatPartyOnTemplate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        AdventureMapTemplateDefinition template = host.Nodes["start"].Surface.Template!.ToCoreDefinition();
        PureRunState run = setup.Snapshot!.ActiveRun!;
        AdventureBoardObject blocker = template.Board.Objects.First();
        RunAdventureActorCell[] broken = run.AdventureState!.ActorCells
            .Select((cell, index) => index == 0 ? cell with { Cell = blocker.Cell } : cell).ToArray();
        PureRunState invalid = service.PlaceParty(run, broken);

        var seated = (PureRunState)seat.Invoke(ui, [service, invalid, "start"])!;
        AssertThat(ReferenceEquals(seated, invalid)).IsFalse();
        AssertThat(seated.AdventureState!.SceneRevision).IsEqual(invalid.AdventureState!.SceneRevision + 1);
        AssertThat(seated.AdventureState.ActorCells.Count).IsEqual(3);
        foreach (RunAdventureActorCell actor in seated.AdventureState.ActorCells)
        {
            AssertThat(template.Board.IsWalkable(actor.Cell)).IsTrue();
            AssertThat(template.Board.Objects.Any(value => value.Cell == actor.Cell)).IsFalse();
        }
        AssertThat(seated.AdventureState.ActorCells.Select(value => value.Cell).Distinct().Count()).IsEqual(3);

        var again = (PureRunState)seat.Invoke(ui, [service, seated, "start"])!;
        AssertThat(ReferenceEquals(again, seated)).IsTrue();
        await Task.CompletedTask;
    });

    /// <summary>Cells win over the rim marker; the rim exit marker only answers clicks outside the board.</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task ClickingBesideTheExitWalksAndOnlyTheRimMarkerLeaves() => await WithRun(async (ui, host, setup, _) =>
    {
        InvokePrivate(ui, "ShowInitialAdventure", setup.Snapshot!.ActiveRun!);
        GodotAdventureBoardView board = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
        AdventureBoardObject exit = board.Definition.Objects.First(value => value.Kind == AdventureObjectKind.Exit);
        AssertThat(board.TryGetExitRimPosition(exit.ObjectId, out Vector2 rim)).IsTrue();
        GridPoint[] occupied = board.Definition.Actors.Select(value => value.Cell)
            .Concat(board.Definition.Objects.Select(value => value.Cell)).ToArray();
        GridPoint beside = new[] { new GridPoint(exit.Cell.X, exit.Cell.Y - 1), new GridPoint(exit.Cell.X, exit.Cell.Y + 1),
                new GridPoint(exit.Cell.X - 1, exit.Cell.Y) }
            .Where(cell => board.Definition.IsWalkable(cell) && !occupied.Contains(cell))
            .OrderBy(cell => board.CellCenter(cell).DistanceTo(rim)).First();
        Vector2 contestedPoint = board.CellCenter(beside);

        var cells = new List<GridPoint>();
        var objects = new List<string>();
        board.CellPressed += cell => cells.Add(cell);
        board.ObjectPressed += id => objects.Add(id);
        InvokePrivate(board, "OnGuiInput", new InputEventMouseButton
        { ButtonIndex = MouseButton.Left, Pressed = true, Position = contestedPoint });
        AssertThat(cells.SequenceEqual([beside])).IsTrue();
        AssertThat(objects.Count).IsEqual(0);
        await WaitWalk(ui);

        // The page re-rendered after the move; the new board answers the rim marker.
        GodotAdventureBoardView refreshed = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
        AssertThat(refreshed.TryGetExitRimPosition(exit.ObjectId, out Vector2 refreshedRim)).IsTrue();
        var rimObjects = new List<string>();
        refreshed.ObjectPressed += id => rimObjects.Add(id);
        InvokePrivate(refreshed, "OnGuiInput", new InputEventMouseButton
        { ButtonIndex = MouseButton.Left, Pressed = true, Position = refreshedRim });
        AssertThat(rimObjects.SequenceEqual([exit.ObjectId])).IsTrue();
        await Task.CompletedTask;
    });

    /// <summary>Every interaction object and exit of every formal map has a neighbour the party can walk to from each entry.</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task EveryObjectOfEveryFormalMapHasAReachableNeighbourFromEachEntry() => await WithRun(async (ui, host, setup, _) =>
    {
        var problems = new List<string>();
        foreach ((string nodeId, GodotAdventureMapInstance map) in host.Nodes)
        {
            AdventureMapTemplateDefinition template = map.Surface.Template!.ToCoreDefinition();
            foreach (AdventureMapAnchor entry in template.Entries)
            foreach (AdventureBoardObject value in template.Board.Objects)
            {
                bool reachable = new[] { new GridPoint(value.Cell.X, value.Cell.Y - 1), new GridPoint(value.Cell.X - 1, value.Cell.Y),
                        new GridPoint(value.Cell.X + 1, value.Cell.Y), new GridPoint(value.Cell.X, value.Cell.Y + 1) }
                    .Any(neighbour => template.Board.IsWalkable(neighbour) && (neighbour == entry.Cell ||
                        AdventureBoardPathfinder.FindPath(template.Board, entry.Cell, neighbour).Count > 0));
                if (!reachable) problems.Add($"{nodeId}:{value.ObjectId}@{value.Cell} from entry {entry.AnchorId}");
            }
        }
        AssertThat(string.Join("; ", problems)).IsEqual(string.Empty);
        await Task.CompletedTask;
    });

    /// <summary>The Inventory button sits in one shared slot and never covers an exit rim marker (the canvas is a fixed 1600x900 stretch).</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task InventoryButtonSharesOneSlotAndNeverCoversAnExitMarker() => await WithRun(async (ui, host, setup, _) =>
    {
        // The check below covers the x = 9 rim only; pin the authoring rule it relies on.
        foreach ((string id, GodotAdventureMapInstance map) in host.Nodes)
            AssertThat(map.Surface.Template!.ToCoreDefinition().Exits.All(exit => exit.Cell.X == 9)).IsTrue();
        MethodInfo rim = typeof(GodotAdventureBoardView).GetMethod("RimPosition", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var slots = new List<Vector2>();
        foreach (string page in new[] { "ShowInitialAdventure", "ShowResolvedAdventureNode" })
        {
            InvokePrivate(ui, page, setup.Snapshot!.ActiveRun!);
            GodotAdventureBoardView board = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
            var button = (Button)ui.FindChild("ExplorationInventoryButton", true, false)!;
            slots.Add(button.Position);
            var buttons = new List<Control> { button };
            if (ui.FindChild("PostBattleOverviewButton", true, false) is LinkButton overview && !overview.IsQueuedForDeletion())
                buttons.Add(overview);
            // Exits live on the x = 9 column; every possible rim marker (radius 32) must stay clear of the buttons.
            foreach (Control hud in buttons)
            {
                Rect2 buttonRect = new(hud.GetGlobalTransformWithCanvas().Origin, hud.Size);
                for (int y = 0; y < 10; y++)
                {
                    Vector2 marker = board.GetGlobalTransformWithCanvas() * (Vector2)rim.Invoke(board, [new GridPoint(9, y)])!;
                    AssertThat(buttonRect.Intersects(new Rect2(marker - new Vector2(32, 32), new Vector2(64, 64)))).IsFalse();
                }
            }
        }
        AssertThat(slots[0]).IsEqual(slots[1]);
        await Task.CompletedTask;
    });

    /// <summary>
    /// Successor maps sit one layer up-right of the current map, centred on it: a single exit's route is one straight
    /// line along the isometric slope, several fan out symmetrically, and route lines never cross.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task PostBattleSuccessorsLineUpParallelAndRoutesNeverCross() => await WithRun(async (ui, host, setup, _) =>
    {
        var board = new GodotAdventureBoardView();
        try
        {
            foreach ((string nodeId, GodotAdventureMapInstance map) in host.Nodes)
            {
                AdventureMapTemplateDefinition template = map.Surface.Template!.ToCoreDefinition();
                string[] targets = template.Exits.Select(exit => exit.TargetNodeId).Where(host.Nodes.ContainsKey).Distinct().ToArray();
                if (targets.Length == 0) continue;
                InvokePrivate(ui, "PreparePostBattleWorld", nodeId, template, targets, board);
                Rect2 bounds = GodotBattleBoardFitter.BoardBounds();
                Rect2 Diamond(string id) => new(host.Nodes[id].Transform * bounds.Position, bounds.Size);
                float laneSum = 0f;
                foreach (string target in targets)
                {
                    // Beyond the rim: the successor's centre lies further along grid +X (up-right) than the current map.
                    Vector2 delta = Diamond(target).GetCenter() - Diamond(nodeId).GetCenter();
                    var axes = new Transform2D(GodotAdventureAtlasLayout.BoardAxisX(bounds.Size),
                        GodotAdventureAtlasLayout.BoardAxisY(bounds.Size), Vector2.Zero);
                    Vector2 inBoards = axes.AffineInverse() * delta;
                    AssertThat(inBoards.X).IsEqualApprox(1f + GodotAdventureAtlasLayout.LayerGapFraction, 1e-3f);
                    laneSum += inBoards.Y;
                    AssertThat(GodotAdventureAtlasLayout.DiamondContains(Diamond(nodeId), Diamond(target).GetCenter())).IsFalse();
                    foreach (string other in targets.Where(value => string.CompareOrdinal(value, target) > 0))
                        AssertThat(GodotAdventureAtlasLayout.DiamondContains(Diamond(target), Diamond(other).GetCenter())).IsFalse();
                }
                // Centred on the current map: the successors' lane offsets cancel out (zero for a single successor).
                AssertThat(laneSum).IsEqualApprox(0f, 1e-3f);
                Line2D[] routes = host.Nodes[nodeId].GetNode("PostBattleRoutes").GetChildren().OfType<Line2D>().ToArray();
                AssertThat(routes.Length).IsEqual(targets.Length);
                if (routes.Length == 1)
                {
                    // One exit: centred on the rim and joined to the next map's entry by a line along grid +X.
                    AssertThat(template.Exits.Single().Cell).IsEqual(new GridPoint(9, 5));
                    Vector2 direction = (routes[0].Points[1] - routes[0].Points[0]).Normalized();
                    AssertThat(direction.IsEqualApprox(GodotAdventureAtlasLayout.BoardAxisX(bounds.Size).Normalized()))
                        .OverrideFailureMessage($"{routes[0].Name} direction {direction}").IsTrue();
                }
                for (int a = 0; a < routes.Length; a++)
                for (int b = a + 1; b < routes.Length; b++)
                    AssertThat(Geometry2D.SegmentIntersectsSegment(routes[a].Points[0], routes[a].Points[1],
                        routes[b].Points[0], routes[b].Points[1]).VariantType == Variant.Type.Nil)
                        .OverrideFailureMessage($"{routes[a].Name} {routes[a].Points[0]}->{routes[a].Points[1]} crosses {routes[b].Name} {routes[b].Points[0]}->{routes[b].Points[1]}")
                        .IsTrue();
            }
        }
        finally
        {
            InvokePrivate(ui, "RestoreHostBaseline", host);
            board.Free();
        }
        await Task.CompletedTask;
    });

    [TestCase]
    [RequireGodotRuntime]
    public async Task PostBattleLayoutSkipsRoutesWhoseTargetOrEntryIsMissing() => await WithRun(async (ui, host, setup, _) =>
    {
        (string nodeId, AdventureMapTemplateDefinition template) = host.Nodes
            .Select(pair => (pair.Key, pair.Value.Surface.Template!.ToCoreDefinition()))
            .First(value => value.Item2.Exits.Count > 0 && value.Item2.Exits.All(exit => host.Nodes.ContainsKey(exit.TargetNodeId)));
        AdventureMapExitAnchor good = template.Exits[0];
        AdventureMapExitAnchor ghost = good with { ExitId = "ghost", TargetNodeId = "ghost_node" };
        AdventureMapExitAnchor noEntry = good with { ExitId = "no-entry", TargetEntryId = "missing_entry" };
        AdventureMapTemplateDefinition damaged = template with { Exits = [good, ghost, noEntry] };
        var board = new GodotAdventureBoardView();
        try
        {
            InvokePrivate(ui, "PreparePostBattleWorld", nodeId, damaged, new[] { good.TargetNodeId, "ghost_node" }, board);
            var diagnostics = Field<List<string>>(ui, "_postBattleDiagnostics");
            AssertThat(diagnostics.Contains($"postbattle.route_target_missing:{nodeId}->ghost_node")).IsTrue();
            AssertThat(diagnostics.Contains($"postbattle.route_entry_missing:{nodeId}->{good.TargetNodeId}:missing_entry")).IsTrue();
            Node routes = host.Nodes[nodeId].GetNode("PostBattleRoutes");
            AssertThat(routes.GetChildCount()).IsEqual(1);
            AssertThat(board.FrameExtraPoints.Count).IsEqual(0);
            AssertThat(Field<IReadOnlyList<Vector2>>(ui, "_postBattleOverviewPoints").Count).IsEqual(4);
        }
        finally
        {
            InvokePrivate(ui, "RestoreHostBaseline", host);
            board.Free();
        }
        await Task.CompletedTask;
    });

    private static GodotAdventureBoardView AssertResidentPage(GodotPlayableRunMain ui, GodotActWorldHost host, string nodeId)
    {
        GodotAdventureBoardView view = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
        GodotAdventureMapInstance map = host.Nodes[nodeId];
        AssertThat(view.UsesResidentSurface).IsTrue();
        AssertThat(view.ResidentSurface!.GetInstanceId()).IsEqual(map.Surface.GetInstanceId());
        AssertThat(host.ActiveNodeId).IsEqual(nodeId);
        AssertThat(map.Visible).IsTrue();
        AdventureMapTemplateDefinition formal = map.Surface.Template!.ToCoreDefinition();
        AssertThat(view.Definition.BlockedCells.SequenceEqual(formal.Board.BlockedCells)).IsTrue();
        AssertThat(view.Definition.Actors.Count).IsEqual(3);
        foreach (AdventureActorPlacement actor in view.Definition.Actors)
        {
            AssertThat(view.IsActorUnit(actor.ActorId)).IsTrue();
            AssertThat(view.Definition.IsWalkable(actor.Cell)).IsTrue();
        }
        // No text-label stand-ins for the party.
        AssertThat(view.GetChildren().OfType<Label>()
            .Any(label => view.Definition.Actors.Any(actor => actor.ActorId == label.Name.ToString()))).IsFalse();
        return view;
    }

    private static PureRunSaveSnapshot NodeFixture(RunSessionResult setup, PureRunMapDefinition map, string nodeId, PureRunNodeKind kind)
    {
        PureRunState source = setup.Snapshot!.ActiveRun!;
        var mapService = new PureRunMapService(map);
        PureRunMapResult selected = mapService.BeginNode(mapService.UnlockLayerFour(source.Seed), nodeId);
        if (!selected.Succeeded || selected.Transaction is null)
            throw new InvalidOperationException($"Cannot select node fixture: {selected.RejectionCode}");
        PureRunState entered = new RunAdventureTransitionService().EnterBoard(source,
            new ContentId($"adventure-board.pure-run.{kind.ToString().ToLowerInvariant()}"));
        var ready = new PureRunState(source.RunId, source.Seed, source.Revision, PureRunPhase.ResolvingLayerFourNode,
            source.EncounterIndex, source.EncounterContentId, source.Party, source.BackpackConsumables,
            source.BackpackEquipment, source.PendingProgression, source.AppliedTransactionKeys, source.Gold,
            source.BattlesCompleted, source.EnemiesDefeated, source.AcquiredItems, null, selected.State,
            selected.Transaction, null, entered.AdventureState);
        return setup.Snapshot with { ActiveRun = ready };
    }

    /// <summary>Clicking a far exit walks at a constant pace to its best neighbour, ignores clicks on the way, then leaves.</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task ClickingAFarExitWalksBesideItThenLeaves() => await WithRun(async (ui, host, setup, store) =>
    {
        // WithRun's committed setup already left for the first battle; put the start-camp save back so the exit is real.
        store.RestoreFixture(setup.Snapshot!);
        PureRunState run = setup.Snapshot!.ActiveRun!;
        InvokePrivate(ui, "ShowInitialAdventure", run);
        GodotAdventureBoardView board = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
        AdventureBoardObject exit = board.Definition.Objects.First(value => value.Kind == AdventureObjectKind.Exit && !value.IsLocked);
        RunAdventureState state = run.AdventureState!;
        GridPoint leader = state.ActorCells.Single(value => value.ActorId == state.LeaderId).Cell;
        AssertThat(RunAdventureTransitionService.IsAdjacent(leader, exit.Cell)).IsFalse();
        GridPoint? approach = GodotPlayableRunMain.BestApproachCell(run, board.Definition, exit.Cell);
        AssertThat(approach.HasValue).IsTrue();
        IReadOnlyList<GridPoint> path = AdventureBoardPathfinder.FindPath(board.Definition, leader, approach!.Value,
            state.ActorCells.Where(value => value.ActorId != state.LeaderId).Select(value => value.Cell).ToArray());

        long exitsBefore = store.Load().Snapshot!.ActiveRun!.AdventureState!.ExitRevision;
        InvokePrivate(ui, "HandleExitPointer", run, board.Definition, exit.ObjectId);
        AssertThat(store.Load().Snapshot!.ActiveRun!.AdventureState!.ExitRevision).IsEqual(exitsBefore);
        AssertThat(board.IsWalking).IsTrue();
        AssertThat(board.LastWalkSeconds).IsEqualApprox((path.Count - 1) * GodotAdventureBoardView.ExplorationStepSeconds, 1e-4f);
        var ignored = new List<GridPoint>();
        board.CellPressed += cell => ignored.Add(cell);
        InvokePrivate(board, "OnGuiInput", new InputEventMouseButton
        { ButtonIndex = MouseButton.Left, Pressed = true, Position = board.CellCenter(leader) });
        AssertThat(ignored.Count).IsEqual(0);

        await WaitWalk(ui);
        PureRunState saved = store.Load().Snapshot!.ActiveRun!;
        AssertThat(RunAdventureTransitionService.CurrentNodeId(saved)).IsEqual(exit.TargetNodeId!);
        AssertThat(saved.AdventureState!.ExitRevision)
            .OverrideFailureMessage($"status={Field<Label>(ui, "_status").Text} node={RunAdventureTransitionService.CurrentNodeId(saved)} lifecycle={saved.AdventureState.Lifecycle} before={exitsBefore} after={saved.AdventureState.ExitRevision} runNode={RunAdventureTransitionService.CurrentNodeId(run)}/{run.AdventureState!.Lifecycle}/{run.AdventureState.ExitRevision}")
            .IsEqual(exitsBefore + 1);
    });

    /// <summary>A target no neighbour can be walked to only reports it and writes nothing.</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task UnreachableTargetOnlyReportsAndWritesNothing() => await WithRun(async (ui, host, setup, store) =>
    {
        PureRunState run = setup.Snapshot!.ActiveRun!;
        InvokePrivate(ui, "ShowInitialAdventure", run);
        GodotAdventureBoardView board = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
        string before = System.Text.Json.JsonSerializer.Serialize(store.Load().Snapshot);
        bool called = false;
        InvokePrivate(ui, "ApproachThen", run, board.Definition, new GridPoint(-5, -5), (Action<PureRunState>)(_ => called = true));
        AssertThat(called).IsFalse();
        AssertThat(board.IsWalking).IsFalse();
        AssertThat(Field<Label>(ui, "_status").Text).Contains(GodotPlayableRunMain.AdventureTargetUnreachable);
        AssertThat(System.Text.Json.JsonSerializer.Serialize(store.Load().Snapshot)).IsEqual(before);
        await Task.CompletedTask;
    });

    /// <summary>The Inventory button waits for the walk, and a walk whose board is no longer current never leaves the node.</summary>
    [TestCase]
    [RequireGodotRuntime]
    public async Task WalkInProgressBlocksInventoryAndAStaleBoardNeverInteracts() => await WithRun(async (ui, host, setup, store) =>
    {
        store.RestoreFixture(setup.Snapshot!);
        PureRunState run = setup.Snapshot!.ActiveRun!;
        InvokePrivate(ui, "ShowInitialAdventure", run);
        GodotAdventureBoardView board = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
        AdventureBoardObject exit = board.Definition.Objects.First(value => value.Kind == AdventureObjectKind.Exit && !value.IsLocked);
        long exitsBefore = store.Load().Snapshot!.ActiveRun!.AdventureState!.ExitRevision;
        InvokePrivate(ui, "HandleExitPointer", run, board.Definition, exit.ObjectId);
        AssertThat(board.IsWalking).IsTrue();

        Button inventory = (Button)ui.FindChild("ExplorationInventoryButton", true, false);
        inventory.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(ReferenceEquals(Field<GodotAdventureBoardView>(ui, "_adventureBoard"), board)).IsTrue();
        AssertThat(ui.FindChild("InventoryCharacterPanel", true, false) is null).IsTrue();

        // Another page took over while the old board (still alive until the frame ends) finishes its tween.
        FieldInfo current = typeof(GodotPlayableRunMain).GetField("_adventureBoard", BindingFlags.Instance | BindingFlags.NonPublic)!;
        current.SetValue(ui, null);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < 600 && board.IsWalking; frame++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(board.IsWalking).IsFalse();
        AssertThat(store.Load().Snapshot!.ActiveRun!.AdventureState!.ExitRevision).IsEqual(exitsBefore);
        AssertThat(ReferenceEquals(board.GetParent(), Field<Control>(ui, "_page"))).IsTrue();
    });

    private static async Task WaitWalk(GodotPlayableRunMain ui)
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < 600 && ui.IsAdventureWalking; frame++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(ui.IsAdventureWalking).IsFalse();
    }

    private static async Task WithRun(Func<GodotPlayableRunMain, GodotActWorldHost, RunSessionResult, MemoryStore, Task> body)
    {
        var store = new MemoryStore();
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, Seed, "resident-exploration-pages"));
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(ui);
        try
        {
            PureRunSessionService service = Field<PureRunSessionService>(ui, "_run");
            RunSessionResult setup = CompleteSetup(service);
            Invoke(ui, "RouteCommittedSetup", setup);
            for (int frame = 0; frame < 240 && !ui.ActWorldReady; frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(ui.ActWorldReady).IsTrue();
            await body(ui, ui.ActWorld!, setup, store);
        }
        finally
        {
            ui.QueueFree();
            for (int frame = 0; frame < 4 && GodotObject.IsInstanceValid(ui); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static RunSessionResult CompleteSetup(PureRunSessionService service)
    {
        service.ChooseParty(["pure_run_amazon", "pure_run_demonbound", "pure_run_mage"]);
        service.ChooseStartingSkill("pure_run_amazon", new ContentId("skill.amazon.thrust.lv1"));
        RunSessionResult result = service.ChooseStartingSkill("pure_run_mage", new ContentId("skill.mage.fireball.lv1"));
        if (!result.Succeeded || result.Snapshot?.ActiveRun is null)
            throw new InvalidOperationException($"Setup fixture failed: {result.ErrorCode}");
        return result;
    }

    private static PureRunMapDefinition LoadMap() => RequiredResourceLoader.Load<PureRunMapResource>(
        "res://content/map/PureRunDefaultMap.tres", "resident exploration production map").ToCoreDefinition();

    private static T Field<T>(object target, string name) => (T)typeof(GodotPlayableRunMain)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Invoke(object target, string name, object argument) => typeof(GodotPlayableRunMain)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, [argument]);

    private static void InvokePrivate(object target, string name, params object[] arguments) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);

    private sealed class MemoryStore : IRunSaveStore
    {
        private PureRunSaveSnapshot? _snapshot;
        public void RestoreFixture(PureRunSaveSnapshot snapshot) => _snapshot = snapshot;
        public RunStoreResult Load() => new(true, null, _snapshot);
        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
        {
            if ((_snapshot?.Revision ?? 0) != expectedRevision)
                return new(false, "save.revision_conflict", _snapshot);
            _snapshot = snapshot;
            return new(true, null, snapshot);
        }
    }
}
