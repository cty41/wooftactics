using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Pathfinding;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.AI;

/// <summary>Declares which units are legal targets for an AI actor's decisions.</summary>
public enum TargetRelationshipStrategy
{
    /// <summary>Enemy faction only; the standard contract outside the possessed form.</summary>
    StandardHostile,

    /// <summary>Every living formal unit plus summons, excluding the actor and non-acting decoys.</summary>
    UnifiedAll
}

/// <summary>Generates current-position, reposition and move-then-skill candidates through canonical transitions.</summary>
public sealed class AiDecisionService
{
    private readonly BattleTransitionService _transitions;
    public AiDecisionService(BattleTransitionService? transitions = null) => _transitions = transitions ?? new BattleTransitionService();

    public AiTurnPlan Decide(BattleState state, AiDefinition definition,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills, int patternIndex = 0,
        TargetRelationshipStrategy strategy = TargetRelationshipStrategy.StandardHostile,
        UnitInstanceId? priorityTargetId = null,
        bool requirePriorityTarget = false)
    {
        BattleUnitState actor = state.Units[state.ActiveUnitId];
        BattleUnitState[] enemies = state.Units.Values.Where(unit => IsTarget(actor, unit, strategy))
            .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal).ToArray();
        var origins = new List<(GridPoint Cell, BattleState State)> { (actor.Unit.Position, state) };
        foreach (GridPoint cell in state.Board.Cells.Keys.OrderBy(cell => cell.X).ThenBy(cell => cell.Y))
        {
            BattleTransition move = _transitions.Apply(state, new MoveUnitCommand(actor.Unit.InstanceId, cell));
            if (move.Succeeded) origins.Add((cell, move.State));
        }

        var candidates = new List<AiIntentCandidate>();
        foreach (ContentId skillId in definition.SkillIds.OrderBy(value => value.Value, StringComparer.Ordinal))
        {
            SkillDefinition skill = skills[skillId];
            if (skill.ExecutionKind == SkillExecutionKind.DemonicRegeneration)
            {
                BattleTransition selfProbe = _transitions.Apply(state, new UseSkillCommand(
                    actor.Unit.InstanceId, actor.Unit.InstanceId, actor.Unit.Position, skill));
                if (selfProbe.Succeeded)
                {
                    const bool regeneration = true;
                    float missingHealth = 1f - actor.CurrentHealth / (float)actor.MaxHealth;
                    float priority = regeneration ? 20f + missingHealth * 30f : 18f;
                    candidates.Add(new AiIntentCandidate(
                        regeneration ? AiIntentKind.Retreat : AiIntentKind.Debuff,
                        skillId, actor.Unit.Position, actor.Unit.InstanceId, actor.Unit.Position,
                        0, 0, 0, regeneration ? missingHealth * 10f : definition.Profile.HarmfulStatusWeight,
                        true, string.Empty, priority));
                }
                continue;
            }
            foreach ((GridPoint origin, BattleState probeState) in origins)
            foreach (BattleUnitState originalTarget in enemies)
            {
                BattleUnitState target = probeState.Units[originalTarget.Unit.InstanceId];
                GridPoint targetCell = skill.ExecutionKind == SkillExecutionKind.Bane
                    ? DirectionCell(origin, target.Unit.Position)
                    : target.Unit.Position;
                BattleTransition probe = _transitions.Apply(probeState, new UseSkillCommand(actor.Unit.InstanceId, target.Unit.InstanceId, targetCell, skill));
                if (!probe.Succeeded || !ActuallyTargets(probeState, probe, actor.Unit.InstanceId,
                        target.Unit.InstanceId, skill)) continue;
                int distance = Manhattan(origin, target.Unit.Position);
                int targets = skill.ExecutionKind == SkillExecutionKind.AreaBlast
                    ? probeState.Units.Values.Count(unit => IsTarget(actor, unit, strategy) &&
                        Manhattan(unit.Unit.Position, target.Unit.Position) <= 2) : 1;
                bool basic = skill.ExecutionKind is SkillExecutionKind.MeleeAttack or SkillExecutionKind.MagicAttack or SkillExecutionKind.RangedAttack;
                AiIntentKind intent = skill.ExecutionKind == SkillExecutionKind.AreaBlast ? AiIntentKind.AreaAttack : skill.ExecutionKind == SkillExecutionKind.AmplifyDamage ? AiIntentKind.Debuff : AiIntentKind.Attack;
                float basePriority = basic ? IntentPriority(definition, "BasicAttack", 25) : 10;
                float proximity = GraphScore(definition, "BasicAttack", "DistanceToTarget", Math.Clamp(distance / 10f, 0f, 1f), 5f);
                float damage = skill.Damage * definition.Profile.DamageWeight;
                float targetScore = targets * definition.Profile.TargetCountWeight +
                    GraphScore(definition, "BasicAttack", "TargetHealth", target.CurrentHealth / (float)target.MaxHealth, 0f) +
                    (priorityTargetId is UnitInstanceId priority && target.Unit.InstanceId == priority ? 100f : 0f);
                float status = intent == AiIntentKind.Debuff ? definition.Profile.HarmfulStatusWeight : 0;
                float reposition = PreferredRangeBonus(definition, actor.Unit.Position, origin, target.Unit.Position);
                candidates.Add(new AiIntentCandidate(intent, skillId, origin, target.Unit.InstanceId, targetCell,
                    proximity + reposition, damage, targetScore, status, true, string.Empty, basePriority, origin != actor.Unit.Position));
                if (target.CurrentHealth <= Math.Max(1, skill.Damage))
                    candidates.Add(new AiIntentCandidate(AiIntentKind.FinishOff, skillId, origin, target.Unit.InstanceId, targetCell,
                        GraphScore(definition, "FinishOff", "DistanceToTarget", Math.Clamp(distance / 10f, 0f, 1f), 5f) + reposition,
                        damage + GraphScore(definition, "FinishOff", "KillPotential", 1f, 8f), targetScore, status, true, string.Empty,
                        IntentPriority(definition, "FinishOff", 35), origin != actor.Unit.Position));
            }
        }

        foreach (BattleUnitState target in enemies)
        foreach ((GridPoint origin, _) in origins.Where(value => value.Cell != actor.Unit.Position)
                     .OrderBy(value => Manhattan(value.Cell, target.Unit.Position)).Take(definition.MaximumEngageCandidatesPerTarget))
        {
            float proximity = GraphScore(definition, "Engage", "DistanceToTarget", Math.Clamp(Manhattan(origin, target.Unit.Position) / 10f, 0f, 1f), 5f);
            candidates.Add(new AiIntentCandidate(AiIntentKind.Engage, null, origin, target.Unit.InstanceId, target.Unit.Position,
                proximity + PreferredRangeBonus(definition, actor.Unit.Position, origin, target.Unit.Position), 0, 0, 0, true, string.Empty,
                IntentPriority(definition, "Engage", 15)));
        }

        if (enemies.Length > 0 && actor.CurrentHealth <= actor.MaxHealth * .3f)
        {
            (GridPoint Cell, BattleState State) retreat = origins.OrderByDescending(value => enemies.Min(enemy => Manhattan(value.Cell, enemy.Unit.Position)))
                .ThenBy(value => value.Cell.X).ThenBy(value => value.Cell.Y).First();
            if (retreat.Cell != actor.Unit.Position)
                candidates.Add(new AiIntentCandidate(AiIntentKind.Retreat, null, retreat.Cell, null, retreat.Cell, 0, 0, 0, 0, true, string.Empty,
                    IntentPriority(definition, "Retreat", 30)));
        }
        candidates.Add(new AiIntentCandidate(AiIntentKind.HoldPosition, null, actor.Unit.Position, null, actor.Unit.Position, 0, 0, 0, 0, true, string.Empty,
            IntentPriority(definition, "HoldPosition", 1)));

        IReadOnlyList<AiIntentCandidate> eligibleCandidates = candidates;
        bool priorityForced = false;
        if (requirePriorityTarget && priorityTargetId is UnitInstanceId requiredTarget)
        {
            AiIntentCandidate[] forced = candidates.Where(candidate => candidate.TargetId == requiredTarget &&
                candidate.SkillId is ContentId skillId && skills[skillId].IsDirectAttack &&
                candidate.Intent is AiIntentKind.Attack or AiIntentKind.FinishOff)
                .ToArray();
            if (forced.Length > 0)
            {
                eligibleCandidates = forced;
                priorityForced = true;
            }
        }

        if (definition.Archetype == AiArchetype.PredatoryDiver && !priorityForced)
            return SelectPredatoryDiver(state, actor, skills, enemies, eligibleCandidates, patternIndex);

        IOrderedEnumerable<AiIntentCandidate> ranked = eligibleCandidates.OrderByDescending(item => item.TotalScore).ThenBy(item => item.Intent)
            .ThenBy(item => item.SkillId?.Value ?? string.Empty, StringComparer.Ordinal).ThenBy(item => item.Destination.X).ThenBy(item => item.Destination.Y)
            .ThenBy(item => item.TargetId?.Value ?? string.Empty, StringComparer.Ordinal);
        ContentId? patternSkill = definition.PatternSkillIds.Count == 0 ? null : definition.PatternSkillIds[patternIndex % definition.PatternSkillIds.Count];
        AiIntentCandidate? patternCandidate = patternSkill is null ? null : ranked.FirstOrDefault(item => item.SkillId == patternSkill);
        AiIntentCandidate selected = patternCandidate ?? ranked.First();
        return new AiTurnPlan(actor.Unit.InstanceId, selected, candidates, patternIndex, patternCandidate is not null);
    }

    private AiTurnPlan SelectPredatoryDiver(BattleState state, BattleUnitState actor,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills, IReadOnlyList<BattleUnitState> enemies,
        IReadOnlyList<AiIntentCandidate> candidates, int patternIndex)
    {
        var pathfinder = new DeterministicDijkstraPathfinder();
        int Cost(AiIntentCandidate candidate)
        {
            IReadOnlyList<GridPoint> path = pathfinder.FindPath(state.CreateMovementBoard(actor.Unit.InstanceId),
                actor.Unit.Position, candidate.Destination, movementKind: actor.Unit.MovementKind);
            return DeterministicDijkstraPathfinder.MovementPointCost(state.Board, path, actor.Unit.MovementKind);
        }

        bool IsKillable(AiIntentCandidate candidate)
        {
            BattleState probeState = state;
            if (candidate.Destination != actor.Unit.Position)
            {
                BattleTransition move = _transitions.Apply(state,
                    new MoveUnitCommand(actor.Unit.InstanceId, candidate.Destination));
                if (!move.Succeeded) return false;
                probeState = move.State;
            }
            SkillDefinition skill = skills[candidate.SkillId!.Value];
            BattleTransition attack = _transitions.Apply(probeState,
                new UseSkillCommand(actor.Unit.InstanceId, candidate.TargetId, candidate.TargetCell, skill));
            return attack.Succeeded && !attack.State.Units[candidate.TargetId!.Value].IsAlive;
        }

        AiIntentCandidate? attack = candidates
            .Where(candidate => candidate.SkillId is ContentId id &&
                skills[id].ExecutionKind == SkillExecutionKind.DirectAttack && candidate.TargetId is not null)
            .OrderByDescending(IsKillable)
            .ThenBy(candidate => state.Units[candidate.TargetId!.Value].CurrentHealth)
            .ThenBy(Cost)
            .ThenBy(candidate => candidate.TargetId!.Value.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (attack is not null)
            return new AiTurnPlan(actor.Unit.InstanceId, attack, candidates, patternIndex, false);

        UnitInstanceId? prey = enemies.OrderBy(enemy => enemy.CurrentHealth)
            .ThenBy(enemy => enemy.Unit.InstanceId.Value, StringComparer.Ordinal)
            .Select(enemy => (UnitInstanceId?)enemy.Unit.InstanceId).FirstOrDefault();
        AiIntentCandidate selected = candidates.Where(candidate => candidate.Intent == AiIntentKind.Engage &&
                candidate.TargetId == prey)
            .OrderBy(candidate => Manhattan(candidate.Destination, candidate.TargetCell))
            .ThenBy(Cost).ThenBy(candidate => candidate.Destination.X).ThenBy(candidate => candidate.Destination.Y)
            .FirstOrDefault() ?? candidates.Single(candidate => candidate.Intent == AiIntentKind.HoldPosition);
        return new AiTurnPlan(actor.Unit.InstanceId, selected, candidates, patternIndex, false);
    }

    private static bool ActuallyTargets(BattleState before, BattleTransition transition,
        UnitInstanceId actorId, UnitInstanceId targetId, SkillDefinition skill)
    {
        DamageAppliedEvent[] damage = transition.Events.OfType<DamageAppliedEvent>()
            .Where(value => value.SourceId == actorId && value.SkillId == skill.ContentId).ToArray();
        if (damage.Length > 0)
            return damage.Any(value => value.TargetId == targetId);

        CombatRollResolvedEvent[] rolls = transition.Events.OfType<CombatRollResolvedEvent>()
            .Where(value => value.SourceId == actorId && value.SkillId == skill.ContentId).ToArray();
        if (rolls.Length > 0)
            return rolls.Any(value => value.TargetId == targetId);

        StatusAppliedEvent[] statuses = transition.Events.OfType<StatusAppliedEvent>()
            .Where(value => value.SourceId == actorId).ToArray();
        if (statuses.Length > 0)
            return statuses.Any(value => value.TargetId == targetId);

        if (before.Units.TryGetValue(targetId, out BattleUnitState? previous) &&
            transition.State.Units.TryGetValue(targetId, out BattleUnitState? current) &&
            !Equals(previous, current))
            return true;

        return transition.Events.OfType<SkillUsedEvent>().Any(value =>
            value.ActorId == actorId && value.SkillId == skill.ContentId && value.TargetId == targetId);
    }

    private static float PreferredRangeBonus(AiDefinition definition, GridPoint current, GridPoint destination, GridPoint target)
    {
        if (definition.PreferredRangeRepositionBonus <= 0 || Manhattan(current, target) >= definition.PreferredMinimumRange) return 0;
        int distance = Manhattan(destination, target);
        return distance >= definition.PreferredMinimumRange && distance <= definition.PreferredMaximumRange ? definition.PreferredRangeRepositionBonus : 0;
    }

    private static GridPoint DirectionCell(GridPoint origin, GridPoint target)
    {
        int dx = target.X - origin.X;
        int dy = target.Y - origin.Y;
        if (dx != 0 && dy != 0) return target;
        return new GridPoint(origin.X + Math.Sign(dx), origin.Y + Math.Sign(dy));
    }

    private static bool IsTarget(BattleUnitState actor, BattleUnitState unit, TargetRelationshipStrategy strategy)
    {
        if (!unit.IsAlive || unit.Unit.InstanceId == actor.Unit.InstanceId) return false;
        return strategy switch
        {
            TargetRelationshipStrategy.UnifiedAll => !SkillRuntimeService.IsNonActingDecoy(unit),
            _ => unit.Unit.PlayerNumber != actor.Unit.PlayerNumber
        };
    }

    private static float IntentPriority(AiDefinition definition, string intentType, float fallback) =>
        definition.DecisionGraph?.Intents.FirstOrDefault(intent => intent.Enabled && intent.IntentType == intentType)?.BasePriority ?? fallback;

    private static float GraphScore(AiDefinition definition, string intentType, string scoreType, float input, float fallbackWeight)
    {
        AiDecisionGraphDefinition? graph = definition.DecisionGraph;
        AiIntentDefinition? intent = graph?.Intents.FirstOrDefault(value => value.Enabled && value.IntentType == intentType);
        if (graph is null || intent is null)
            return (1f - input) * fallbackWeight;
        HashSet<string> connected = graph.Edges.Where(edge => edge.SourceNodeId == intent.NodeId).Select(edge => edge.TargetNodeId).ToHashSet(StringComparer.Ordinal);
        AiScoreDefinition? score = graph.Scores.FirstOrDefault(value => value.Enabled && value.ScoreType == scoreType && connected.Contains(value.NodeId));
        if (score is null)
            return 0;
        return EvaluateCurve(score.Curve, input) * score.Weight;
    }

    private static float EvaluateCurve(IReadOnlyList<AiCurveKey> keys, float input)
    {
        if (keys.Count == 0) return input;
        AiCurveKey[] ordered = keys.OrderBy(key => key.Time).ToArray();
        if (input <= ordered[0].Time) return ordered[0].Value;
        for (int index = 1; index < ordered.Length; index++)
        {
            if (input > ordered[index].Time) continue;
            AiCurveKey left = ordered[index - 1]; AiCurveKey right = ordered[index];
            float amount = (input - left.Time) / Math.Max(float.Epsilon, right.Time - left.Time);
            return left.Value + ((right.Value - left.Value) * amount);
        }
        return ordered[^1].Value;
    }
    private static int Manhattan(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}
