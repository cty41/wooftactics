using System.Text.Json;
using Tactics.Core.Content;
using Tactics.Core.Skills;

namespace Tactics.Application.Authoring;

public enum SkillAuthoringSourceKind { FrozenMigration, GodotAuthored }

public sealed class SkillAuthoringDocument : IAuthoringDocument
{
    public SkillAuthoringDocument(SkillDefinition definition, string displayName, string description,
        string sourcePath, string sourceGuid, long sourceLocalFileId, string graphPath, string graphDependencyHash,
        SkillAuthoringSourceKind sourceKind = SkillAuthoringSourceKind.FrozenMigration)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? throw new ArgumentException("DisplayName is required.") : displayName;
        Description = description ?? string.Empty; SourcePath = sourcePath ?? string.Empty; SourceGuid = sourceGuid ?? string.Empty;
        SourceLocalFileId = sourceLocalFileId; GraphPath = graphPath ?? string.Empty; GraphDependencyHash = graphDependencyHash ?? string.Empty;
        SourceKind = sourceKind;
        if (SourceKind == SkillAuthoringSourceKind.FrozenMigration && !Definition.ExternalDependency && Definition.ExecutionKind != SkillExecutionKind.CombatTechniques &&
            (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(SourceGuid) || SourceLocalFileId <= 0 || string.IsNullOrWhiteSpace(GraphPath) || string.IsNullOrWhiteSpace(GraphDependencyHash)))
            throw new ArgumentException("Non-external skills require complete frozen source audit fields.");
        if (SourceKind == SkillAuthoringSourceKind.GodotAuthored && (!string.IsNullOrWhiteSpace(SourceGuid) || SourceLocalFileId != 0))
            throw new ArgumentException("Godot-authored skills cannot carry Unity GUID/local-file audit identities.");
    }
    public SkillDefinition Definition { get; }
    public string ContentId => Definition.ContentId.Value;
    public int SchemaVersion => 1;
    public string DisplayName { get; }
    public string Description { get; }
    public string SourcePath { get; }
    public string SourceGuid { get; }
    public long SourceLocalFileId { get; }
    public string GraphPath { get; }
    public string GraphDependencyHash { get; }
    public SkillAuthoringSourceKind SourceKind { get; }
    public IReadOnlyList<string> Dependencies => Array.AsReadOnly(new[]
    {
        Definition.StatusContentId?.Value, Definition.PrerequisiteContentId?.Value,
        Definition.ExecutionProfile.DetonateStatusContentId?.Value, Definition.ExecutionProfile.SummonAttackContentId?.Value,
        Definition.ExecutionProfile.SummonDefinitionId?.Value
    }.OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    public void WriteCanonical(Utf8JsonWriter writer)
    {
        SkillDefinition d = Definition; SkillExecutionProfile p = d.ExecutionProfile;
        writer.WriteStartObject(); writer.WriteString("contentId", ContentId); writer.WriteNumber("schemaVersion", SchemaVersion); writer.WriteString("sourceId", d.SourceId); writer.WriteString("displayName", DisplayName); writer.WriteString("description", Description);
        writer.WriteString("role", d.Role.ToString()); writer.WriteString("kind", d.Kind.ToString()); writer.WriteNumber("level", d.Level); writer.WriteNumber("manaCost", d.ManaCost); writer.WriteNumber("minRange", d.MinRange); writer.WriteNumber("maxRange", d.MaxRange); writer.WriteString("executionKind", d.ExecutionKind.ToString()); writer.WriteNumber("damage", d.Damage); writer.WriteString("damageKind", d.DamageKind.ToString());
        Optional(writer, "statusContentId", d.StatusContentId?.Value); writer.WriteNumber("statusDuration", d.StatusDuration); writer.WriteBoolean("hidden", d.Hidden); writer.WriteBoolean("externalDependency", d.ExternalDependency); writer.WriteBoolean("isBasicAbility", d.IsBasicAbility); writer.WriteNumber("maxUsesPerTurn", d.MaxUsesPerTurn); writer.WriteBoolean("canCrit", d.CanCrit); writer.WriteString("branchId", d.BranchId); Optional(writer, "prerequisiteContentId", d.PrerequisiteContentId?.Value); writer.WriteString("prerequisiteBranchId", d.PrerequisiteBranchId); writer.WriteBoolean("growthVisible", d.GrowthVisible); writer.WriteString("requiredAttribute", d.RequiredAttribute); writer.WriteNumber("minimumAttribute", d.MinimumAttribute);
        writer.WriteStartObject("executionProfile"); writer.WriteNumber("areaRadius", p.AreaRadius); writer.WriteNumber("orderedTargetCount", p.OrderedTargetCount); Optional(writer, "summonDefinitionId", p.SummonDefinitionId?.Value); writer.WriteNumber("summonCount", p.SummonCount); writer.WriteNumber("summonLimit", p.SummonLimit); writer.WriteString("summonCategory", p.SummonCategory); writer.WriteBoolean("requiresCorpse", p.RequiresCorpse); writer.WriteBoolean("ignoreLineOfSight", p.IgnoreLineOfSight); writer.WriteNumber("shieldMultiplier", p.ShieldMultiplier); writer.WriteBoolean("shieldAbsorbsAllDamage", p.ShieldAbsorbsAllDamage); writer.WriteBoolean("cleanseHarmful", p.CleanseHarmful); writer.WriteNumber("secondaryDamage", p.SecondaryDamage); writer.WriteString("areaShape", p.AreaShape); writer.WriteNumber("statusChancePercent", p.StatusChancePercent); Optional(writer, "detonateStatusContentId", p.DetonateStatusContentId?.Value); writer.WriteNumber("bounceRange", p.BounceRange); writer.WriteNumber("bounceCount", p.BounceCount); writer.WriteBoolean("pierceAll", p.PierceAll); writer.WriteBoolean("allowsEmptyTarget", p.AllowsEmptyTarget); writer.WriteNumber("movementDamagePerCell", p.MovementDamagePerCell); Optional(writer, "summonAttackContentId", p.SummonAttackContentId?.Value); writer.WriteNumber("corruptionCost", p.CorruptionCost); writer.WriteString("damageScaling", p.DamageScaling.ToString()); writer.WriteNumber("lifeStealPercent", p.LifeStealPercent); writer.WriteString("effectScaling", p.EffectScaling.ToString()); writer.WriteNumber("accuracyFactor", p.AccuracyFactor); writer.WriteNumber("killManaRefund", p.KillManaRefund); writer.WriteNumber("repeatChancePercent", p.RepeatChancePercent); writer.WriteNumber("repeatDamagePercent", p.RepeatDamagePercent); writer.WriteNumber("healingTickCount", p.HealingTickCount); writer.WriteNumber("healingBase", p.HealingBase); writer.WriteNumber("attributeModifier", p.AttributeModifier); writer.WriteNumber("directHitCharges", p.DirectHitCharges); writer.WriteNumber("retreatDistance", p.RetreatDistance); writer.WriteEndObject();
        writer.WriteString("sourceKind", SourceKind.ToString()); writer.WriteString("sourcePath", SourcePath); writer.WriteString("sourceGuid", SourceGuid); writer.WriteNumber("sourceLocalFileId", SourceLocalFileId); writer.WriteString("graphPath", GraphPath); writer.WriteString("graphDependencyHash", GraphDependencyHash); writer.WriteEndObject();
    }
    private static void Optional(Utf8JsonWriter writer, string name, string? value) { writer.WritePropertyName(name); if (string.IsNullOrWhiteSpace(value)) writer.WriteNullValue(); else writer.WriteStringValue(value); }
}

public static class SkillAuthoringJson
{
    public static string Serialize(SkillAuthoringDocument document) { using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true })) document.WriteCanonical(writer); return System.Text.Encoding.UTF8.GetString(stream.ToArray()); }
    public static SkillAuthoringDocument Deserialize(string json)
    {
        using JsonDocument payload = JsonDocument.Parse(json); JsonElement r = payload.RootElement; JsonElement p = r.GetProperty("executionProfile");
        static ContentId? Id(JsonElement parent, string name) => parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? new ContentId(value.GetString()!) : null;
        var profile = new SkillExecutionProfile(p.GetProperty("areaRadius").GetInt32(), p.GetProperty("orderedTargetCount").GetInt32(), Id(p, "summonDefinitionId"), p.GetProperty("summonCount").GetInt32(), p.GetProperty("summonLimit").GetInt32(), p.GetProperty("summonCategory").GetString() ?? string.Empty, p.GetProperty("requiresCorpse").GetBoolean(), p.GetProperty("ignoreLineOfSight").GetBoolean(), p.GetProperty("shieldMultiplier").GetInt32(), p.GetProperty("shieldAbsorbsAllDamage").GetBoolean(), p.GetProperty("cleanseHarmful").GetBoolean(), p.GetProperty("secondaryDamage").GetInt32(), p.GetProperty("areaShape").GetString() ?? string.Empty, p.GetProperty("statusChancePercent").GetInt32(), Id(p, "detonateStatusContentId"), p.GetProperty("bounceRange").GetInt32(), p.GetProperty("bounceCount").GetInt32(), p.GetProperty("pierceAll").GetBoolean(), p.GetProperty("allowsEmptyTarget").GetBoolean(), p.GetProperty("movementDamagePerCell").GetInt32(), Id(p, "summonAttackContentId"), p.TryGetProperty("corruptionCost", out JsonElement corruption) ? corruption.GetInt32() : 0, p.TryGetProperty("damageScaling", out JsonElement scaling) ? Enum.Parse<SkillDamageScalingKind>(scaling.GetString()!) : SkillDamageScalingKind.None, p.TryGetProperty("lifeStealPercent", out JsonElement lifeSteal) ? lifeSteal.GetInt32() : 0, p.TryGetProperty("effectScaling", out JsonElement effectScaling) ? Enum.Parse<SkillEffectScalingKind>(effectScaling.GetString()!) : SkillEffectScalingKind.None, p.TryGetProperty("accuracyFactor", out JsonElement accuracyFactor) ? accuracyFactor.GetDecimal() : 1m, p.TryGetProperty("killManaRefund", out JsonElement killManaRefund) ? killManaRefund.GetInt32() : 0, p.TryGetProperty("repeatChancePercent", out JsonElement repeatChance) ? repeatChance.GetInt32() : 0, p.TryGetProperty("repeatDamagePercent", out JsonElement repeatDamage) ? repeatDamage.GetInt32() : 0, p.TryGetProperty("healingTickCount", out JsonElement healingTicks) ? healingTicks.GetInt32() : 0, p.TryGetProperty("healingBase", out JsonElement healingBase) ? healingBase.GetInt32() : 0, p.TryGetProperty("attributeModifier", out JsonElement attributeModifier) ? attributeModifier.GetInt32() : 0, p.TryGetProperty("directHitCharges", out JsonElement directHitCharges) ? directHitCharges.GetInt32() : 0, p.TryGetProperty("retreatDistance", out JsonElement retreatDistance) ? retreatDistance.GetInt32() : 1);
        var definition = new SkillDefinition(new ContentId(r.GetProperty("contentId").GetString()!), r.GetProperty("sourceId").GetString()!, Enum.Parse<SkillRole>(r.GetProperty("role").GetString()!), Enum.Parse<SkillKind>(r.GetProperty("kind").GetString()!), r.GetProperty("level").GetInt32(), r.GetProperty("manaCost").GetInt32(), r.GetProperty("minRange").GetInt32(), r.GetProperty("maxRange").GetInt32(), Enum.Parse<SkillExecutionKind>(r.GetProperty("executionKind").GetString()!), r.GetProperty("damage").GetInt32(), Enum.Parse<SkillDamageKind>(r.GetProperty("damageKind").GetString()!), Id(r, "statusContentId"), r.GetProperty("statusDuration").GetInt32(), r.GetProperty("hidden").GetBoolean(), r.GetProperty("externalDependency").GetBoolean(), r.GetProperty("isBasicAbility").GetBoolean(), r.GetProperty("maxUsesPerTurn").GetInt32(), r.GetProperty("branchId").GetString()!, Id(r, "prerequisiteContentId"), r.GetProperty("growthVisible").GetBoolean(), profile, r.GetProperty("requiredAttribute").GetString()!, r.GetProperty("minimumAttribute").GetInt32(), r.GetProperty("prerequisiteBranchId").GetString()!, r.GetProperty("canCrit").GetBoolean());
        SkillAuthoringSourceKind sourceKind = r.TryGetProperty("sourceKind", out JsonElement sourceKindValue) ? Enum.Parse<SkillAuthoringSourceKind>(sourceKindValue.GetString()!) : SkillAuthoringSourceKind.FrozenMigration;
        return new SkillAuthoringDocument(definition, r.GetProperty("displayName").GetString()!, r.GetProperty("description").GetString() ?? string.Empty, r.GetProperty("sourcePath").GetString() ?? string.Empty, r.GetProperty("sourceGuid").GetString() ?? string.Empty, r.GetProperty("sourceLocalFileId").GetInt64(), r.GetProperty("graphPath").GetString() ?? string.Empty, r.GetProperty("graphDependencyHash").GetString() ?? string.Empty, sourceKind);
    }
}
