using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Core.Tests;

public sealed class PureRunActBlueprintTests
{
    [TestCase(0)]
    [TestCase(37)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void ResolvedContentMatchesTheExistingDemoSelectors(int seed)
    {
        PureRunMapDefinition map = DemoMap();
        var selector = new PureRunMapService(map);
        PureRunMapState four = selector.UnlockLayerFour(seed);
        PureRunMapState six = selector.UnlockLayerSix(four, seed);
        PureRunActBlueprint blueprint = PureRunActBlueprint.Resolve(map, seed);
        Assert.Multiple(() =>
        {
            Assert.That(blueprint.ActDefinition.ActId, Is.EqualTo(new ContentId("act-01")));
            Assert.That(blueprint.ActDefinition.DefinitionVersion, Is.EqualTo(1));
            Assert.That(blueprint.ActDefinition.StartNodeId, Is.EqualTo("start"));
            Assert.That(blueprint.ActDefinition.TerminalNodeId, Is.EqualTo("layer_07_battle"));
            Assert.That(blueprint.Bindings.Keys, Is.EquivalentTo(map.Nodes.Select(node => node.NodeId)));
            Assert.That(blueprint.Bindings["start"].EncounterId, Is.Null);
            Assert.That(blueprint.Bindings["start"].TemplateId.Value,
                Is.EqualTo("adventure-map-template.pure-run.node.start"));
        });
        foreach (PureRunMapNodeDefinition node in map.Nodes)
        {
            PureRunNodeContentBinding binding = blueprint.Bindings[node.NodeId];
            Assert.That(binding.NodeId, Is.EqualTo(node.NodeId));
            Assert.That(blueprint.ActDefinition.Nodes.Single(scene => scene.NodeId == node.NodeId).TemplateId,
                Is.EqualTo(binding.TemplateId));
            if (node.NodeId is "layer_05_battle" or "layer_06_battle")
            {
                ContentId encounter = selector.SelectLateEncounter(seed, node.NodeId);
                Assert.That(binding.EncounterId, Is.EqualTo(encounter));
                Assert.That(binding.TemplateId.Value, Is.EqualTo(
                    $"adventure-map-template.pure-run.node.{node.NodeId.Replace('_', '-')}.{encounter.Value.Split('.').Last()}"));
            }
            else if (node.Kind == PureRunNodeKind.Boss)
                Assert.That(binding.EncounterId, Is.EqualTo(selector.SelectLateEncounter(seed, node.NodeId, boss: true)));
            else if (node.Kind == PureRunNodeKind.Mystery)
            {
                (RunAdventureEventContextKind context, string suffix) = six.MysteryEventAssignments[node.NodeId] switch
                {
                    "cursed_chest_001" => (RunAdventureEventContextKind.CursedChestMimic, "cursedchestmimic"),
                    "fallen_altar_001" => (RunAdventureEventContextKind.FallenAltarGuardian, "fallenaltarguardian"),
                    "lost_villager_001" => (RunAdventureEventContextKind.LostVillagerEscort, "lostvillagerescort"),
                    _ => throw new AssertionException("Unknown legacy event fixture.")
                };
                Assert.That(binding.EventContext, Is.EqualTo(context));
                Assert.That(binding.EncounterId, Is.EqualTo(new ContentId("encounter.pure-run.n1")));
                Assert.That(binding.TemplateId.Value,
                    Is.EqualTo($"adventure-map-template.pure-run.node.{node.NodeId.Replace('_', '-')}.{suffix}"));
                Assert.That(blueprint.ActDefinition.Nodes.Single(scene => scene.NodeId == node.NodeId).EntryStrategy,
                    Is.EqualTo(PureRunNodeEntryStrategy.Exploration));
            }
            else
            {
                Assert.That(binding.EncounterId, node.NodeId != "start" && node.Kind == PureRunNodeKind.Battle
                    ? Is.EqualTo(node.ContentId) : Is.Null);
                Assert.That(binding.EventContext, Is.EqualTo(RunAdventureEventContextKind.None));
                Assert.That(binding.TemplateId.Value,
                    Is.EqualTo($"adventure-map-template.pure-run.node.{node.NodeId.Replace('_', '-')}"));
            }
        }
    }

    [Test]
    public void RepeatedResolutionIsStableAndSeedRangeCoversBothElitesAndAllEvents()
    {
        PureRunMapDefinition map = DemoMap();
        var elites = new HashSet<ContentId>();
        var events = new HashSet<RunAdventureEventContextKind>();
        for (int seed = 0; seed < 1024; seed++)
        {
            PureRunActBlueprint first = PureRunActBlueprint.Resolve(map, seed);
            PureRunActBlueprint replay = PureRunActBlueprint.Resolve(map, seed);
            Assert.That(replay.Bindings.Values, Is.EqualTo(first.Bindings.Values));
            elites.Add(first.Bindings["layer_05_battle"].EncounterId!.Value);
            elites.Add(first.Bindings["layer_06_battle"].EncounterId!.Value);
            events.Add(first.Bindings["layer_04_event"].EventContext);
            events.Add(first.Bindings["layer_06_event"].EventContext);
            Assert.That(first.Bindings["layer_06_event"].EventContext,
                Is.Not.EqualTo(first.Bindings["layer_04_event"].EventContext));
        }
        Assert.That(elites, Is.EquivalentTo(new[] { new ContentId("encounter.pure-run.e1"), new ContentId("encounter.pure-run.e2") }));
        Assert.That(events, Is.EquivalentTo(new[] { RunAdventureEventContextKind.CursedChestMimic,
            RunAdventureEventContextKind.FallenAltarGuardian, RunAdventureEventContextKind.LostVillagerEscort }));
    }

    [Test]
    public void VariantEnumerationIsCompleteWithoutSamplingAndUsesTheSameTemplateIdentityRules()
    {
        PureRunMapDefinition map = DemoMap();
        IReadOnlyList<PureRunNodeContentBinding> variants = PureRunActBlueprint.EnumerateContentVariants(map);
        Assert.That(variants, Has.Count.EqualTo(22));
        Assert.That(variants.Select(binding => binding.TemplateId).Distinct().Count(), Is.EqualTo(22));
        foreach (PureRunMapNodeDefinition node in map.Nodes)
            Assert.That(variants.Count(binding => binding.NodeId == node.NodeId), Is.EqualTo(
                node.Kind == PureRunNodeKind.Mystery ? 3 : node.NodeId is "layer_05_battle" or "layer_06_battle" ? 2 : 1));
        for (int seed = -16; seed < 64; seed++)
            foreach (PureRunNodeContentBinding binding in PureRunActBlueprint.Resolve(map, seed).Bindings.Values)
                Assert.That(variants, Does.Contain(binding));
        Assert.That(PureRunActBlueprint.EnumerateContentVariants(map), Is.EqualTo(variants));
        Assert.Throws<NotSupportedException>(() => ((IList<PureRunNodeContentBinding>)variants).Clear());
    }

    [Test]
    public void ResolutionDoesNotMutateTheInputMapOrExposeMutableBindingCollections()
    {
        PureRunMapDefinition map = DemoMap();
        PureRunMapNodeDefinition[] nodes = map.Nodes.ToArray();
        PureRunMapConnectionDefinition[] edges = map.Connections.ToArray();
        PureRunActBlueprint blueprint = PureRunActBlueprint.Resolve(map, 37);
        PureRunActBlueprint.Resolve(map, 999);
        Assert.Multiple(() =>
        {
            Assert.That(map.Nodes, Is.EqualTo(nodes));
            Assert.That(map.Connections, Is.EqualTo(edges));
            Assert.That(blueprint.ActDefinition.Map, Is.SameAs(map));
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<string, PureRunNodeContentBinding>)blueprint.Bindings).Clear());
        });
    }

    [Test]
    public void RejectsUnknownLayersKindsNodesOrRoutesInsteadOfGuessingAnotherAct()
    {
        PureRunMapDefinition map = DemoMap();
        PureRunMapDefinition ChangedNodes(IEnumerable<PureRunMapNodeDefinition> nodes) =>
            new(map.ContentId, map.LayoutVersion, nodes, map.Connections);
        Assert.Throws<NotSupportedException>(() => PureRunActBlueprint.Resolve(
            ChangedNodes(map.Nodes.Select(node => node.NodeId == "layer_07_battle" ? node with { Layer = 8 } : node)), 0));
        Assert.Throws<NotSupportedException>(() => PureRunActBlueprint.Resolve(
            ChangedNodes(map.Nodes.Select(node => node.NodeId == "layer_05_battle" ? node with { Kind = PureRunNodeKind.Battle } : node)), 0));
        Assert.Throws<NotSupportedException>(() => PureRunActBlueprint.Resolve(
            ChangedNodes(map.Nodes.Append(new("extra", 8, PureRunNodeKind.Boss, new ContentId("encounter.extra")))), 0));
        Assert.Throws<NotSupportedException>(() => PureRunActBlueprint.Resolve(new PureRunMapDefinition(
            map.ContentId, map.LayoutVersion, map.Nodes, map.Connections.Where(edge => edge.FromNodeId != "start")), 0));
        Assert.Throws<ArgumentNullException>(() => PureRunActBlueprint.Resolve(null!, 0));
    }

    public static IEnumerable<PureRunNodeContentBinding> AllPersistedVariants() =>
        PureRunActBlueprint.EnumerateContentVariants(DemoMap());

    [TestCaseSource(nameof(AllPersistedVariants))]
    public void RestoreAcceptsEveryKnownNodeVariantWithoutChangingTheOtherNodes(PureRunNodeContentBinding variant)
    {
        PureRunMapDefinition map = DemoMap();
        var identity = Identity(variant);
        foreach (int seed in new[] { 0, 37, int.MinValue })
        {
            PureRunActBlueprint defaults = PureRunActBlueprint.Resolve(map, seed);
            PureRunActBlueprint restored = PureRunActBlueprint.Restore(map, seed, [identity]);
            Assert.Multiple(() =>
            {
                Assert.That(restored.Bindings[variant.NodeId], Is.EqualTo(variant));
                Assert.That(restored.PersistedSceneIdentities, Is.EqualTo(new[] { identity }));
                Assert.That(restored.Bindings, Has.Count.EqualTo(16));
                Assert.That(restored.ActDefinition.Map, Is.SameAs(map));
                Assert.That(restored.ActDefinition.ActId, Is.EqualTo(defaults.ActDefinition.ActId));
                Assert.That(restored.ActDefinition.DefinitionVersion, Is.EqualTo(defaults.ActDefinition.DefinitionVersion));
                Assert.That(restored.ActDefinition.StartNodeId, Is.EqualTo(defaults.ActDefinition.StartNodeId));
                Assert.That(restored.ActDefinition.TerminalNodeId, Is.EqualTo(defaults.ActDefinition.TerminalNodeId));
            });
            foreach (PureRunNodeSceneDefinition scene in restored.ActDefinition.Nodes)
            {
                Assert.That(scene.EntryStrategy, Is.EqualTo(defaults.ActDefinition.Nodes.Single(node => node.NodeId == scene.NodeId).EntryStrategy));
                Assert.That(scene.TemplateId, Is.EqualTo(restored.Bindings[scene.NodeId].TemplateId));
                if (scene.NodeId != variant.NodeId)
                    Assert.That(restored.Bindings[scene.NodeId], Is.EqualTo(defaults.Bindings[scene.NodeId]));
            }
        }
    }

    [TestCase("layer_04_event")]
    [TestCase("layer_06_event")]
    [TestCase("layer_05_battle")]
    [TestCase("layer_06_battle")]
    public void PersistedActualVariantWinsEvenWhenAnotherSeedSelectsADifferentDefault(string nodeId)
    {
        PureRunMapDefinition map = DemoMap();
        PureRunNodeContentBinding saved = PureRunActBlueprint.Resolve(map, 0).Bindings[nodeId];
        int differentSeed = Enumerable.Range(1, 1024).First(seed =>
            PureRunActBlueprint.Resolve(map, seed).Bindings[nodeId].TemplateId != saved.TemplateId);
        PureRunActBlueprint restored = PureRunActBlueprint.Restore(map, differentSeed, [Identity(saved)]);
        Assert.That(restored.Bindings[nodeId], Is.EqualTo(saved));
        Assert.That(restored.ActDefinition.Nodes.Single(node => node.NodeId == nodeId).TemplateId, Is.EqualTo(saved.TemplateId));
        Assert.That(PureRunActBlueprint.Resolve(map, differentSeed).Bindings[nodeId], Is.Not.EqualTo(saved));
    }

    [TestCase(0)]
    [TestCase(37)]
    [TestCase(int.MinValue)]
    public void EmptyRecoveryIsEquivalentToResolveAndDoesNotInventEvidence(int seed)
    {
        PureRunMapDefinition map = DemoMap();
        PureRunActBlueprint defaults = PureRunActBlueprint.Resolve(map, seed);
        PureRunActBlueprint restored = PureRunActBlueprint.Restore(map, seed, []);
        Assert.Multiple(() =>
        {
            Assert.That(restored.Bindings.Values, Is.EqualTo(defaults.Bindings.Values));
            Assert.That(restored.ActDefinition.Nodes, Is.EqualTo(defaults.ActDefinition.Nodes));
            Assert.That(restored.ActDefinition.Map, Is.SameAs(defaults.ActDefinition.Map));
            Assert.That(restored.ActDefinition.ActId, Is.EqualTo(defaults.ActDefinition.ActId));
            Assert.That(restored.ActDefinition.DefinitionVersion, Is.EqualTo(defaults.ActDefinition.DefinitionVersion));
            Assert.That(restored.ActDefinition.StartNodeId, Is.EqualTo(defaults.ActDefinition.StartNodeId));
            Assert.That(restored.ActDefinition.TerminalNodeId, Is.EqualTo(defaults.ActDefinition.TerminalNodeId));
            Assert.That(restored.PersistedSceneIdentities, Is.Empty);
            Assert.That(defaults.PersistedSceneIdentities, Is.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<RunNodeSceneIdentity>)defaults.PersistedSceneIdentities)
                .Add(Identity(defaults.Bindings["start"])));
        });
    }

    [Test]
    public void RecoveryCopiesEvidenceAndDoesNotChangeMapOrDefaultAssignments()
    {
        PureRunMapDefinition map = DemoMap();
        PureRunMapNodeDefinition[] nodes = map.Nodes.ToArray();
        PureRunMapConnectionDefinition[] edges = map.Connections.ToArray();
        var selector = new PureRunMapService(map);
        PureRunMapState assignments = selector.UnlockLayerSix(selector.UnlockLayerFour(37), 37);
        KeyValuePair<string, string>[] beforeAssignments = assignments.MysteryEventAssignments.ToArray();
        PureRunNodeContentBinding[] defaults = PureRunActBlueprint.Resolve(map, 37).Bindings.Values.ToArray();
        RunNodeSceneIdentity[] evidence = PureRunActBlueprint.EnumerateContentVariants(map)
            .GroupBy(value => value.NodeId).Select(group => Identity(group.Last())).ToArray();
        RunNodeSceneIdentity[] original = evidence.ToArray();
        PureRunActBlueprint restored = PureRunActBlueprint.Restore(map, 37, evidence);
        evidence[0] = new RunNodeSceneIdentity(new ContentId("act-other"), 2, "not-input-node", new ContentId("template.other"));
        Assert.Multiple(() =>
        {
            Assert.That(restored.PersistedSceneIdentities, Is.EqualTo(original));
            Assert.That(restored.PersistedSceneIdentities, Has.Count.EqualTo(16));
            Assert.That(map.Nodes, Is.EqualTo(nodes));
            Assert.That(map.Connections, Is.EqualTo(edges));
            Assert.That(assignments.MysteryEventAssignments.ToArray(), Is.EqualTo(beforeAssignments));
            Assert.That(PureRunActBlueprint.Resolve(map, 37).Bindings.Values, Is.EqualTo(defaults));
            Assert.Throws<NotSupportedException>(() => ((IList<RunNodeSceneIdentity>)restored.PersistedSceneIdentities).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<RunNodeSceneIdentity>)restored.PersistedSceneIdentities)[0] = original[0]);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, PureRunNodeContentBinding>)restored.Bindings).Clear());
        });
        foreach (RunNodeSceneIdentity identity in original)
            Assert.That(restored.Bindings[identity.NodeId].TemplateId, Is.EqualTo(identity.TemplateId));
    }

    [TestCase("act")]
    [TestCase("version")]
    [TestCase("node")]
    [TestCase("other-node-template")]
    [TestCase("unknown-template")]
    public void RejectsPersistedIdentityOutsideItsExactActNodeVariant(string invalid)
    {
        PureRunMapDefinition map = DemoMap();
        PureRunActBlueprint defaults = PureRunActBlueprint.Resolve(map, 37);
        PureRunNodeContentBinding binding = defaults.Bindings["layer_04_event"];
        var identity = new RunNodeSceneIdentity(
            invalid == "act" ? new ContentId("act-other") : defaults.ActDefinition.ActId,
            invalid == "version" ? 2 : defaults.ActDefinition.DefinitionVersion,
            invalid == "node" ? "unknown-node" : binding.NodeId,
            invalid switch
            {
                "other-node-template" => defaults.Bindings["layer_06_event"].TemplateId,
                "unknown-template" => new ContentId("template.unknown"),
                _ => binding.TemplateId
            });
        Assert.Throws<ArgumentException>(() => PureRunActBlueprint.Restore(map, 37, [identity]));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RejectsDuplicateNodeEvidenceEvenWhenTheIdentityIsIdentical(bool differentVariant)
    {
        PureRunMapDefinition map = DemoMap();
        PureRunNodeContentBinding[] variants = PureRunActBlueprint.EnumerateContentVariants(map)
            .Where(value => value.NodeId == "layer_04_event").ToArray();
        RunNodeSceneIdentity first = Identity(variants[0]);
        RunNodeSceneIdentity second = differentVariant ? Identity(variants[1]) : first;
        Assert.Throws<ArgumentException>(() => PureRunActBlueprint.Restore(map, 37, [first, second]));
    }

    [Test]
    public void RejectsNullRecoveryInputs()
    {
        Assert.Throws<ArgumentNullException>(() => PureRunActBlueprint.Restore(null!, 37, []));
        Assert.Throws<ArgumentNullException>(() => PureRunActBlueprint.Restore(DemoMap(), 37, null!));
        Assert.Throws<ArgumentException>(() => PureRunActBlueprint.Restore(DemoMap(), 37, [null!]));
    }

    private static RunNodeSceneIdentity Identity(PureRunNodeContentBinding binding) =>
        new(new ContentId("act-01"), 1, binding.NodeId, binding.TemplateId);

    // Mirrors the canonical MapTreasureAssetFactory's Demo topology without a Godot dependency.
    internal static PureRunMapDefinition DemoMap()
    {
        var nodes = new List<PureRunMapNodeDefinition>
        {
            new("start", 0, PureRunNodeKind.Battle, new ContentId("run.pure-run.three-encounter-v1")),
            new("layer_01_battle", 1, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n1")),
            new("layer_02_battle", 2, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n2")),
            new("layer_03_battle", 3, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n3")),
            new("layer_05_battle", 5, PureRunNodeKind.Elite, new ContentId("encounter.pure-run.e1")),
            new("layer_07_battle", 7, PureRunNodeKind.Boss, new ContentId("encounter.pure-run.special"))
        };
        var edges = new List<PureRunMapConnectionDefinition>
        {
            new("start", "layer_01_battle"), new("layer_01_battle", "layer_02_battle"),
            new("layer_02_battle", "layer_03_battle")
        };
        foreach (int layer in new[] { 4, 6 })
        {
            string prefix = $"layer_{layer:00}";
            nodes.Add(new(prefix + "_battle", layer, PureRunNodeKind.Battle, new ContentId(layer == 4 ? "encounter.pure-run.n4" : "encounter.pure-run.e1")));
            nodes.Add(new(prefix + "_rest", layer, PureRunNodeKind.Rest, new ContentId("rest.pure-run.standard-v1")));
            nodes.Add(new(prefix + "_store", layer, PureRunNodeKind.Store, new ContentId("store.pure-run.standard-v1")));
            nodes.Add(new(prefix + "_event", layer, PureRunNodeKind.Mystery, new ContentId("event.pure-run.cursed-chest")));
            nodes.Add(new(prefix + "_treasure", layer, PureRunNodeKind.Treasure, new ContentId("treasure.pure-run.standard-v1")));
        }
        foreach (string suffix in new[] { "battle", "rest", "store", "event", "treasure" })
        {
            edges.Add(new("layer_03_battle", $"layer_04_{suffix}"));
            edges.Add(new($"layer_04_{suffix}", "layer_05_battle"));
            edges.Add(new("layer_05_battle", $"layer_06_{suffix}"));
            edges.Add(new($"layer_06_{suffix}", "layer_07_battle"));
        }
        return new PureRunMapDefinition(new ContentId("run-map.pure-run.layer4-v1"), 3, nodes, edges);
    }
}
