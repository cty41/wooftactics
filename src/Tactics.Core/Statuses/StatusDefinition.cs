using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Core.Statuses;

/// <summary>
/// Classifies whether a status is removed by harmful-status cleansing.
/// </summary>
public enum StatusPolarity
{
    Beneficial,
    Harmful
}

/// <summary>
/// Selects the deterministic gameplay policy associated with a status.
/// </summary>
public enum StatusEffectKind
{
    None,
    Frozen,
    Marked,
    CurseDamageAmplifier,
    DamageReduction,
    Poison,
    Burning,
    Slow,
    Stun,
    Fear,
    DamageOutputReduction
}

/// <summary>
/// Identifies when a status policy is evaluated.
/// </summary>
public enum StatusTriggerTiming
{
    None,
    TurnStart,
    DamageTaken,
    BeforeAttacked
}

/// <summary>
/// Defines how another application merges into an active status.
/// </summary>
public enum StatusRefreshStrategy
{
    AddDuration,
    RefreshDuration,
    AddStacks
}

public enum StatusElementKind
{
    None,
    Fire,
    Ice,
    Poison,
    Lightning,
    Dark
}

public enum StatusDamageCategory
{
    Physical,
    Magic
}

/// <summary>
/// Stores the engine-neutral authored contract for one battle status.
/// </summary>
public sealed record StatusDefinition
{
    public StatusDefinition(
        ContentId contentId,
        string sourceId,
        int defaultDuration,
        bool canAct,
        StatusPolarity polarity,
        StatusEffectKind effectKind,
        StatusTriggerTiming triggerTiming,
        StatusRefreshStrategy refreshStrategy,
        string curseCategory = "",
        float damagePerTurn = 0f,
        StatusElementKind elementKind = StatusElementKind.None,
        StatusDamageCategory damageCategory = StatusDamageCategory.Magic,
        float speedModifier = 0f,
        float damageReductionPercent = 0f,
        ContentId? meleeRetaliationStatusId = null,
        int meleeRetaliationDuration = 0,
        int initiativeModifier = 0,
        int movementModifier = 0,
        int frozenTotalDamage = 0,
        UnitAttributeModifiers attributeModifiers = default,
        int frozenTotalHealing = 0)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("SourceId cannot be empty.", nameof(sourceId));
        if (frozenTotalDamage < 0)
            throw new ArgumentOutOfRangeException(nameof(frozenTotalDamage));
        if (frozenTotalHealing < 0)
            throw new ArgumentOutOfRangeException(nameof(frozenTotalHealing));
        if (defaultDuration <= 0)
            throw new ArgumentOutOfRangeException(nameof(defaultDuration));
        if (!Enum.IsDefined(polarity))
            throw new ArgumentOutOfRangeException(nameof(polarity));
        if (!Enum.IsDefined(effectKind))
            throw new ArgumentOutOfRangeException(nameof(effectKind));
        if (!Enum.IsDefined(triggerTiming))
            throw new ArgumentOutOfRangeException(nameof(triggerTiming));
        if (!Enum.IsDefined(refreshStrategy))
            throw new ArgumentOutOfRangeException(nameof(refreshStrategy));
        if (!Enum.IsDefined(elementKind))
            throw new ArgumentOutOfRangeException(nameof(elementKind));
        if (!Enum.IsDefined(damageCategory))
            throw new ArgumentOutOfRangeException(nameof(damageCategory));
        if (!float.IsFinite(damagePerTurn) || damagePerTurn < 0f || damagePerTurn != MathF.Truncate(damagePerTurn))
            throw new ArgumentOutOfRangeException(nameof(damagePerTurn));
        if (!float.IsFinite(speedModifier))
            throw new ArgumentOutOfRangeException(nameof(speedModifier));
        if (!float.IsFinite(damageReductionPercent) || damageReductionPercent < 0f || damageReductionPercent > 1f)
            throw new ArgumentOutOfRangeException(nameof(damageReductionPercent));
        if (meleeRetaliationDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(meleeRetaliationDuration));
        if ((meleeRetaliationStatusId is null) != (meleeRetaliationDuration == 0))
        {
            throw new ArgumentException(
                "Melee retaliation status and duration must either both be configured or both be absent.");
        }

        ContentId = contentId;
        SourceId = sourceId.Trim();
        DefaultDuration = defaultDuration;
        CanAct = canAct;
        Polarity = polarity;
        EffectKind = effectKind;
        TriggerTiming = triggerTiming;
        RefreshStrategy = refreshStrategy;
        CurseCategory = curseCategory?.Trim() ?? string.Empty;
        DamagePerTurn = damagePerTurn;
        ElementKind = elementKind;
        DamageCategory = damageCategory;
        SpeedModifier = speedModifier;
        DamageReductionPercent = damageReductionPercent;
        MeleeRetaliationStatusId = meleeRetaliationStatusId;
        MeleeRetaliationDuration = meleeRetaliationDuration;
        InitiativeModifier = initiativeModifier;
        MovementModifier = movementModifier;
        FrozenTotalDamage = frozenTotalDamage;
        AttributeModifiers = attributeModifiers;
        FrozenTotalHealing = frozenTotalHealing;
    }

    public ContentId ContentId { get; }
    public string SourceId { get; }
    public int DefaultDuration { get; }
    public bool CanAct { get; }
    public StatusPolarity Polarity { get; }
    public StatusEffectKind EffectKind { get; }
    public StatusTriggerTiming TriggerTiming { get; }
    public StatusRefreshStrategy RefreshStrategy { get; }
    public string CurseCategory { get; }
    public float DamagePerTurn { get; }
    public StatusElementKind ElementKind { get; }
    public StatusDamageCategory DamageCategory { get; }
    public float SpeedModifier { get; }
    public float DamageReductionPercent { get; }
    public ContentId? MeleeRetaliationStatusId { get; }
    public int MeleeRetaliationDuration { get; }
    public int InitiativeModifier { get; }
    public int MovementModifier { get; }
    public int FrozenTotalDamage { get; }
    public UnitAttributeModifiers AttributeModifiers { get; }
    public int FrozenTotalHealing { get; }
}
