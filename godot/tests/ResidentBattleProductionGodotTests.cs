using GdUnit4;
using Godot;
using System.Reflection;
using Tactics.Application.Runs;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

/// <summary>
/// Production presentation fixtures, not full Run journeys. Setup supplies the real party; late battles
/// explicitly restore a supported legacy checkpoint without inventing choice transactions or victories.
/// Event scenes, permanent Actors/VFX and settlement/remnant ownership are deliberately not claimed here.
/// </summary>
[TestSuite]
public sealed class ResidentBattleProductionGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task OrdinaryFirstBattleUsesItsResidentMapAcrossColdAndWarmRoutes() =>
        await VerifyProductionBattle("layer_01_battle", 0);

    [TestCase]
    [RequireGodotRuntime]
    public async Task EliteBattleUsesItsResidentMapAcrossColdAndWarmRoutes() =>
        await VerifyProductionBattle("layer_05_battle", 4);

    [TestCase]
    [RequireGodotRuntime]
    public async Task BossBattleUsesItsResidentMapAcrossColdAndWarmRoutes() =>
        await VerifyProductionBattle("layer_07_battle", 6);

    [TestCase]
    [RequireGodotRuntime]
    public async Task MismatchedRuntimeLayoutCannotReplaceAnActiveResidentBattle() =>
        await VerifyProductionBattle("layer_01_battle", 0, rejectChangedLayout: true);

    private static async Task VerifyProductionBattle(string nodeId, int encounterIndex, bool rejectChangedLayout = false)
    {
        const int seed = 37;
        var store = new MemoryStore();
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, seed, $"resident-production-{nodeId}"));
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(ui);
        // Exercise a genuine transformed production root, not only a board with identity ancestors.
        // Actor/VFX coordinates remain board-local; the resident terrain camera must match that basis.
        if (encounterIndex == 4)
        {
            ui.Position = new Vector2(37, -19);
            ui.Scale = new Vector2(.85f, 1.15f);
        }
        try
        {
            PureRunSessionService service = Field<PureRunSessionService>(ui, "_run");
            service.ChooseParty(["pure_run_amazon", "pure_run_demonbound", "pure_run_mage"]);
            service.ChooseStartingSkill("pure_run_amazon", new ContentId("skill.amazon.thrust.lv1"));
            RunSessionResult setup = service.ChooseStartingSkill("pure_run_mage", new ContentId("skill.mage.fireball.lv1"));
            if (!setup.Succeeded || setup.Snapshot?.ActiveRun is null)
                throw new InvalidOperationException($"Setup fixture failed: {setup.ErrorCode}");
            RunSessionResult pending = service.BeginEncounter();
            if (!pending.Succeeded || pending.Snapshot?.ActiveRun?.Checkpoint is null)
                throw new InvalidOperationException($"Pending fixture failed: {pending.ErrorCode}");

            var map = RequiredResourceLoader.Load<PureRunMapResource>(
                "res://content/map/PureRunDefaultMap.tres", "resident production map").ToCoreDefinition();
            PureRunActBlueprint blueprint = PureRunActBlueprint.Resolve(map, seed);
            PureRunSaveSnapshot fixture = encounterIndex == 0 ? pending.Snapshot :
                LatePresentationCheckpoint(pending.Snapshot, blueprint.Bindings[nodeId].EncounterId!.Value, encounterIndex);
            store.RestoreFixture(fixture);
            PureRunPendingSceneRecoveryResult recovered = PureRunPendingSceneRecovery.Recover(fixture, map);
            if (!recovered.Succeeded)
                throw new InvalidOperationException($"Pending fixture is not recoverable: {nodeId}:{recovered.ErrorCode}");
            AssertThat(recovered.Snapshot.ActiveRun!.Checkpoint!.SceneIdentity!.NodeId).IsEqual(nodeId);
            int saves = store.SaveCount;

            // The real route performs cold Act preparation, including its production Prepared callback.
            Invoke(ui, "RouteRunState", fixture);
            await WaitForBattle(ui, tree);
            GodotActWorldHost host = ui.ActWorld!;
            ulong hostId = host.GetInstanceId();
            AssertThat(host.Nodes.Count).IsEqual(16);
            AssertResidentBinding(ui, host, nodeId);
            Dictionary<string, TerrainSnapshot> terrain = CaptureTerrain(host);
            ulong firstBoardId = Field<GodotIsometricBattleBoard>(ui, "_board").GetInstanceId();

            // Application Resume is the request authority, including zero-write legacy party repair.
            RunSessionResult resumed = service.ResumeRun();
            if (!resumed.Succeeded || resumed.EncounterRequest is null)
                throw new InvalidOperationException($"Resume fixture failed: {resumed.ErrorCode}");
            Invoke(ui, "StartBattle", resumed.EncounterRequest);
            await WaitForBattle(ui, tree);
            AssertThat(ui.ActWorld!.GetInstanceId()).IsEqual(hostId);
            AssertThat(Field<GodotIsometricBattleBoard>(ui, "_board").GetInstanceId() != firstBoardId).IsTrue();
            AssertResidentBinding(ui, host, nodeId);
            AssertTerrainUnchanged(host, terrain);

            ulong secondBoardId = Field<GodotIsometricBattleBoard>(ui, "_board").GetInstanceId();
            Invoke(ui, "RouteRunState", service.ResumeRun().Snapshot!);
            await WaitForBattle(ui, tree);
            AssertThat(ui.ActWorld!.GetInstanceId()).IsEqual(hostId);
            AssertThat(Field<GodotIsometricBattleBoard>(ui, "_board").GetInstanceId() != secondBoardId).IsTrue();
            AssertResidentBinding(ui, host, nodeId);
            AssertTerrainUnchanged(host, terrain);
            AssertThat(store.SaveCount).IsEqual(saves);
            AssertThat(ReferenceEquals(store.Load().Snapshot, fixture)).IsTrue();
            if (encounterIndex == 4)
            {
                AssertThat(ui.Position.IsEqualApprox(new Vector2(37, -19))).IsTrue();
                AssertThat(ui.Scale.IsEqualApprox(new Vector2(.85f, 1.15f))).IsTrue();
            }
            if (rejectChangedLayout)
                AssertChangedLayoutRejected(ui, service.ResumeRun().EncounterRequest!, store, host, terrain);
        }
        finally
        {
            ui.QueueFree();
            for (int frame = 0; frame < 4 && GodotObject.IsInstanceValid(ui); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void AssertResidentBinding(GodotPlayableRunMain ui, GodotActWorldHost host, string nodeId)
    {
        GodotIsometricBattleBoard board = Field<GodotIsometricBattleBoard>(ui, "_board");
        AssertThat(board.UsesResidentSurface).IsTrue();
        AssertThat(ReferenceEquals(board.ResidentSurface, host.Nodes[nodeId].Surface)).IsTrue();
        AssertThat(host.ActiveNodeId).IsEqual(nodeId);
        foreach (var pair in host.Nodes)
            AssertThat(pair.Value.Mode).IsEqual(pair.Key == nodeId
                ? AdventureMapInstanceMode.Active : AdventureMapInstanceMode.Preview);
        for (int y = 0; y < IsometricBattleBoardLayout.GridSize; y++)
        for (int x = 0; x < IsometricBattleBoardLayout.GridSize; x++)
        {
            GridPoint cell = new(x, y);
            Vector2 center = board.CellCenter(cell);
            AssertThat(center.IsEqualApprox(IsometricBattleBoardLayout.GridToScreen(cell))).IsTrue();
            Vector2 viewportCenter = board.GetGlobalTransformWithCanvas() * center;
            Vector2 terrainCenter = board.ResidentSurface!.GetGlobalTransformWithCanvas() * board.ResidentSurface.CellCenter(cell);
            AssertThat(viewportCenter.IsEqualApprox(terrainCenter)).IsTrue();
            AssertThat(board.TryPointToCell(center, out GridPoint picked)).IsTrue();
            AssertThat(picked).IsEqual(cell);
        }
    }

    private static void AssertChangedLayoutRejected(GodotPlayableRunMain ui, EncounterRequest request,
        MemoryStore store, GodotActWorldHost host, Dictionary<string, TerrainSnapshot> terrain)
    {
        var layouts = Field<Dictionary<ContentId, BattleLayoutDefinition>>(ui, "_layouts");
        var encounters = Field<Dictionary<ContentId, EncounterDefinition>>(ui, "_encounters");
        ContentId layoutId = encounters[request.EncounterContentId].LayoutId;
        BattleLayoutDefinition original = layouts[layoutId];
        GridPoint extra = Enumerable.Range(0, 10).SelectMany(y => Enumerable.Range(0, 10).Select(x => new GridPoint(x, y)))
            .First(cell => !original.BlockedCells.Contains(cell) && !original.ShallowWater.Contains(cell) &&
                !original.PartySpawns.Contains(cell) && !original.EnemySpawns.Contains(cell));
        object battle = Field<object>(ui, "_battle");
        Control page = Field<Control>(ui, "_page");
        string? active = host.ActiveNodeId;
        long generation = host.Generation;
        int saves = store.SaveCount;
        try
        {
            foreach (BattleLayoutDefinition changed in new[]
            {
                original with { BlockedCells = original.BlockedCells.Concat([extra]).ToArray() },
                original with { ShallowWaterCells = original.ShallowWater.Concat([extra]).ToArray() },
                original with { PartySpawns = original.PartySpawns.Reverse().ToArray() },
                original with { EnemySpawns = original.EnemySpawns.Reverse().ToArray() }
            })
            {
                // Change only the in-memory runtime layout, retaining its identity and all resource files.
                // A valid existing battle avoids cold-loader timing obscuring the rejection's before-state.
                layouts[layoutId] = changed;
                Invoke(ui, "StartBattle", request);
                AssertThat(Field<Label>(ui, "_status").Text).IsEqual("battle.surface_layout_mismatch");
                AssertThat(ReferenceEquals(Field<object>(ui, "_battle"), battle)).IsTrue();
                AssertThat(ReferenceEquals(Field<Control>(ui, "_page"), page)).IsTrue();
                AssertThat(host.ActiveNodeId == active).IsTrue();
                AssertThat(host.Generation).IsEqual(generation);
                AssertThat(store.SaveCount).IsEqual(saves);
                AssertTerrainUnchanged(host, terrain);
            }
        }
        finally { layouts[layoutId] = original; }
    }

    private sealed record TerrainSnapshot(ulong MapId, ulong SurfaceId, ulong TerrainId,
        Transform2D MapTransform, Transform2D SurfaceTransform, Transform2D TerrainTransform,
        Vector2I[] UsedCells, Vector2I[] AtlasCells);

    private static Dictionary<string, TerrainSnapshot> CaptureTerrain(GodotActWorldHost host) =>
        host.Nodes.ToDictionary(pair => pair.Key, pair =>
        {
            GodotAdventureMapInstance map = pair.Value;
            var surface = map.Surface;
            var terrain = surface.TerrainLayer;
            Vector2I[] cells = terrain.GetUsedCells().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
            return new TerrainSnapshot(map.GetInstanceId(), surface.GetInstanceId(), terrain.GetInstanceId(),
                map.Transform, surface.Transform, terrain.Transform, cells,
                cells.Select(cell => terrain.GetCellAtlasCoords(cell)).ToArray());
        }, StringComparer.Ordinal);

    private static void AssertTerrainUnchanged(GodotActWorldHost host, Dictionary<string, TerrainSnapshot> expected)
    {
        Dictionary<string, TerrainSnapshot> actual = CaptureTerrain(host);
        AssertThat(actual.Count).IsEqual(expected.Count);
        foreach (var pair in expected)
        {
            TerrainSnapshot after = actual[pair.Key], before = pair.Value;
            AssertThat(after.MapId).IsEqual(before.MapId);
            AssertThat(after.SurfaceId).IsEqual(before.SurfaceId);
            AssertThat(after.TerrainId).IsEqual(before.TerrainId);
            AssertThat(after.MapTransform).IsEqual(before.MapTransform);
            AssertThat(after.SurfaceTransform).IsEqual(before.SurfaceTransform);
            AssertThat(after.TerrainTransform).IsEqual(before.TerrainTransform);
            AssertThat(after.UsedCells.SequenceEqual(before.UsedCells)).IsTrue();
            AssertThat(after.AtlasCells.SequenceEqual(before.AtlasCells)).IsTrue();
        }
    }

    private static PureRunSaveSnapshot LatePresentationCheckpoint(PureRunSaveSnapshot source,
        ContentId encounterId, int encounterIndex)
    {
        PureRunState run = source.ActiveRun!;
        // Legacy direct FullRun APIs can retain Initial with an already committed late checkpoint.
        // No Map/transaction is invented here: the fixed checkpoint index and canonical content identify the node.
        RunAdventureState adventure = run.AdventureState! with
        {
            Lifecycle = RunAdventureLifecycle.InitialExploration,
            BoardContentId = RunAdventureTransitionService.InitialBoardContentId,
            SceneIdentity = null,
            ActorCells = Array.Empty<RunAdventureActorCell>()
        };
        var checkpoint = run.Checkpoint! with
        { EncounterContentId = encounterId, EncounterIndex = encounterIndex, SceneIdentity = null };
        var pending = new PureRunState(run.RunId, run.Seed, run.Revision, PureRunPhase.PendingBattle,
            encounterIndex, encounterId, run.Party, run.BackpackConsumables, run.BackpackEquipment,
            run.PendingProgression, run.AppliedTransactionKeys, run.Gold, run.BattlesCompleted,
            run.EnemiesDefeated, run.AcquiredItems, checkpoint, null, null, null, adventure);
        return source with { ActiveRun = pending };
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
                typeof(GodotPlayableRunMain).GetMethod("BeginReadyEncounter",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ui, null);
        }
        string? status = Field<Label>(ui, "_status")?.Text;
        throw new InvalidOperationException($"Resident production battle not ready: page={ui.CaptureTestProbe().PageTitle}, status={status}");
    }

    private static T Field<T>(object target, string name) => (T)typeof(GodotPlayableRunMain)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Invoke(object target, string name, object argument) => typeof(GodotPlayableRunMain)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, [argument]);

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
