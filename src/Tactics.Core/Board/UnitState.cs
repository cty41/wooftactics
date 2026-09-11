using Tactics.Core.Content;
using Tactics.Core.Units;
using Tactics.Core.Skills;

namespace Tactics.Core.Board;

/// <summary>
/// Immutable unit facts consumed by Core rules. Runtime counters stay outside content definitions.
/// </summary>
public readonly record struct UnitState
{
    public UnitState(
        UnitInstanceId instanceId,
        ContentId definitionId,
        GridPoint position,
        int moveRange,
        float initiative,
        int playerNumber,
        int spawnOrdinal,
        bool isAlive = true,
        UnitMovementKind movementKind = UnitMovementKind.Land,
        UnitAttributes? effectiveAttributes = null,
        int? baseMoveRange = null,
        float? baseInitiative = null,
        SkillRole combatRole = SkillRole.Any,
        UnitFacing? facing = null,
        UnitAttributes? baseAttributes = null)
    {
        if (!float.IsFinite(initiative))
            throw new ArgumentOutOfRangeException(nameof(initiative));
        if (playerNumber < 0)
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        if (spawnOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(spawnOrdinal));

        InstanceId = instanceId;
        DefinitionId = definitionId;
        Position = position;
        MoveRange = ValidateMoveRange(moveRange);
        Initiative = initiative;
        PlayerNumber = playerNumber;
        SpawnOrdinal = spawnOrdinal;
        IsAlive = isAlive;
        MovementKind = movementKind;
        EffectiveAttributes = effectiveAttributes ?? new UnitAttributes(5, 5, 5, 5, 5, 5);
        BaseAttributes = baseAttributes ?? EffectiveAttributes;
        BaseMoveRange = baseMoveRange ?? MoveRange;
        BaseInitiative = baseInitiative ?? Initiative;
        CombatRole = combatRole;
        Facing = facing ?? UnitFacingResolver.Initial(playerNumber);
    }

    public UnitInstanceId InstanceId { get; init; }
    public ContentId DefinitionId { get; init; }
    public GridPoint Position { get; init; }
    public int MoveRange { get; init; }
    public float Initiative { get; init; }
    public int PlayerNumber { get; init; }
    public int SpawnOrdinal { get; init; }
    public bool IsAlive { get; init; }
    public UnitMovementKind MovementKind { get; init; }
    public UnitAttributes EffectiveAttributes { get; init; }
    public UnitAttributes BaseAttributes { get; init; }
    public int BaseMoveRange { get; init; }
    public float BaseInitiative { get; init; }
    public SkillRole CombatRole { get; init; }
    public UnitFacing Facing { get; init; }

    private static int ValidateMoveRange(int moveRange) =>
        moveRange < 0 ? throw new ArgumentOutOfRangeException(nameof(moveRange)) : moveRange;
}
