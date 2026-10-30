using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;

namespace Tactics.Core.Units;

/// <summary>
/// Identifies the engine-neutral movement rule family used by a unit definition.
/// </summary>
public enum UnitMovementKind
{
    Land,
    Air,
    Swim
}

/// <summary>Declares whether serialized derived stats follow the frozen formula or an authored class contract.</summary>
public enum UnitDerivedStatMode
{
    FrozenFormula,
    Explicit
}

/// <summary>
/// Stores immutable gameplay facts used to instantiate one or more battle units.
/// </summary>
public sealed class UnitDefinition
{
    public UnitDefinition(
        ContentId contentId,
        string sourceId,
        string displayName,
        string familyId,
        string roleId,
        UnitAttributes attributes,
        float speed,
        UnitDerivedStats derivedStats,
        int attackRange,
        float attackFactor,
        float defenceFactor,
        UnitMovementKind movementKind,
        bool canProduceCorpse,
        UnitDerivedStatMode derivedStatMode = UnitDerivedStatMode.FrozenFormula,
        int movementTraitModifier = 0,
        UnitPrimaryAttribute primaryAttribute = UnitPrimaryAttribute.None)
    {
        if (!Enum.IsDefined(primaryAttribute))
            throw new ArgumentOutOfRangeException(nameof(primaryAttribute));
        if (!float.IsFinite(speed) || speed < 0)
            throw new ArgumentOutOfRangeException(nameof(speed));
        if (attackRange < 0)
            throw new ArgumentOutOfRangeException(nameof(attackRange));
        if (!float.IsFinite(attackFactor) || attackFactor < 0)
            throw new ArgumentOutOfRangeException(nameof(attackFactor));
        if (!float.IsFinite(defenceFactor) || defenceFactor < 0)
            throw new ArgumentOutOfRangeException(nameof(defenceFactor));
        if (!Enum.IsDefined(movementKind))
            throw new ArgumentOutOfRangeException(nameof(movementKind));

        if (!Enum.IsDefined(derivedStatMode))
            throw new ArgumentOutOfRangeException(nameof(derivedStatMode));
        UnitDerivedStats expected = UnitDerivedStatRules.Calculate(attributes, movementTraitModifier);
        if (derivedStatMode == UnitDerivedStatMode.FrozenFormula && derivedStats != expected)
        {
            throw new ArgumentException(
                $"Explicit derived values do not match {UnitDerivedStatRules.ContractId}.",
                nameof(derivedStats));
        }

        ContentId = contentId;
        SourceId = Required(sourceId, nameof(sourceId));
        DisplayName = Required(displayName, nameof(displayName));
        FamilyId = Required(familyId, nameof(familyId));
        RoleId = Required(roleId, nameof(roleId));
        Attributes = attributes;
        Speed = speed;
        DerivedStats = derivedStats;
        AttackRange = attackRange;
        AttackFactor = attackFactor;
        DefenceFactor = defenceFactor;
        MovementKind = movementKind;
        CanProduceCorpse = canProduceCorpse;
        DerivedStatMode = derivedStatMode;
        MovementTraitModifier = movementTraitModifier;
        PrimaryAttribute = primaryAttribute;
    }

    /// <summary>Authored class identity attribute; <see cref="UnitPrimaryAttribute.None"/> for all-rounders and monsters.</summary>
    public UnitPrimaryAttribute PrimaryAttribute { get; }

    public ContentId ContentId { get; }
    public string SourceId { get; }
    public string DisplayName { get; }
    public string FamilyId { get; }
    public string RoleId { get; }
    public UnitAttributes Attributes { get; }
    public float Speed { get; }
    public UnitDerivedStats DerivedStats { get; }
    public int AttackRange { get; }
    public float AttackFactor { get; }
    public float DefenceFactor { get; }
    public UnitMovementKind MovementKind { get; }
    public bool CanProduceCorpse { get; }
    public UnitDerivedStatMode DerivedStatMode { get; }
    public int MovementTraitModifier { get; }

    /// <summary>
    /// Creates mutable-per-battle state while preserving definition identity separately from instance identity.
    /// </summary>
    public BattleUnitState CreateBattleState(
        UnitInstanceId instanceId,
        GridPoint position,
        int playerNumber,
        int spawnOrdinal)
    {
        var unit = new UnitState(
            instanceId,
            ContentId,
            position,
            DerivedStats.MoveRange,
            DerivedStats.Initiative,
            playerNumber,
            spawnOrdinal,
            movementKind: MovementKind,
            effectiveAttributes: Attributes);
        return new BattleUnitState(
            unit,
            DerivedStats.MaxHealth,
            DerivedStats.MaxHealth,
            maxMana: DerivedStats.MaxMana,
            currentMana: DerivedStats.StartingMana,
            baseSpeed: Speed,
            canProduceCorpse: CanProduceCorpse);
    }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be empty.", parameterName)
            : value.Trim();
}
