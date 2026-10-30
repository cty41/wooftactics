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
    public async Task ResolvedBattleNodeShowsTheSameSpritesOnTheFormalMap() => await WithRun(async (ui, host, setup, _) =>
    {
        InvokePrivate(ui, "ShowResolvedAdventureNode", setup.Snapshot!.ActiveRun!);
        AssertResidentPage(ui, host, RunAdventureTransitionService.CurrentNodeId(setup.Snapshot!.ActiveRun!));
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

        GodotAdventureBoardView refreshed = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
        AssertThat(ReferenceEquals(refreshed, first)).IsFalse();
        AssertThat(refreshed.Definition.Actors.Single(value => value.ActorId == state.LeaderId).Cell).IsEqual(destination);
        AssertThat(refreshed.ActorCells[state.LeaderId]).IsEqual(destination);
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
