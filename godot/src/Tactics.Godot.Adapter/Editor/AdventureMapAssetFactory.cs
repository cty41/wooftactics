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
        EnsureDirectory("res://content/adventure_maps");
        RegisterExistingUid(TileSetPath); RegisterExistingUid(StartCampPath); RegisterExistingUid(BasicBattlePath);
        TileSet tileSet = CreateTileSet();
        Save(tileSet, TileSetPath);
        tileSet = ResourceLoader.Load<TileSet>(TileSetPath, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Generated adventure TileSet cannot be reloaded.");
        AdventureMapTemplateResource camp = CreateStartCampTemplate(tileSet);
        AdventureMapTemplateResource battle = CreateBasicBattleTemplate(tileSet);
        Save(camp, StartCampPath); Save(battle, BasicBattlePath);

        GodotResourceCatalog global = ResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Canonical ContentCatalog is missing.");
        RegisterCatalogUids(global);
        string[] replaced = [StartCampContentId, BasicBattleContentId];
        if (global.Entries.Any(value => replaced.Contains(value.ContentIdValue, StringComparer.Ordinal)))
            Save(new GodotResourceCatalog { Entries = global.Entries.Where(value => !replaced.Contains(value.ContentIdValue, StringComparer.Ordinal))
                .Select(Copy).OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray() }, GlobalCatalogPath);
        GodotResourceEntry[] entries =
            new[]
            {
                Entry(StartCampContentId, StartCampPath), Entry(BasicBattleContentId, BasicBattlePath)
            }.OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray();
        var updated = new GodotResourceCatalog { Entries = entries };
        Save(updated, CatalogPath);
        updated.Validate();

        string[] paths = [TileSetPath, StartCampPath, BasicBattlePath, CatalogPath];
        var uids = paths.ToDictionary(path => path, path => ResourceUid.IdToText(Uid(path)), StringComparer.Ordinal);
        var semantics = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [StartCampPath] = SemanticFingerprint(camp),
            [BasicBattlePath] = SemanticFingerprint(battle)
        };
        WriteLedger(paths, uids, semantics, entries.Length);
        return new AdventureMapAssetBuildResult(paths, uids, semantics, entries.Length);
    }

    public static AdventureMapTemplateResource CreateStartCampTemplate() => CreateStartCampTemplate(CreateTileSet());
    public static AdventureMapTemplateResource CreateBasicBattleTemplate() => CreateBasicBattleTemplate(CreateTileSet());

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
        Image image = Image.CreateEmpty(480, 48, false, Image.Format.Rgba8); image.Fill(Colors.Transparent);
        Color[] colors = [new("344b40"), new("1b2923"), new("8d7146"), new(0.01f, 0.015f, 0.02f, .94f), new(0.2f, .72f, .92f, .58f)];
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
    private static void Save(Resource value, string path) => DeterministicResourceSaver.Save(value, path, Uid(path));
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
    private static void RegisterExistingUid(string path)
    {
        string absolute = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolute)) return;
        long uid = Uid(path); if (!ResourceUid.HasId(uid)) ResourceUid.AddId(uid, path);
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
    private static void WriteLedger(string[] paths, IReadOnlyDictionary<string, string> uids,
        IReadOnlyDictionary<string, string> semantics, int catalogCount)
    {
        string project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repo = Directory.GetParent(project)!.FullName;
        string ledger = Path.Combine(repo, "Tools", "migration", "manifest", "state", BatchId + ".json");
        var payload = new { schemaVersion = 1, batchId = BatchId, state = "Generated", ownership = "GodotOwned",
            contractId = AdventureMapContractIds.Template, catalogCount,
            artifacts = paths.Select(path => new { resourcePath = path, resourceUid = uids[path],
                semanticFingerprint = semantics.TryGetValue(path, out string? fingerprint) ? fingerprint : null }).ToArray() };
        File.WriteAllText(ledger, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
    }
}
#endif
