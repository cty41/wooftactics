using GdUnit4;
using Godot;
using System.Reflection;
using Tactics.Application.Battle;
using Tactics.Application.Runs;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

/// <summary>
/// Pending-event presentation evidence only. These tests do not claim the exploration trigger,
/// settlement sequence or post-battle remnant is migrated to resident ownership.
/// </summary>
[TestSuite]
public sealed class ResidentEventBattleProductionGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task ColdCursedChestPendingUsesItsActualResidentTemplate() =>
        await VerifyColdPending("layer_04_event", RunAdventureEventContextKind.CursedChestMimic);

    [TestCase]
    [RequireGodotRuntime]
    public async Task ColdFallenAltarPendingUsesItsActualResidentTemplate() =>
        await VerifyColdPending("layer_06_event", RunAdventureEventContextKind.FallenAltarGuardian);

    [TestCase]
    [RequireGodotRuntime]
    public async Task ColdLostVillagerPendingUsesItsActualResidentTemplateAndProtectedNpc() =>
        await VerifyColdPending("layer_06_event", RunAdventureEventContextKind.LostVillagerEscort);

    [TestCase]
    [RequireGodotRuntime]
    public async Task WarmResidentExplorationTriggersBattleOnTheSameSurfaceAndFencesTheOldView()
    {
        const int seed = 37;
        var store = new MemoryStore();
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, seed, "resident-event-exploration"));
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(ui);
        try
        {
            PureRunSessionService service = Field<PureRunSessionService>(ui, "_run");
            RunSessionResult setup = CompleteSetup(service);
            Invoke(ui, "RouteCommittedSetup", setup);
            await WaitForBattle(ui, tree);
            GodotActWorldHost host = ui.ActWorld!;
            Dictionary<string, TemplateSnapshot> baseline = CaptureTemplates(host);
            PureRunSaveSnapshot fixture = EventReady(setup.Snapshot!, LoadMap(), "layer_04_event",
                RunAdventureEventContextKind.CursedChestMimic);
            store.RestoreFixture(fixture);
            int beforeTrigger = store.SaveCount;

            InvokePrivate(ui, "ShowAdventureEventEntry", fixture.ActiveRun!, "CursedChestMimic");
            GodotAdventureBoardView exploration = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
            GodotAdventureMapInstance formalMap = host.Nodes["layer_04_event"];
            ulong surfaceId = formalMap.Surface.GetInstanceId();
            AdventureMapTemplateDefinition formal = formalMap.Surface.Template!.ToCoreDefinition();
            AssertThat(exploration.UsesResidentSurface).IsTrue();
            AssertThat(exploration.ResidentSurface!.GetInstanceId()).IsEqual(surfaceId);
            AssertThat(exploration.Definition.BlockedCells.SequenceEqual(formal.Board.BlockedCells)).IsTrue();
            AssertThat(exploration.Definition.Objects.SequenceEqual(formal.Board.Objects)).IsTrue();
            AssertThat(exploration.Definition.Actors.Select(value => value.Cell)
                .OrderBy(value => value.Y).ThenBy(value => value.X)
                .SequenceEqual(formal.PartyEntrySlots.Select(value => value.Cell)
                    .OrderBy(value => value.Y).ThenBy(value => value.X))).IsTrue();
            TileMapLayer ownedTiles = InstanceField<TileMapLayer>(exploration, "_tiles");
            AssertThat(ownedTiles.Visible).IsFalse();
            AssertThat(ownedTiles.GetUsedCells().Count).IsEqual(0);
            AssertTemplatesUnchanged(host, baseline);
            exploration.MoveActor(fixture.ActiveRun!.AdventureState!.LeaderId, new GridPoint(4, 5));

            var click = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
                Position = exploration.CellCenter(new GridPoint(5, 5)),
                GlobalPosition = exploration.GetGlobalTransformWithCanvas() * exploration.CellCenter(new GridPoint(5, 5))
            };
            InvokePrivate(exploration, "OnGuiInput", click);
            GodotIsometricBattleBoard? board = Field<GodotIsometricBattleBoard?>(ui, "_board");
            if (board is null)
                throw new InvalidOperationException($"Event click did not start battle: {Field<Label>(ui, "_status")?.Text}");
            AssertThat(board.UsesResidentSurface).IsTrue();
            AssertThat(board.ResidentSurface!.GetInstanceId()).IsEqual(surfaceId);
            AssertThat(host.ActiveNodeId).IsEqual("layer_04_event");
            AssertThat(store.SaveCount).IsEqual(beforeTrigger + 1);

            // NewPage queued the exploration view, but its stale lease must fence any delayed input
            // before _ExitTree executes and must not invalidate the new battle lease afterward.
            InvokePrivate(exploration, "OnGuiInput", click);
            AssertThat(store.SaveCount).IsEqual(beforeTrigger + 1);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(host.ActiveNodeId).IsEqual("layer_04_event");
            AssertThat(board.ResidentSurface!.GetInstanceId()).IsEqual(surfaceId);
            AssertTemplatesUnchanged(host, baseline);
        }
        finally
        {
            ui.QueueFree();
            for (int frame = 0; frame < 4 && GodotObject.IsInstanceValid(ui); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ResolvedEventSceneReturnsToTheSameFormalSurface()
    {
        const int seed = 37;
        var store = new MemoryStore();
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, seed, "resident-event-resolved"));
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(ui);
        try
        {
            PureRunSessionService service = Field<PureRunSessionService>(ui, "_run");
            RunSessionResult setup = CompleteSetup(service);
            Invoke(ui, "RouteCommittedSetup", setup);
            await WaitForBattle(ui, tree);
            GodotActWorldHost host = ui.ActWorld!;
            GodotAdventureMapInstance map = host.Nodes["layer_04_event"];
            ulong surfaceId = map.Surface.GetInstanceId();
            PureRunState pending = EventPending(setup.Snapshot!, LoadMap(), "layer_04_event",
                RunAdventureEventContextKind.CursedChestMimic).ActiveRun!;
            PureRunState resolved = new RunAdventureTransitionService().ResolveEventBattle(pending);
            InvokePrivate(ui, "ShowPostEventScene", resolved, "CursedChestMimic");
            GodotAdventureBoardView view = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
            AssertThat(view.UsesResidentSurface).IsTrue();
            AssertThat(view.ResidentSurface!.GetInstanceId()).IsEqual(surfaceId);
            AssertThat(view.Definition.Objects.Single(value => value.Kind != AdventureObjectKind.Exit).Cell)
                .IsEqual(new GridPoint(5, 5));
            AssertThat(map.Surface.GetInstanceId()).IsEqual(surfaceId);
            AssertThat(host.ActiveNodeId).IsEqual("layer_04_event");
        }
        finally
        {
            ui.QueueFree();
            for (int frame = 0; frame < 4 && GodotObject.IsInstanceValid(ui); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ColdTravelingEscortPreparesLostExplorationAndBattleOnTheSameSurface()
    {
        const int seed = 37;
        var store = new MemoryStore();
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, seed, "resident-lost-exploration"));
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(ui);
        try
        {
            PureRunSessionService service = Field<PureRunSessionService>(ui, "_run");
            RunSessionResult setup = CompleteSetup(service);
            Invoke(ui, "RouteCommittedSetup", setup);
            await WaitForBattle(ui, tree);
            InvokePrivate(ui, "ReleaseActWorld");
            PureRunSaveSnapshot fixture = EventReady(setup.Snapshot!, LoadMap(), "layer_06_event",
                RunAdventureEventContextKind.LostVillagerEscort);
            store.RestoreFixture(fixture);
            RunSessionResult resumed = service.ResumeRun();
            AssertThat(resumed.Succeeded).IsTrue();
            Invoke(ui, "RouteRunState", resumed.Snapshot!);
            for (int frame = 0; frame < 180; frame++)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                GodotAdventureBoardView? candidate = Field<GodotAdventureBoardView?>(ui, "_adventureBoard");
                if (ui.ActWorldReady && candidate?.UsesResidentSurface == true) break;
            }
            GodotActWorldHost host = ui.ActWorld!;
            GodotAdventureBoardView exploration = Field<GodotAdventureBoardView>(ui, "_adventureBoard");
            GodotAdventureMapInstance map = host.Nodes["layer_06_event"];
            AdventureMapTemplateDefinition formal = map.Surface.Template!.ToCoreDefinition();
            AssertThat(formal.Board.Objects.Single(value => value.Kind != AdventureObjectKind.Exit).ObjectId)
                .IsEqual("lost-villager");
            AssertThat(exploration.UsesResidentSurface).IsTrue();
            ulong surfaceId = map.Surface.GetInstanceId();
            AssertThat(exploration.ResidentSurface!.GetInstanceId()).IsEqual(surfaceId);
            exploration.MoveActor(fixture.ActiveRun!.AdventureState!.LeaderId, new GridPoint(4, 5));
            int before = store.SaveCount;
            var click = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left, Pressed = true,
                Position = exploration.CellCenter(new GridPoint(5, 5))
            };
            InvokePrivate(exploration, "OnGuiInput", click);
            GodotIsometricBattleBoard board = Field<GodotIsometricBattleBoard>(ui, "_board");
            AssertThat(board.ResidentSurface!.GetInstanceId()).IsEqual(surfaceId);
            PlayableBattleSessionService battle = Field<PlayableBattleSessionService>(ui, "_battle");
            AssertThat(battle.CaptureSnapshot().Units.Any(unit => unit.UnitId.Value == "escort-lost-villager")).IsTrue();
            AssertThat(store.SaveCount).IsEqual(before + 1);
        }
        finally
        {
            ui.QueueFree();
            for (int frame = 0; frame < 4 && GodotObject.IsInstanceValid(ui); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task WarmBaselineBorrowsKnownEventsButKeepsMismatchedEscortOnLegacyBoard()
    {
        const int seed = 37;
        var store = new MemoryStore();
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, seed, "resident-event-warm"));
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(ui);
        try
        {
            PureRunSessionService service = Field<PureRunSessionService>(ui, "_run");
            RunSessionResult setup = CompleteSetup(service);
            Invoke(ui, "RouteCommittedSetup", setup);
            await WaitForBattle(ui, tree);
            GodotActWorldHost host = ui.ActWorld!;
            ulong hostId = host.GetInstanceId();
            Dictionary<string, TemplateSnapshot> baseline = CaptureTemplates(host);
            PureRunMapDefinition map = LoadMap();

            foreach ((string node, RunAdventureEventContextKind context, bool resident) in new[]
            {
                ("layer_04_event", RunAdventureEventContextKind.CursedChestMimic, true),
                ("layer_06_event", RunAdventureEventContextKind.FallenAltarGuardian, true),
                ("layer_06_event", RunAdventureEventContextKind.LostVillagerEscort, false)
            })
            {
                PureRunSaveSnapshot fixture = EventPending(setup.Snapshot!, map, node, context);
                store.RestoreFixture(fixture);
                RunSessionResult resumed = service.ResumeRun();
                if (!resumed.Succeeded || resumed.EncounterRequest is null)
                    throw new InvalidOperationException($"Warm event resume failed: {resumed.ErrorCode}");
                Invoke(ui, "StartBattle", resumed.EncounterRequest);
                await WaitForBattle(ui, tree);
                AssertThat(ui.ActWorld!.GetInstanceId()).IsEqual(hostId);
                GodotIsometricBattleBoard board = Field<GodotIsometricBattleBoard>(ui, "_board");
                AssertThat(board.UsesResidentSurface).IsEqual(resident);
                if (resident)
                {
                    AssertThat(board.ResidentSurface!.GetInstanceId()).IsEqual(host.Nodes[node].Surface.GetInstanceId());
                    AssertThat(host.ActiveNodeId).IsEqual(node);
                }
                else
                {
                    AssertThat(board.ResidentSurface).IsNull();
                    // A saved actual variant may differ from an already prepared baseline. The V11
                    // transition keeps the old board rather than reloading or mutating the Act host.
                    AssertThat(host.Visible).IsFalse();
                }
                AssertTemplatesUnchanged(host, baseline);
            }
        }
        finally
        {
            ui.QueueFree();
            for (int frame = 0; frame < 4 && GodotObject.IsInstanceValid(ui); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static async Task VerifyColdPending(string nodeId, RunAdventureEventContextKind context)
    {
        PureRunMapDefinition map = LoadMap();
        int seed = Enumerable.Range(0, 512)
            .First(value => PureRunActBlueprint.Resolve(map, value).Bindings[nodeId].EventContext != context);
        var store = new MemoryStore();
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, seed, $"resident-event-{nodeId}-{context}"));
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(ui);
        try
        {
            PureRunSessionService service = Field<PureRunSessionService>(ui, "_run");
            RunSessionResult setup = CompleteSetup(service);
            PureRunSaveSnapshot fixture = EventPending(setup.Snapshot!, map, nodeId, context);
            store.RestoreFixture(fixture);
            PureRunPendingSceneRecoveryResult proof = PureRunPendingSceneRecovery.Recover(fixture, map);
            if (!proof.Succeeded) throw new InvalidOperationException($"Event fixture invalid: {proof.ErrorCode}");
            RunNodeSceneIdentity identity = proof.Snapshot.ActiveRun!.Checkpoint!.SceneIdentity!;
            int saves = store.SaveCount;

            Invoke(ui, "RouteRunState", fixture);
            await WaitForBattle(ui, tree);
            GodotActWorldHost host = ui.ActWorld!;
            GodotIsometricBattleBoard board = Field<GodotIsometricBattleBoard>(ui, "_board");
            AssertThat(board.UsesResidentSurface).IsTrue();
            AssertThat(board.ResidentSurface!.GetInstanceId()).IsEqual(host.Nodes[nodeId].Surface.GetInstanceId());
            AssertThat(host.ActiveNodeId).IsEqual(nodeId);
            AdventureMapTemplateDefinition template = host.Nodes[nodeId].Surface.Template!.ToCoreDefinition();
            AssertThat(template.ContentId).IsEqual(identity.TemplateId);
            AssertFormalEventTemplate(ui, template, context);
            AssertGridProjection(board);
            AssertThat(store.SaveCount).IsEqual(saves);
            AssertThat(ReferenceEquals(store.Load().Snapshot, fixture)).IsTrue();

            if (context == RunAdventureEventContextKind.LostVillagerEscort)
            {
                PlayableBattleSessionService battle = Field<PlayableBattleSessionService>(ui, "_battle");
                AssertThat(battle.CaptureSnapshot().Units.Any(unit => unit.UnitId.Value == "escort-lost-villager")).IsTrue();
            }
        }
        finally
        {
            ui.QueueFree();
            for (int frame = 0; frame < 4 && GodotObject.IsInstanceValid(ui); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void AssertFormalEventTemplate(GodotPlayableRunMain ui, AdventureMapTemplateDefinition template,
        RunAdventureEventContextKind context)
    {
        (string id, AdventureObjectKind kind) = context switch
        {
            RunAdventureEventContextKind.CursedChestMimic => ("cursed-chest", AdventureObjectKind.Chest),
            RunAdventureEventContextKind.FallenAltarGuardian => ("fallen-altar", AdventureObjectKind.Altar),
            RunAdventureEventContextKind.LostVillagerEscort => ("lost-villager", AdventureObjectKind.Npc),
            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };
        AdventureBoardObject obj = template.Board.Objects.Single(value => value.ObjectId == id);
        AssertThat(obj.Kind).IsEqual(kind);
        AssertThat(obj.Cell).IsEqual(new GridPoint(5, 5));
        var encounters = Field<Dictionary<ContentId, EncounterDefinition>>(ui, "_encounters");
        var layouts = Field<Dictionary<ContentId, BattleLayoutDefinition>>(ui, "_layouts");
        BattleLayoutDefinition layout = layouts[encounters[new ContentId("encounter.pure-run.n1")].LayoutId];
        AssertThat(template.Board.BlockedCells.SequenceEqual(layout.BlockedCells)).IsTrue();
        AssertThat((template.ShallowWaterCells ?? Array.Empty<GridPoint>()).SequenceEqual(layout.ShallowWater)).IsTrue();
        AssertThat(template.PlayerBattleSlots.Select(value => value.Cell).SequenceEqual(layout.PartySpawns)).IsTrue();
        AssertThat(template.EnemyBattleSlots.Select(value => value.Cell).SequenceEqual(layout.EnemySpawns)).IsTrue();
    }

    private static void AssertGridProjection(GodotIsometricBattleBoard board)
    {
        for (int y = 0; y < IsometricBattleBoardLayout.GridSize; y++)
        for (int x = 0; x < IsometricBattleBoardLayout.GridSize; x++)
        {
            GridPoint cell = new(x, y);
            Vector2 center = board.CellCenter(cell);
            AssertThat(center.IsEqualApprox(IsometricBattleBoardLayout.GridToScreen(cell))).IsTrue();
            AssertThat(board.TryPointToCell(center, out GridPoint picked)).IsTrue();
            AssertThat(picked).IsEqual(cell);
        }
    }

    private static PureRunSaveSnapshot EventReady(PureRunSaveSnapshot setup, PureRunMapDefinition map,
        string nodeId, RunAdventureEventContextKind context)
    {
        PureRunState source = setup.ActiveRun!;
        var mapService = new PureRunMapService(map);
        PureRunMapState mapState = mapService.UnlockLayerFour(source.Seed);
        if (nodeId.StartsWith("layer_06", StringComparison.Ordinal))
            mapState = mapService.UnlockLayerSix(mapState, source.Seed);
        PureRunMapResult selected = mapService.BeginNode(mapState, nodeId);
        if (!selected.Succeeded || selected.Transaction is null)
            throw new InvalidOperationException($"Cannot select ready event node: {selected.RejectionCode}");
        if (context is not (RunAdventureEventContextKind.CursedChestMimic or RunAdventureEventContextKind.LostVillagerEscort))
            throw new ArgumentOutOfRangeException(nameof(context));
        ContentId boardId = new($"adventure-board.pure-run.event.{context.ToString().ToLowerInvariant()}");
        GridPoint[] cells = [new(4, 5), new(3, 4), new(3, 6)];
        RunAdventureState adventure = source.AdventureState! with
        {
            Lifecycle = RunAdventureLifecycle.MapActive,
            BoardContentId = boardId,
            LeaderId = source.Party[0].CharacterId,
            ActorCells = source.Party.Select((member, index) => new RunAdventureActorCell(member.CharacterId, cells[index])).ToArray(),
            PendingEventContext = RunAdventureEventContextKind.None,
            PendingEventNodeId = null,
            PendingEventObjectId = null,
            SceneIdentity = null
        };
        bool layerSix = nodeId.StartsWith("layer_06", StringComparison.Ordinal);
        RunEscortState? escort = context == RunAdventureEventContextKind.LostVillagerEscort
            ? new RunEscortState("escort-test", RunEscortLifecycle.Traveling, true, "layer_04_event", nodeId, 7)
            : null;
        var ready = new PureRunState(source.RunId, source.Seed, source.Revision,
            layerSix ? PureRunPhase.ResolvingLayerSixNode : PureRunPhase.ResolvingLayerFourNode,
            source.EncounterIndex, source.EncounterContentId, source.Party, source.BackpackConsumables,
            source.BackpackEquipment, source.PendingProgression, source.AppliedTransactionKeys, source.Gold,
            source.BattlesCompleted, source.EnemiesDefeated, source.AcquiredItems, null, selected.State,
            selected.Transaction, escort, adventure);
        return setup with { ActiveRun = ready };
    }

    private static PureRunSaveSnapshot EventPending(PureRunSaveSnapshot setup, PureRunMapDefinition map,
        string nodeId, RunAdventureEventContextKind context)
    {
        PureRunState source = setup.ActiveRun!;
        var mapService = new PureRunMapService(map);
        PureRunMapState mapState = mapService.UnlockLayerFour(source.Seed);
        if (nodeId.StartsWith("layer_06", StringComparison.Ordinal))
            mapState = mapService.UnlockLayerSix(mapState, source.Seed);
        PureRunMapResult selected = mapService.BeginNode(mapState, nodeId);
        if (!selected.Succeeded || selected.Transaction is null)
            throw new InvalidOperationException($"Cannot select event node: {selected.RejectionCode}");
        int encounterIndex = nodeId.StartsWith("layer_04", StringComparison.Ordinal) ? 3 : 5;
        string objectId = context switch
        {
            RunAdventureEventContextKind.CursedChestMimic => "cursed-chest",
            RunAdventureEventContextKind.FallenAltarGuardian => "fallen-altar",
            RunAdventureEventContextKind.LostVillagerEscort => "lost-villager",
            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };
        ContentId encounter = new("encounter.pure-run.n1");
        RunAdventureState adventure = source.AdventureState! with
        {
            Lifecycle = RunAdventureLifecycle.MapActive,
            BoardContentId = new ContentId($"adventure-board.pure-run.event.{context.ToString().ToLowerInvariant()}"),
            PendingEventContext = context,
            PendingEventNodeId = nodeId,
            PendingEventObjectId = objectId,
            SceneIdentity = null,
            ActorCells = Array.Empty<RunAdventureActorCell>()
        };
        RunEncounterCheckpoint checkpoint = source.Checkpoint! with
        {
            EncounterContentId = encounter,
            EncounterIndex = encounterIndex,
            SceneIdentity = null
        };
        RunEscortState? escort = context == RunAdventureEventContextKind.LostVillagerEscort
            ? new RunEscortState("escort-test", RunEscortLifecycle.BattlePending, true, "layer_04_event", nodeId, 7)
            : null;
        var pending = new PureRunState(source.RunId, source.Seed, source.Revision, PureRunPhase.PendingBattle,
            encounterIndex - 1, encounter, source.Party, source.BackpackConsumables, source.BackpackEquipment,
            source.PendingProgression, source.AppliedTransactionKeys, source.Gold, source.BattlesCompleted,
            source.EnemiesDefeated, source.AcquiredItems, checkpoint,
            selected.State with { NodeLifecycle = RunNodeLifecycle.Pending },
            selected.Transaction, escort, adventure);
        return setup with { ActiveRun = pending };
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
        "res://content/map/PureRunDefaultMap.tres", "resident event production map").ToCoreDefinition();

    private sealed record TemplateSnapshot(ulong MapId, ulong SurfaceId, ulong TerrainId, ContentId TemplateId,
        Transform2D MapTransform, Transform2D SurfaceTransform, Transform2D TerrainTransform, Vector2I[] UsedCells);

    private static Dictionary<string, TemplateSnapshot> CaptureTemplates(GodotActWorldHost host) =>
        host.Nodes.ToDictionary(pair => pair.Key, pair => new TemplateSnapshot(pair.Value.GetInstanceId(),
            pair.Value.Surface.GetInstanceId(), pair.Value.Surface.TerrainLayer.GetInstanceId(),
            pair.Value.Surface.Template!.ToCoreDefinition().ContentId, pair.Value.Transform, pair.Value.Surface.Transform,
            pair.Value.Surface.TerrainLayer.Transform, pair.Value.Surface.TerrainLayer.GetUsedCells()
                .OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray()), StringComparer.Ordinal);

    private static void AssertTemplatesUnchanged(GodotActWorldHost host, Dictionary<string, TemplateSnapshot> expected)
    {
        Dictionary<string, TemplateSnapshot> actual = CaptureTemplates(host);
        AssertThat(actual.Count).IsEqual(expected.Count);
        foreach (var pair in expected)
        {
            TemplateSnapshot before = pair.Value, after = actual[pair.Key];
            AssertThat(after.MapId).IsEqual(before.MapId);
            AssertThat(after.SurfaceId).IsEqual(before.SurfaceId);
            AssertThat(after.TerrainId).IsEqual(before.TerrainId);
            AssertThat(after.TemplateId).IsEqual(before.TemplateId);
            AssertThat(after.MapTransform).IsEqual(before.MapTransform);
            AssertThat(after.SurfaceTransform).IsEqual(before.SurfaceTransform);
            AssertThat(after.TerrainTransform).IsEqual(before.TerrainTransform);
            AssertThat(after.UsedCells.SequenceEqual(before.UsedCells)).IsTrue();
        }
    }

    private static async Task WaitForBattle(GodotPlayableRunMain ui, SceneTree tree)
    {
        for (int frame = 0; frame < 180; frame++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (ui.ActWorldReady && ui.CaptureTestProbe().BattleActive) return;
            string? page = ui.CaptureTestProbe().PageTitle;
            if (ui.ActWorldReady && page is not null &&
                (page.Contains("TILE ADVENTURE", StringComparison.Ordinal) ||
                 page.Contains("TILE BATTLE", StringComparison.Ordinal)))
                InvokePrivate(ui, "BeginReadyEncounter");
        }
        string? status = Field<Label>(ui, "_status")?.Text;
        throw new InvalidOperationException($"Resident event battle not ready: page={ui.CaptureTestProbe().PageTitle}, status={status}");
    }

    private static T Field<T>(object target, string name) => (T)typeof(GodotPlayableRunMain)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Invoke(object target, string name, object argument) => typeof(GodotPlayableRunMain)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, [argument]);

    private static T InstanceField<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void InvokePrivate(object target, string name, params object[] arguments) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);

    private sealed class MemoryStore : IRunSaveStore
    {
        private PureRunSaveSnapshot? _snapshot;
        public int SaveCount { get; private set; }
        public void RestoreFixture(PureRunSaveSnapshot snapshot) => _snapshot = snapshot;
        public RunStoreResult Load() => new(true, null, _snapshot);
        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
        {
            if ((_snapshot?.Revision ?? 0) != expectedRevision)
                return new(false, "save.revision_conflict", _snapshot);
            _snapshot = snapshot;
            SaveCount++;
            return new(true, null, snapshot);
        }
    }
}
