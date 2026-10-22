#if TOOLS
using System.Text.Json;
using Godot;
using Tactics.Core.Board;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class AdventureMapAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try
        {
            AdventureMapAssetBuildResult result = AdventureMapAssetFactory.Build();
            ValidatePersistedUids(result);
            string[] variants = result.ResourcePaths.Where(path => path.Contains("/adventure_maps/nodes/", StringComparison.Ordinal)).ToArray();
            if (variants.Length != 22 || result.CatalogCount != 24)
                throw new InvalidOperationException("Generated Act batch must contain 22 variants and 24 catalog entries.");
            foreach (string path in variants.Append(AdventureMapAssetFactory.StartCampPath).Append(AdventureMapAssetFactory.BasicBattlePath))
                ValidateGeneratedSurface(path);
            GD.Print($"Adventure map templates generated through ResourceSaver: variants={variants.Length}, catalog={result.CatalogCount}, manual_qa_pending.");
            Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            Quit(1);
        }
    }

    private static void ValidatePersistedUids(AdventureMapAssetBuildResult result)
    {
        string project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string ledger = Path.Combine(Directory.GetParent(project)!.FullName, "Tools", "migration", "manifest", "state",
            AdventureMapAssetFactory.BatchId + ".json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(ledger));
        var expected = document.RootElement.GetProperty("artifacts").EnumerateArray().ToDictionary(
            artifact => artifact.GetProperty("resourcePath").GetString()!,
            artifact => artifact.GetProperty("resourceUid").GetString()!, StringComparer.Ordinal);
        if (expected.Count != result.ResourcePaths.Count) throw new InvalidOperationException("Generated ledger artifact count differs from output.");
        foreach (string path in result.ResourcePaths)
        {
            if (!expected.TryGetValue(path, out string? uid) || uid != result.ResourceUids[path] ||
                AdventureMapAssetFactory.ReadPersistedUid(path) != ResourceUid.TextToId(uid))
                throw new InvalidOperationException($"Generated resource lacks its persisted ledger UID: {path}");
        }
    }

    private static void ValidateGeneratedSurface(string path)
    {
        AdventureMapTemplateResource template = ResourceLoader.Load<AdventureMapTemplateResource>(
            path, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException($"Generated template cannot be loaded: {path}");
        template.ToCoreDefinition();
        var surface = new GodotIsometricTileMapSurface();
        try
        {
            surface.Configure(template);
            for (int y = 0; y < IsometricGridProjection.GridSize; y++)
            for (int x = 0; x < IsometricGridProjection.GridSize; x++)
            {
                GridPoint cell = new(x, y);
                Vector2 actual = surface.CellCenter(cell), expected = IsometricGridProjection.GridToScreen(cell);
                if (!actual.IsEqualApprox(expected))
                    throw new InvalidOperationException($"TileMap projection mismatch at {cell}: actual={actual}, expected={expected}.");
                if (!surface.TryPointToCell(surface.CellCenter(cell), out GridPoint picked) || picked != cell)
                    throw new InvalidOperationException($"TileMap picking mismatch at {cell}.");
            }
            Vector2 edge = surface.CellCenter(new GridPoint(2, 2)) + new Vector2(48f, 0f);
            if (!surface.TryPointToCell(edge, out GridPoint edgeCell) || edgeCell != new GridPoint(2, 1))
                throw new InvalidOperationException("TileMap shared-edge picking is not deterministic.");
        }
        finally { surface.Free(); }
    }
}
#endif
