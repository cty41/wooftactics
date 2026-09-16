using GdUnit4;
using Godot;
using Tactics.Godot.Adapter.Editor;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class PoetAssetFactoryGodotTests
{
    private const string RunId = "run.pure-run.three-encounter-v1";

    [TestCase]
    [RequireGodotRuntime]
    public void PlaceholderPortraitUsesTheDefaultFaceCropWithoutAFullTextureOverride()
    {
        var template = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/demonbound/PureRunDemonbound.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(template).IsNotNull();
        if (template is null) return;

        var poet = (UnitDefinitionResource)template.Duplicate(true);
        poet.PortraitTextureOverride = poet.DownRightTexture;
        poet.HasPortraitRegion = false;

        PoetAssetFactory.ApplyDefaultPortraitCrop(poet);

        AssertThat(poet.DownRightTexture).IsNotNull();
        AssertThat(poet.DownRightTexture?.ResourcePath).IsEqual(
            "res://assets/units/doge_demonbound.png");
        AssertThat(poet.PortraitTextureOverride).IsNull();
        AssertThat(poet.HasPortraitRegion).IsFalse();
        Rect2 defaultRegion = GodotInitiativeStrip.DefaultFaceRegion(poet.DownRightTexture!);
        AssertThat(defaultRegion.Size.X < poet.DownRightTexture!.GetWidth()).IsTrue();
        AssertThat(defaultRegion.Size.Y < poet.DownRightTexture.GetHeight()).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ApprovedPoetIdleAndDecoyVisualContractsUseExplicitSafeFallbacks()
    {
        var poet = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/poet/PureRunPoet.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        var decoy = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/poet/PoetAfterimage.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(poet).IsNotNull();
        AssertThat(decoy).IsNotNull();
        if (poet is null || decoy is null) return;

        AssertThat(poet.DownRightTexture?.ResourcePath).IsEqual("res://assets/units/doge_poet.png");
        AssertThat(poet.UpLeftTexture?.ResourcePath).IsEqual("res://assets/units/doge_poet_ul.png");
        AssertThat(ReferenceEquals(poet.DeathTexture, poet.DownRightTexture)).IsTrue();
        AssertThat(poet.DeathBodyOffset.IsEqualApprox(poet.DownRightBodyOffset)).IsTrue();
        AssertThat(HasNoActionTextures(poet)).IsTrue();
        AssertThat(poet.BodyTint.IsEqualApprox(Colors.White)).IsTrue();
        AssertThat(decoy.DownRightTexture?.ResourcePath).IsEqual("res://assets/units/doge_poet.png");
        AssertThat(decoy.UpLeftTexture?.ResourcePath).IsEqual("res://assets/units/doge_poet_ul.png");
        AssertThat(decoy.DeathTexture).IsNull();
        AssertThat(decoy.CanProduceCorpse).IsFalse();
        AssertThat(HasNoActionTextures(decoy)).IsTrue();
        AssertThat(decoy.BodyTint.A < 1f).IsTrue();

        GodotUnitActor actor = GodotUnitFactory.InstantiateActor(poet);
        AssertThat(actor.Body).IsNotNull();
        if (actor.Body is null)
        {
            actor.Free();
            return;
        }
        actor.SetSpearHeld(true);
        actor.SetFacing(GodotUnitFacing.South);
        actor.SetActionPose(GodotUnitActionPose.Melee);
        AssertThat(actor.Body.Texture?.ResourcePath).IsEqual("res://assets/units/doge_poet.png");
        AssertThat(actor.Body.FlipH).IsFalse();
        actor.SetFacing(GodotUnitFacing.North);
        actor.SetActionPose(GodotUnitActionPose.Cast);
        AssertThat(actor.Body.Texture?.ResourcePath).IsEqual("res://assets/units/doge_poet_ul.png");
        AssertThat(actor.Body.FlipH).IsFalse();
        actor.SetFacing(GodotUnitFacing.East);
        actor.SetActionPose(GodotUnitActionPose.Hit);
        AssertThat(actor.Body.Texture?.ResourcePath).IsEqual("res://assets/units/doge_poet_ul.png");
        AssertThat(actor.Body.FlipH).IsTrue();
        actor.SetFacing(GodotUnitFacing.West);
        actor.SetActionPose(GodotUnitActionPose.Ranged);
        AssertThat(actor.Body.Texture?.ResourcePath).IsEqual("res://assets/units/doge_poet.png");
        AssertThat(actor.Body.FlipH).IsTrue();
        actor.SetDeathVisual(true);
        AssertThat(actor.Body.Texture?.ResourcePath).IsEqual("res://assets/units/doge_poet.png");
        AssertThat(actor.Body.FlipH).IsFalse();
        AssertThat(actor.Body.Offset.IsEqualApprox(poet.DownRightBodyOffset)).IsTrue();
        actor.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PureRunRootReferencesIncludeTheNonEmptyLayerFourMapId()
    {
        var run = new PureRunDefinitionResource
        {
            ContentIdValue = RunId,
            LayerFourMapContentId = "run-map.pure-run.layer4-v1"
        };

        string[] references = PoetAssetFactory.PureRunRootReferences(
            run,
            Array.Empty<GodotResourceEntry>());

        AssertThat(references).ContainsExactly("run-map.pure-run.layer4-v1");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CatalogUpdateOwnsCopiesAndBuildsASortedPureRunRootClosure()
    {
        var run = new PureRunDefinitionResource
        {
            ContentIdValue = RunId,
            LayerFourMapContentId = "run-map.pure-run.layer4-v1"
        };
        PoetAssetFactory.ConfigurePureRun(run, "unit.pure-run.poet");
        GodotResourceEntry[] generated = PoetIds().Reverse().Select(Stub).ToArray();
        string[] expected = PoetAssetFactory.PureRunRootReferences(run, generated);
        var originalRoot = Stub(RunId);
        originalRoot.ReferenceContentIds = ["stale.reference"];
        var catalog = new GodotResourceCatalog
        {
            Entries = expected.Select(Stub).Append(originalRoot).ToArray()
        };

        PoetAssetFactory.ApplyCatalogUpdates(catalog, generated, run);
        GodotResourceEntry root = catalog.Entries.Single(value => value.ContentIdValue == RunId);

        AssertThat(root.ReferenceContentIds).ContainsExactly(expected);
        AssertThat(root.ReferenceContentIds).Contains("run-map.pure-run.layer4-v1");
        AssertThat(root.ReferenceContentIds.SequenceEqual(
            root.ReferenceContentIds.Order(StringComparer.Ordinal))).IsTrue();
        AssertThat(originalRoot.ReferenceContentIds).ContainsExactly("stale.reference");
        AssertThat(catalog.Entries.Select(value => value.ContentIdValue).SequenceEqual(
            catalog.Entries.Select(value => value.ContentIdValue).Order(StringComparer.Ordinal))).IsTrue();
        AssertThat(Reachable(root.ContentIdValue, catalog).IsSupersetOf(expected)).IsTrue();

        GodotResourceEntry[] firstPass = catalog.Entries;
        PoetAssetFactory.ApplyCatalogUpdates(catalog, generated.Reverse(), run);
        AssertThat(catalog.Entries.Select(Signature).SequenceEqual(firstPass.Select(Signature))).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CanonicalCatalogClosesOverFiveClassesAndEveryGeneratedPoetEntryInMemory()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/ContentCatalog.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        var run = ResourceLoader.Load<PureRunDefinitionResource>(
            "res://content/runs/PureRunThreeEncounterV1.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(catalog).IsNotNull();
        AssertThat(run).IsNotNull();
        if (catalog is null || run is null) return;

        GodotResourceEntry[] generated = catalog.Entries.Where(value =>
            value.ContentIdValue.StartsWith("skill.poet.", StringComparison.Ordinal) ||
            value.ContentIdValue.StartsWith("status.poet.", StringComparison.Ordinal) ||
            value.ContentIdValue is "unit.pure-run.poet" or "unit.pure-run.poet-decoy").ToArray();
        AssertThat(generated.Length).IsEqual(19);

        var updated = (GodotResourceCatalog)catalog.Duplicate(true);
        PoetAssetFactory.ApplyCatalogUpdates(updated, generated, run);
        GodotResourceEntry root = updated.Entries.Single(value => value.ContentIdValue == RunId);
        string[] expected = PoetAssetFactory.PureRunRootReferences(run, generated);
        var available = updated.Entries.Select(value => value.ContentIdValue).ToHashSet(StringComparer.Ordinal);

        AssertThat(run.UnitContentIds.Length).IsEqual(5);
        AssertThat(expected.All(available.Contains)).IsTrue();
        AssertThat(Reachable(root.ContentIdValue, updated).IsSupersetOf(expected)).IsTrue();
    }

    private static bool HasNoActionTextures(UnitDefinitionResource unit) =>
        unit.MeleeDownRightTexture is null && unit.MeleeUpLeftTexture is null &&
        unit.RangedDownRightTexture is null && unit.RangedUpLeftTexture is null &&
        unit.CastDownRightTexture is null && unit.CastUpLeftTexture is null &&
        unit.HitDownRightTexture is null && unit.HitUpLeftTexture is null;

    private static HashSet<string> Reachable(string rootId, GodotResourceCatalog catalog)
    {
        Dictionary<string, GodotResourceEntry> entries = catalog.Entries.ToDictionary(
            value => value.ContentIdValue, StringComparer.Ordinal);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(rootId);
        while (pending.TryDequeue(out string? id) && entries.TryGetValue(id, out GodotResourceEntry? entry))
            foreach (string reference in entry.ReferenceContentIds)
                if (reachable.Add(reference)) pending.Enqueue(reference);
        return reachable;
    }

    private static string Signature(GodotResourceEntry entry) =>
        entry.ContentIdValue + "|" + string.Join(",", entry.ReferenceContentIds);

    private static GodotResourceEntry Stub(string id) => new()
    {
        ContentIdValue = id,
        ResourceTypeIdValue = id.StartsWith("skill.", StringComparison.Ordinal) ? "skill" :
            id.StartsWith("status.", StringComparison.Ordinal) ? "buff" :
            id.StartsWith("unit.", StringComparison.Ordinal) ? "unit" : "run",
        ResourceUidValue = "uid://test",
        DiagnosticPathValue = "res://test/" + id + ".tres",
        SchemaVersion = 1
    };

    private static string[] PoetIds() =>
    [
        "skill.poet.xiake-xing.lv1", "skill.poet.xiake-xing.lv2", "skill.poet.xiake-xing.lv3",
        "skill.poet.sword-rain.lv1", "skill.poet.sword-rain.lv2",
        "skill.poet.jiang-jin-jiu.lv1", "skill.poet.jiang-jin-jiu.lv2", "skill.poet.jiang-jin-jiu.lv3",
        "skill.poet.road-is-hard.lv1", "skill.poet.road-is-hard.lv2",
        "skill.poet.moon-drink.lv1", "skill.poet.moon-drink.lv2", "skill.poet.moon-drink.lv3",
        "skill.poet.mountain-dialogue.lv1", "skill.poet.mountain-dialogue.lv2",
        "status.poet.wine-heal", "status.poet.agility-verse",
        "unit.pure-run.poet", "unit.pure-run.poet-decoy"
    ];
}
