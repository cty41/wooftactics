#if TOOLS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public sealed record AdventureMapAssetBuildResult(
    IReadOnlyList<string> ResourcePaths,
    IReadOnlyDictionary<string, string> ResourceUids,
    IReadOnlyDictionary<string, string> SemanticFingerprints,
    int CatalogCount);

public static class AdventureMapAssetFactory
{
    public const string BatchId = "roguelike-map-templates-v1";
    public const string StartCampContentId = "adventure-map-template.pure-run.start-camp-v1";
    public const string BasicBattleContentId = "adventure-map-template.pure-run.basic-battle-v1";
    public const string StartCampPath = "res://content/adventure_maps/StartCampTemplateV1.tres";
    public const string BasicBattlePath = "res://content/adventure_maps/BasicBattleTemplateV1.tres";
    public const string TileSetPath = "res://content/adventure_maps/AdventureMapTileSetV1.tres";
    public const string TileSetUid = "uid://dsp2ybrc0vxay";
    public const string StartCampUid = "uid://b3m7aevx135rv";
    public const string BasicBattleUid = "uid://cmy865eeymqss";
    public const string CatalogUid = "uid://d0pd8f783qad2";
    public const string CatalogPath = "res://content/adventure_maps/ContentCatalog.tres";
    private const string GlobalCatalogPath = "res://content/ContentCatalog.tres";

    public static AdventureMapAssetBuildResult Build()
    {
        string project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repo = Directory.GetParent(project)!.FullName;
        string ledgerPath = Path.Combine(repo, "Tools", "migration", "manifest", "state", BatchId + ".json");
        string? ledgerBefore = FileHashOrNull(ledgerPath);
        string sourceCommit = ReadSourceCommit(repo);
        Dictionary<string, PriorArtifact> prior = ReadPriorLedger(ledgerPath);
        foreach ((string path, PriorArtifact artifact) in prior) RegisterStableUid(path, artifact.Uid);

        GodotResourceCatalog global = ResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Canonical ContentCatalog is missing.");
        RegisterCatalogUids(global);
        global.Validate();
        GodotResourceEntry mapEntry = global.Entries.Single(entry => entry.ContentIdValue == "run-map.pure-run.layer4-v1");
        PureRunMapDefinition map = (ResourceLoader.Load<PureRunMapResource>(mapEntry.ResourceLocator)
            ?? throw new InvalidOperationException("Canonical Demo map is missing.")).ToCoreDefinition();
        IReadOnlyList<PureRunNodeContentBinding> bindings = PureRunActBlueprint.EnumerateContentVariants(map);
        if (bindings.Count != 22) throw new InvalidOperationException("act.variant_count_mismatch");

        // Build the complete batch before touching any authored target. All new maps use formal sources.
        TileSet tileSet = CreateTileSet();
        var resources = new Dictionary<string, Resource>(StringComparer.Ordinal)
        {
            [TileSetPath] = tileSet,
            [StartCampPath] = CreateStartCampTemplate(tileSet),
            [BasicBattlePath] = CreateBasicBattleTemplate(tileSet)
        };
        var entries = new List<GodotResourceEntry> { Entry(StartCampContentId, StartCampPath), Entry(BasicBattleContentId, BasicBattlePath) };
        foreach ((ContentId id, AdventureMapTemplateResource template) in CreateActTemplates(map, bindings, tileSet, global))
        {
            const string prefix = "adventure-map-template.pure-run.node.";
            if (!id.Value.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException($"act.template_namespace:{id}");
            string path = "res://content/adventure_maps/nodes/" + id.Value[prefix.Length..] + ".tres";
            resources.Add(path, template);
            entries.Add(Entry(id.Value, path));
        }
        var local = new GodotResourceCatalog { Entries = entries.OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal).ToArray() };
        resources.Add(CatalogPath, local);
        local.Validate();
        string[] legacyIds = [StartCampContentId, BasicBattleContentId];
        if (global.Entries.Any(entry => legacyIds.Contains(entry.ContentIdValue, StringComparer.Ordinal)))
            resources.Add(GlobalCatalogPath, new GodotResourceCatalog { Entries = global.Entries
                .Where(entry => !legacyIds.Contains(entry.ContentIdValue, StringComparer.Ordinal)).Select(Copy)
                .OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal).ToArray() });
        ValidateCompleteBatch(map, bindings, resources);

        string[] paths = resources.Keys.Order(StringComparer.Ordinal).ToArray();
        var semantics = paths.ToDictionary(path => path, path => ArtifactSemantic(resources[path]), StringComparer.Ordinal);
        var uids = paths.ToDictionary(path => path, path => ResourceUid.IdToText(Uid(path)), StringComparer.Ordinal);
        var before = paths.ToDictionary(path => path, path => FileHashOrNull(ProjectSettings.GlobalizePath(path)), StringComparer.Ordinal);
        foreach (string path in paths) GuardTarget(repo, path, before[path], prior);
        // The catalog is the source authority; snapshot all resource inputs, not only its locator strings.
        var sources = global.Entries.Select(entry => entry.DiagnosticPathValue).Append(GlobalCatalogPath)
            .Distinct(StringComparer.Ordinal).ToDictionary(path => path,
                path => FileHashOrNull(ProjectSettings.GlobalizePath(path)) ?? throw new InvalidOperationException($"act.source_missing:{path}"),
                StringComparer.Ordinal);
        foreach (string source in new[]
        {
            "repo://godot/src/Tactics.Godot.Adapter/Editor/AdventureMapAssetFactory.cs",
            "repo://godot/src/Tactics.Godot.Adapter/Editor/AdventureMapAssetBuilder.cs",
            "repo://src/Tactics.Core/Runs/PureRunActBlueprint.cs",
            "repo://src/Tactics.Core/Runs/AdventureMapContracts.cs"
        }) sources.Add(source, FileHashOrNull(SourcePath(repo, source))
            ?? throw new InvalidOperationException($"act.generator_source_missing:{source}"));

        string staging = "res://content/adventure_maps/act_staging/" + Guid.NewGuid().ToString("N");
        EnsureDirectory(staging);
        string stageAbsolute = ProjectSettings.GlobalizePath(staging);
        var staged = new Dictionary<string, string>(StringComparer.Ordinal);
        var published = new List<(string Path, byte[]? Previous, string WrittenHash)>();
        try
        {
            // ResourceSaver writes external TileSet references to the final path, never to staging.
            tileSet.TakeOverPath(TileSetPath);
            foreach (string path in paths.OrderBy(path => path == TileSetPath ? 0 : 1).ThenBy(path => path, StringComparer.Ordinal))
            {
                string candidate = staging + "/" + paths.ToList().IndexOf(path).ToString("D3") + ".tres";
                long expectedUid = ResourceUid.TextToId(uids[path]);
                DeterministicResourceSaver.Save(resources[path], candidate, expectedUid);
                // SetUid success and ResourceLoader's UID cache are not disk-persistence proofs.
                // A local 4.7.1 probe reproduced both stale positive and negative cache values.
                RequirePersistedUid(candidate, expectedUid);
                RegisterStableUid(path, uids[path]);
                Resource reloaded = ResourceLoader.Load(candidate, string.Empty, ResourceLoader.CacheMode.Ignore)
                    ?? throw new InvalidOperationException($"act.staging_reload_failed:{path}");
                if (ArtifactSemantic(reloaded) != semantics[path]) throw new InvalidOperationException($"act.staging_semantic_mismatch:{path}");
                staged.Add(path, ProjectSettings.GlobalizePath(candidate));
            }
            foreach ((string path, string hash) in sources)
                RequireUnchanged(SourcePath(repo, path), hash);
            foreach (string path in paths) RequireUnchanged(ProjectSettings.GlobalizePath(path), before[path]);
            RequireUnchanged(ledgerPath, ledgerBefore);

            foreach (string path in paths.OrderBy(path => resources[path] is GodotResourceCatalog ? 1 : 0)
                         .ThenBy(path => path, StringComparer.Ordinal))
            {
                string target = ProjectSettings.GlobalizePath(path);
                RequireUnchanged(target, before[path]);
                // Preserve bytes only when both semantics and the persisted resource UID are correct.
                // A hash-guarded artifact from the old user:// staging bug may require UID repair.
                if (before[path] is not null && prior.TryGetValue(path, out PriorArtifact? old) && old.Semantic == semantics[path] &&
                    ReadPersistedUid(path) == ResourceUid.TextToId(uids[path])) continue;
                byte[]? previous = File.Exists(target) ? File.ReadAllBytes(target) : null;
                byte[] candidate = File.ReadAllBytes(staged[path]);
                string writtenHash = Hash(candidate);
                if (before[path] == writtenHash) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                AtomicWrite(target, candidate);
                published.Add((target, previous, writtenHash));
            }
            foreach (string path in paths) RequirePersistedUid(path, ResourceUid.TextToId(uids[path]));
            var hashes = paths.ToDictionary(path => path, path => FileHashOrNull(ProjectSettings.GlobalizePath(path))!, StringComparer.Ordinal);
            byte[] ledger = CreateLedger(paths, uids, semantics, hashes, sources, sourceCommit, local.Entries.Length);
            RequireUnchanged(ledgerPath, ledgerBefore);
            if (ledgerBefore != Hash(ledger)) AtomicWrite(ledgerPath, ledger);
            return new AdventureMapAssetBuildResult(paths, uids, semantics, local.Entries.Length);
        }
        catch (Exception failure)
        {
            // Restore only our own writes; still attempt other restores if an external edit blocks one.
            var rollbackErrors = new List<Exception>();
            foreach (var item in published.AsEnumerable().Reverse())
            {
                try
                {
                    RequireUnchanged(item.Path, item.WrittenHash);
                    if (item.Previous is null) File.Delete(item.Path); else AtomicWrite(item.Path, item.Previous);
                }
                catch (Exception rollbackError) { rollbackErrors.Add(rollbackError); }
            }
            if (rollbackErrors.Count != 0)
                throw new AggregateException("act.rollback_incomplete", new[] { failure }.Concat(rollbackErrors));
            throw;
        }
        finally
        {
            foreach ((string path, string uid) in uids) RegisterStableUid(path, uid);
            if (Directory.Exists(stageAbsolute)) Directory.Delete(stageAbsolute, true);
        }
    }

    public static AdventureMapTemplateResource CreateStartCampTemplate() => CreateStartCampTemplate(CreateTileSet());
    public static AdventureMapTemplateResource CreateBasicBattleTemplate() => CreateBasicBattleTemplate(CreateTileSet());

    /// <summary>
    /// Builds an unsaved node template. Battle inputs must be resolved by the caller from the
    /// canonical catalog; this factory never guesses resource paths or creates a fallback board.
    /// </summary>
    public static AdventureMapTemplateResource CreateNodeTemplate(PureRunMapDefinition map, string nodeId,
        TileSet tileSet, EncounterDefinitionResource? encounter = null, BattleLayoutResource? layout = null) =>
        CreateNodeTemplateCore(map, nodeId, tileSet, encounter, layout, null);

    public static AdventureMapTemplateResource CreateNodeTemplate(PureRunMapDefinition map,
        PureRunNodeContentBinding binding, TileSet tileSet, EncounterDefinitionResource? encounter = null,
        BattleLayoutResource? layout = null) =>
        CreateNodeTemplateCore(map, binding.NodeId, tileSet, encounter, layout, binding);

    private static AdventureMapTemplateResource CreateNodeTemplateCore(PureRunMapDefinition map, string nodeId,
        TileSet tileSet, EncounterDefinitionResource? encounter, BattleLayoutResource? layout,
        PureRunNodeContentBinding? binding)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tileSet);
        PureRunMapNodeDefinition node = map.Nodes.SingleOrDefault(value => value.NodeId == nodeId)
            ?? throw new ArgumentException($"Unknown node '{nodeId}'.", nameof(nodeId));
        string key = nodeId.Replace('_', '-');
        if (map.Nodes.Count(value => value.NodeId.Replace('_', '-') == key) != 1)
            throw new ArgumentException($"Node '{nodeId}' has an ambiguous resource identity.", nameof(map));
        ContentId templateId = binding?.TemplateId ?? new ContentId($"adventure-map-template.pure-run.node.{key}");
        var boardId = new ContentId($"adventure-board.pure-run.node.{key}");
        PureRunMapConnectionDefinition[] successors = map.Connections.Where(edge => edge.FromNodeId == nodeId)
            .OrderBy(edge => edge.ToNodeId, StringComparer.Ordinal).ToArray();
        bool terminal = successors.Length == 0 && node.Kind == PureRunNodeKind.Boss;
        if (successors.Length == 0 && !terminal)
            throw new NotSupportedException($"Non-Boss node '{nodeId}' has no successor; terminal intent must be explicit.");
        if (successors.Length > 8)
            throw new NotSupportedException($"Node '{nodeId}' exceeds the authored eight exit anchors.");

        bool camp = nodeId == "start" && node.Layer == 0;
        bool battle = !camp && node.Kind is PureRunNodeKind.Battle or PureRunNodeKind.Elite or PureRunNodeKind.Boss;
        bool mystery = node.Kind == PureRunNodeKind.Mystery;
        RunAdventureEventContextKind eventContext = binding?.EventContext ?? RunAdventureEventContextKind.None;
        if (mystery && eventContext is not (RunAdventureEventContextKind.CursedChestMimic or
            RunAdventureEventContextKind.FallenAltarGuardian or RunAdventureEventContextKind.LostVillagerEscort))
            throw new NotSupportedException($"Node '{nodeId}' requires an explicitly resolved event template and encounter mapping.");
        if (!mystery && eventContext != RunAdventureEventContextKind.None)
            throw new ArgumentException($"Node '{nodeId}' cannot host an unrelated event context.");
        if (!camp && !battle && !mystery && node.Kind is not (PureRunNodeKind.Rest or PureRunNodeKind.Store or PureRunNodeKind.Treasure))
            throw new NotSupportedException($"No authored template for node '{nodeId}' ({node.Kind}).");

        GridPoint[] playerCells;
        GridPoint[] enemyCells;
        GridPoint[] blocked;
        GridPoint[] shallowWater = [];
        if (battle || mystery)
        {
            if (encounter is null || layout is null)
                throw new ArgumentException($"Battle node '{nodeId}' requires its resolved Encounter and BattleLayout resources.");
            if (encounter.SchemaVersion != 1 || layout.SchemaVersion is not (1 or 2) ||
                encounter.ContentIdValue != (binding?.EncounterId ?? node.ContentId).Value || encounter.LayoutContentId != layout.ContentIdValue)
                throw new ArgumentException($"Battle node '{nodeId}' resource identity or schema mismatch.");
            var definition = layout.ToCoreDefinition();
            shallowWater = definition.ShallowWater.ToArray();
            playerCells = definition.PartySpawns.ToArray();
            enemyCells = definition.EnemySpawns.ToArray();
            blocked = definition.BlockedCells.ToArray();
            GridPoint[] layoutCells = playerCells.Concat(enemyCells).Concat(blocked).ToArray();
            if (playerCells.Length < 3 || enemyCells.Length < encounter.MonsterUnitContentIds.Length ||
                encounter.MonsterUnitContentIds.Length == 0 ||
                encounter.MonsterUnitContentIds.Length != encounter.MonsterAiContentIds.Length ||
                layoutCells.Any(cell => cell.X is < 0 or >= 10 || cell.Y is < 0 or >= 10) ||
                layoutCells.Distinct().Count() != layoutCells.Length)
                throw new ArgumentException($"Battle node '{nodeId}' has invalid slots, occupancy, or encounter capacity.");
        }
        else
        {
            if (encounter is not null || layout is not null)
                throw new ArgumentException($"Exploration node '{nodeId}' cannot silently adopt a battle encounter.");
            // Exploration-only schema reserve slots do not authorize combat; event nodes above
            // require their explicitly resolved encounter and layout instead.
            playerCells = [new(3, 4), new(3, 5), new(3, 6)];
            enemyCells = [new(7, 3), new(8, 4), new(8, 6)];
            blocked = camp ? [new(5, 5)] : node.Kind switch
            {
                PureRunNodeKind.Rest => [new(5, 5), new(4, 3), new(6, 3)],
                PureRunNodeKind.Store => [new(5, 5), new(4, 4), new(4, 6)],
                PureRunNodeKind.Treasure => [new(5, 5), new(6, 4), new(6, 6)],
                _ => throw new NotSupportedException($"No terrain for '{nodeId}'.")
            };
        }

        GridPoint entry = new(0, 5);
        GridPoint[] exitCells = Enumerable.Range(1, 8).Select(y => new GridPoint(9, y)).Take(successors.Length).ToArray();
        HashSet<GridPoint> occupied = blocked.Concat(playerCells).Concat(enemyCells).ToHashSet();
        if (occupied.Contains(entry) || exitCells.Any(occupied.Contains))
            throw new NotSupportedException($"Node '{nodeId}' layout conflicts with authored entry/exit anchors.");
        occupied.Add(entry);
        foreach (GridPoint cell in exitCells) occupied.Add(cell);
        // Layouts may contain isolated walkable pockets. Exploration slots must be reachable
        // from the authored entry; never repair an invalid placement by changing battle terrain.
        var explorationBoard = new AdventureBoardDefinition(boardId, 10, 10, blocked,
            Array.Empty<AdventureBoardObject>(), Array.Empty<AdventureActorPlacement>(), entry,
            terminal ? entry : exitCells[0]);
        GridPoint[] free = Enumerable.Range(1, 8).SelectMany(x => Enumerable.Range(1, 8).Select(y => new GridPoint(x, y)))
            .Where(cell => !occupied.Contains(cell) &&
                AdventureBoardPathfinder.FindPath(explorationBoard, entry, cell).Count > 0).ToArray();
        int candidateCount = camp ? 6 : 1;
        if (free.Length < candidateCount + 3)
            throw new ArgumentException($"Node '{nodeId}' has insufficient distinct exploration slots.");
        GridPoint[] party = free.Take(3).ToArray();
        GridPoint[] candidates = free.Skip(3).Take(candidateCount).ToArray();
        AdventureObjectKind objectKind = mystery ? eventContext switch
        {
            RunAdventureEventContextKind.FallenAltarGuardian => AdventureObjectKind.Altar,
            RunAdventureEventContextKind.LostVillagerEscort => AdventureObjectKind.Npc,
            _ => AdventureObjectKind.Chest
        } : camp || node.Kind == PureRunNodeKind.Rest ? AdventureObjectKind.Campfire
            : node.Kind == PureRunNodeKind.Store ? AdventureObjectKind.Merchant : AdventureObjectKind.Chest;
        string objectId = mystery ? eventContext switch
        {
            RunAdventureEventContextKind.FallenAltarGuardian => "fallen-altar",
            RunAdventureEventContextKind.LostVillagerEscort => "lost-villager",
            _ => "cursed-chest"
        } : camp ? "campfire" : node.Kind.ToString().ToLowerInvariant();
        string objects = battle ? string.Empty : $"{objectId}@{objectKind}@5,5@True@False@@False";
        string exits = string.Join(';', successors.Select((edge, index) => $"exit:{edge.ToNodeId}@{Cell(exitCells[index])}@{edge.ToNodeId}@entry-main"));
        string exitObjects = string.Join(';', successors.Select((edge, index) => $"exit:{edge.ToNodeId}@Exit@{Cell(exitCells[index])}@False@False@{edge.ToNodeId}@False"));
        string terrain = string.Join(';', Enumerable.Range(0, 10).SelectMany(y => Enumerable.Range(0, 10).Select(x => $"{x},{y}")));
        var resource = new AdventureMapTemplateResource
        {
            ContentIdValue = templateId.Value, BoardContentIdValue = boardId.Value,
            Width = 10, Height = 10, TileSet = tileSet,
            BlockedCellsValue = string.Join(';', blocked.Select(Cell)),
            ShallowWaterCellsValue = string.Join(';', shallowWater.Select(Cell)),
            ObjectsValue = string.IsNullOrEmpty(objects) ? exitObjects : objects + ";" + exitObjects,
            ActorsValue = string.Empty, BoardEntryCell = new Vector2I(entry.X, entry.Y),
            IsTerminal = terminal,
            // Board.ExitCell is the legacy required coordinate, not a navigable exit object.
            BoardExitCell = terminal ? new Vector2I(entry.X, entry.Y) : new Vector2I(exitCells[0].X, exitCells[0].Y),
            CandidateSlotsValue = Slots("candidate", candidates), PartyEntrySlotsValue = Slots("party", party),
            PlayerBattleSlotsValue = Slots("player", playerCells), EnemyBattleSlotsValue = Slots("enemy", enemyCells),
            EntriesValue = "entry-main@0,5", ExitsValue = exits,
            ConnectionAnchorsValue = string.Join(';', exitCells.Select((cell, index) => $"connection-{index + 1}@{Cell(cell)}")),
            CameraFocusAnchorValue = "camera-focus@5,5", AtlasBoundsAnchorValue = "atlas-bounds@9,9",
            StateLayerIds = AdventureMapStateLayers.Required.Concat([AdventureMapStateLayers.Combat, AdventureMapStateLayers.Resolved]).ToArray(),
            TerrainCellsValue = terrain, DecorationCellsValue = string.Join(';', blocked.Select(Cell)), MaskCellsValue = terrain
        };
        try { resource.ToCoreDefinition(); return resource; }
        catch { resource.Dispose(); throw; }
    }

    private static string Cell(GridPoint cell) => $"{cell.X},{cell.Y}";
    private static string Slots(string prefix, IEnumerable<GridPoint> cells) =>
        string.Join(';', cells.Select((cell, index) => $"{prefix}-{index + 1}@{Cell(cell)}"));

    /// <summary>Resolve source resources from the canonical catalog for a selected Act or its authored variants.</summary>
    public static IReadOnlyDictionary<ContentId, AdventureMapTemplateResource> CreateActTemplates(
        PureRunMapDefinition map, IEnumerable<PureRunNodeContentBinding> bindings, TileSet tiles,
        GodotResourceCatalog catalog)
    {
        var entries = catalog.Entries.ToDictionary(entry => entry.ContentIdValue, StringComparer.Ordinal);
        var result = new Dictionary<ContentId, AdventureMapTemplateResource>();
        foreach (PureRunNodeContentBinding binding in bindings)
        {
            EncounterDefinitionResource? encounter = null;
            BattleLayoutResource? layout = null;
            if (binding.EncounterId is ContentId encounterId)
            {
                if (!entries.TryGetValue(encounterId.Value, out GodotResourceEntry? encounterEntry))
                    throw new InvalidOperationException($"act.encounter_missing:{binding.NodeId}:{encounterId}");
                Resource source = ResourceLoader.Load(encounterEntry.ResourceLocator)
                    ?? throw new InvalidOperationException($"act.encounter_unloadable:{encounterId}");
                encounter = source as EncounterDefinitionResource;
                if (encounter is null && source is PureRunLayerFourResource { KindValue: "encounter" } legacy)
                {
                    // The production Main uses this same frozen N4 payload and authored split-flank layout.
                    using JsonDocument payload = JsonDocument.Parse(legacy.PayloadJson);
                    string[] units = payload.RootElement.GetProperty("monsters").EnumerateArray()
                        .Select(value => value.GetString()!).ToArray();
                    encounter = new EncounterDefinitionResource
                    {
                        ContentIdValue = encounterId.Value,
                        LayoutContentId = "battle-layout.pure-run.split-flank",
                        MonsterUnitContentIds = units,
                        MonsterAiContentIds = units.Select(unit => "ai.pure-run." + unit["unit.pure-run.".Length..]
                            .Replace("goat-", string.Empty, StringComparison.Ordinal)).ToArray()
                    };
                }
                if (encounter is null || !entries.TryGetValue(encounter.LayoutContentId, out GodotResourceEntry? layoutEntry))
                    throw new InvalidOperationException($"act.layout_missing:{binding.NodeId}:{encounterId}");
                layout = ResourceLoader.Load<BattleLayoutResource>(layoutEntry.ResourceLocator)
                    ?? throw new InvalidOperationException($"act.layout_unloadable:{encounter.LayoutContentId}");
            }
            if (!result.TryAdd(binding.TemplateId, CreateNodeTemplate(map, binding, tiles, encounter, layout)))
                throw new InvalidOperationException($"act.template_duplicate:{binding.TemplateId}");
        }
        return result;
    }

    public static string SemanticFingerprint(AdventureMapTemplateResource value)
    {
        AdventureMapTemplateDefinition core = value.ToCoreDefinition();
        string canonical = string.Join('|', value.SchemaVersion, core.ContentId.Value, core.Board.ContentId.Value,
            core.Board.Width, core.Board.Height, value.BlockedCellsValue, value.ObjectsValue, value.ActorsValue,
            value.CandidateSlotsValue, value.PartyEntrySlotsValue, value.PlayerBattleSlotsValue,
            value.EnemyBattleSlotsValue, value.EntriesValue, value.ExitsValue, value.ConnectionAnchorsValue,
            value.CameraFocusAnchorValue, value.AtlasBoundsAnchorValue, string.Join(',', value.StateLayerIds),
            value.TerrainCellsValue, value.DecorationCellsValue, value.MaskCellsValue,
            value.TileSet!.TileSize.X, value.TileSet.TileSize.Y, value.TileSet.TileShape, value.TileSet.TileLayout);
        // Preserve all existing non-terminal generated fingerprints byte for byte.
        if (value.IsTerminal) canonical += "|terminal:true";
        if (!string.IsNullOrEmpty(value.ShallowWaterCellsValue)) canonical += "|shallow-water:" + value.ShallowWaterCellsValue;
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static AdventureMapTemplateResource CreateStartCampTemplate(TileSet tileSet) => Template(
        StartCampContentId, "adventure-board.pure-run.start-camp-v1", tileSet,
        objects: "campfire@Campfire@5,5@True@False@@False",
        candidates: "candidate-1@2,2;candidate-2@3,2;candidate-3@4,2;candidate-4@5,2;candidate-5@6,2;candidate-6@7,2",
        party: "party-1@1,4;party-2@1,5;party-3@1,6",
        players: "player-1@3,4;player-2@3,5;player-3@3,6",
        enemies: "enemy-1@7,3;enemy-2@8,4;enemy-3@8,6",
        decorations: "5,5", blocked: "5,5");

    private static AdventureMapTemplateResource CreateBasicBattleTemplate(TileSet tileSet) => Template(
        BasicBattleContentId, "adventure-board.pure-run.basic-battle-v1", tileSet,
        objects: string.Empty,
        candidates: "candidate-1@1,1",
        party: "party-1@1,4;party-2@1,5;party-3@1,6",
        players: "player-1@3,4;player-2@3,5;player-3@3,6",
        enemies: "enemy-1@7,3;enemy-2@8,4;enemy-3@8,5;enemy-4@8,6",
        decorations: "4,1;5,8", blocked: "4,1;5,8");

    private static AdventureMapTemplateResource Template(string id, string boardId, TileSet tileSet, string objects,
        string candidates, string party, string players, string enemies, string decorations, string blocked)
    {
        string terrain = string.Join(';', Enumerable.Range(0, 10).SelectMany(y => Enumerable.Range(0, 10).Select(x => $"{x},{y}")));
        var resource = new AdventureMapTemplateResource
        {
            ContentIdValue = id, BoardContentIdValue = boardId, Width = 10, Height = 10,
            BlockedCellsValue = blocked, ObjectsValue = objects, ActorsValue = string.Empty,
            BoardEntryCell = new Vector2I(0, 5), BoardExitCell = new Vector2I(9, 5),
            CandidateSlotsValue = candidates, PartyEntrySlotsValue = party,
            PlayerBattleSlotsValue = players, EnemyBattleSlotsValue = enemies,
            EntriesValue = "entry-main@0,5", ExitsValue = "exit-main@9,5@next@entry-main",
            ConnectionAnchorsValue = "connection-main@9,4", CameraFocusAnchorValue = "camera-focus@5,5",
            AtlasBoundsAnchorValue = "atlas-bounds@9,9", StateLayerIds = AdventureMapStateLayers.Required.ToArray(),
            TerrainCellsValue = terrain, DecorationCellsValue = decorations, MaskCellsValue = terrain, TileSet = tileSet
        };
        resource.ToCoreDefinition();
        return resource;
    }

    private static TileSet CreateTileSet()
    {
        Image image = Image.CreateEmpty(576, 48, false, Image.Format.Rgba8); image.Fill(Colors.Transparent);
        Color[] colors = [new("344b40"), new("1b2923"), new("8d7146"), new(0.01f, 0.015f, 0.02f, .94f), new(0.2f, .72f, .92f, .58f), new("356f8c")];
        for (int tile = 0; tile < colors.Length; tile++) PaintDiamond(image, tile * 96, colors[tile]);
        var source = new TileSetAtlasSource { Texture = ImageTexture.CreateFromImage(image), TextureRegionSize = new Vector2I(96, 48) };
        for (int tile = 0; tile < colors.Length; tile++) source.CreateTile(new Vector2I(tile, 0));
        var tileSet = new TileSet { TileSize = new Vector2I(96, 48), TileShape = TileSet.TileShapeEnum.Isometric,
            TileLayout = TileSet.TileLayoutEnum.DiamondDown };
        tileSet.AddSource(source, 0);
        return tileSet;
    }

    private static void PaintDiamond(Image image, int offset, Color color)
    {
        for (int y = 0; y < 48; y++)
        {
            int half = y < 24 ? y * 2 : (47 - y) * 2;
            for (int x = 48 - half; x <= 48 + half && x < 96; x++) image.SetPixel(offset + x, y, color);
        }
    }

    private static GodotResourceEntry Entry(string id, string path) => new()
    {
        ContentIdValue = id, ResourceTypeIdValue = "adventure-map-template",
        ResourceUidValue = ResourceUid.IdToText(Uid(path)), DiagnosticPathValue = path, SchemaVersion = 1,
        ReferenceContentIds = Array.Empty<string>()
    };
    private static GodotResourceEntry Copy(GodotResourceEntry value) => new()
    {
        ContentIdValue = value.ContentIdValue, ResourceTypeIdValue = value.ResourceTypeIdValue,
        ResourceUidValue = value.ResourceUidValue, DiagnosticPathValue = value.DiagnosticPathValue,
        SchemaVersion = value.SchemaVersion, ReferenceContentIds = value.ReferenceContentIds
    };
    private static long Uid(string path)
    {
        string text = path switch
        {
            TileSetPath => TileSetUid,
            StartCampPath => StartCampUid,
            BasicBattlePath => BasicBattleUid,
            CatalogPath => CatalogUid,
            _ => ResourceUid.PathToUid(path)
        };
        long id = text.StartsWith("uid://", StringComparison.Ordinal) ? ResourceUid.TextToId(text) : ResourceUid.CreateIdForPath(path);
        if (!ResourceUid.HasId(id)) ResourceUid.AddId(id, path);
        return id;
    }
    private static void RegisterCatalogUids(GodotResourceCatalog catalog)
    {
        foreach (GodotResourceEntry entry in catalog.Entries)
        {
            long uid = ResourceUid.TextToId(entry.ResourceUidValue);
            if (uid != ResourceUid.InvalidId && !ResourceUid.HasId(uid)) ResourceUid.AddId(uid, entry.DiagnosticPathValue);
        }
    }
    private static void EnsureDirectory(string path)
    {
        Error error = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path));
        if (error is not Error.Ok and not Error.AlreadyExists) throw new InvalidOperationException($"Cannot create '{path}': {error}.");
    }
    private sealed record PriorArtifact(string Uid, string? TargetHash, string? Semantic);

    private static Dictionary<string, PriorArtifact> ReadPriorLedger(string path)
    {
        var result = new Dictionary<string, PriorArtifact>(StringComparer.Ordinal);
        if (!File.Exists(path)) return result;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.GetProperty("batchId").GetString() != BatchId)
            throw new InvalidOperationException("act.ledger_batch_mismatch");
        foreach (JsonElement artifact in document.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            string target = artifact.GetProperty("resourcePath").GetString()!;
            string uid = artifact.GetProperty("resourceUid").GetString()!;
            string? hash = artifact.TryGetProperty("targetHash", out JsonElement value) ? value.GetString() : null;
            string? semantic = artifact.TryGetProperty("semanticFingerprint", out value) ? value.GetString() : null;
            result.Add(target, new PriorArtifact(uid, hash, semantic));
        }
        return result;
    }

    private static void GuardTarget(string repo, string path, string? currentHash, IReadOnlyDictionary<string, PriorArtifact> prior)
    {
        if (currentHash is null)
        {
            if (prior.ContainsKey(path)) throw new InvalidOperationException($"act.generated_target_missing:{path}");
            return;
        }
        if (prior.TryGetValue(path, out PriorArtifact? old))
        {
            if (old.TargetHash is not null)
            {
                if (currentHash != old.TargetHash) throw new InvalidOperationException($"act.target_modified:{path}");
                return;
            }
            // V1 ledgers did not record byte hashes. Bootstrap only from a clean tracked HEAD artifact.
            RequireCleanTrackedTarget(repo, path);
            return;
        }
        if (path == GlobalCatalogPath) { RequireCleanTrackedTarget(repo, path); return; }
        throw new InvalidOperationException($"act.unowned_existing_target:{path}");
    }

    private static void RequireCleanTrackedTarget(string repo, string path)
    {
        string relative = Path.GetRelativePath(repo, ProjectSettings.GlobalizePath(path));
        foreach (string[] arguments in new[]
        {
            new[] { "ls-files", "--error-unmatch", "--", relative },
            new[] { "diff", "--quiet", "HEAD", "--", relative }
        })
        {
            var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = repo, UseShellExecute = false };
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)
                ?? throw new InvalidOperationException("act.git_guard_unavailable");
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"act.bootstrap_target_not_clean:{path}");
        }
    }

    private static string ReadSourceCommit(string repo)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = repo, UseShellExecute = false, RedirectStandardOutput = true
        };
        start.ArgumentList.Add("rev-parse"); start.ArgumentList.Add("HEAD");
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException("act.git_source_unavailable");
        string commit = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0 || commit.Length != 40) throw new InvalidOperationException("act.source_commit_missing");
        return commit;
    }

    private static void ValidateCompleteBatch(PureRunMapDefinition map,
        IReadOnlyList<PureRunNodeContentBinding> bindings, IReadOnlyDictionary<string, Resource> resources)
    {
        _ = PureRunActBlueprint.Resolve(map, 0); // Includes the current Demo topology/strategy contract.
        var templates = resources.Values.OfType<AdventureMapTemplateResource>()
            .ToDictionary(template => template.ContentIdValue, template => template.ToCoreDefinition(), StringComparer.Ordinal);
        foreach (PureRunNodeContentBinding binding in bindings)
        {
            AdventureMapTemplateDefinition template = templates[binding.TemplateId.Value];
            PureRunMapNodeDefinition node = map.Nodes.Single(value => value.NodeId == binding.NodeId);
            if (template.IsTerminal != (node.Kind == PureRunNodeKind.Boss))
                throw new InvalidOperationException($"act.terminal_mismatch:{binding.NodeId}");
            string[] expected = map.Connections.Where(edge => edge.FromNodeId == binding.NodeId)
                .Select(edge => edge.ToNodeId).Order(StringComparer.Ordinal).ToArray();
            if (!template.Exits.Select(exit => exit.TargetNodeId).Order(StringComparer.Ordinal).SequenceEqual(expected))
                throw new InvalidOperationException($"act.exit_topology_mismatch:{binding.NodeId}");
            foreach (AdventureMapExitAnchor exit in template.Exits)
                foreach (PureRunNodeContentBinding destination in bindings.Where(value => value.NodeId == exit.TargetNodeId))
                    if (!templates[destination.TemplateId.Value].Entries.Any(entry => entry.AnchorId == exit.TargetEntryId))
                        throw new InvalidOperationException($"act.target_entry_missing:{binding.NodeId}:{exit.ExitId}");
        }
        foreach (Resource resource in resources.Values)
        {
            if (resource is GodotResourceCatalog catalog) catalog.Validate();
            if (resource is not AdventureMapTemplateResource template) continue;
            var surface = new GodotIsometricTileMapSurface();
            try
            {
                surface.Configure(template);
                for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                {
                    GridPoint cell = new(x, y);
                    if (!surface.CellCenter(cell).IsEqualApprox(IsometricGridProjection.GridToScreen(cell)) ||
                        !surface.TryPointToCell(surface.CellCenter(cell), out GridPoint actual) || actual != cell)
                        throw new InvalidOperationException($"act.surface_validation_failed:{template.ContentIdValue}:{cell}");
                }
            }
            finally { surface.Free(); }
        }
    }

    private static string ArtifactSemantic(Resource resource) => resource switch
    {
        AdventureMapTemplateResource template => SemanticFingerprint(template),
        GodotResourceCatalog catalog => Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(catalog.Entries
            .OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal).Select(entry => new
            {
                entry.ContentIdValue, entry.ResourceTypeIdValue, entry.ResourceUidValue,
                entry.DiagnosticPathValue, entry.SchemaVersion, entry.ReferenceContentIds
            })))),
        TileSet tiles => TileSetSemantic(tiles),
        _ => throw new InvalidOperationException($"act.unsupported_staged_resource:{resource.GetType().Name}")
    };

    private static string TileSetSemantic(TileSet tiles)
    {
        if (tiles.GetSource(0) is not TileSetAtlasSource atlas || atlas.Texture is null)
            throw new InvalidOperationException("act.tileset_atlas_missing");
        using Image image = atlas.Texture.GetImage();
        string metadata = $"{tiles.TileSize}|{tiles.TileShape}|{tiles.TileLayout}|{atlas.TextureRegionSize}|{atlas.GetTilesCount()}|";
        return Hash(Encoding.UTF8.GetBytes(metadata).Concat(image.GetData()).ToArray());
    }

    public static long ReadPersistedUid(string path)
    {
        // 4.7.1 can return a cached -1 (or stale positive UID) from GetResourceUid after saving.
        // Inspect only the ResourceSaver-authored header; never rewrite resource text here.
        string? header = File.ReadLines(ProjectSettings.GlobalizePath(path)).FirstOrDefault();
        var match = System.Text.RegularExpressions.Regex.Match(header ?? string.Empty,
            "\\buid=\"(uid://[a-z0-9]+)\"");
        return match.Success ? ResourceUid.TextToId(match.Groups[1].Value) : ResourceUid.InvalidId;
    }

    private static void RequirePersistedUid(string path, long expected)
    {
        long actual = ReadPersistedUid(path);
        if (actual != expected)
            throw new InvalidOperationException($"act.persisted_uid_mismatch:{path}:expected={ResourceUid.IdToText(expected)}:actual={actual}");
    }

    private static void RegisterStableUid(string path, string text)
    {
        long uid = ResourceUid.TextToId(text);
        if (uid == ResourceUid.InvalidId) throw new InvalidOperationException($"act.invalid_uid:{path}");
        if (ResourceUid.HasId(uid))
        {
            string existing = ResourceUid.GetIdPath(uid);
            if (existing != path && !existing.StartsWith("res://content/adventure_maps/act_staging/", StringComparison.Ordinal))
                throw new InvalidOperationException($"act.uid_collision:{path}:{existing}");
            ResourceUid.SetId(uid, path);
        }
        else ResourceUid.AddId(uid, path);
    }

    private static string SourcePath(string repo, string path) => path.StartsWith("repo://", StringComparison.Ordinal)
        ? Path.Combine(repo, path["repo://".Length..]) : ProjectSettings.GlobalizePath(path);

    private static string Hash(byte[] bytes) => "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string? FileHashOrNull(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;
    private static void RequireUnchanged(string path, string? expected)
    {
        if (FileHashOrNull(path) != expected) throw new InvalidOperationException($"act.concurrent_target_change:{path}");
    }

    private static void AtomicWrite(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".act-build-" + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static byte[] CreateLedger(string[] paths, IReadOnlyDictionary<string, string> uids,
        IReadOnlyDictionary<string, string> semantics, IReadOnlyDictionary<string, string> hashes,
        IReadOnlyDictionary<string, string> sources, string sourceCommit, int catalogCount)
    {
        var payload = new
        {
            schemaVersion = 2, batchId = BatchId, state = "Generated", ownership = "GodotOwned",
            manualQaStatus = "manual_qa_pending", contractId = AdventureMapContractIds.Template,
            variantCount = 22, catalogCount,
            sourceAuthority = "canonical-godot-catalog", sourceCommit,
            sources = sources.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new { resourcePath = pair.Key, sourceHash = pair.Value }).ToArray(),
            artifacts = paths.Select(path => new
            {
                resourcePath = path, resourceUid = uids[path], targetHash = hashes[path], semanticFingerprint = semantics[path]
            }).ToArray()
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
#endif
