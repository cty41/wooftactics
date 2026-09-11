using Tactics.Core.Content;
using Tactics.Core.Statuses;
using Tactics.Core.Units;

namespace Tactics.Core.Battle;

/// <summary>
/// Stores immutable runtime state for one status instance.
/// </summary>
/// <remarks>
/// The source and tick value are captured when the status is applied so later content reloads cannot
/// retroactively change an in-progress battle or replay.
/// </remarks>
public sealed record BattleStatusState
{
    public BattleStatusState(
        ContentId contentId,
        UnitInstanceId sourceId,
        int remainingTurns,
        int damagePerTurn,
        int stackCount = 1,
        bool canAct = true,
        StatusPolarity polarity = StatusPolarity.Harmful,
        StatusEffectKind effectKind = StatusEffectKind.None,
        StatusTriggerTiming triggerTiming = StatusTriggerTiming.None,
        StatusRefreshStrategy refreshStrategy = StatusRefreshStrategy.AddDuration,
        string curseCategory = "",
        float speedModifier = 0f,
        float damageReductionPercent = 0f,
        ContentId? meleeRetaliationStatusId = null,
        int meleeRetaliationDuration = 0,
        int initiativeModifier = 0,
        int movementModifier = 0,
        int frozenTotalDamageRemaining = 0,
        UnitAttributeModifiers attributeModifiers = default,
        int frozenTotalHealingRemaining = 0)
    {
        if (remainingTurns <= 0)
            throw new ArgumentOutOfRangeException(nameof(remainingTurns));
        if (damagePerTurn < 0)
            throw new ArgumentOutOfRangeException(nameof(damagePerTurn));
        if (frozenTotalDamageRemaining < 0)
            throw new ArgumentOutOfRangeException(nameof(frozenTotalDamageRemaining));
        if (frozenTotalHealingRemaining < 0)
            throw new ArgumentOutOfRangeException(nameof(frozenTotalHealingRemaining));
        if (stackCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(stackCount));
        if (!Enum.IsDefined(polarity))
            throw new ArgumentOutOfRangeException(nameof(polarity));
        if (!Enum.IsDefined(effectKind))
            throw new ArgumentOutOfRangeException(nameof(effectKind));
        if (!Enum.IsDefined(triggerTiming))
            throw new ArgumentOutOfRangeException(nameof(triggerTiming));
        if (!Enum.IsDefined(refreshStrategy))
            throw new ArgumentOutOfRangeException(nameof(refreshStrategy));
        if (!float.IsFinite(speedModifier))
            throw new ArgumentOutOfRangeException(nameof(speedModifier));
        if (!float.IsFinite(damageReductionPercent) || damageReductionPercent < 0f || damageReductionPercent > 1f)
            throw new ArgumentOutOfRangeException(nameof(damageReductionPercent));
        if (meleeRetaliationDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(meleeRetaliationDuration));
        if ((meleeRetaliationStatusId is null) != (meleeRetaliationDuration == 0))
            throw new ArgumentException("Melee retaliation status and duration must be configured together.");

        ContentId = contentId;
        SourceId = sourceId;
        RemainingTurns = remainingTurns;
        DamagePerTurn = damagePerTurn;
        StackCount = stackCount;
        CanAct = canAct;
        Polarity = polarity;
        EffectKind = effectKind;
        TriggerTiming = triggerTiming;
        RefreshStrategy = refreshStrategy;
        CurseCategory = curseCategory?.Trim() ?? string.Empty;
        SpeedModifier = speedModifier;
        DamageReductionPercent = damageReductionPercent;
        MeleeRetaliationStatusId = meleeRetaliationStatusId;
        MeleeRetaliationDuration = meleeRetaliationDuration;
        InitiativeModifier = initiativeModifier;
        MovementModifier = movementModifier;
        FrozenTotalDamageRemaining = frozenTotalDamageRemaining;
        AttributeModifiers = attributeModifiers;
        FrozenTotalHealingRemaining = frozenTotalHealingRemaining;
    }

    public ContentId ContentId { get; }
    public UnitInstanceId SourceId { get; }
    public int RemainingTurns { get; }
    public int DamagePerTurn { get; }
    public int StackCount { get; }
    public bool CanAct { get; }
    public StatusPolarity Polarity { get; }
    public StatusEffectKind EffectKind { get; }
    public StatusTriggerTiming TriggerTiming { get; }
    public StatusRefreshStrategy RefreshStrategy { get; }
    public string CurseCategory { get; }
    public float SpeedModifier { get; }
    public float DamageReductionPercent { get; }
    public ContentId? MeleeRetaliationStatusId { get; }
    public int MeleeRetaliationDuration { get; }
    public int InitiativeModifier { get; }
    public int MovementModifier { get; }
    public int FrozenTotalDamageRemaining { get; }
    public UnitAttributeModifiers AttributeModifiers { get; }
    public int FrozenTotalHealingRemaining { get; }

    public BattleStatusState WithRemainingTurns(int remainingTurns) =>
        Copy(remainingTurns, StackCount);

    public BattleStatusState WithStackCount(int stackCount) =>
        Copy(RemainingTurns, stackCount);

    public BattleStatusState WithFrozenTotalDamageRemaining(int value) => new(
        ContentId, SourceId, RemainingTurns, DamagePerTurn, StackCount, CanAct, Polarity, EffectKind,
        TriggerTiming, RefreshStrategy, CurseCategory, SpeedModifier, DamageReductionPercent,
        MeleeRetaliationStatusId, MeleeRetaliationDuration, InitiativeModifier, MovementModifier,
        Math.Max(0, value), AttributeModifiers, FrozenTotalHealingRemaining);

    public BattleStatusState WithAttributeModifiers(UnitAttributeModifiers modifiers) => new(
        ContentId, SourceId, RemainingTurns, DamagePerTurn, StackCount, CanAct, Polarity, EffectKind,
        TriggerTiming, RefreshStrategy, CurseCategory, SpeedModifier, DamageReductionPercent,
        MeleeRetaliationStatusId, MeleeRetaliationDuration, InitiativeModifier, MovementModifier,
        FrozenTotalDamageRemaining, modifiers, FrozenTotalHealingRemaining);

    public BattleStatusState WithFrozenTotalHealingRemaining(int value) => new(
        ContentId, SourceId, RemainingTurns, DamagePerTurn, StackCount, CanAct, Polarity, EffectKind,
        TriggerTiming, RefreshStrategy, CurseCategory, SpeedModifier, DamageReductionPercent,
        MeleeRetaliationStatusId, MeleeRetaliationDuration, InitiativeModifier, MovementModifier,
        FrozenTotalDamageRemaining, AttributeModifiers, Math.Max(0, value));

    private BattleStatusState Copy(int remainingTurns, int stackCount) => new(
        ContentId,
        SourceId,
        remainingTurns,
        DamagePerTurn,
        stackCount,
        CanAct,
        Polarity,
        EffectKind,
        TriggerTiming,
        RefreshStrategy,
        CurseCategory,
        SpeedModifier,
        DamageReductionPercent,
        MeleeRetaliationStatusId,
        MeleeRetaliationDuration,
        InitiativeModifier,
        MovementModifier,
        FrozenTotalDamageRemaining,
        AttributeModifiers,
        FrozenTotalHealingRemaining);
}
