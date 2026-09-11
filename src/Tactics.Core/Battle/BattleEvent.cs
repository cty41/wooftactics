using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Units;

namespace Tactics.Core.Battle;

/// <summary>
/// Describes an immutable gameplay fact emitted by a battle transition.
/// </summary>
/// <remarks>
/// UI, audio, VFX, and presentation may consume these events but must never mutate the transition result.
/// Event order is deterministic and forms part of the replay/Golden contract.
/// </remarks>
public abstract record BattleEvent;

/// <summary>
/// Reports a rejected command that left battle state unchanged.
/// </summary>
/// <param name="ActorId">Unit that issued the rejected command.</param>
/// <param name="Reason">Stable machine-readable failure reason.</param>
public sealed record CommandRejectedEvent(UnitInstanceId ActorId, string Reason) : BattleEvent;

/// <summary>
/// Reports a completed unit movement and its deterministic path.
/// </summary>
/// <param name="UnitId">Moved unit ID.</param>
/// <param name="Origin">Previous local cell.</param>
/// <param name="Destination">New local cell.</param>
/// <param name="Path">Path excluding origin and including destination.</param>
public sealed record UnitMovedEvent(
    UnitInstanceId UnitId,
    GridPoint Origin,
    GridPoint Destination,
    IReadOnlyList<GridPoint> Path) : BattleEvent;

/// <summary>
/// Reports that a skill passed gameplay validation and began resolving.
/// </summary>
/// <param name="ActorId">Skill user ID.</param>
/// <param name="TargetId">Primary target ID.</param>
/// <param name="SkillId">Stable skill content ID.</param>
public sealed record SkillUsedEvent(UnitInstanceId ActorId, UnitInstanceId TargetId, ContentId SkillId) : BattleEvent;

/// <summary>
/// Reports mana committed after a skill completes gameplay resolution.
/// </summary>
public sealed record ManaSpentEvent(
    UnitInstanceId UnitId,
    ContentId SkillId,
    int Amount,
    int RemainingMana) : BattleEvent;

/// <summary>
/// Reports applied damage after health clamping.
/// </summary>
/// <param name="SourceId">Damage source unit ID.</param>
/// <param name="TargetId">Damaged unit ID.</param>
/// <param name="SkillId">Skill responsible for the damage.</param>
/// <param name="Amount">Actual health removed.</param>
/// <param name="RemainingHealth">Target health after damage.</param>
public sealed record DamageAppliedEvent(
    UnitInstanceId SourceId,
    UnitInstanceId TargetId,
    ContentId SkillId,
    int Amount,
    int RemainingHealth) : BattleEvent;

public sealed record DamageShieldAppliedEvent(UnitInstanceId UnitId, ContentId SkillId, int Points, bool AbsorbsAllDamage) : BattleEvent;
public sealed record DamageShieldAbsorbedEvent(UnitInstanceId UnitId, ContentId SkillId, int Amount, int RemainingPoints) : BattleEvent;

/// <summary>
/// Reports a status application or duration refresh.
/// </summary>
/// <param name="SourceId">Status source unit ID.</param>
/// <param name="TargetId">Affected unit ID.</param>
/// <param name="StatusId">Stable status content ID.</param>
/// <param name="RemainingTurns">Resulting duration.</param>
public sealed record StatusAppliedEvent(
    UnitInstanceId SourceId,
    UnitInstanceId TargetId,
    ContentId StatusId,
    int RemainingTurns) : BattleEvent;

/// <summary>
/// Reports damage caused by a status at its configured turn trigger.
/// </summary>
public sealed record StatusTickedEvent(
    UnitInstanceId SourceId,
    UnitInstanceId TargetId,
    ContentId StatusId,
    int Amount,
    int RemainingHealth) : BattleEvent;

public sealed record StatusHealingTickedEvent(
    UnitInstanceId SourceId,
    UnitInstanceId TargetId,
    ContentId StatusId,
    int Amount,
    int CurrentHealth) : BattleEvent;

/// <summary>
/// Reports a status duration decrement at the affected unit's turn end.
/// </summary>
public sealed record StatusDurationChangedEvent(
    UnitInstanceId TargetId,
    ContentId StatusId,
    int RemainingTurns) : BattleEvent;

/// <summary>
/// Reports status removal after its duration reaches zero.
/// </summary>
public sealed record StatusExpiredEvent(UnitInstanceId TargetId, ContentId StatusId) : BattleEvent;

public sealed record StatusStackChangedEvent(
    UnitInstanceId TargetId,
    ContentId StatusId,
    int StackCount) : BattleEvent;

public sealed record ConsumableUsedEvent(
    UnitInstanceId ActorId,
    UnitInstanceId TargetId,
    ItemInstanceId ItemInstanceId,
    ContentId ConsumableId) : BattleEvent;

public sealed record HealthRestoredEvent(
    UnitInstanceId SourceId,
    UnitInstanceId TargetId,
    ContentId ConsumableId,
    int Amount,
    int CurrentHealth) : BattleEvent;

public sealed record ManaRestoredEvent(
    UnitInstanceId SourceId,
    UnitInstanceId TargetId,
    ContentId ConsumableId,
    int Amount,
    int CurrentMana) : BattleEvent;

public sealed record StatusesCleansedEvent(
    UnitInstanceId SourceId,
    UnitInstanceId TargetId,
    ContentId ConsumableId,
    IReadOnlyList<ContentId> RemovedStatusIds) : BattleEvent;

public sealed record ConsumableChargesChangedEvent(
    UnitInstanceId UnitId,
    ItemInstanceId ItemInstanceId,
    int RemainingCharges) : BattleEvent;

public sealed record CombatRollResolvedEvent(
    UnitInstanceId SourceId,
    UnitInstanceId TargetId,
    ContentId SkillId,
    int Roll,
    int Threshold,
    string Outcome,
    ulong RandomState) : BattleEvent;

public sealed record StatusRollResolvedEvent(
    UnitInstanceId SourceId,
    UnitInstanceId TargetId,
    ContentId SkillId,
    ContentId StatusId,
    int Roll,
    int Threshold,
    bool Applied,
    ulong RandomState) : BattleEvent;

public sealed record SemanticCueEmittedEvent(
    UnitInstanceId SourceId,
    UnitInstanceId? TargetId,
    ContentId SkillId,
    string CueId) : BattleEvent;

public sealed record CorpseConsumedEvent(GridPoint Cell, UnitInstanceId SourceId) : BattleEvent;
public sealed record CorpseCreatedEvent(GridPoint Cell, UnitInstanceId UnitId) : BattleEvent;

public sealed record UnitSummonedEvent(
    UnitInstanceId OwnerId,
    UnitInstanceId SummonId,
    ContentId DefinitionId,
    GridPoint Cell) : BattleEvent;

public sealed record SpearRecoveredEvent(UnitInstanceId OwnerId, GridPoint Cell) : BattleEvent;

/// <summary>
/// Reports that an Amazon committed its held spear to a deterministic board cell.
/// </summary>
public sealed record SpearDroppedEvent(UnitInstanceId OwnerId, GridPoint Cell) : BattleEvent;

/// <summary>
/// Reports a unit reaching zero health.
/// </summary>
/// <param name="UnitId">Defeated unit ID.</param>
public sealed record UnitDefeatedEvent(UnitInstanceId UnitId) : BattleEvent;

public sealed record MeditationUsedEvent(
    UnitInstanceId UnitId,
    int CorruptionReduced,
    int RemainingCorruption) : BattleEvent;

public sealed record CorruptionChangedEvent(
    UnitInstanceId UnitId,
    ContentId SourceSkillId,
    int Amount,
    int CurrentCorruption) : BattleEvent;

public sealed record DemonboundPossessedEvent(UnitInstanceId UnitId) : BattleEvent;

public sealed record RunPermanentDeathRolledEvent(
    UnitInstanceId AttackerId,
    UnitInstanceId TargetId,
    int Roll,
    bool PermanentDeath,
    ulong RandomState) : BattleEvent;

/// <summary>
/// Reports deterministic turn advancement.
/// </summary>
/// <param name="PreviousUnitId">Unit whose turn ended.</param>
/// <param name="ActiveUnitId">Incoming active unit.</param>
/// <param name="Round">Resulting one-based round number.</param>
public sealed record TurnAdvancedEvent(
    UnitInstanceId PreviousUnitId,
    UnitInstanceId ActiveUnitId,
    int Round) : BattleEvent;
