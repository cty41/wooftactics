using Godot;
using Tactics.Core.Content;
using Tactics.Core.Skills;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record StartingSkillBatchValidation(int BatchCount, int GlobalCount, int GeneratedCount);

/// <summary>Validates the generated starting-skill batch and its external Poison ownership.</summary>
public static class StartingSkillBatchValidator
{
    public static StartingSkillBatchValidation Validate(
        GodotResourceCatalog batchCatalog,
        GodotResourceCatalog globalCatalog)
    {
        ArgumentNullException.ThrowIfNull(batchCatalog);
        ArgumentNullException.ThrowIfNull(globalCatalog);
        batchCatalog.Validate();
        globalCatalog.Validate();
        if (batchCatalog.Entries.Length != 12 || globalCatalog.Entries.Length is not (74 or 101 or 108 or 114 or 115 or 116 or 119 or 123 or 124 or 125 or 131 or 132 or 141 or 142 or 143 or 160 or 161 or 162 or 166 or 185))
            throw new InvalidOperationException("Starting-skill or canonical Catalog entry count is invalid.");
        if (globalCatalog.Entries.Select(entry => entry.ContentIdValue).Distinct(StringComparer.Ordinal).Count() != globalCatalog.Entries.Length)
            throw new InvalidOperationException("Canonical Catalog contains duplicate ContentIds.");

        int generated = 0;
        foreach (GodotResourceEntry entry in batchCatalog.Entries.OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal))
        {
            Resource resource = ResourceLoader.Load(entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException($"Starting-skill Resource is missing: {entry.ContentIdValue}");
            if (entry.ContentIdValue == "skill.poison-spear.lv1")
            {
                if (resource is not PoisonSpearSkillResource || entry.DiagnosticPathValue != "res://content/poison_spear/PoisonSpearSkillLv1.tres")
                    throw new InvalidOperationException("Poison Spear external ownership drifted.");
                continue;
            }
            if (resource is not SkillDefinitionResource definition || definition.ToCoreDefinition().ContentId != new ContentId(entry.ContentIdValue))
                throw new InvalidOperationException($"Starting-skill Resource has the wrong type or ContentId: {entry.ContentIdValue}");
            generated++;
        }
        if (generated != 11)
            throw new InvalidOperationException("Starting-skill batch must generate exactly 11 Resources.");
        return new StartingSkillBatchValidation(12, globalCatalog.Entries.Length, generated);
    }
}
