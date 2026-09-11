using Tactics.Core.Battle;
using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Core.Statuses;

public sealed record StatusApplicationResult(
    BattleUnitState Unit,
    BattleStatusState AppliedStatus,
    IReadOnlyList<ContentId> ReplacedStatusIds);

public sealed record StatusBeforeAttackPolicy(bool ForceCritical);

public sealed record StatusRetaliationPolicy(
    ContentId SourceStatusId,
    UnitInstanceId SourceUnitId,
    UnitInstanceId TargetUnitId,
    ContentId AppliedStatusId,
    int Duration);

public sealed record StatusDamagePolicy(
    float DamageMultiplier,
    bool CounterRequested,
    IReadOnlyList<StatusRetaliationPolicy> Retaliations);

public sealed record FearMovementPolicy(
    bool MovementConsumed,
    UnitInstanceId SourceUnitId,
    UnitInstanceId TargetUnitId,
    ContentId StatusId);

public sealed record StatusRuntimeSnapshot(
    IReadOnlyList<BattleStatusState> Statuses,
    float EffectiveSpeed,
    int MoveRange,
    float Initiative,
    bool CanAct);

/// <summary>
/// Applies immutable status transitions and exposes typed policies for future attack and AI commands.
/// </summary>
public sealed class StatusRuntimeService
{
    public const string ContractId = "status-runtime-v1";

    public bool CanAct(BattleUnitState unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return unit.Statuses.Values.All(status => status.CanAct);
    }

    public StatusRuntimeSnapshot Capture(BattleUnitState unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        BattleStatusState[] statuses = unit.Statuses.Values
            .OrderBy(status => status.ContentId.Value, StringComparer.Ordinal)
            .ToArray();
        return new StatusRuntimeSnapshot(
            Array.AsReadOnly(statuses),
            unit.Unit.Initiative * 0.5f,
            unit.Unit.MoveRange,
            unit.Unit.Initiative,
            CanAct(unit));
    }

    public StatusApplicationResult Apply(
        BattleUnitState unit,
        StatusDefinition definition,
        UnitInstanceId sourceId,
        int? duration = null)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(definition);
        int appliedAmount = duration ?? definition.DefaultDuration;
        if (appliedAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));

        StatusRefreshStrategy strategy = ResolveRefreshStrategy(definition);
        BattleStatusState? active = unit.Statuses.Values.FirstOrDefault(status =>
            IsSameRuntimeStatus(status, definition));
        var replaced = new List<ContentId>();
        BattleStatusState result;

        if (active is not null)
        {
            result = strategy switch
            {
                StatusRefreshStrategy.RefreshDuration => CreateState(definition, sourceId, appliedAmount, active.StackCount)
                    .WithAttributeModifiers(MergeStrongest(active.AttributeModifiers,
                        definition.AttributeModifiers, definition.Polarity)),
                StatusRefreshStrategy.AddStacks => CreateState(
                    definition,
                    sourceId,
                    active.RemainingTurns,
                    SaturatingAdd(active.StackCount, appliedAmount)),
                _ => CreateState(
                    definition,
                    sourceId,
                    SaturatingAdd(active.RemainingTurns, appliedAmount),
                    active.StackCount).WithFrozenTotalDamageRemaining(
                        SaturatingAdd(active.FrozenTotalDamageRemaining, definition.FrozenTotalDamage))
            };
            if (active.ContentId != result.ContentId)
            {
                unit = unit.WithoutStatus(active.ContentId);
                replaced.Add(active.ContentId);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(definition.CurseCategory))
            {
                foreach (BattleStatusState status in unit.Statuses.Values
                             .Where(status => status.CurseCategory == definition.CurseCategory)
                             .OrderBy(status => status.ContentId.Value, StringComparer.Ordinal))
                {
                    unit = unit.WithoutStatus(status.ContentId);
                    replaced.Add(status.ContentId);
                }
            }
            result = CreateState(
                definition,
                sourceId,
                definition.EffectKind == StatusEffectKind.Burning ? definition.DefaultDuration : appliedAmount,
                definition.EffectKind == StatusEffectKind.Burning ? appliedAmount : 1);
        }

        return new StatusApplicationResult(
            RecalculateSpeed(unit.WithStatus(result)),
            result,
            replaced.AsReadOnly());
    }

    public BattleUnitState Remove(BattleUnitState unit, ContentId statusId) =>
        RecalculateSpeed(unit.WithoutStatus(statusId));

    public BattleUnitState RemoveHarmful(BattleUnitState unit, out IReadOnlyList<ContentId> removedStatusIds)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ContentId[] removed = unit.Statuses.Values
            .Where(status => status.Polarity == StatusPolarity.Harmful)
            .Select(status => status.ContentId)
            .OrderBy(statusId => statusId.Value, StringComparer.Ordinal)
            .ToArray();
        BattleUnitState updated = removed.Aggregate(unit, (current, statusId) => current.WithoutStatus(statusId));
        removedStatusIds = Array.AsReadOnly(removed);
        return RecalculateSpeed(updated);
    }

    public BattleUnitState RecalculateSpeed(BattleUnitState unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        UnitAttributeModifiers modifiers = unit.Statuses.Values
            .Select(status => status.AttributeModifiers)
            .Aggregate(default(UnitAttributeModifiers), (total, value) => total + value);
        UnitAttributes effective = modifiers.Apply(unit.Unit.BaseAttributes);
        UnitDerivedStats baseDerived = UnitDerivedStatRules.Calculate(unit.Unit.BaseAttributes);
        UnitDerivedStats previousFormula = UnitDerivedStatRules.Calculate(unit.Unit.EffectiveAttributes);
        int movementTraitModifier = unit.Unit.BaseMoveRange - baseDerived.MoveRange;
        UnitDerivedStats projected = UnitDerivedStatRules.Calculate(effective, movementTraitModifier);
        int moveModifier = unit.Statuses.Values.Sum(status => status.EffectKind == StatusEffectKind.Slow
            ? -1 : status.MovementModifier);
        int initiativeModifier = unit.Statuses.Values.Sum(status => status.EffectKind == StatusEffectKind.Slow
            ? -4 : status.InitiativeModifier);
        int projectedMaxHealth = Math.Max(1,
            checked(unit.MaxHealth + projected.MaxHealth - previousFormula.MaxHealth));
        int projectedMaxMana = Math.Max(0,
            checked(unit.MaxMana + projected.MaxMana - previousFormula.MaxMana));
        var finalDerived = new UnitDerivedStats(
            projectedMaxHealth,
            projectedMaxMana,
            Math.Min(projected.StartingMana, projectedMaxMana),
            Math.Clamp(projected.MoveRange + moveModifier, 2, 5),
            Math.Max(0f, unit.Unit.BaseInitiative +
                (projected.Initiative - baseDerived.Initiative) + initiativeModifier));
        int manaRecovery = Math.Max(0, checked(unit.ManaRecoveryPerTurn +
            effective.Intelligence - unit.Unit.EffectiveAttributes.Intelligence));
        return unit.WithAttributeProjection(effective, finalDerived, manaRecovery);
    }

    public StatusBeforeAttackPolicy EvaluateBeforeAttack(BattleUnitState target)
    {
        ArgumentNullException.ThrowIfNull(target);
        bool forceCritical = target.Statuses.Values.Any(status =>
            status.TriggerTiming == StatusTriggerTiming.BeforeAttacked ||
            status.EffectKind == StatusEffectKind.Marked);
        return new StatusBeforeAttackPolicy(forceCritical);
    }

    public StatusDamagePolicy EvaluateDamageTaken(
        BattleUnitState target,
        BattleUnitState attacker,
        bool isRangedDamage)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attacker);
        float multiplier = 1f;
        bool adjacentEnemy = target.Unit.InstanceId != attacker.Unit.InstanceId &&
                             target.Unit.PlayerNumber != attacker.Unit.PlayerNumber &&
                             Manhattan(target, attacker) <= 1;
        bool counter = false;
        var retaliation = new List<StatusRetaliationPolicy>();

        foreach (BattleStatusState status in target.Statuses.Values
                     .OrderBy(value => value.ContentId.Value, StringComparer.Ordinal))
        {
            if (status.EffectKind == StatusEffectKind.CurseDamageAmplifier)
                multiplier *= 1.3f;
            if (status.EffectKind == StatusEffectKind.DamageReduction)
                multiplier *= Math.Clamp(1f - status.DamageReductionPercent, 0f, 1f);
            if (!isRangedDamage && adjacentEnemy && status.TriggerTiming == StatusTriggerTiming.DamageTaken)
                counter = true;
            if (!isRangedDamage && adjacentEnemy && status.MeleeRetaliationStatusId is ContentId retaliationId &&
                status.MeleeRetaliationDuration > 0)
            {
                retaliation.Add(new StatusRetaliationPolicy(
                    status.ContentId,
                    target.Unit.InstanceId,
                    attacker.Unit.InstanceId,
                    retaliationId,
                    status.MeleeRetaliationDuration));
            }
        }

        return new StatusDamagePolicy(multiplier, counter, retaliation.AsReadOnly());
    }

    public FearMovementPolicy? EvaluateFearMovement(BattleUnitState target)
    {
        ArgumentNullException.ThrowIfNull(target);
        BattleStatusState? fear = target.Statuses.Values
            .Where(status => status.EffectKind == StatusEffectKind.Fear)
            .OrderBy(status => status.ContentId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        return fear is null
            ? null
            : new FearMovementPolicy(
                MovementConsumed: true,
                fear.SourceId,
                target.Unit.InstanceId,
                fear.ContentId);
    }

    public static StatusRefreshStrategy ResolveRefreshStrategy(StatusDefinition definition) =>
        definition.EffectKind switch
        {
            StatusEffectKind.Burning => StatusRefreshStrategy.AddStacks,
            StatusEffectKind.Poison => StatusRefreshStrategy.AddDuration,
            StatusEffectKind.Slow or StatusEffectKind.Stun => StatusRefreshStrategy.RefreshDuration,
            _ => definition.RefreshStrategy
        };

    private static bool IsSameRuntimeStatus(BattleStatusState active, StatusDefinition definition) =>
        active.ContentId == definition.ContentId ||
        (active.EffectKind == definition.EffectKind && definition.EffectKind is
            StatusEffectKind.Burning or StatusEffectKind.Poison or StatusEffectKind.Slow or StatusEffectKind.Stun);

    private static BattleStatusState CreateState(
        StatusDefinition definition,
        UnitInstanceId sourceId,
        int remainingTurns,
        int stackCount) => new(
            definition.ContentId,
            sourceId,
            remainingTurns,
            checked((int)definition.DamagePerTurn),
            stackCount,
            definition.CanAct,
            definition.Polarity,
            definition.EffectKind,
            definition.TriggerTiming,
            ResolveRefreshStrategy(definition),
            definition.CurseCategory,
            definition.SpeedModifier,
            definition.DamageReductionPercent,
            definition.MeleeRetaliationStatusId,
            definition.MeleeRetaliationDuration,
            definition.InitiativeModifier,
            definition.MovementModifier,
            definition.FrozenTotalDamage,
            definition.AttributeModifiers,
            definition.FrozenTotalHealing);

    private static UnitAttributeModifiers MergeStrongest(
        UnitAttributeModifiers active,
        UnitAttributeModifiers incoming,
        StatusPolarity polarity)
    {
        int Pick(int left, int right) => polarity == StatusPolarity.Beneficial
            ? Math.Max(left, right)
            : Math.Min(left, right);
        return new UnitAttributeModifiers(
            Pick(active.Strength, incoming.Strength),
            Pick(active.Agility, incoming.Agility),
            Pick(active.Constitution, incoming.Constitution),
            Pick(active.Intelligence, incoming.Intelligence),
            Pick(active.Charisma, incoming.Charisma),
            Pick(active.Luck, incoming.Luck));
    }

    private static int Manhattan(BattleUnitState left, BattleUnitState right) =>
        Math.Abs(left.Unit.Position.X - right.Unit.Position.X) +
        Math.Abs(left.Unit.Position.Y - right.Unit.Position.Y);

    private static int SaturatingAdd(int left, int right) =>
        (int)Math.Min(int.MaxValue, (long)left + right);
}
