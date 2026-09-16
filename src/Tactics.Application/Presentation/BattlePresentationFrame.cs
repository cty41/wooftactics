using Tactics.Application.Battle;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Application.Presentation;

public enum PresentationCueKind { Move, Melee, Ranged, Cast, Hit, StatusTick, Defeat, CorpseRemoved }
public enum PresentationMarkerKind { Begin, Release, Impact, Recover, Complete }
public enum BattlePresentationEffectKind { StatusApplied, StatusTicked, StatusDurationChanged, StatusStackChanged, StatusExpired, StatusCleansed, SpearDropped, SpearRecovered }
public enum BattlePresentationNumberKind { Normal, Critical, Heal, Mana, Miss }

public sealed record BattlePresentationMarker(PresentationMarkerKind Kind, int Order);
public sealed record BattlePresentationEffect(BattlePresentationEffectKind Kind, UnitInstanceId ActorId,
    UnitInstanceId? TargetId, ContentId? ContentId, GridPoint? Cell, int Amount);
public sealed record BattlePresentationNumber(BattlePresentationNumberKind Kind, UnitInstanceId TargetId,
    string Text, PresentationMarkerKind Marker, int Sequence);
public sealed record BattlePresentationCue(
    PresentationCueKind Kind,
    UnitInstanceId ActorId,
    UnitInstanceId? TargetId,
    ContentId? SkillId,
    GridPoint Origin,
    GridPoint Destination,
    IReadOnlyList<GridPoint> Path,
    IReadOnlyList<UnitInstanceId> AffectedUnitIds,
    IReadOnlyList<BattlePresentationMarker> Markers,
    IReadOnlyList<BattlePresentationEffect>? Effects = null,
    UnitInstanceId? InstigatorId = null);

public sealed record BattlePresentationFrame(
    string Stage,
    BattleUiSnapshot Before,
    BattleUiSnapshot After,
    IReadOnlyList<BattlePresentationCue> Cues,
    IReadOnlyList<BattlePresentationNumber> Numbers);

/// <summary>Compiles immutable gameplay events into deterministic presentation instructions.</summary>
public static class BattlePresentationFrameCompiler
{
    public static BattlePresentationFrame Compile(
        string stage,
        BattleUiSnapshot before,
        BattleUiSnapshot after,
        IReadOnlyList<BattleEvent> events,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(skills);
        var cues = new List<BattlePresentationCue>();
        foreach (BattleEvent item in events)
        {
            switch (item)
            {
                case UnitMovedEvent moved:
                    cues.Add(Cue(PresentationCueKind.Move, moved.UnitId, null, null, moved.Origin, moved.Destination, moved.Path, []));
                    break;
                case SkillUsedEvent used:
                    BattleUiUnitSnapshot actor = Find(before, after, used.ActorId);
                    BattleUiUnitSnapshot target = Find(before, after, used.TargetId);
                    SkillDefinition definition = skills[used.SkillId];
                    GridPoint destination = ResolveActionDestination(definition, used, actor.Cell, target.Cell, events);
                    PresentationCueKind actionKind = ResolveAction(definition);
                    cues.Add(Cue(actionKind, used.ActorId, used.TargetId, used.SkillId, actor.Cell, destination, [], [used.TargetId]));
                    break;
                case DamageAppliedEvent damage:
                    BattleUiUnitSnapshot damaged = Find(before, after, damage.TargetId);
                    cues.Add(Cue(PresentationCueKind.Hit, damage.TargetId, damage.TargetId, damage.SkillId,
                        damaged.Cell, damaged.Cell, [], [damage.TargetId], damage.SourceId));
                    break;
                case StatusTickedEvent tick when tick.Amount > 0:
                    BattleUiUnitSnapshot ticking = Find(before, after, tick.TargetId);
                    cues.Add(Cue(PresentationCueKind.StatusTick, tick.TargetId, tick.TargetId, null,
                        ticking.Cell, ticking.Cell, [], [tick.TargetId], tick.SourceId));
                    break;
                case StatusHealingTickedEvent tick when tick.Amount > 0:
                    BattleUiUnitSnapshot healing = Find(before, after, tick.TargetId);
                    cues.Add(Cue(PresentationCueKind.StatusTick, tick.TargetId, tick.TargetId, null,
                        healing.Cell, healing.Cell, [], [tick.TargetId], tick.SourceId));
                    break;
                case UnitDefeatedEvent defeated:
                    BattleUiUnitSnapshot unit = Find(before, after, defeated.UnitId);
                    cues.Add(Cue(PresentationCueKind.Defeat, defeated.UnitId, null, null, unit.Cell, unit.Cell, [], [defeated.UnitId]));
                    break;
                case CorpseConsumedEvent consumed:
                    BattleUiUnitSnapshot? corpse = before.Units.FirstOrDefault(value => !value.IsAlive && value.Cell == consumed.Cell);
                    if (corpse is not null)
                        cues.Add(Cue(PresentationCueKind.CorpseRemoved, corpse.UnitId, null, null, consumed.Cell, consumed.Cell, [], [corpse.UnitId]));
                    break;
            }
        }
        var affectedBySkill = events.OfType<DamageAppliedEvent>()
            .GroupBy(value => (value.SourceId, value.SkillId))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<UnitInstanceId>)group.Select(value => value.TargetId).Distinct().ToArray());
        for (int index = 0; index < cues.Count; index++)
        {
            BattlePresentationCue cue = cues[index];
            if (cue.SkillId is not ContentId skillId || cue.Kind is not (PresentationCueKind.Melee or PresentationCueKind.Ranged or PresentationCueKind.Cast)) continue;
            IReadOnlyList<UnitInstanceId> affected = affectedBySkill.GetValueOrDefault((cue.ActorId, skillId), cue.AffectedUnitIds);
            cues[index] = cue with { Path = Ray(cue.Origin, cue.Destination), AffectedUnitIds = affected };
        }
        BattlePresentationEffect[] effects = events.SelectMany(Effect).ToArray();
        for (int index = 0; index < cues.Count; index++)
        {
            BattlePresentationCue cue = cues[index];
            if (cue.SkillId is not null)
            {
                cues[index] = cue with { Effects = effects };
                continue;
            }
            if (cue.Kind == PresentationCueKind.StatusTick)
                cues[index] = cue with
                {
                    Effects = effects.Where(effect => effect.Kind == BattlePresentationEffectKind.StatusTicked &&
                        effect.TargetId == cue.TargetId).ToArray()
                };
        }
        return new BattlePresentationFrame(stage, before, after, cues, CompileNumbers(events));
    }

    private static PresentationCueKind ResolveAction(SkillDefinition skill) => skill.ExecutionKind switch
    {
        SkillExecutionKind.MeleeAttack or SkillExecutionKind.DirectAttack or SkillExecutionKind.Thrust or
            SkillExecutionKind.MultiStab or SkillExecutionKind.Bane or SkillExecutionKind.PoetCharge => PresentationCueKind.Melee,
        SkillExecutionKind.RangedAttack or SkillExecutionKind.HeavyShot or SkillExecutionKind.PoisonSpear => PresentationCueKind.Ranged,
        _ => PresentationCueKind.Cast
    };

    private static GridPoint ResolveActionDestination(SkillDefinition skill, SkillUsedEvent used,
        GridPoint origin, GridPoint fallback, IReadOnlyList<BattleEvent> events)
    {
        if (skill.ExecutionKind == SkillExecutionKind.Bane)
        {
            int dx = Math.Sign(fallback.X - origin.X);
            int dy = Math.Sign(fallback.Y - origin.Y);
            return new GridPoint(origin.X + dx * 2, origin.Y + dy * 2);
        }
        if (skill.ExecutionKind is not (SkillExecutionKind.SummonSkeleton or
            SkillExecutionKind.SummonSkeletonMage or SkillExecutionKind.SummonFireDemon or
            SkillExecutionKind.Decoy or SkillExecutionKind.PoetDecoyRetreat))
            return fallback;
        return events.OfType<UnitSummonedEvent>()
            .FirstOrDefault(value => value.OwnerId == used.ActorId)?.Cell ?? fallback;
    }

    private static BattlePresentationCue Cue(PresentationCueKind kind, UnitInstanceId actor, UnitInstanceId? target,
        ContentId? skill, GridPoint origin, GridPoint destination, IReadOnlyList<GridPoint> path,
        IReadOnlyList<UnitInstanceId> affected, UnitInstanceId? instigator = null) => new(kind, actor, target, skill, origin, destination, path, affected,
        [new(PresentationMarkerKind.Begin, 0), new(PresentationMarkerKind.Release, 1), new(PresentationMarkerKind.Impact, 2), new(PresentationMarkerKind.Recover, 3), new(PresentationMarkerKind.Complete, 4)], InstigatorId: instigator);

    private static BattleUiUnitSnapshot Find(BattleUiSnapshot before, BattleUiSnapshot after, UnitInstanceId id) =>
        before.Units.Concat(after.Units).First(value => value.UnitId == id);

    private static IReadOnlyList<GridPoint> Ray(GridPoint origin, GridPoint target)
    {
        int dx=target.X-origin.X,dy=target.Y-origin.Y,steps=GreatestCommonDivisor(Math.Abs(dx),Math.Abs(dy));
        if(steps==0)return [];
        int sx=dx/steps,sy=dy/steps;
        return Enumerable.Range(1,steps).Select(index=>new GridPoint(origin.X+sx*index,origin.Y+sy*index)).ToArray();
    }
    private static int GreatestCommonDivisor(int left,int right){while(right!=0)(left,right)=(right,left%right);return left;}

    private static IEnumerable<BattlePresentationEffect> Effect(BattleEvent value) => value switch
    {
        StatusAppliedEvent status => [new(BattlePresentationEffectKind.StatusApplied, status.SourceId, status.TargetId, status.StatusId, null, status.RemainingTurns)],
        StatusTickedEvent status => [new(BattlePresentationEffectKind.StatusTicked, status.SourceId, status.TargetId, status.StatusId, null, status.Amount)],
        StatusHealingTickedEvent status => [new(BattlePresentationEffectKind.StatusTicked, status.SourceId, status.TargetId, status.StatusId, null, status.Amount)],
        StatusDurationChangedEvent status => [new(BattlePresentationEffectKind.StatusDurationChanged, status.TargetId, status.TargetId, status.StatusId, null, status.RemainingTurns)],
        StatusStackChangedEvent status => [new(BattlePresentationEffectKind.StatusStackChanged, status.TargetId, status.TargetId, status.StatusId, null, status.StackCount)],
        StatusExpiredEvent status => [new(BattlePresentationEffectKind.StatusExpired, status.TargetId, status.TargetId, status.StatusId, null, 0)],
        StatusesCleansedEvent status => status.RemovedStatusIds.Select(id => new BattlePresentationEffect(BattlePresentationEffectKind.StatusCleansed, status.SourceId, status.TargetId, id, null, 0)),
        SpearDroppedEvent spear => [new(BattlePresentationEffectKind.SpearDropped, spear.OwnerId, spear.OwnerId, null, spear.Cell, 0)],
        SpearRecoveredEvent spear => [new(BattlePresentationEffectKind.SpearRecovered, spear.OwnerId, spear.OwnerId, null, spear.Cell, 0)],
        _ => []
    };

    private static IReadOnlyList<BattlePresentationNumber> CompileNumbers(IReadOnlyList<BattleEvent> events)
    {
        var result = new List<BattlePresentationNumber>();
        Dictionary<(UnitInstanceId SourceId, UnitInstanceId TargetId, ContentId SkillId), Queue<CombatRollResolvedEvent>> rolls =
            events.OfType<CombatRollResolvedEvent>()
                .GroupBy(value => (value.SourceId, value.TargetId, value.SkillId))
                .ToDictionary(group => group.Key, group => new Queue<CombatRollResolvedEvent>(group));
        foreach (BattleEvent value in events)
        {
            switch (value)
            {
                case DamageAppliedEvent damage when damage.Amount > 0:
                    CombatRollResolvedEvent? resolved = TakeRoll(rolls, damage);
                    BattlePresentationNumberKind kind = resolved?.Outcome == "critical"
                        ? BattlePresentationNumberKind.Critical : BattlePresentationNumberKind.Normal;
                    result.Add(new(kind, damage.TargetId, $"-{damage.Amount}", PresentationMarkerKind.Impact, result.Count));
                    break;
                case DamageAppliedEvent damage:
                    CombatRollResolvedEvent? roll = TakeRoll(rolls, damage);
                    if (roll?.Outcome == "dodge")
                        result.Add(new(BattlePresentationNumberKind.Miss, damage.TargetId, "Miss", PresentationMarkerKind.Impact, result.Count));
                    break;
                case StatusTickedEvent status when status.Amount > 0:
                    result.Add(new(BattlePresentationNumberKind.Normal, status.TargetId, $"-{status.Amount}", PresentationMarkerKind.Impact, result.Count));
                    break;
                case StatusHealingTickedEvent status when status.Amount > 0:
                    result.Add(new(BattlePresentationNumberKind.Heal, status.TargetId, $"+{status.Amount}", PresentationMarkerKind.Impact, result.Count));
                    break;
                case HealthRestoredEvent health when health.Amount > 0:
                    result.Add(new(BattlePresentationNumberKind.Heal, health.TargetId, $"+{health.Amount}", PresentationMarkerKind.Impact, result.Count));
                    break;
                case ManaRestoredEvent mana when mana.Amount > 0:
                    result.Add(new(BattlePresentationNumberKind.Mana, mana.TargetId, $"+{mana.Amount} MP", PresentationMarkerKind.Impact, result.Count));
                    break;
            }
        }
        return result;
    }

    private static CombatRollResolvedEvent? TakeRoll(
        IReadOnlyDictionary<(UnitInstanceId SourceId, UnitInstanceId TargetId, ContentId SkillId), Queue<CombatRollResolvedEvent>> rolls,
        DamageAppliedEvent damage) =>
        rolls.TryGetValue((damage.SourceId, damage.TargetId, damage.SkillId), out Queue<CombatRollResolvedEvent>? queue) && queue.Count > 0
            ? queue.Dequeue()
            : null;
}
