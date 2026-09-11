using System.Collections.ObjectModel;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Units;

namespace Tactics.Core.Battle;

public sealed record BattleDamageShieldState(int RemainingPoints, bool AbsorbsAllDamage);

public sealed record DemonboundBattleState
{
    /// <summary>Default possessed-form identity used by the current corruption contract.</summary>
    public static readonly ContentId DefaultPossessedFormId = new("demonbound.possessed-form.v1");

    /// <summary>Default boost configuration bound when a possessed form is entered.</summary>
    public static readonly ContentId DefaultPossessedBoostConfigurationId = new("demonbound.possessed-boost.v1");

    public DemonboundBattleState(
        int corruption = 0,
        int mindfulnessLevel = 0,
        bool meditationUsedThisTurn = false,
        bool basicAttackUsedThisTurn = false,
        bool nonMeditationSkillUsedThisTurn = false,
        bool isPossessed = false,
        ContentId? possessedFormId = null,
        ContentId? possessedBoostConfigurationId = null,
        bool possessedBoostApplied = false)
    {
        if (corruption is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(corruption));
        if (mindfulnessLevel is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(mindfulnessLevel));
        if (possessedBoostApplied && possessedFormId is null && !isPossessed)
            throw new ArgumentException("Boost cannot be applied before a possessed form is entered.", nameof(possessedBoostApplied));
        Corruption = corruption;
        MindfulnessLevel = mindfulnessLevel;
        MeditationUsedThisTurn = meditationUsedThisTurn;
        BasicAttackUsedThisTurn = basicAttackUsedThisTurn;
        NonMeditationSkillUsedThisTurn = nonMeditationSkillUsedThisTurn;
        PossessedFormId = possessedFormId ?? (isPossessed ? DefaultPossessedFormId : null);
        PossessedBoostConfigurationId = PossessedFormId is null
            ? null
            : possessedBoostConfigurationId ?? DefaultPossessedBoostConfigurationId;
        PossessedBoostApplied = possessedBoostApplied && PossessedFormId is not null;
    }

    public int Corruption { get; }
    public int MindfulnessLevel { get; }
    public bool MeditationUsedThisTurn { get; }
    public bool BasicAttackUsedThisTurn { get; }
    public bool NonMeditationSkillUsedThisTurn { get; }

    /// <summary>Gets whether the unit is inside the possessed demon form.</summary>
    public bool IsPossessed => PossessedFormId is not null;

    /// <summary>Gets the active possessed-form identity; null before corruption reaches ten.</summary>
    public ContentId? PossessedFormId { get; }

    /// <summary>Gets the boost configuration identity bound when the form was entered.</summary>
    public ContentId? PossessedBoostConfigurationId { get; }

    /// <summary>Gets whether the one-time form boost has already been applied to this battle unit.</summary>
    public bool PossessedBoostApplied { get; }

    public DemonboundBattleState PrepareForTurn() => new(Corruption, MindfulnessLevel, isPossessed: IsPossessed,
        possessedFormId: PossessedFormId, possessedBoostConfigurationId: PossessedBoostConfigurationId,
        possessedBoostApplied: PossessedBoostApplied);
    public DemonboundBattleState WithMindfulnessLevel(int level) => new(Corruption, level,
        MeditationUsedThisTurn, BasicAttackUsedThisTurn, NonMeditationSkillUsedThisTurn,
        isPossessed: IsPossessed, possessedFormId: PossessedFormId,
        possessedBoostConfigurationId: PossessedBoostConfigurationId, possessedBoostApplied: PossessedBoostApplied);
    public DemonboundBattleState WithCorruption(int value) => new(Math.Clamp(value, 0, 10), MindfulnessLevel,
        MeditationUsedThisTurn, BasicAttackUsedThisTurn, NonMeditationSkillUsedThisTurn,
        isPossessed: IsPossessed || value >= 10,
        possessedFormId: IsPossessed ? PossessedFormId : null,
        possessedBoostConfigurationId: PossessedBoostConfigurationId,
        possessedBoostApplied: PossessedBoostApplied);
    public DemonboundBattleState WithMeditationUsed() => new(Corruption, MindfulnessLevel, true,
        BasicAttackUsedThisTurn, NonMeditationSkillUsedThisTurn, isPossessed: IsPossessed,
        possessedFormId: PossessedFormId, possessedBoostConfigurationId: PossessedBoostConfigurationId,
        possessedBoostApplied: PossessedBoostApplied);
    public DemonboundBattleState WithBasicAttackUsed() => new(Corruption, MindfulnessLevel,
        MeditationUsedThisTurn, true, NonMeditationSkillUsedThisTurn, isPossessed: IsPossessed,
        possessedFormId: PossessedFormId, possessedBoostConfigurationId: PossessedBoostConfigurationId,
        possessedBoostApplied: PossessedBoostApplied);
    public DemonboundBattleState WithNonMeditationSkillUsed() => new(Corruption, MindfulnessLevel,
        MeditationUsedThisTurn, BasicAttackUsedThisTurn, true, isPossessed: IsPossessed,
        possessedFormId: PossessedFormId, possessedBoostConfigurationId: PossessedBoostConfigurationId,
        possessedBoostApplied: PossessedBoostApplied);

    /// <summary>
    /// Marks the form boost as applied. Idempotent: repeated calls and Save/Reload replays
    /// must never apply a per-battle attribute boost more than once.
    /// </summary>
    public DemonboundBattleState WithPossessedBoostApplied() => new(Corruption, MindfulnessLevel,
        MeditationUsedThisTurn, BasicAttackUsedThisTurn, NonMeditationSkillUsedThisTurn,
        isPossessed: IsPossessed, possessedFormId: PossessedFormId,
        possessedBoostConfigurationId: PossessedBoostConfigurationId, possessedBoostApplied: true);
}

/// <summary>
/// Stores immutable runtime state for one battle unit.
/// </summary>
/// <remarks>
/// Content definitions remain outside this type. Health, movement use, and status duration are per-battle
/// values and every mutation returns a new instance so AI evaluation and replay snapshots cannot alias state.
/// </remarks>
public sealed class BattleUnitState
{
    private readonly IReadOnlyDictionary<ContentId, BattleStatusState> _statuses;
    private readonly IReadOnlyDictionary<ContentId, int> _statusDurations;
    private readonly IReadOnlyDictionary<ItemInstanceId, BattleConsumableState> _consumables;
    private readonly IReadOnlyDictionary<ContentId, int> _successfulSkillUses;

    /// <summary>
    /// Creates runtime state for a unit.
    /// </summary>
    /// <param name="unit">Immutable unit facts and current board position.</param>
    /// <param name="maxHealth">Maximum health. Must be positive.</param>
    /// <param name="currentHealth">Current health in the inclusive range from zero to max health.</param>
    /// <param name="hasMovedThisTurn">Whether the one movement use has been consumed this turn.</param>
    /// <param name="statusDurations">Optional status ID to remaining-turn mapping.</param>
    public BattleUnitState(
        UnitState unit,
        int maxHealth,
        int currentHealth,
        bool hasMovedThisTurn = false,
        IReadOnlyDictionary<ContentId, int>? statusDurations = null,
        int maxMana = 0,
        int currentMana = 0,
        IReadOnlyDictionary<ContentId, BattleStatusState>? statuses = null,
        float? baseSpeed = null,
        IReadOnlyDictionary<ItemInstanceId, BattleConsumableState>? consumables = null,
        int lastSuccessfulConsumableUseRound = 0,
        int physicalAttack = 2,
        int magicalAttack = 2,
        UnitInstanceId? summonOwnerId = null,
        bool canReceiveStandardHealing = true,
        bool hasCombatTechniquesLevelOne = false,
        bool canProduceCorpse = true,
        IReadOnlyDictionary<ContentId, int>? successfulSkillUses = null,
        int manaRecoveryPerTurn = 0,
        string summonCategory = "",
        int combatTechniquesLevel = 0,
        BattleDamageShieldState? damageShield = null,
        int movementCellsThisTurn = 0,
        DemonboundBattleState? demonboundState = null,
        int primaryAttributeDamageBonus = 0)
    {
        if (maxHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHealth));
        if (currentHealth < 0 || currentHealth > maxHealth)
            throw new ArgumentOutOfRangeException(nameof(currentHealth));
        if (maxMana < 0)
            throw new ArgumentOutOfRangeException(nameof(maxMana));
        if (currentMana < 0 || currentMana > maxMana)
            throw new ArgumentOutOfRangeException(nameof(currentMana));
        if (baseSpeed is not null && (!float.IsFinite(baseSpeed.Value) || baseSpeed.Value < 0f))
            throw new ArgumentOutOfRangeException(nameof(baseSpeed));
        if (lastSuccessfulConsumableUseRound < 0)
            throw new ArgumentOutOfRangeException(nameof(lastSuccessfulConsumableUseRound));
        if (physicalAttack < 0 || magicalAttack < 0)
            throw new ArgumentOutOfRangeException(nameof(physicalAttack));
        if (manaRecoveryPerTurn < 0)
            throw new ArgumentOutOfRangeException(nameof(manaRecoveryPerTurn));
        if (movementCellsThisTurn < 0)
            throw new ArgumentOutOfRangeException(nameof(movementCellsThisTurn));
        if (primaryAttributeDamageBonus < 0)
            throw new ArgumentOutOfRangeException(nameof(primaryAttributeDamageBonus));
        if (statusDurations is not null && statusDurations.Count > 0 && statuses is not null && statuses.Count > 0)
            throw new ArgumentException("Provide either legacy status durations or detailed statuses, not both.");

        Unit = unit with { IsAlive = currentHealth > 0 };
        MaxHealth = maxHealth;
        CurrentHealth = currentHealth;
        HasMovedThisTurn = hasMovedThisTurn;
        MaxMana = maxMana;
        CurrentMana = currentMana;
        BaseSpeed = baseSpeed ?? Math.Max(0f, unit.Initiative * 0.5f);
        LastSuccessfulConsumableUseRound = lastSuccessfulConsumableUseRound;
        PhysicalAttack = physicalAttack;
        MagicalAttack = magicalAttack;
        SummonOwnerId = summonOwnerId;
        CanReceiveStandardHealing = canReceiveStandardHealing;
        HasCombatTechniquesLevelOne = hasCombatTechniquesLevelOne;
        CanProduceCorpse = canProduceCorpse;
        ManaRecoveryPerTurn = manaRecoveryPerTurn;
        SummonCategory = summonCategory?.Trim() ?? string.Empty;
        CombatTechniquesLevel = combatTechniquesLevel > 0 ? combatTechniquesLevel : hasCombatTechniquesLevelOne ? 1 : 0;
        if (CombatTechniquesLevel < 0 || CombatTechniquesLevel > 3) throw new ArgumentOutOfRangeException(nameof(combatTechniquesLevel));
        if (damageShield is { RemainingPoints: <= 0 }) throw new ArgumentOutOfRangeException(nameof(damageShield));
        DamageShield = damageShield;
        MovementCellsThisTurn = movementCellsThisTurn;
        DemonboundState = demonboundState;
        PrimaryAttributeDamageBonus = primaryAttributeDamageBonus;
        var skillUses = new Dictionary<ContentId, int>();
        foreach ((ContentId skillId, int count) in successfulSkillUses ?? new Dictionary<ContentId, int>())
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(successfulSkillUses));
            skillUses.Add(skillId, count);
        }
        _successfulSkillUses = new ReadOnlyDictionary<ContentId, int>(skillUses);

        var detailedStatuses = new Dictionary<ContentId, BattleStatusState>();
        foreach ((ContentId statusId, int remainingTurns) in statusDurations ?? new Dictionary<ContentId, int>())
        {
            if (remainingTurns <= 0)
                throw new ArgumentOutOfRangeException(nameof(statusDurations), "Status durations must be positive.");
            detailedStatuses.Add(statusId, new BattleStatusState(statusId, Unit.InstanceId, remainingTurns, 0));
        }
        foreach ((ContentId statusId, BattleStatusState status) in statuses ??
                 new Dictionary<ContentId, BattleStatusState>())
        {
            if (statusId != status.ContentId)
                throw new ArgumentException("Detailed status key must match its ContentId.", nameof(statuses));
            detailedStatuses.Add(statusId, status);
        }

        _statuses = new ReadOnlyDictionary<ContentId, BattleStatusState>(detailedStatuses);
        _statusDurations = new ReadOnlyDictionary<ContentId, int>(
            detailedStatuses.ToDictionary(item => item.Key, item => item.Value.RemainingTurns));

        var consumableMap = new Dictionary<ItemInstanceId, BattleConsumableState>();
        foreach ((ItemInstanceId instanceId, BattleConsumableState consumable) in consumables ??
                 new Dictionary<ItemInstanceId, BattleConsumableState>())
        {
            if (instanceId != consumable.InstanceId)
                throw new ArgumentException("Consumable key must match its InstanceId.", nameof(consumables));
            consumableMap.Add(instanceId, consumable);
        }
        _consumables = new ReadOnlyDictionary<ItemInstanceId, BattleConsumableState>(consumableMap);
    }

    /// <summary>
    /// Gets immutable unit facts including the current board position.
    /// </summary>
    public UnitState Unit { get; }

    /// <summary>
    /// Gets the unit's maximum health.
    /// </summary>
    public int MaxHealth { get; }

    /// <summary>
    /// Gets current health. Zero means the unit is defeated.
    /// </summary>
    public int CurrentHealth { get; }

    /// <summary>
    /// Gets the unit's maximum mana captured in this battle.
    /// </summary>
    public int MaxMana { get; }

    /// <summary>
    /// Gets current mana available for ability costs.
    /// </summary>
    public int CurrentMana { get; }

    /// <summary>
    /// Gets the immutable unmodified speed used to recalculate Slow effects.
    /// </summary>
    public float BaseSpeed { get; }

    public int LastSuccessfulConsumableUseRound { get; }
    public int PhysicalAttack { get; }
    public int MagicalAttack { get; }
    public UnitInstanceId? SummonOwnerId { get; }
    public bool CanReceiveStandardHealing { get; }
    public bool HasCombatTechniquesLevelOne { get; }
    public bool CanProduceCorpse { get; }
    public int ManaRecoveryPerTurn { get; }
    public string SummonCategory { get; }
    public int CombatTechniquesLevel { get; }
    public BattleDamageShieldState? DamageShield { get; }
    public int MovementCellsThisTurn { get; }
    public DemonboundBattleState? DemonboundState { get; }
    public int PrimaryAttributeDamageBonus { get; }
    public IReadOnlyDictionary<ContentId, int> SuccessfulSkillUses => _successfulSkillUses;

    public int SuccessfulUsesOf(ContentId skillId) => _successfulSkillUses.TryGetValue(skillId, out int count) ? count : 0;

    public BattleUnitState WithSuccessfulSkillUse(ContentId skillId)
    {
        var uses = new Dictionary<ContentId, int>(_successfulSkillUses)
        {
            [skillId] = SuccessfulUsesOf(skillId) + 1
        };
        return Copy(successfulSkillUses: uses);
    }

    /// <summary>
    /// Gets whether the unit has consumed its one movement use this turn.
    /// </summary>
    public bool HasMovedThisTurn { get; }

    /// <summary>
    /// Gets immutable status durations keyed by stable content ID.
    /// </summary>
    public IReadOnlyDictionary<ContentId, int> StatusDurations => _statusDurations;

    /// <summary>
    /// Gets detailed immutable status instances keyed by stable content ID.
    /// </summary>
    public IReadOnlyDictionary<ContentId, BattleStatusState> Statuses => _statuses;

    public IReadOnlyDictionary<ItemInstanceId, BattleConsumableState> Consumables => _consumables;

    /// <summary>
    /// Gets whether this runtime unit can still participate in battle.
    /// </summary>
    public bool IsAlive => CurrentHealth > 0 && Unit.IsAlive;

    /// <summary>
    /// Returns a copy at a new cell and optionally updates movement-use state.
    /// </summary>
    /// <param name="position">New local board position.</param>
    /// <param name="hasMovedThisTurn">New movement-use state.</param>
    /// <returns>The updated immutable unit state.</returns>
    public BattleUnitState WithPosition(GridPoint position, bool hasMovedThisTurn, int? movementCellsThisTurn = null) =>
        new(
            Unit with { Position = position },
            MaxHealth,
            CurrentHealth,
            hasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: _statuses,
            baseSpeed: BaseSpeed,
            consumables: _consumables,
            lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
            physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
            canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne, canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses, manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: movementCellsThisTurn ?? MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    public BattleUnitState ResetMovementCells() => Copy(movementCellsThisTurn: 0);

    public BattleUnitState WithDemonboundState(DemonboundBattleState? state) => new(
        Unit, MaxHealth, CurrentHealth, HasMovedThisTurn, maxMana: MaxMana, currentMana: CurrentMana,
        statuses: _statuses, baseSpeed: BaseSpeed, consumables: _consumables,
        lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
        physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
        canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne,
        canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses,
        manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory,
        combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield,
        movementCellsThisTurn: MovementCellsThisTurn, demonboundState: state, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    /// <summary>
    /// Returns a copy with clamped health and matching alive state.
    /// </summary>
    /// <param name="currentHealth">New health before clamping to the valid range.</param>
    /// <returns>The updated immutable unit state.</returns>
    public BattleUnitState WithHealth(int currentHealth) =>
        new(
            Unit,
            MaxHealth,
            Math.Clamp(currentHealth, 0, MaxHealth),
            HasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: _statuses,
            baseSpeed: BaseSpeed,
            consumables: _consumables,
            lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
            physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
            canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne, canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses, manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    /// <summary>
    /// Returns a copy with clamped mana.
    /// </summary>
    public BattleUnitState WithMana(int currentMana) =>
        new(
            Unit,
            MaxHealth,
            CurrentHealth,
            HasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: Math.Clamp(currentMana, 0, MaxMana),
            statuses: _statuses,
            baseSpeed: BaseSpeed,
            consumables: _consumables,
            lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
            physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
            canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne, canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses, manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    public BattleUnitState WithAttributeProjection(
        UnitAttributes effectiveAttributes,
        UnitDerivedStats derivedStats,
        int manaRecoveryPerTurn) => new(
        Unit with
        {
            EffectiveAttributes = effectiveAttributes,
            MoveRange = derivedStats.MoveRange,
            Initiative = derivedStats.Initiative
        },
        derivedStats.MaxHealth,
        Math.Min(CurrentHealth, derivedStats.MaxHealth),
        HasMovedThisTurn,
        maxMana: derivedStats.MaxMana,
        currentMana: Math.Min(CurrentMana, derivedStats.MaxMana),
        statuses: _statuses,
        baseSpeed: BaseSpeed,
        consumables: _consumables,
        lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
        physicalAttack: PhysicalAttack,
        magicalAttack: MagicalAttack,
        summonOwnerId: SummonOwnerId,
        canReceiveStandardHealing: CanReceiveStandardHealing,
        hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne,
        canProduceCorpse: CanProduceCorpse,
        successfulSkillUses: _successfulSkillUses,
        manaRecoveryPerTurn: manaRecoveryPerTurn,
        summonCategory: SummonCategory,
        combatTechniquesLevel: CombatTechniquesLevel,
        damageShield: DamageShield,
        movementCellsThisTurn: MovementCellsThisTurn,
        demonboundState: DemonboundState,
        primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    public BattleUnitState WithHealthAndMana(int maxHealth, int currentHealth, int maxMana, int currentMana) => new(
        Unit, maxHealth, currentHealth, HasMovedThisTurn, maxMana: maxMana, currentMana: currentMana,
        statuses: _statuses, baseSpeed: BaseSpeed, consumables: _consumables,
        lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
        physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
        canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne,
        canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses,
        manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory,
combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    public BattleUnitState WithDamageOutputMultiplier(float multiplier)
    {
        if (!float.IsFinite(multiplier) || multiplier <= 0f) throw new ArgumentOutOfRangeException(nameof(multiplier));
        return new BattleUnitState(Unit, MaxHealth, CurrentHealth, HasMovedThisTurn, maxMana: MaxMana,
            currentMana: CurrentMana, statuses: _statuses, baseSpeed: BaseSpeed, consumables: _consumables,
            lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
            physicalAttack: (int)Math.Ceiling(PhysicalAttack * multiplier),
            magicalAttack: (int)Math.Ceiling(MagicalAttack * multiplier), summonOwnerId: SummonOwnerId,
            canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne,
            canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses,
            manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory,
combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);
    }

    /// <summary>
    /// Returns a copy with the given status duration.
    /// </summary>
    /// <param name="statusId">Stable status content ID.</param>
    /// <param name="remainingTurns">Positive remaining duration.</param>
    /// <returns>The updated immutable unit state.</returns>
    public BattleUnitState WithStatus(ContentId statusId, int remainingTurns)
    {
        if (remainingTurns <= 0)
            throw new ArgumentOutOfRangeException(nameof(remainingTurns));

        BattleStatusState status = _statuses.TryGetValue(statusId, out BattleStatusState? current)
            ? current.WithRemainingTurns(remainingTurns)
            : new BattleStatusState(statusId, Unit.InstanceId, remainingTurns, 0);
        return WithStatus(status);
    }

    /// <summary>
    /// Returns a copy with a complete status instance added or replaced.
    /// </summary>
    public BattleUnitState WithStatus(BattleStatusState status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var statuses = new Dictionary<ContentId, BattleStatusState>(_statuses)
        {
            [status.ContentId] = status
        };
        return new BattleUnitState(
            Unit,
            MaxHealth,
            CurrentHealth,
            HasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: statuses,
            baseSpeed: BaseSpeed,
            consumables: _consumables,
            lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
            physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
            canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne, canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses, manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);
    }

    /// <summary>
    /// Returns a copy without the selected status.
    /// </summary>
    public BattleUnitState WithoutStatus(ContentId statusId)
    {
        var statuses = new Dictionary<ContentId, BattleStatusState>(_statuses);
        statuses.Remove(statusId);
        return new BattleUnitState(
            Unit,
            MaxHealth,
            CurrentHealth,
            HasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: statuses,
            baseSpeed: BaseSpeed,
            consumables: _consumables,
            lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
            physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
            canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne, canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses, manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);
    }

    public BattleUnitState WithUnitFacts(UnitState unit) => new(
        unit,
        MaxHealth,
        CurrentHealth,
        HasMovedThisTurn,
        maxMana: MaxMana,
        currentMana: CurrentMana,
        statuses: _statuses,
        baseSpeed: BaseSpeed,
        consumables: _consumables,
        lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
        physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
            canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne, canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses, manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    public BattleUnitState WithConsumable(BattleConsumableState consumable)
    {
        ArgumentNullException.ThrowIfNull(consumable);
        var consumables = new Dictionary<ItemInstanceId, BattleConsumableState>(_consumables)
        {
            [consumable.InstanceId] = consumable
        };
        return new BattleUnitState(
            Unit,
            MaxHealth,
            CurrentHealth,
            HasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: _statuses,
            baseSpeed: BaseSpeed,
            consumables: consumables,
            lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
            physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
            canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne, canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses, manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);
    }

    public BattleUnitState WithSuccessfulConsumableUse(int round) => new(
        Unit,
        MaxHealth,
        CurrentHealth,
        HasMovedThisTurn,
        maxMana: MaxMana,
        currentMana: CurrentMana,
        statuses: _statuses,
        baseSpeed: BaseSpeed,
        consumables: _consumables,
        lastSuccessfulConsumableUseRound: round,
        physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
        canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne, canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses, manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    /// <summary>
    /// Returns a copy whose one-per-turn movement use is available again.
    /// </summary>
    /// <returns>The updated immutable unit state.</returns>
    public BattleUnitState PrepareForTurn() =>
        new(
            Unit,
            MaxHealth,
            CurrentHealth,
            hasMovedThisTurn: false,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: _statuses,
            baseSpeed: BaseSpeed,
            consumables: _consumables,
            lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
            physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
            canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne, canProduceCorpse: CanProduceCorpse, successfulSkillUses: null, manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: 0, demonboundState: DemonboundState?.PrepareForTurn(), primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    public BattleUnitState WithCombatTechniquesLevelOne(bool enabled) => new(
        Unit, MaxHealth, CurrentHealth, HasMovedThisTurn, maxMana: MaxMana, currentMana: CurrentMana,
        statuses: _statuses, baseSpeed: BaseSpeed, consumables: _consumables,
        lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
        physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
        canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: enabled, canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses, manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: enabled ? Math.Max(1, CombatTechniquesLevel) : 0, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    public BattleUnitState WithCombatTechniquesLevel(int level) => new(
        Unit, MaxHealth, CurrentHealth, HasMovedThisTurn, maxMana: MaxMana, currentMana: CurrentMana,
        statuses: _statuses, baseSpeed: BaseSpeed, consumables: _consumables,
        lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
        physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
        canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: level > 0,
        canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses,
        manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory,
        combatTechniquesLevel: level, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    public BattleUnitState WithDamageShield(BattleDamageShieldState? shield) => new(
        Unit, MaxHealth, CurrentHealth, HasMovedThisTurn, maxMana: MaxMana, currentMana: CurrentMana,
        statuses: _statuses, baseSpeed: BaseSpeed, consumables: _consumables,
        lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
        physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
        canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne,
        canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses,
        manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory,
        combatTechniquesLevel: CombatTechniquesLevel, damageShield: shield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);

    public BattleUnitState WithBaseSpeed(float baseSpeed)
    {
        if (!float.IsFinite(baseSpeed) || baseSpeed < 0f)
            throw new ArgumentOutOfRangeException(nameof(baseSpeed));
        return new BattleUnitState(Unit, MaxHealth, CurrentHealth, HasMovedThisTurn,
            maxMana: MaxMana, currentMana: CurrentMana, statuses: _statuses, baseSpeed: baseSpeed,
            consumables: _consumables, lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
            physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
            canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne,
            canProduceCorpse: CanProduceCorpse, successfulSkillUses: _successfulSkillUses,
            manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory,
combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);
    }

    private BattleUnitState Copy(IReadOnlyDictionary<ContentId, int>? successfulSkillUses = null, int? movementCellsThisTurn = null) => new(
        Unit, MaxHealth, CurrentHealth, HasMovedThisTurn, maxMana: MaxMana, currentMana: CurrentMana,
        statuses: _statuses, baseSpeed: BaseSpeed, consumables: _consumables,
        lastSuccessfulConsumableUseRound: LastSuccessfulConsumableUseRound,
        physicalAttack: PhysicalAttack, magicalAttack: MagicalAttack, summonOwnerId: SummonOwnerId,
        canReceiveStandardHealing: CanReceiveStandardHealing, hasCombatTechniquesLevelOne: HasCombatTechniquesLevelOne,
        canProduceCorpse: CanProduceCorpse, successfulSkillUses: successfulSkillUses ?? _successfulSkillUses,
        manaRecoveryPerTurn: ManaRecoveryPerTurn, summonCategory: SummonCategory, combatTechniquesLevel: CombatTechniquesLevel, damageShield: DamageShield, movementCellsThisTurn: movementCellsThisTurn ?? MovementCellsThisTurn, demonboundState: DemonboundState, primaryAttributeDamageBonus: PrimaryAttributeDamageBonus);
}
