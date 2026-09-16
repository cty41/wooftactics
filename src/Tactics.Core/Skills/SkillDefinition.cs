using Tactics.Core.Content;
using Tactics.Core.Statuses;

namespace Tactics.Core.Skills;

public enum SkillRole { Any, Mage, Necromancer, Amazon, Demonbound, Poet }
public enum SkillKind { Basic, Active, Passive, Utility }
public enum SkillDamageKind { None, Physical, Magical }
public enum SkillDamageScalingKind { None, PrimaryAttributeAboveNeutral }
public enum SkillEffectScalingKind { None, MeleePhysical, RangedPhysical, Magical, Healing, Shield, DamageOverTime }
public enum SkillExecutionKind
{
    MagicAttack,
    MeleeAttack,
    Fireball,
    IceBolt,
    Lightning,
    SummonSkeleton,
    AmplifyDamage,
    BoneSpear,
    Thrust,
    PoisonSpear,
    CombatTechniques,
    PickupSpear,
    RangedAttack,
    ChargeStrike,
    HeavyShot,
    AreaBlast,
    SummonFireDemon,
    IceArmor,
    Teleport,
    SummonSkeletonMage,
    FearCurse,
    BoneShield,
    MultiStab,
    RecoverSpear,
    Decoy,
    FireDemonAttack,
    Meditation,
    Mindfulness,
    Bane,
    Cleave,
    InfernalBlast,
    Hellfire,
    DemonicRegeneration,
    DirectAttack,
    PoetCharge,
    PoetSwordRain,
    PoetWineHeal,
    PoetAgilityVerse,
    PoetMoonDrink,
    PoetDecoyRetreat
}

/// <summary>Optional normalized parameters used by the complete Pure Run Lv1/Lv2 skill set.</summary>
public sealed record SkillExecutionProfile(
    int AreaRadius = 0,
    int OrderedTargetCount = 0,
    ContentId? SummonDefinitionId = null,
    int SummonCount = 0,
    int SummonLimit = 0,
    string SummonCategory = "",
    bool RequiresCorpse = false,
    bool IgnoreLineOfSight = false,
    int ShieldMultiplier = 0,
    bool ShieldAbsorbsAllDamage = false,
    bool CleanseHarmful = false,
    int SecondaryDamage = 0,
    string AreaShape = "",
    int StatusChancePercent = 100,
    ContentId? DetonateStatusContentId = null,
    int BounceRange = 0,
    int BounceCount = 0,
    bool PierceAll = false,
    bool AllowsEmptyTarget = false,
    int MovementDamagePerCell = 0,
    ContentId? SummonAttackContentId = null,
    int CorruptionCost = 0,
    SkillDamageScalingKind DamageScaling = SkillDamageScalingKind.None,
    int LifeStealPercent = 0,
    SkillEffectScalingKind EffectScaling = SkillEffectScalingKind.None,
    decimal AccuracyFactor = 1m,
    int KillManaRefund = 0,
    int RepeatChancePercent = 0,
    int RepeatDamagePercent = 0,
    int HealingTickCount = 0,
    int HealingBase = 0,
    int AttributeModifier = 0,
    int DirectHitCharges = 0,
    int RetreatDistance = 1);

/// <summary>Normalized engine-neutral execution contract for one migrated skill level.</summary>
public sealed record SkillDefinition
{
    public SkillDefinition(
        ContentId contentId,
        string sourceId,
        SkillRole role,
        SkillKind kind,
        int level,
        int manaCost,
        int minRange,
        int maxRange,
        SkillExecutionKind executionKind,
        int damage,
        SkillDamageKind damageKind,
        ContentId? statusContentId = null,
        int statusDuration = 0,
        bool hidden = false,
        bool externalDependency = false,
        bool? isBasicAbility = null,
        int maxUsesPerTurn = 0,
        string branchId = "",
        ContentId? prerequisiteContentId = null,
        bool growthVisible = true,
        SkillExecutionProfile? executionProfile = null,
        string requiredAttribute = "",
        int minimumAttribute = 0,
        string prerequisiteBranchId = "",
        bool canCrit = true)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("SourceId cannot be empty.", nameof(sourceId));
        if (!Enum.IsDefined(role) || !Enum.IsDefined(kind) || !Enum.IsDefined(executionKind) || !Enum.IsDefined(damageKind)) throw new ArgumentOutOfRangeException(nameof(executionKind));
        if (level <= 0 || manaCost < 0 || minRange < 0 || maxRange < minRange || damage < 0 || maxUsesPerTurn < 0 || minimumAttribute < 0) throw new ArgumentOutOfRangeException(nameof(level));
        if ((statusContentId is null) != (statusDuration == 0)) throw new ArgumentException("Status identity and duration must be configured together.");
        ContentId = contentId;
        SourceId = sourceId.Trim();
        Role = role;
        Kind = kind;
        Level = level;
        ManaCost = manaCost;
        MinRange = minRange;
        MaxRange = maxRange;
        ExecutionKind = executionKind;
        Damage = damage;
        DamageKind = damageKind;
        StatusContentId = statusContentId;
        StatusDuration = statusDuration;
        Hidden = hidden;
        ExternalDependency = externalDependency;
        IsBasicAbility = isBasicAbility ?? kind == SkillKind.Basic;
        MaxUsesPerTurn = maxUsesPerTurn;
        BranchId = string.IsNullOrWhiteSpace(branchId) ? contentId.Value : branchId.Trim();
        PrerequisiteContentId = prerequisiteContentId;
        GrowthVisible = growthVisible;
        ExecutionProfile = executionProfile ?? new SkillExecutionProfile();
        if (ExecutionProfile.StatusChancePercent is < 0 or > 100 || ExecutionProfile.BounceRange < 0 ||
            ExecutionProfile.BounceCount < 0 || ExecutionProfile.MovementDamagePerCell < 0 ||
            ExecutionProfile.CorruptionCost < 0 || !Enum.IsDefined(ExecutionProfile.DamageScaling) ||
            !Enum.IsDefined(ExecutionProfile.EffectScaling) || ExecutionProfile.AccuracyFactor <= 0m)
            throw new ArgumentOutOfRangeException(nameof(executionProfile));
        if (ExecutionProfile.LifeStealPercent is < 0 or > 100 ||
            ExecutionProfile.RepeatChancePercent is < 0 or > 100 ||
            ExecutionProfile.RepeatDamagePercent is < 0 or > 100 ||
            ExecutionProfile.KillManaRefund < 0 || ExecutionProfile.HealingTickCount < 0 ||
            ExecutionProfile.HealingBase < 0 || ExecutionProfile.AttributeModifier < 0 ||
            ExecutionProfile.DirectHitCharges < 0 || ExecutionProfile.RetreatDistance <= 0)
            throw new ArgumentOutOfRangeException(nameof(executionProfile));
        if (executionKind == SkillExecutionKind.PoetWineHeal && ExecutionProfile.HealingTickCount <= 0 ||
            executionKind == SkillExecutionKind.PoetAgilityVerse && ExecutionProfile.AttributeModifier <= 0 ||
            executionKind == SkillExecutionKind.PoetDecoyRetreat && ExecutionProfile.DirectHitCharges <= 0 ||
            executionKind == SkillExecutionKind.PoetSwordRain &&
            (ExecutionProfile.RepeatChancePercent == 0) != (ExecutionProfile.RepeatDamagePercent == 0))
            throw new ArgumentException("Poet execution profile is incomplete.", nameof(executionProfile));
        RequiredAttribute = requiredAttribute.Trim();
        MinimumAttribute = minimumAttribute;
        PrerequisiteBranchId = prerequisiteBranchId.Trim();
        CanCrit = canCrit;
    }

    public ContentId ContentId { get; }
    public string SourceId { get; }
    public SkillRole Role { get; }
    public SkillKind Kind { get; }
    public int Level { get; }
    public int ManaCost { get; }
    public int MinRange { get; }
    public int MaxRange { get; }
    public SkillExecutionKind ExecutionKind { get; }
    public int Damage { get; }
    public SkillDamageKind DamageKind { get; }
    public ContentId? StatusContentId { get; }
    public int StatusDuration { get; }
    public bool Hidden { get; }
    public bool ExternalDependency { get; }
    public bool IsBasicAbility { get; }
    public int MaxUsesPerTurn { get; }
    public string BranchId { get; }
    public ContentId? PrerequisiteContentId { get; }
    public bool GrowthVisible { get; }
    public SkillExecutionProfile ExecutionProfile { get; }
    public string RequiredAttribute { get; }
    public int MinimumAttribute { get; }
    public string PrerequisiteBranchId { get; }
    public bool CanCrit { get; }
    public bool IsPassive => Kind == SkillKind.Passive;
    public bool IsDirectAttack => ExecutionKind is SkillExecutionKind.MagicAttack or SkillExecutionKind.MeleeAttack or
        SkillExecutionKind.Fireball or SkillExecutionKind.IceBolt or SkillExecutionKind.Lightning or
        SkillExecutionKind.BoneSpear or SkillExecutionKind.RangedAttack or SkillExecutionKind.DirectAttack or
        SkillExecutionKind.FireDemonAttack or SkillExecutionKind.MultiStab or SkillExecutionKind.Thrust or
        SkillExecutionKind.ChargeStrike or SkillExecutionKind.HeavyShot or SkillExecutionKind.PoisonSpear or
        SkillExecutionKind.PoetCharge;
    public int AreaRadius => ExecutionProfile.AreaRadius > 0 ? ExecutionProfile.AreaRadius : ExecutionKind == SkillExecutionKind.AreaBlast ? 2 : 0;
    public bool UsesLineTargeting => ExecutionKind is SkillExecutionKind.Fireball or SkillExecutionKind.IceBolt or SkillExecutionKind.BoneSpear or SkillExecutionKind.Thrust or SkillExecutionKind.PoetCharge;
    public bool RequiresLineOfSight => !ExecutionProfile.IgnoreLineOfSight &&
        ExecutionKind is (SkillExecutionKind.MagicAttack or SkillExecutionKind.Fireball or SkillExecutionKind.IceBolt or SkillExecutionKind.BoneSpear or SkillExecutionKind.RangedAttack or SkillExecutionKind.HeavyShot or SkillExecutionKind.ChargeStrike or SkillExecutionKind.FireDemonAttack or SkillExecutionKind.PoetSwordRain);
}

public sealed class SkillCatalogDefinition
{
    private readonly IReadOnlyDictionary<ContentId, SkillDefinition> _definitions;

    public SkillCatalogDefinition(IEnumerable<SkillDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        SkillDefinition[] values = definitions.OrderBy(item => item.ContentId.Value, StringComparer.Ordinal).ToArray();
        if (values.Length == 0 || values.Select(item => item.ContentId).Distinct().Count() != values.Length)
            throw new ArgumentException("Skill Catalog must contain unique definitions.", nameof(definitions));
        _definitions = values.ToDictionary(item => item.ContentId);
    }

    public IReadOnlyDictionary<ContentId, SkillDefinition> Definitions => _definitions;
    public SkillDefinition Get(ContentId contentId) => _definitions.TryGetValue(contentId, out SkillDefinition? value) ? value : throw new KeyNotFoundException(contentId.Value);
}
