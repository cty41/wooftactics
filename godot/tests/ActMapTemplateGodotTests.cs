using GdUnit4;
using Godot;
using Tactics.Core.Board;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Editor;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public sealed class ActMapTemplateGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void ExplorationTemplatesBindActualSuccessorsAndHaveDistinctTypedContent()
    {
        PureRunMapDefinition map = LoadMap();
        TileSet tiles = LoadTiles();
        foreach (string nodeId in new[] { "start", "layer_04_rest", "layer_04_store", "layer_04_treasure" })
        {
            using AdventureMapTemplateResource template = AdventureMapAssetFactory.CreateNodeTemplate(map, nodeId, tiles);
            AdventureMapTemplateDefinition core = template.ToCoreDefinition();
            AssertThat(core.ContentId.Value).IsEqual($"adventure-map-template.pure-run.node.{nodeId.Replace('_', '-')}");
            AssertThat(core.Board.ContentId.Value).IsEqual($"adventure-board.pure-run.node.{nodeId.Replace('_', '-')}");
            AssertThat(core.Exits.Select(exit => exit.TargetNodeId).ToArray()).ContainsExactly(
                map.Connections.Where(edge => edge.FromNodeId == nodeId).Select(edge => edge.ToNodeId)
                    .OrderBy(id => map.Nodes.Single(value => value.NodeId == id).Lane).ThenBy(id => id, StringComparer.Ordinal).ToArray());
            AssertThat(core.Exits.Any(exit => exit.TargetNodeId == "next")).IsFalse();
            AssertThat(core.Board.Objects.Count(value => value.Kind == AdventureObjectKind.Exit)).IsEqual(core.Exits.Count);
            AssertThat(core.PartyEntrySlots.Count).IsEqual(3);
            AssertThat(template.IsTerminal).IsFalse();
            AssertThat(core.StateLayerIds.ToArray()).Contains(AdventureMapStateLayers.Resolved);
            AssertThat(core.Board.Objects.Any(value => value.Kind == (nodeId == "layer_04_store"
                ? AdventureObjectKind.Merchant : nodeId == "layer_04_treasure" ? AdventureObjectKind.Chest : AdventureObjectKind.Campfire))).IsTrue();
        }
        using AdventureMapTemplateResource rest = AdventureMapAssetFactory.CreateNodeTemplate(map, "layer_04_rest", tiles);
        using AdventureMapTemplateResource store = AdventureMapAssetFactory.CreateNodeTemplate(map, "layer_04_store", tiles);
        AssertThat(rest.BlockedCellsValue == store.BlockedCellsValue).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FirstBattleCopiesActualEncounterLayoutWithoutInventingSlots()
    {
        var encounter = ResourceLoader.Load<EncounterDefinitionResource>("res://content/ai_encounters/EncounterPureRunN1.tres");
        var layout = ResourceLoader.Load<BattleLayoutResource>("res://content/ai_encounters/BattleLayoutPureRunOpen.tres");
        using AdventureMapTemplateResource template = AdventureMapAssetFactory.CreateNodeTemplate(LoadMap(), "layer_01_battle", LoadTiles(), encounter, layout);
        AdventureMapTemplateDefinition core = template.ToCoreDefinition();
        AssertThat(core.PlayerBattleSlots.Select(value => value.Cell).ToArray()).ContainsExactly(layout.ToCoreDefinition().PartySpawns.ToArray());
        AssertThat(core.EnemyBattleSlots.Select(value => value.Cell).ToArray()).ContainsExactly(layout.ToCoreDefinition().EnemySpawns.ToArray());
        AssertThat(core.Board.BlockedCells.ToArray()).ContainsExactly(layout.ToCoreDefinition().BlockedCells.ToArray());
        AssertThat(core.Exits.Single().TargetNodeId).IsEqual("layer_02_battle");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FinalBossHasNoInventedNextActExit()
    {
        var encounter = ResourceLoader.Load<EncounterDefinitionResource>("res://content/full_run/encounter_pure_run_special.tres");
        var layout = ResourceLoader.Load<BattleLayoutResource>("res://content/full_run/BattleLayoutPureRunSpecialOpen.tres");
        using AdventureMapTemplateResource template = AdventureMapAssetFactory.CreateNodeTemplate(LoadMap(), "layer_07_battle", LoadTiles(), encounter, layout);
        AdventureMapTemplateDefinition core = template.ToCoreDefinition();
        AssertThat(template.IsTerminal).IsTrue();
        AssertThat(core.Exits.Count).IsEqual(0);
        AssertThat(core.ConnectionAnchors.Count).IsEqual(0);
        AssertThat(core.Board.Objects.Any(value => value.Kind == AdventureObjectKind.Exit)).IsFalse();
        AssertThat(core.EnemyBattleSlots.Select(value => value.Cell).ToArray()).ContainsExactly(layout.ToCoreDefinition().EnemySpawns.ToArray());
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BattleRequiresMatchingExplicitSourcesAndPreservesShallowWater()
    {
        PureRunMapDefinition map = LoadMap();
        TileSet tiles = LoadTiles();
        AssertThrown(() => AdventureMapAssetFactory.CreateNodeTemplate(map, "layer_01_battle", tiles)).IsInstanceOf<ArgumentException>();
        var n1 = ResourceLoader.Load<EncounterDefinitionResource>("res://content/ai_encounters/EncounterPureRunN1.tres");
        var n2 = ResourceLoader.Load<EncounterDefinitionResource>("res://content/ai_encounters/EncounterPureRunN2.tres");
        var open = ResourceLoader.Load<BattleLayoutResource>("res://content/ai_encounters/BattleLayoutPureRunOpen.tres");
        var water = ResourceLoader.Load<BattleLayoutResource>("res://content/ai_encounters/BattleLayoutPureRunN2ShallowWater.tres");
        AssertThrown(() => AdventureMapAssetFactory.CreateNodeTemplate(map, "layer_02_battle", tiles, n1, open)).IsInstanceOf<ArgumentException>();
        using AdventureMapTemplateResource wet = AdventureMapAssetFactory.CreateNodeTemplate(map, "layer_02_battle", tiles, n2, water);
        AssertThat(wet.ShallowWaterCells.ToArray()).ContainsExactly(water.ToCoreDefinition().ShallowWater.ToArray());
        AssertThat(wet.ToCoreDefinition().Board.BlockedCells.ToArray()).ContainsExactly(water.ToCoreDefinition().BlockedCells.ToArray());
        AssertThrown(() => AdventureMapAssetFactory.CreateNodeTemplate(map, "layer_04_event", tiles)).IsInstanceOf<NotSupportedException>();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FactoryIsDeterministicAndDoesNotMutateCallerResources()
    {
        PureRunMapDefinition map = LoadMap();
        TileSet tiles = LoadTiles();
        var layout = ResourceLoader.Load<BattleLayoutResource>("res://content/ai_encounters/BattleLayoutPureRunOpen.tres");
        var encounter = ResourceLoader.Load<EncounterDefinitionResource>("res://content/ai_encounters/EncounterPureRunN1.tres");
        string playerSpawns = layout.PartySpawnsValue;
        string enemies = layout.EnemySpawnsValue;
        using AdventureMapTemplateResource first = AdventureMapAssetFactory.CreateNodeTemplate(map, "layer_01_battle", tiles, encounter, layout);
        using AdventureMapTemplateResource second = AdventureMapAssetFactory.CreateNodeTemplate(map, "layer_01_battle", tiles, encounter, layout);
        AssertThat(AdventureMapAssetFactory.SemanticFingerprint(first)).IsEqual(AdventureMapAssetFactory.SemanticFingerprint(second));
        AssertThat(first.ResourcePath).IsEqual(string.Empty);
        AssertThat(layout.PartySpawnsValue).IsEqual(playerSpawns);
        AssertThat(layout.EnemySpawnsValue).IsEqual(enemies);
        AssertThat(ReferenceEquals(first.TileSet, tiles)).IsTrue();
        using AdventureMapTemplateResource legacyCamp = AdventureMapAssetFactory.CreateStartCampTemplate();
        AssertThat(legacyCamp.ContentIdValue).IsEqual(AdventureMapAssetFactory.StartCampContentId);
        AssertThat(legacyCamp.ExitsValue).IsEqual("exit-main@9,5@next@entry-main");
        AssertThat(legacyCamp.IsTerminal).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ExplorationSlotsSkipIsolatedWalkablePocketsWithoutChangingLayout()
    {
        var encounter = ResourceLoader.Load<EncounterDefinitionResource>("res://content/ai_encounters/EncounterPureRunN1.tres");
        var canonical = ResourceLoader.Load<BattleLayoutResource>("res://content/ai_encounters/BattleLayoutPureRunOpen.tres");
        using var layout = new BattleLayoutResource
        {
            SchemaVersion = 2,
            ContentIdValue = canonical.ContentIdValue,
            PartySpawnsValue = canonical.PartySpawnsValue,
            EnemySpawnsValue = canonical.EnemySpawnsValue,
            BlockedCellsValue = "0,1;1,0;2,1;1,2"
        };
        using AdventureMapTemplateResource template = AdventureMapAssetFactory.CreateNodeTemplate(
            LoadMap(), "layer_01_battle", LoadTiles(), encounter, layout);
        AdventureMapTemplateDefinition core = template.ToCoreDefinition();
        AssertThat(template.BlockedCellsValue).IsEqual(layout.BlockedCellsValue);
        AssertThat(core.Board.IsWalkable(new GridPoint(1, 1))).IsTrue();
        AssertThat(AdventureBoardPathfinder.FindPath(core.Board, core.Board.EntryCell, new GridPoint(1, 1)).Count).IsEqual(0);
        foreach (AdventureMapSlot slot in core.PartyEntrySlots.Concat(core.CandidateSlots))
        {
            AssertThat(slot.Cell == new GridPoint(1, 1)).IsFalse();
            AssertThat(AdventureBoardPathfinder.FindPath(core.Board, core.Board.EntryCell, slot.Cell).Count).IsGreater(0);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EveryResolvedDemoNodeBuildsAFormalMapWithRealSources()
    {
        PureRunMapDefinition map = LoadMap();
        var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres");
        using AdventureMapTemplateResource tileSource = AdventureMapAssetFactory.CreateStartCampTemplate();
        foreach (int seed in new[] { 1, 37, 99 })
        {
            PureRunActBlueprint blueprint = PureRunActBlueprint.Resolve(map, seed);
            var templates = AdventureMapAssetFactory.CreateActTemplates(map, blueprint.Bindings.Values, tileSource.TileSet!, catalog);
            var host = new GodotActWorldHost();
            try
            {
                host.BeginPreparation($"template-test-{seed}", blueprint.ActDefinition);
                foreach (PureRunNodeContentBinding binding in blueprint.Bindings.Values)
                    host.PrepareNode(binding.NodeId, templates[binding.TemplateId]);
                host.CompletePreparation();
                AssertThat(host.PreparedCount).IsEqual(map.Nodes.Count);
                AssertThat(host.IsPrepared).IsTrue();
                AssertThat(host.ActiveNodeId is null).IsTrue();
                foreach (PureRunNodeContentBinding binding in blueprint.Bindings.Values.Where(value =>
                             value.EventContext != RunAdventureEventContextKind.None))
                    AssertThat(templates[binding.TemplateId].ToCoreDefinition().Board.Objects.Count(value =>
                        value.Kind != AdventureObjectKind.Exit)).IsEqual(1);
            }
            finally { host.Free(); }
        }
    }

    /// <summary>
    /// Every generated node map keeps its exits on the x = 9 rim, symmetric about the entry row y = 5 (a single exit
    /// sits on it) and in the successors' lane order.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedExitsAreCentredOnTheRimInLaneOrder()
    {
        PureRunMapDefinition map = LoadMap();
        AssertThat(AdventureMapAssetFactory.CenteredExitRows(1)).ContainsExactly(new[] { 5 });
        AssertThat(AdventureMapAssetFactory.CenteredExitRows(2)).ContainsExactly(new[] { 4, 6 });
        AssertThat(AdventureMapAssetFactory.CenteredExitRows(5)).ContainsExactly(new[] { 1, 3, 5, 7, 9 });
        AssertThat(AdventureMapAssetFactory.CenteredExitRows(8)).ContainsExactly(new[] { 2, 3, 4, 5, 6, 7, 8, 9 });
        string[] files = DirAccess.GetFilesAt("res://content/adventure_maps/nodes")
            .Where(file => file.EndsWith(".tres", StringComparison.Ordinal)).ToArray();
        AssertThat(files.Length).IsEqual(22);
        foreach (string file in files)
        {
            AdventureMapTemplateDefinition core = ResourceLoader.Load<AdventureMapTemplateResource>(
                "res://content/adventure_maps/nodes/" + file).ToCoreDefinition();
            if (core.Exits.Count == 0) continue;
            AssertThat(core.Entries.Single().Cell).IsEqual(new GridPoint(0, 5));
            AssertThat(core.Exits.All(exit => exit.Cell.X == 9)).IsTrue();
            int[] rows = core.Exits.Select(exit => exit.Cell.Y).ToArray();
            AssertThat(rows).ContainsExactly(AdventureMapAssetFactory.CenteredExitRows(rows.Length));
            AssertThat(rows.Sum()).IsEqual(5 * rows.Length);
            float[] lanes = core.Exits.Select(exit => map.Nodes.Single(node => node.NodeId == exit.TargetNodeId).Lane).ToArray();
            AssertThat(lanes.SequenceEqual(lanes.Order())).IsTrue();
        }
    }

    private static PureRunMapDefinition LoadMap() => ResourceLoader.Load<PureRunMapResource>("res://content/map/PureRunDefaultMap.tres").ToCoreDefinition();
    private static TileSet LoadTiles() => ResourceLoader.Load<TileSet>(AdventureMapAssetFactory.TileSetPath);
}
