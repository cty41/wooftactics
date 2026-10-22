using System.Collections.ObjectModel;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record GodotResolvedActContent(PureRunActBlueprint Blueprint,
    IReadOnlyDictionary<ContentId, AdventureMapTemplateResource> Templates);

/// <summary>Loads only the current Act's resolved templates; missing formal content never becomes a placeholder.</summary>
public static class GodotActContentCatalog
{
    public const string CatalogPath = "res://content/adventure_maps/ContentCatalog.tres";

    public static GodotResolvedActContent Load(PureRunMapDefinition map, int seed) =>
        Load(PureRunActBlueprint.Resolve(map, seed));

    // The caller must first validate saved binding evidence in Application. Never discard a restored
    // blueprint and regenerate its current node from the default seed during resource preparation.
    public static GodotResolvedActContent Load(PureRunActBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        // Match the canonical runtime boundary: fresh typed resources plus exact path/type diagnostics.
        // Loading happens only during Act preparation, not when selecting a resident node.
        GodotResourceCatalog catalog = RequiredResourceLoader.Load<GodotResourceCatalog>(CatalogPath, "act.catalog_missing");
        catalog.Validate();
        var entries = catalog.Entries.ToDictionary(entry => entry.ContentIdValue, StringComparer.Ordinal);
        var templates = new Dictionary<ContentId, AdventureMapTemplateResource>();
        foreach (PureRunNodeContentBinding binding in blueprint.Bindings.Values)
        {
            if (templates.ContainsKey(binding.TemplateId)) continue;
            if (!entries.TryGetValue(binding.TemplateId.Value, out GodotResourceEntry? entry) ||
                entry.ResourceTypeIdValue != "adventure-map-template")
                throw new InvalidOperationException($"act.template_missing:{binding.NodeId}:{binding.TemplateId}");
            AdventureMapTemplateResource template = RequiredResourceLoader.Load<AdventureMapTemplateResource>(
                entry.ResourceLocator, $"act.template_unloadable:{binding.TemplateId}");
            if (template.ToCoreDefinition().ContentId != binding.TemplateId)
                throw new InvalidOperationException($"act.template_identity_mismatch:{binding.NodeId}");
            templates.Add(binding.TemplateId, template);
        }
        return new GodotResolvedActContent(blueprint,
            new ReadOnlyDictionary<ContentId, AdventureMapTemplateResource>(templates));
    }
}
