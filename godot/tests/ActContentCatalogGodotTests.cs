using GdUnit4;
using Godot;
using Tactics.Godot.Adapter.Runtime;
using Tactics.Core.Runs;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public sealed class ActContentCatalogGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void PreparationUsesTheSavedVariantInsteadOfRegeneratingTheDefaultSeedChoice()
    {
        var map = RequiredResourceLoader.Load<PureRunMapResource>(
            "res://content/map/PureRunDefaultMap.tres", "test map").ToCoreDefinition();
        const string node = "layer_04_event";
        int seed = Enumerable.Range(0, 100).First(value => PureRunActBlueprint.Resolve(map, value)
            .Bindings[node].EventContext != RunAdventureEventContextKind.CursedChestMimic);
        var defaults = PureRunActBlueprint.Resolve(map, seed);
        var actual = PureRunActBlueprint.EnumerateContentVariants(map).Single(value =>
            value.NodeId == node && value.EventContext == RunAdventureEventContextKind.CursedChestMimic);
        var identity = new RunNodeSceneIdentity(defaults.ActDefinition.ActId,
            defaults.ActDefinition.DefinitionVersion, node, actual.TemplateId);
        var restored = PureRunActBlueprint.Restore(map, seed, [identity]);
        GodotResolvedActContent loaded = GodotActContentCatalog.Load(restored);
        AssertThat(ReferenceEquals(loaded.Blueprint, restored)).IsTrue();
        AssertThat(loaded.Templates.Count).IsEqual(16);
        AssertThat(loaded.Templates.ContainsKey(defaults.Bindings[node].TemplateId)).IsFalse();
        var template = loaded.Templates[actual.TemplateId].ToCoreDefinition();
        AssertThat(template.ContentId).IsEqual(actual.TemplateId);
        AssertThat(template.Board.Objects.Any(value => value.ObjectId == "cursed-chest")).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PreparationUsesTheCanonicalTypedBoundaryRatherThanCachedPlaceholderResources()
    {
        var map = RequiredResourceLoader.Load<PureRunMapResource>(
            "res://content/map/PureRunDefaultMap.tres", "test map").ToCoreDefinition();
        foreach (string path in new[] { GodotActContentCatalog.CatalogPath,
            "res://content/adventure_maps/nodes/layer-01-battle.tres" })
        {
            // In-memory cache injection only: no resource file is saved or rewritten.
            using var stale = new Resource();
            stale.TakeOverPath(path);
            try
            {
                AssertThat(ResourceLoader.Load(path).GetInstanceId()).IsEqual(stale.GetInstanceId());
                GodotResolvedActContent loaded = GodotActContentCatalog.Load(map, 41);
                AssertThat(loaded.Templates.Count).IsEqual(16);
                foreach (var binding in loaded.Blueprint.Bindings.Values)
                    AssertThat(loaded.Templates[binding.TemplateId].ToCoreDefinition().ContentId).IsEqual(binding.TemplateId);
            }
            finally { stale.ResourcePath = string.Empty; }
        }
    }
}
