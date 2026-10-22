using System.Collections.ObjectModel;
using Tactics.Core.Content;

namespace Tactics.Core.Runs;

/// <summary>Resolved content identity for a node; does not instantiate or mutate gameplay state.</summary>
public sealed record PureRunNodeContentBinding(
    string NodeId,
    ContentId TemplateId,
    ContentId? EncounterId,
    RunAdventureEventContextKind EventContext);

/// <summary>
/// Current seven-layer, single-Act Demo content selection. Reuses the existing deterministic
/// map rules without consuming runtime battle RNG or pretending to support arbitrary future Acts.
/// </summary>
public sealed class PureRunActBlueprint
{
    private PureRunActBlueprint(PureRunActDefinition definition,
        Dictionary<string, PureRunNodeContentBinding> bindings,
        IEnumerable<RunNodeSceneIdentity>? persistedSceneIdentities = null)
    {
        ActDefinition = definition;
        Bindings = new ReadOnlyDictionary<string, PureRunNodeContentBinding>(bindings);
        PersistedSceneIdentities = Array.AsReadOnly(persistedSceneIdentities?.ToArray() ?? []);
    }

    public PureRunActDefinition ActDefinition { get; }
    public IReadOnlyDictionary<string, PureRunNodeContentBinding> Bindings { get; }
    /// <summary>Explicit recovery evidence, not inferred from this seed or permission to mutate gameplay state.</summary>
    public IReadOnlyList<RunNodeSceneIdentity> PersistedSceneIdentities { get; }

    /// <summary>
    /// Restores explicitly persisted content variants over deterministic defaults. Only the current Demo's
    /// finite node/template pairs are supported; checkpoint, event and progression evidence is validated by Application.
    /// </summary>
    public static PureRunActBlueprint Restore(PureRunMapDefinition map, int seed,
        IEnumerable<RunNodeSceneIdentity> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);
        PureRunActBlueprint defaults = Resolve(map, seed);
        RunNodeSceneIdentity[] evidence = identities.ToArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var bindings = defaults.Bindings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        IReadOnlyList<PureRunNodeContentBinding> variants = EnumerateContentVariants(map);
        foreach (RunNodeSceneIdentity identity in evidence)
        {
            if (identity is null) throw new ArgumentException("Persisted scene identities cannot contain null.", nameof(identities));
            if (identity.ActId != defaults.ActDefinition.ActId ||
                identity.DefinitionVersion != defaults.ActDefinition.DefinitionVersion)
                throw new ArgumentException("Persisted scene identity belongs to another Act or definition version.", nameof(identities));
            if (!bindings.ContainsKey(identity.NodeId))
                throw new ArgumentException($"Unknown persisted node '{identity.NodeId}'.", nameof(identities));
            if (!seen.Add(identity.NodeId))
                throw new ArgumentException($"Duplicate persisted node '{identity.NodeId}'.", nameof(identities));
            PureRunNodeContentBinding? variant = variants.SingleOrDefault(value =>
                value.NodeId == identity.NodeId && value.TemplateId == identity.TemplateId);
            if (variant is null)
                throw new ArgumentException($"Unsupported persisted template for node '{identity.NodeId}'.", nameof(identities));
            bindings[identity.NodeId] = variant;
        }
        PureRunActDefinition prior = defaults.ActDefinition;
        var definition = new PureRunActDefinition(prior.ActId, prior.DefinitionVersion, prior.Map,
            prior.StartNodeId, prior.TerminalNodeId, prior.Nodes.Select(node => new PureRunNodeSceneDefinition(
                node.NodeId, bindings[node.NodeId].TemplateId, node.EntryStrategy)));
        return new PureRunActBlueprint(definition, bindings, evidence);
    }

    public static PureRunActBlueprint Resolve(PureRunMapDefinition map, int seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        ValidateCurrentDemoTopology(map);
        var selector = new PureRunMapService(map);
        PureRunMapState layerFour = selector.UnlockLayerFour(seed);
        PureRunMapState layerSix = selector.UnlockLayerSix(layerFour, seed);
        var bindings = new Dictionary<string, PureRunNodeContentBinding>(StringComparer.Ordinal);
        foreach (PureRunMapNodeDefinition node in map.Nodes)
        {
            ContentId? encounter = null;
            RunAdventureEventContextKind context = RunAdventureEventContextKind.None;
            if (node.Kind == PureRunNodeKind.Mystery)
            {
                string eventId = layerSix.MysteryEventAssignments[node.NodeId];
                context = eventId switch
                {
                    "cursed_chest_001" => RunAdventureEventContextKind.CursedChestMimic,
                    "fallen_altar_001" => RunAdventureEventContextKind.FallenAltarGuardian,
                    "lost_villager_001" => RunAdventureEventContextKind.LostVillagerEscort,
                    _ => throw new NotSupportedException($"demo_act.event_unsupported:{eventId}")
                };
                // The current Adapter's event battles all use the canonical N1 encounter.
                encounter = new ContentId("encounter.pure-run.n1");
            }
            else if (node.NodeId is "layer_05_battle" or "layer_06_battle")
            {
                encounter = selector.SelectLateEncounter(seed, node.NodeId);
            }
            else if (node.Kind == PureRunNodeKind.Boss)
            {
                encounter = selector.SelectLateEncounter(seed, node.NodeId, boss: true);
            }
            else if (node.NodeId != "start" && node.Kind == PureRunNodeKind.Battle)
            {
                encounter = node.ContentId;
            }
            bindings.Add(node.NodeId, CreateBinding(node, encounter, context));
        }
        var definition = new PureRunActDefinition(new ContentId("act-01"), 1, map, "start",
            map.Nodes.Single(node => node.Kind == PureRunNodeKind.Boss).NodeId,
            map.Nodes.Select(node => new PureRunNodeSceneDefinition(node.NodeId, bindings[node.NodeId].TemplateId,
                node.NodeId != "start" && node.Kind is PureRunNodeKind.Battle or PureRunNodeKind.Elite or PureRunNodeKind.Boss
                    ? PureRunNodeEntryStrategy.ImmediateCombat : PureRunNodeEntryStrategy.Exploration)));
        return new PureRunActBlueprint(definition, bindings);
    }

    /// <summary>Finite build-time content closure; never discovers variants by sampling seeds.</summary>
    public static IReadOnlyList<PureRunNodeContentBinding> EnumerateContentVariants(PureRunMapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        ValidateCurrentDemoTopology(map);
        var variants = new List<PureRunNodeContentBinding>();
        foreach (PureRunMapNodeDefinition node in map.Nodes)
        {
            if (node.Kind == PureRunNodeKind.Mystery)
            {
                foreach (RunAdventureEventContextKind context in new[]
                {
                    RunAdventureEventContextKind.CursedChestMimic,
                    RunAdventureEventContextKind.FallenAltarGuardian,
                    RunAdventureEventContextKind.LostVillagerEscort
                })
                    variants.Add(CreateBinding(node, new ContentId("encounter.pure-run.n1"), context));
            }
            else if (node.NodeId is "layer_05_battle" or "layer_06_battle")
            {
                foreach (string variant in new[] { "e1", "e2" })
                    variants.Add(CreateBinding(node, new ContentId($"encounter.pure-run.{variant}"), RunAdventureEventContextKind.None));
            }
            else
            {
                ContentId? encounter = node.Kind == PureRunNodeKind.Boss
                    ? new ContentId("encounter.pure-run.special")
                    : node.NodeId != "start" && node.Kind == PureRunNodeKind.Battle ? node.ContentId : null;
                variants.Add(CreateBinding(node, encounter, RunAdventureEventContextKind.None));
            }
        }
        return Array.AsReadOnly(variants.OrderBy(binding => binding.TemplateId.Value, StringComparer.Ordinal).ToArray());
    }

    private static PureRunNodeContentBinding CreateBinding(PureRunMapNodeDefinition node, ContentId? encounter,
        RunAdventureEventContextKind context)
    {
        string suffix = context != RunAdventureEventContextKind.None
            ? "." + context.ToString().ToLowerInvariant()
            : node.NodeId is "layer_05_battle" or "layer_06_battle"
                ? "." + encounter!.Value.Value.Split('.').Last() : string.Empty;
        var template = new ContentId($"adventure-map-template.pure-run.node.{node.NodeId.Replace('_', '-')}{suffix}");
        return new PureRunNodeContentBinding(node.NodeId, template, encounter, context);
    }

    private static void ValidateCurrentDemoTopology(PureRunMapDefinition map)
    {
        var expected = new Dictionary<string, (int Layer, PureRunNodeKind Kind)>(StringComparer.Ordinal)
        {
            ["start"] = (0, PureRunNodeKind.Battle),
            ["layer_01_battle"] = (1, PureRunNodeKind.Battle),
            ["layer_02_battle"] = (2, PureRunNodeKind.Battle),
            ["layer_03_battle"] = (3, PureRunNodeKind.Battle),
            ["layer_05_battle"] = (5, PureRunNodeKind.Elite),
            ["layer_07_battle"] = (7, PureRunNodeKind.Boss)
        };
        (string Suffix, PureRunNodeKind Kind)[] branches =
        [
            ("battle", PureRunNodeKind.Battle), ("rest", PureRunNodeKind.Rest),
            ("store", PureRunNodeKind.Store), ("event", PureRunNodeKind.Mystery),
            ("treasure", PureRunNodeKind.Treasure)
        ];
        foreach (int layer in new[] { 4, 6 })
        foreach ((string suffix, PureRunNodeKind kind) in branches)
            expected.Add($"layer_{layer:00}_{suffix}", (layer, kind));
        if (map.Nodes.Count != expected.Count || map.Nodes.Any(node =>
                !expected.TryGetValue(node.NodeId, out var role) || role.Layer != node.Layer || role.Kind != node.Kind))
            throw new NotSupportedException("demo_act.topology_unsupported: expected the current seven-layer Demo nodes");

        var edges = new HashSet<PureRunMapConnectionDefinition>
        {
            new("start", "layer_01_battle"), new("layer_01_battle", "layer_02_battle"),
            new("layer_02_battle", "layer_03_battle")
        };
        foreach (var (suffix, _) in branches)
        {
            edges.Add(new("layer_03_battle", $"layer_04_{suffix}"));
            edges.Add(new($"layer_04_{suffix}", "layer_05_battle"));
            edges.Add(new("layer_05_battle", $"layer_06_{suffix}"));
            edges.Add(new($"layer_06_{suffix}", "layer_07_battle"));
        }
        if (!edges.SetEquals(map.Connections))
            throw new NotSupportedException("demo_act.topology_unsupported: unexpected Demo route connections");
    }
}
