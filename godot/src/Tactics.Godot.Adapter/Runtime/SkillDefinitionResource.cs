using Godot;
using Tactics.Core.Content;
using Tactics.Core.Skills;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class SkillDefinitionResource : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string SourceId { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
    [Export] public string RoleValue { get; set; } = string.Empty;
    [Export] public string KindValue { get; set; } = string.Empty;
    [Export] public int Level { get; set; }
    [Export] public int ManaCost { get; set; }
    [Export] public int MinRange { get; set; }
    [Export] public int MaxRange { get; set; }
    [Export] public string ExecutionKindValue { get; set; } = string.Empty;
    [Export] public int Damage { get; set; }
    [Export] public string DamageKindValue { get; set; } = string.Empty;
    [Export] public string StatusContentIdValue { get; set; } = string.Empty;
    [Export] public int StatusDuration { get; set; }
    [Export] public bool Hidden { get; set; }
    [Export] public bool ExternalDependency { get; set; }
    [Export] public bool IsBasicAbility { get; set; }
    [Export] public int MaxUsesPerTurn { get; set; }
    [Export] public bool CanCrit { get; set; } = true;
    [Export] public string BranchId { get; set; } = string.Empty;
    [Export] public string PrerequisiteContentIdValue { get; set; } = string.Empty;
    [Export] public string PrerequisiteBranchId { get; set; } = string.Empty;
    [Export] public bool GrowthVisible { get; set; } = true;
    [Export] public string RequiredAttribute { get; set; } = string.Empty;
    [Export] public int MinimumAttribute { get; set; }
    [Export] public int AreaRadius { get; set; }
    [Export] public int OrderedTargetCount { get; set; }
    [Export] public string SummonDefinitionIdValue { get; set; } = string.Empty;
    [Export] public int SummonCount { get; set; }
    [Export] public int SummonLimit { get; set; }
    [Export] public string SummonCategory { get; set; } = string.Empty;
    [Export] public bool RequiresCorpse { get; set; }
    [Export] public bool IgnoreLineOfSight { get; set; }
    [Export] public int ShieldMultiplier { get; set; }
    [Export] public bool ShieldAbsorbsAllDamage { get; set; }
    [Export] public bool CleanseHarmful { get; set; }
    [Export] public int SecondaryDamage { get; set; }
    [Export] public string AreaShape { get; set; } = string.Empty;
    [Export] public int StatusChancePercent { get; set; } = 100;
    [Export] public string DetonateStatusContentIdValue { get; set; } = string.Empty;
    [Export] public int BounceRange { get; set; }
    [Export] public int BounceCount { get; set; }
    [Export] public bool PierceAll { get; set; }
    [Export] public bool AllowsEmptyTarget { get; set; }
    [Export] public int MovementDamagePerCell { get; set; }
    [Export] public string SummonAttackContentIdValue { get; set; } = string.Empty;
    [Export] public int CorruptionCost { get; set; }
    [Export] public string DamageScalingValue { get; set; } = nameof(SkillDamageScalingKind.None);
    [Export] public int LifeStealPercent { get; set; }
    [Export] public string EffectScalingValue { get; set; } = nameof(SkillEffectScalingKind.None);
    [Export] public double AccuracyFactor { get; set; } = 1d;
    [Export] public int KillManaRefund { get; set; }
    [Export] public int RepeatChancePercent { get; set; }
    [Export] public int RepeatDamagePercent { get; set; }
    [Export] public int HealingTickCount { get; set; }
    [Export] public int HealingBase { get; set; }
    [Export] public int AttributeModifier { get; set; }
    [Export] public int DirectHitCharges { get; set; }
    [Export] public int RetreatDistance { get; set; } = 1;
    [Export] public string SourcePath { get; set; } = string.Empty;
    [Export] public string SourceGuid { get; set; } = string.Empty;
    [Export] public long SourceLocalFileId { get; set; }
    [Export] public string GraphPath { get; set; } = string.Empty;
    [Export] public string GraphDependencyHash { get; set; } = string.Empty;
    [Export] public string AuthoringSourceKindValue { get; set; } = "FrozenMigration";
    [Export] public bool PresentationPayloadCopied { get; set; }
    [Export] public bool ThirdPartyPayloadCopied { get; set; }

    public SkillDefinition ToCoreDefinition()
    {
        if (SchemaVersion != 1 || PresentationPayloadCopied || ThirdPartyPayloadCopied)
            throw new InvalidOperationException($"Skill '{ContentIdValue}' violates schema or payload boundary.");
        if (AuthoringSourceKindValue == "FrozenMigration" && !ExternalDependency && ExecutionKindValue != nameof(SkillExecutionKind.CombatTechniques) &&
            (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(SourceGuid) || SourceLocalFileId <= 0 || string.IsNullOrWhiteSpace(GraphPath) || string.IsNullOrWhiteSpace(GraphDependencyHash)))
            throw new InvalidOperationException($"Skill '{ContentIdValue}' has incomplete source audit fields.");
        if (AuthoringSourceKindValue is not ("FrozenMigration" or "GodotAuthored")) throw new InvalidOperationException($"Skill '{ContentIdValue}' has unknown authoring source kind '{AuthoringSourceKindValue}'.");
        if (AuthoringSourceKindValue == "GodotAuthored" && (!string.IsNullOrWhiteSpace(SourceGuid) || SourceLocalFileId != 0)) throw new InvalidOperationException($"Godot-authored skill '{ContentIdValue}' carries Unity audit identity.");
        var profile = new SkillExecutionProfile(AreaRadius, OrderedTargetCount,
            string.IsNullOrEmpty(SummonDefinitionIdValue) ? null : new ContentId(SummonDefinitionIdValue),
            SummonCount, SummonLimit,
            SummonCategory, RequiresCorpse, IgnoreLineOfSight, ShieldMultiplier, ShieldAbsorbsAllDamage,
            CleanseHarmful, SecondaryDamage, AreaShape, StatusChancePercent,
            string.IsNullOrEmpty(DetonateStatusContentIdValue) ? null : new ContentId(DetonateStatusContentIdValue),
            BounceRange, BounceCount, PierceAll, AllowsEmptyTarget, MovementDamagePerCell,
            string.IsNullOrEmpty(SummonAttackContentIdValue) ? null : new ContentId(SummonAttackContentIdValue),
            CorruptionCost, Parse<SkillDamageScalingKind>(DamageScalingValue), LifeStealPercent,
            Parse<SkillEffectScalingKind>(EffectScalingValue), (decimal)AccuracyFactor,
            KillManaRefund, RepeatChancePercent, RepeatDamagePercent, HealingTickCount,
            HealingBase, AttributeModifier, DirectHitCharges, RetreatDistance);
        return new SkillDefinition(new ContentId(ContentIdValue), SourceId, Parse<SkillRole>(RoleValue), Parse<SkillKind>(KindValue), Level, ManaCost, MinRange, MaxRange, Parse<SkillExecutionKind>(ExecutionKindValue), Damage, Parse<SkillDamageKind>(DamageKindValue), string.IsNullOrEmpty(StatusContentIdValue) ? null : new ContentId(StatusContentIdValue), StatusDuration, Hidden, ExternalDependency, IsBasicAbility, MaxUsesPerTurn, BranchId, string.IsNullOrEmpty(PrerequisiteContentIdValue) ? null : new ContentId(PrerequisiteContentIdValue), GrowthVisible, profile, RequiredAttribute, MinimumAttribute, PrerequisiteBranchId, CanCrit);
    }

    private static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse(value, false, out T result) && Enum.IsDefined(result) ? result : throw new InvalidOperationException($"Unknown {typeof(T).Name} '{value}'.");
}
