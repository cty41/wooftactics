using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Pathfinding;
using Tactics.Core.Randomness;
using Tactics.Core.Statuses;
using Tactics.Core.Units;

namespace Tactics.Core.Skills;

/// <summary>Executes the normalized starting-skill contract without engine or presentation dependencies.</summary>
public sealed class SkillRuntimeService
{
    public const string ContractId = "skill-runtime-v1";
    private static readonly ContentId SkeletonDefinitionId = new("unit.pure-run.skeleton-warrior");
    private static readonly ContentId FireDemonDefinitionId = new("unit.pure-run.fire-demon");
    private static readonly ContentId SkeletonMageDefinitionId = new("unit.pure-run.skeleton-mage");
    private static readonly ContentId DecoyDefinitionId = new("unit.pure-run.amazon-decoy");
    private static readonly ContentId PoetDecoyDefinitionId = new("unit.pure-run.poet-decoy");
    private static readonly ContentId PoetMoonDrinkFamilyUseId = new("skill-family.poet.moon-drink");
    public static readonly ContentId RunPermanentDeathStatusId = new("status.run.permanent-death");
    private readonly ILineOfSightService _lineOfSight;
    private readonly StatusRuntimeService _statuses;

    public SkillRuntimeService(ILineOfSightService? lineOfSight = null, StatusRuntimeService? statuses = null)
    {
        _lineOfSight = lineOfSight ?? new ShadowConeLineOfSight();
        _statuses = statuses ?? new StatusRuntimeService();
    }

    public BattleTransition Apply(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        string? usageFailure = UsageFailure(actor, skill);
        if (usageFailure is not null) return Reject(state, actor, usageFailure);
        if (skill.IsPassive) return ApplyPassive(state, actor, skill);
        if (actor.CurrentMana < skill.ManaCost) return Reject(state, actor, "insufficient_mana");
        if (skill.ExecutionKind == SkillExecutionKind.PickupSpear) return ApplyPickup(state, actor, command);
        if (skill.ExecutionKind is SkillExecutionKind.SummonSkeleton or SkillExecutionKind.SummonSkeletonMage or SkillExecutionKind.SummonFireDemon) return ApplySummon(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.Teleport) return ApplyRelocation(state, actor, command, createDecoy: false);
        if (skill.ExecutionKind == SkillExecutionKind.Decoy) return ApplyRelocation(state, actor, command, createDecoy: true);
        if (skill.ExecutionKind == SkillExecutionKind.RecoverSpear) return ApplyRecoverSpear(state, actor, command);
        if (skill.ExecutionKind is SkillExecutionKind.IceArmor or SkillExecutionKind.BoneShield) return ApplySelfDefense(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.DemonicRegeneration) return ApplyDemonicRegeneration(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.MultiStab) return ApplyMultiStab(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.PoetCharge) return ApplyPoetCharge(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.PoetSwordRain) return ApplyPoetSwordRain(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.PoetWineHeal) return ApplyPoetWineHeal(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.PoetAgilityVerse) return ApplyPoetAgilityVerse(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.PoetMoonDrink) return ApplyPoetMoonDrink(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.PoetDecoyRetreat) return ApplyPoetDecoyRetreat(state, actor, command);
        if (skill.RequiresLineOfSight && state.Board.Contains(command.TargetCell) &&
            !_lineOfSight.Trace(state.Board, actor.Unit.Position, command.TargetCell,
                LivingBlockers(state, actor.Unit.InstanceId, command.TargetCell, skill.ExecutionKind)).IsClear)
            return Reject(state, actor, "line_of_sight_blocked");

        BattleUnitState[] targets = ResolveTargets(state, actor, command).ToArray();
        if (targets.Length == 0 && skill.ExecutionProfile.AllowsEmptyTarget)
        {
            BattleUnitState used = actor.WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
            var emptyEvents = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId) };
            if (skill.ManaCost > 0) emptyEvents.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, used.CurrentMana));
            emptyEvents.Add(new SemanticCueEmittedEvent(actor.Unit.InstanceId, null, skill.ContentId, "resolution"));
            return new BattleTransition(state.WithUnit(used), emptyEvents);
        }
        if (targets.Length == 0) return Reject(state, actor, "no_valid_target");
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, targets[0].Unit.InstanceId, skill.ContentId) };
        BattleState next = state;
        BattleUnitState updatedActor = actor.WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        if (skill.ExecutionProfile.MovementDamagePerCell > 0) updatedActor = updatedActor.ResetMovementCells();
        if (skill.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, updatedActor.CurrentMana));
        next = next.WithUnit(updatedActor);

        UnitInstanceId primaryTargetId = targets[0].Unit.InstanceId;
        foreach (BattleUnitState originalTarget in targets)
        {
            BattleUnitState target = next.Units[originalTarget.Unit.InstanceId];
            if (!IsHostile(state, actor, target)) return Reject(state, actor, "target_not_enemy");
            if (!target.IsAlive) return Reject(state, actor, "target_defeated");
            if (originalTarget.Unit.InstanceId == primaryTargetId &&
                skill.ExecutionProfile.DetonateStatusContentId is ContentId detonateId &&
                target.Statuses.TryGetValue(detonateId, out BattleStatusState? detonated))
            {
                int detonationDamage = LimitDirectHitDamage(target,
                    Math.Min(target.CurrentHealth, Math.Max(0, detonated.StackCount)));
                target = target.WithoutStatus(detonateId).WithHealth(target.CurrentHealth - detonationDamage);
                events.Add(new StatusExpiredEvent(target.Unit.InstanceId, detonateId));
                if (detonationDamage > 0)
                    events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId,
                        detonationDamage, target.CurrentHealth));
                next = next.WithUnit(target);
                next = ApplyDefeat(next, actor, originalTarget, target, events);
                if (!target.IsAlive) continue;
            }
            bool dodged = false;
            bool critical = false;
            if (skill.Damage > 0 || skill.ExecutionKind is SkillExecutionKind.MagicAttack or SkillExecutionKind.MeleeAttack)
            {
                var random = new DeterministicRandom(next.RandomState);
                int hitRoll = random.NextInt(100);
                int accuracy = UnitCombatStatRules.Accuracy(actor.Unit.EffectiveAttributes);
                int dodge = UnitCombatStatRules.Dodge(target.Unit.EffectiveAttributes) +
                    (target.HasCombatTechniquesLevelOne ? 30 : 0);
                int hitChance = Math.Clamp((int)Math.Floor((accuracy - dodge) * skill.ExecutionProfile.AccuracyFactor), 0, 100);
                dodged = hitRoll >= hitChance;
                int criticalRoll = random.NextInt(100);
                int criticalChance = Math.Clamp(UnitCombatStatRules.CriticalChance(actor.Unit.EffectiveAttributes) +
                    (actor.CombatTechniquesLevel >= 3 ? 20 : 0), 0, 100);
                critical = skill.CanCrit && !dodged && (_statuses.EvaluateBeforeAttack(target).ForceCritical || criticalRoll < criticalChance);
                events.Add(new CombatRollResolvedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId, hitRoll, dodge, dodged ? "dodge" : critical ? "critical" : "hit", random.State));
                next = next.WithRandomState(random.State);
            }

            int rawDamage = skill.ExecutionKind switch
            {
                SkillExecutionKind.MagicAttack => actor.MagicalAttack,
                SkillExecutionKind.MeleeAttack => actor.PhysicalAttack,
                SkillExecutionKind.Fireball when originalTarget.Unit.InstanceId != primaryTargetId => Math.Max(1, skill.Damage / 2),
                SkillExecutionKind.Thrust => checked(skill.Damage + actor.MovementCellsThisTurn * skill.ExecutionProfile.MovementDamagePerCell),
                _ => skill.Damage
            };
            SkillEffectScalingKind scaling = EffectiveScaling(skill);
            if (rawDamage > 0 && scaling != SkillEffectScalingKind.None)
                rawDamage = checked(rawDamage + UnitCombatStatRules.AttributeContribution(
                    actor.Unit.EffectiveAttributes, EffectiveRole(actor, skill), scaling));
            if (actor.Statuses.Values.Any(status => status.EffectKind == StatusEffectKind.DamageOutputReduction))
                rawDamage = (int)MathF.Round(rawDamage * 0.75f, MidpointRounding.AwayFromZero);
            if (critical)
                rawDamage = checked((int)Math.Floor(rawDamage * UnitCombatStatRules.CriticalMultiplier(actor.Unit.EffectiveAttributes)));
            StatusDamagePolicy damagePolicy = _statuses.EvaluateDamageTaken(target, actor, skill.MaxRange > 1);
            int damage = dodged ? 0 : (int)MathF.Round(rawDamage * damagePolicy.DamageMultiplier, MidpointRounding.AwayFromZero);
            if (!dodged && target.DamageShield is BattleDamageShieldState shield &&
                (skill.DamageKind == SkillDamageKind.Physical || shield.AbsorbsAllDamage))
            {
                int absorbed = Math.Min(shield.RemainingPoints, damage);
                damage -= absorbed;
                int remaining = shield.RemainingPoints - absorbed;
                target = target.WithDamageShield(remaining > 0 ? shield with { RemainingPoints = remaining } : null);
                events.Add(new DamageShieldAbsorbedEvent(target.Unit.InstanceId, skill.ContentId, absorbed, remaining));
            }
            damage = LimitDirectHitDamage(target, damage);
            int beforeHealth = target.CurrentHealth;
            int health = Math.Max(0, beforeHealth - damage);
            target = target.WithHealth(health);
            int actualDamage = beforeHealth - health;
            events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId, actualDamage, health));
            next = next.WithUnit(target);
            if (skill.ExecutionProfile.LifeStealPercent > 0)
            {
                BattleUnitState currentActor = next.Units[actor.Unit.InstanceId];
                int requested = actualDamage * skill.ExecutionProfile.LifeStealPercent / 100;
                BattleUnitState restored = currentActor.WithHealth(currentActor.CurrentHealth + requested);
                int restoredAmount = restored.CurrentHealth - currentActor.CurrentHealth;
                next = next.WithUnit(restored);
                events.Add(new HealthRestoredEvent(actor.Unit.InstanceId, actor.Unit.InstanceId,
                    skill.ContentId, restoredAmount, restored.CurrentHealth));
            }

            if (target.IsAlive && !dodged && skill.StatusContentId is ContentId statusId)
            {
                bool applyStatus = true;
                if (skill.ExecutionProfile.StatusChancePercent < 100)
                {
                    var random = new DeterministicRandom(next.RandomState);
                    int roll = random.NextInt(100);
                    applyStatus = roll < skill.ExecutionProfile.StatusChancePercent;
                    events.Add(new StatusRollResolvedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId,
                        statusId, roll, skill.ExecutionProfile.StatusChancePercent, applyStatus, random.State));
                    next = next.WithRandomState(random.State);
                }
                if (applyStatus)
                {
                    StatusDefinition definition = StatusFor(skill, statusId);
                    int duration = skill.ExecutionKind == SkillExecutionKind.IceBolt && originalTarget.Unit.InstanceId != primaryTargetId
                        ? 1 : skill.StatusDuration;
                    StatusApplicationResult application = _statuses.Apply(target, definition, actor.Unit.InstanceId, duration);
                    target = application.Unit;
                    events.Add(new StatusAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, statusId, application.AppliedStatus.RemainingTurns));
                }
            }
            next = next.WithUnit(target);
            next = ApplyDefeat(next, actor, originalTarget, target, events);

            if (originalTarget.Unit.InstanceId == primaryTargetId && target.IsAlive && !dodged &&
                actor.Unit.DefinitionId == new ContentId("unit.pure-run.amazon") && actor.CombatTechniquesLevel >= 2 &&
                skill.ExecutionKind == SkillExecutionKind.MeleeAttack)
            {
                var followUpRandom = new DeterministicRandom(next.RandomState);
                int followUpRoll = followUpRandom.NextInt(100);
                events.Add(new CombatRollResolvedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId,
                    followUpRoll, 30, followUpRoll < 30 ? "combat-techniques-follow-up" : "combat-techniques-no-follow-up",
                    followUpRandom.State));
                next = next.WithRandomState(followUpRandom.State);
                if (followUpRoll < 30)
                {
                    BattleUnitState current = next.Units[target.Unit.InstanceId];
                    int followUpDamage = LimitDirectHitDamage(current,
                        Math.Min(current.CurrentHealth, actor.PhysicalAttack));
                    BattleUnitState followed = current.WithHealth(current.CurrentHealth - followUpDamage);
                    events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, current.Unit.InstanceId, skill.ContentId,
                        followUpDamage, followed.CurrentHealth));
                    next = next.WithUnit(followed);
                    next = ApplyDefeat(next, actor, current, followed, events);
                }
            }
        }
        if (skill.ExecutionKind == SkillExecutionKind.IceBolt && skill.ExecutionProfile.BounceCount > 0 &&
            next.TryGetUnit(primaryTargetId, out BattleUnitState? primary) && primary is not null)
        {
            foreach (BattleUnitState candidate in next.Units.Values
                         .Where(unit => unit.IsAlive && IsHostile(next, actor, unit) && unit.Unit.InstanceId != primaryTargetId)
                         .Where(unit => Manhattan(unit.Unit.Position, primary.Unit.Position) <= skill.ExecutionProfile.BounceRange)
                         .OrderBy(unit => Manhattan(unit.Unit.Position, primary.Unit.Position))
                         .ThenBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal)
                         .Take(skill.ExecutionProfile.BounceCount).ToArray())
            {
                BattleUnitState bounce = next.Units[candidate.Unit.InstanceId];
                var random = new DeterministicRandom(next.RandomState);
                int roll = random.NextInt(100);
                bool dodged = bounce.HasCombatTechniquesLevelOne && roll < 30;
                events.Add(new CombatRollResolvedEvent(actor.Unit.InstanceId, bounce.Unit.InstanceId, skill.ContentId,
                    roll, bounce.HasCombatTechniquesLevelOne ? 30 : 0, dodged ? "dodge" : "hit", random.State));
                next = next.WithRandomState(random.State);
                int damage = LimitDirectHitDamage(bounce,
                    dodged ? 0 : Math.Min(bounce.CurrentHealth, Math.Max(1, skill.Damage / 2)));
                BattleUnitState damaged = bounce.WithHealth(bounce.CurrentHealth - damage);
                events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, bounce.Unit.InstanceId, skill.ContentId, damage, damaged.CurrentHealth));
                if (!dodged && damaged.IsAlive && skill.StatusContentId is ContentId slowId)
                {
                    StatusApplicationResult application = _statuses.Apply(damaged, StatusFor(skill, slowId), actor.Unit.InstanceId, 1);
                    damaged = application.Unit;
                    events.Add(new StatusAppliedEvent(actor.Unit.InstanceId, damaged.Unit.InstanceId, slowId, 1));
                }
                next = next.WithUnit(damaged);
                next = ApplyDefeat(next, actor, bounce, damaged, events);
            }
        }
        events.Add(new SemanticCueEmittedEvent(actor.Unit.InstanceId, targets[0].Unit.InstanceId, skill.ContentId, "resolution"));
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyPassive(BattleState state, BattleUnitState actor, SkillDefinition skill)
    {
        if (skill.ExecutionKind == SkillExecutionKind.Mindfulness)
        {
            DemonboundBattleState current = actor.DemonboundState ?? new DemonboundBattleState();
            BattleUnitState mindful = actor.WithDemonboundState(current.WithMindfulnessLevel(skill.Level));
            return new BattleTransition(state.WithUnit(mindful), new BattleEvent[]
            {
                new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId),
                new SemanticCueEmittedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId, "passive-enabled")
            });
        }
        if (skill.ExecutionKind != SkillExecutionKind.CombatTechniques) return Reject(state, actor, "unsupported_passive");
        BattleUnitState updated = actor.WithCombatTechniquesLevel(skill.Level).WithSuccessfulSkillUse(skill.ContentId);
        return new BattleTransition(state.WithUnit(updated), new BattleEvent[]
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId),
            new SemanticCueEmittedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId, "passive-enabled")
        });
    }

    private BattleTransition ApplyPoetCharge(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        int dx = command.TargetCell.X - actor.Unit.Position.X;
        int dy = command.TargetCell.Y - actor.Unit.Position.Y;
        int selectedDistance = Manhattan(actor.Unit.Position, command.TargetCell);
        if (selectedDistance < skill.MinRange || selectedDistance > skill.MaxRange ||
            (dx == 0) == (dy == 0))
            return Reject(state, actor, "poet_charge_requires_cardinal_line");

        int stepX = Math.Sign(dx);
        int stepY = Math.Sign(dy);
        BattleUnitState? target = null;
        GridPoint destination = actor.Unit.Position;
        for (int step = 1; step <= selectedDistance; step++)
        {
            GridPoint cell = new(actor.Unit.Position.X + stepX * step, actor.Unit.Position.Y + stepY * step);
            if (!state.Board.Contains(cell) || !state.Board.GetCell(cell).CanTraverse(actor.Unit.MovementKind))
                return Reject(state, actor, "poet_charge_path_blocked");
            BattleUnitState? occupant = state.Units.Values.FirstOrDefault(unit =>
                unit.IsAlive && unit.Unit.InstanceId != actor.Unit.InstanceId && unit.Unit.Position == cell);
            if (occupant is null)
            {
                destination = cell;
                continue;
            }
            if (!IsHostile(state, actor, occupant))
                return Reject(state, actor, "poet_charge_path_blocked");
            target = occupant;
            break;
        }
        if (target is null) return Reject(state, actor, "poet_charge_enemy_not_found");

        BattleUnitState spent = actor.WithPosition(destination, actor.HasMovedThisTurn)
            .WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        BattleState next = state.WithUnit(spent);
        var events = new List<BattleEvent>
        {
            new SkillUsedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId)
        };
        if (skill.ManaCost > 0)
            events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, spent.CurrentMana));
        if (destination != actor.Unit.Position)
            events.Add(new UnitMovedEvent(actor.Unit.InstanceId, actor.Unit.Position, destination,
                CardinalPath(actor.Unit.Position, destination)));

        int rawDamage = checked(skill.Damage + actor.Unit.EffectiveAttributes.Strength +
            (skill.Level >= 2 ? Math.Max(0, actor.Unit.EffectiveAttributes.Strength - 5) : 0));
        PoetDamageSegmentResult segment = ApplyPoetDamageSegment(next, spent, target, skill, rawDamage, events);
        next = segment.State;
        if (segment.Defeated && skill.Level >= 3)
        {
            int requestedRefund = skill.ExecutionProfile.KillManaRefund > 0
                ? skill.ExecutionProfile.KillManaRefund : 6;
            BattleUnitState currentActor = next.Units[actor.Unit.InstanceId];
            BattleUnitState refunded = currentActor.WithMana(currentActor.CurrentMana + requestedRefund);
            int amount = refunded.CurrentMana - currentActor.CurrentMana;
            next = next.WithUnit(refunded);
            events.Add(new ManaRestoredEvent(actor.Unit.InstanceId, actor.Unit.InstanceId,
                skill.ContentId, amount, refunded.CurrentMana));
        }
        events.Add(new SemanticCueEmittedEvent(actor.Unit.InstanceId, target.Unit.InstanceId,
            skill.ContentId, "resolution"));
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyPoetSwordRain(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        int distance = Manhattan(actor.Unit.Position, command.TargetCell);
        if (!state.Board.Contains(command.TargetCell) || distance < skill.MinRange || distance > skill.MaxRange)
            return Reject(state, actor, "poet_sword_rain_center_out_of_range");
        if (!_lineOfSight.Trace(state.Board, actor.Unit.Position, command.TargetCell,
                LivingBlockers(state, actor.Unit.InstanceId, command.TargetCell, skill.ExecutionKind)).IsClear)
            return Reject(state, actor, "line_of_sight_blocked");
        int radius = skill.ExecutionProfile.AreaRadius > 0 ? skill.ExecutionProfile.AreaRadius : 2;
        BattleUnitState[] targets = state.Units.Values
            .Where(unit => unit.IsAlive && IsHostile(state, actor, unit) &&
                           Manhattan(unit.Unit.Position, command.TargetCell) <= radius)
            .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal).ToArray();
        if (targets.Length == 0) return Reject(state, actor, "no_valid_target");

        BattleUnitState spent = actor.WithMana(actor.CurrentMana - skill.ManaCost)
            .WithSuccessfulSkillUse(skill.ContentId);
        BattleState next = state.WithUnit(spent);
        var events = new List<BattleEvent>
        {
            new SkillUsedEvent(actor.Unit.InstanceId, targets[0].Unit.InstanceId, skill.ContentId)
        };
        if (skill.ManaCost > 0)
            events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, spent.CurrentMana));
        int rawDamage = checked(skill.Damage + actor.Unit.EffectiveAttributes.Strength / 2);
        foreach (BattleUnitState original in targets)
        {
            if (!next.TryGetUnit(original.Unit.InstanceId, out BattleUnitState? current) || current is null || !current.IsAlive)
                continue;
            next = ApplyPoetDamageSegment(next, spent, current, skill, rawDamage, events).State;
        }

        int repeatChance = skill.ExecutionProfile.RepeatChancePercent > 0
            ? skill.ExecutionProfile.RepeatChancePercent : skill.Level >= 2 ? 25 : 0;
        if (repeatChance > 0)
        {
            var repeatRandom = new DeterministicRandom(next.RandomState);
            int repeatRoll = repeatRandom.NextInt(100);
            bool repeat = repeatRoll < repeatChance;
            next = next.WithRandomState(repeatRandom.State);
            events.Add(new CombatRollResolvedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId,
                skill.ContentId, repeatRoll, repeatChance,
                repeat ? "poet-sword-rain-repeat" : "poet-sword-rain-no-repeat", repeatRandom.State));
            if (repeat)
            {
                int repeatPercent = skill.ExecutionProfile.RepeatDamagePercent > 0
                    ? skill.ExecutionProfile.RepeatDamagePercent : 50;
                int repeatDamage = (int)Math.Floor(rawDamage * repeatPercent / 100d);
                foreach (BattleUnitState original in targets)
                {
                    if (!next.TryGetUnit(original.Unit.InstanceId, out BattleUnitState? current) || current is null || !current.IsAlive)
                        continue;
                    next = ApplyPoetDamageSegment(next, spent, current, skill, repeatDamage, events).State;
                }
            }
        }
        events.Add(new SemanticCueEmittedEvent(actor.Unit.InstanceId, targets[0].Unit.InstanceId,
            skill.ContentId, "resolution"));
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyPoetWineHeal(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        if (command.TargetCell != actor.Unit.Position ||
            command.TargetId is UnitInstanceId targetId && targetId != actor.Unit.InstanceId)
            return Reject(state, actor, "poet_wine_target_not_self");
        int radius = skill.ExecutionProfile.AreaRadius > 0 ? skill.ExecutionProfile.AreaRadius : 2;
        int tickCount = skill.ExecutionProfile.HealingTickCount > 0 ? skill.ExecutionProfile.HealingTickCount : 3;
        int healingBase = skill.ExecutionProfile.HealingBase > 0
            ? skill.ExecutionProfile.HealingBase : skill.Level >= 2 ? 6 : 3;
        int totalHealing = checked(healingBase + actor.Unit.EffectiveAttributes.Strength);
        ContentId statusId = skill.StatusContentId ?? new ContentId("status.poet.wine-heal");
        var definition = new StatusDefinition(statusId, skill.SourceId, tickCount, true,
            StatusPolarity.Beneficial, StatusEffectKind.None, StatusTriggerTiming.TurnStart,
            StatusRefreshStrategy.RefreshDuration, frozenTotalHealing: totalHealing);
        BattleUnitState[] targets = state.Units.Values
            .Where(unit => unit.IsAlive && unit.Unit.PlayerNumber == actor.Unit.PlayerNumber &&
                           unit.CanReceiveStandardHealing && Manhattan(unit.Unit.Position, actor.Unit.Position) <= radius)
            .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal).ToArray();
        if (targets.Length == 0) return Reject(state, actor, "no_valid_target");

        BattleState next = state;
        var events = new List<BattleEvent>
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId)
        };
        foreach (BattleUnitState original in targets)
        {
            BattleUnitState current = next.Units[original.Unit.InstanceId];
            StatusApplicationResult application = _statuses.Apply(current, definition,
                actor.Unit.InstanceId, tickCount);
            current = application.Unit;
            events.Add(new StatusAppliedEvent(actor.Unit.InstanceId, current.Unit.InstanceId,
                statusId, application.AppliedStatus.RemainingTurns));
            if (skill.Level >= 3)
            {
                BattleStatusState? harmful = current.Statuses.Values
                    .Where(status => status.Polarity == StatusPolarity.Harmful)
                    .OrderByDescending(HarmfulCleansePriority)
                    .ThenByDescending(status => status.RemainingTurns)
                    .ThenBy(status => status.ContentId.Value, StringComparer.Ordinal).FirstOrDefault();
                if (harmful is not null)
                {
                    current = _statuses.Remove(current, harmful.ContentId);
                    events.Add(new StatusesCleansedEvent(actor.Unit.InstanceId, current.Unit.InstanceId,
                        skill.ContentId, new[] { harmful.ContentId }));
                }
            }
            next = next.WithUnit(current);
        }
        BattleUnitState spent = next.Units[actor.Unit.InstanceId]
            .WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        next = next.WithUnit(spent);
        if (skill.ManaCost > 0)
            events.Insert(1, new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId,
                skill.ManaCost, spent.CurrentMana));
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyPoetAgilityVerse(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        if (command.TargetCell != actor.Unit.Position ||
            command.TargetId is UnitInstanceId targetId && targetId != actor.Unit.InstanceId)
            return Reject(state, actor, "poet_agility_target_not_self");
        int radius = skill.ExecutionProfile.AreaRadius > 0 ? skill.ExecutionProfile.AreaRadius : 2;
        int amount = skill.ExecutionProfile.AttributeModifier > 0
            ? skill.ExecutionProfile.AttributeModifier : skill.Level >= 2 ? 4 : 2;
        ContentId statusId = skill.StatusContentId ?? new ContentId("status.poet.agility-verse");
        var definition = new StatusDefinition(statusId, skill.SourceId, 1, true,
            StatusPolarity.Beneficial, StatusEffectKind.None, StatusTriggerTiming.None,
            StatusRefreshStrategy.RefreshDuration,
            attributeModifiers: new UnitAttributeModifiers(Agility: amount));
        BattleUnitState[] targets = state.Units.Values
            .Where(unit => unit.IsAlive && unit.Unit.PlayerNumber == actor.Unit.PlayerNumber &&
                           Manhattan(unit.Unit.Position, actor.Unit.Position) <= radius)
            .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal).ToArray();
        if (targets.Length == 0) return Reject(state, actor, "no_valid_target");

        BattleState next = state;
        var events = new List<BattleEvent>
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId)
        };
        foreach (BattleUnitState original in targets)
        {
            StatusApplicationResult application = _statuses.Apply(next.Units[original.Unit.InstanceId],
                definition, actor.Unit.InstanceId, 1);
            next = next.WithUnit(application.Unit);
            events.Add(new StatusAppliedEvent(actor.Unit.InstanceId, original.Unit.InstanceId,
                statusId, application.AppliedStatus.RemainingTurns));
        }
        BattleUnitState spent = next.Units[actor.Unit.InstanceId]
            .WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        next = next.WithUnit(spent);
        if (skill.ManaCost > 0)
            events.Insert(1, new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId,
                skill.ManaCost, spent.CurrentMana));
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyPoetMoonDrink(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        if (command.TargetCell != actor.Unit.Position ||
            command.TargetId is UnitInstanceId targetId && targetId != actor.Unit.InstanceId)
            return Reject(state, actor, "poet_moon_drink_target_not_self");
        ContentId[] harmful = actor.Statuses.Values.Where(status => status.Polarity == StatusPolarity.Harmful)
            .OrderBy(status => status.ContentId.Value, StringComparer.Ordinal)
            .Select(status => status.ContentId).ToArray();
        bool canCleanse = skill.Level >= 3 && harmful.Length > 0;
        if (actor.CurrentHealth >= actor.MaxHealth && !canCleanse)
            return Reject(state, actor, "poet_moon_drink_no_effect");

        int percent = actor.CurrentHealth * 2 >= actor.MaxHealth ? 20 : 40;
        int requested = Math.Max(1, (int)Math.Floor(actor.MaxHealth * percent / 100d));
        BattleUnitState updated = actor.WithHealth(actor.CurrentHealth + requested);
        int restored = updated.CurrentHealth - actor.CurrentHealth;
        if (canCleanse) updated = _statuses.RemoveHarmful(updated, out _);
        updated = updated.WithMana(actor.CurrentMana - skill.ManaCost)
            .WithSuccessfulSkillUse(skill.ContentId)
            .WithSuccessfulSkillUse(PoetMoonDrinkFamilyUseId);
        var events = new List<BattleEvent>
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId)
        };
        if (skill.ManaCost > 0)
            events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, updated.CurrentMana));
        if (restored > 0)
            events.Add(new HealthRestoredEvent(actor.Unit.InstanceId, actor.Unit.InstanceId,
                skill.ContentId, restored, updated.CurrentHealth));
        if (canCleanse)
            events.Add(new StatusesCleansedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId,
                skill.ContentId, harmful));
        return new BattleTransition(state.WithUnit(updated), events);
    }

    private BattleTransition ApplyPoetDecoyRetreat(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        if (command.TargetCell != actor.Unit.Position ||
            command.TargetId is UnitInstanceId targetId && targetId != actor.Unit.InstanceId)
            return Reject(state, actor, "poet_retreat_target_not_self");
        GridPoint destination = actor.Unit.Position;
        int distance = skill.ExecutionProfile.RetreatDistance;
        for (int step = 0; step < distance; step++)
            destination = UnitFacingResolver.BackwardStep(destination, actor.Unit.Facing);
        if (!state.Board.Contains(destination) ||
            !state.CreateMovementBoard(actor.Unit.InstanceId).GetCell(destination).CanStop(actor.Unit.MovementKind))
            return Reject(state, actor, "poet_retreat_blocked");

        GridPoint origin = actor.Unit.Position;
        BattleUnitState moved = actor.WithPosition(destination, actor.HasMovedThisTurn)
            .WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        int ordinal = state.Units.Values.Select(unit => unit.Unit.SpawnOrdinal).DefaultIfEmpty(-1).Max() + 1;
        var id = new UnitInstanceId($"{actor.Unit.InstanceId.Value}.poet-decoy.{ordinal}");
        ContentId definitionId = skill.ExecutionProfile.SummonDefinitionId ?? PoetDecoyDefinitionId;
        int charges = skill.ExecutionProfile.DirectHitCharges > 0
            ? skill.ExecutionProfile.DirectHitCharges : skill.Level >= 2 ? 2 : 1;
        var facts = new UnitState(id, definitionId, origin, 0, 0, actor.Unit.PlayerNumber, ordinal,
            movementKind: actor.Unit.MovementKind, effectiveAttributes: actor.Unit.EffectiveAttributes,
            combatRole: SkillRole.Poet, facing: actor.Unit.Facing,
            baseAttributes: actor.Unit.BaseAttributes);
        var decoy = new BattleUnitState(facts, charges, charges, summonOwnerId: actor.Unit.InstanceId,
            canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: "Decoy");
        BattleState next = state.WithUnit(moved).WithSummon(decoy, 1, "Decoy");
        var events = new List<BattleEvent>
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId)
        };
        if (skill.ManaCost > 0)
            events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, moved.CurrentMana));
        events.Add(new UnitMovedEvent(actor.Unit.InstanceId, origin, destination, new[] { destination }));
        events.Add(new UnitSummonedEvent(actor.Unit.InstanceId, id, definitionId, origin));
        return new BattleTransition(next, events);
    }

    private sealed record PoetDamageSegmentResult(BattleState State, bool Defeated);

    private PoetDamageSegmentResult ApplyPoetDamageSegment(BattleState state, BattleUnitState actor,
        BattleUnitState originalTarget, SkillDefinition skill, int rawDamage, ICollection<BattleEvent> events)
    {
        BattleUnitState target = state.Units[originalTarget.Unit.InstanceId];
        var random = new DeterministicRandom(state.RandomState);
        int hitRoll = random.NextInt(100);
        int accuracy = UnitCombatStatRules.Accuracy(actor.Unit.EffectiveAttributes);
        int dodge = UnitCombatStatRules.Dodge(target.Unit.EffectiveAttributes) +
                    (target.HasCombatTechniquesLevelOne ? 30 : 0);
        int hitChance = Math.Clamp((int)Math.Floor((accuracy - dodge) * skill.ExecutionProfile.AccuracyFactor), 0, 100);
        bool dodged = hitRoll >= hitChance;
        int criticalRoll = random.NextInt(100);
        int criticalChance = Math.Clamp(UnitCombatStatRules.CriticalChance(actor.Unit.EffectiveAttributes) +
                                        (actor.CombatTechniquesLevel >= 3 ? 20 : 0), 0, 100);
        bool critical = skill.CanCrit && !dodged &&
                        (_statuses.EvaluateBeforeAttack(target).ForceCritical || criticalRoll < criticalChance);
        events.Add(new CombatRollResolvedEvent(actor.Unit.InstanceId, target.Unit.InstanceId,
            skill.ContentId, hitRoll, hitChance, dodged ? "dodge" : critical ? "critical" : "hit", random.State));
        BattleState next = state.WithRandomState(random.State);
        if (actor.Statuses.Values.Any(status => status.EffectKind == StatusEffectKind.DamageOutputReduction))
            rawDamage = (int)MathF.Round(rawDamage * 0.75f, MidpointRounding.AwayFromZero);
        if (critical)
            rawDamage = checked((int)Math.Floor(rawDamage * UnitCombatStatRules.CriticalMultiplier(
                actor.Unit.EffectiveAttributes)));
        StatusDamagePolicy damagePolicy = _statuses.EvaluateDamageTaken(target, actor,
            skill.ExecutionKind == SkillExecutionKind.PoetSwordRain);
        int damage = dodged ? 0 : (int)MathF.Round(rawDamage * damagePolicy.DamageMultiplier,
            MidpointRounding.AwayFromZero);
        if (!dodged && target.DamageShield is BattleDamageShieldState shield &&
            (skill.DamageKind == SkillDamageKind.Physical || shield.AbsorbsAllDamage))
        {
            int absorbed = Math.Min(shield.RemainingPoints, damage);
            damage -= absorbed;
            int remaining = shield.RemainingPoints - absorbed;
            target = target.WithDamageShield(remaining > 0 ? shield with { RemainingPoints = remaining } : null);
            events.Add(new DamageShieldAbsorbedEvent(target.Unit.InstanceId, skill.ContentId, absorbed, remaining));
        }
        damage = LimitDirectHitDamage(target, damage);
        int before = target.CurrentHealth;
        BattleUnitState damaged = target.WithHealth(before - damage);
        int actual = before - damaged.CurrentHealth;
        events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId,
            skill.ContentId, actual, damaged.CurrentHealth));
        next = next.WithUnit(damaged);
        next = ApplyDefeat(next, actor, target, damaged, events);
        return new PoetDamageSegmentResult(next, target.IsAlive && !damaged.IsAlive && actual > 0);
    }

    private static int HarmfulCleansePriority(BattleStatusState status) => status.EffectKind switch
    {
        StatusEffectKind.Stun or StatusEffectKind.Fear or StatusEffectKind.Frozen => 100,
        StatusEffectKind.Slow or StatusEffectKind.DamageOutputReduction => 80,
        StatusEffectKind.CurseDamageAmplifier => 60,
        StatusEffectKind.Burning or StatusEffectKind.Poison => 40,
        _ => 0
    };

    private static BattleTransition ApplyDemonicRegeneration(
        BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        if (actor.CurrentHealth >= actor.MaxHealth) return Reject(state, actor, "target_at_full_health");
        if (command.TargetId is UnitInstanceId targetId && targetId != actor.Unit.InstanceId)
            return Reject(state, actor, "regeneration_target_not_self");
        int amount = (int)Math.Ceiling(actor.MaxHealth * (skill.Level >= 2 ? 0.8d : 0.5d));
        BattleUnitState updated = actor.WithHealth(actor.CurrentHealth + amount)
            .WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        var events = new List<BattleEvent>
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId),
            new HealthRestoredEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId,
                updated.CurrentHealth - actor.CurrentHealth, updated.CurrentHealth)
        };
        if (skill.ManaCost > 0)
            events.Insert(1, new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, updated.CurrentMana));
        return new BattleTransition(state.WithUnit(updated), events);
    }

    private static BattleTransition ApplyPickup(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        if (!state.TryGetDroppedSpear(actor.Unit.InstanceId, out GridPoint spear)) return Reject(state, actor, "spear_not_dropped");
        if (command.TargetCell != spear) return Reject(state, actor, "spear_cell_mismatch");
        if (Math.Max(Math.Abs(actor.Unit.Position.X - spear.X), Math.Abs(actor.Unit.Position.Y - spear.Y)) != 1) return Reject(state, actor, "spear_not_adjacent");
        BattleState next = state.WithoutDroppedSpear(actor.Unit.InstanceId);
        return new BattleTransition(next, new BattleEvent[]
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, command.Definition.ContentId),
            new SpearRecoveredEvent(actor.Unit.InstanceId, spear),
            new SemanticCueEmittedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, command.Definition.ContentId, "spear-recovered")
        });
    }

    private static BattleTransition ApplySummon(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        GridPoint cell = command.TargetCell;
        bool requiresCorpse = command.Definition.ExecutionKind is SkillExecutionKind.SummonSkeleton or SkillExecutionKind.SummonSkeletonMage || command.Definition.ExecutionProfile.RequiresCorpse;
        if (requiresCorpse && !state.Corpses.Contains(cell)) return Reject(state, actor, "corpse_not_found");
        if (!requiresCorpse && (!state.Board.Contains(cell) || Manhattan(actor.Unit.Position, cell) > command.Definition.MaxRange)) return Reject(state, actor, "summon_cell_out_of_range");
        bool occupied = state.Units.Values.Any(unit => unit.IsAlive && unit.Unit.Position == cell) ||
            state.DroppedSpears.Values.Contains(cell) || (!requiresCorpse && state.Corpses.Contains(cell));
        if (!state.Board.Contains(cell) || !state.Board.GetCell(cell).CanStop(UnitMovementKind.Land) || occupied)
            return Reject(state, actor, "corpse_cell_occupied");
        if (actor.CurrentMana < command.Definition.ManaCost) return Reject(state, actor, "insufficient_mana");
        int ordinal = state.Units.Values.Where(unit => unit.SummonOwnerId == actor.Unit.InstanceId).Select(unit => unit.Unit.SpawnOrdinal).DefaultIfEmpty(-1).Max() + 1;
        ContentId definitionId = command.Definition.ExecutionProfile.SummonDefinitionId ?? command.Definition.ExecutionKind switch
        {
            SkillExecutionKind.SummonFireDemon => FireDemonDefinitionId,
            SkillExecutionKind.SummonSkeletonMage => SkeletonMageDefinitionId,
            _ => SkeletonDefinitionId
        };
        string category = string.IsNullOrEmpty(command.Definition.ExecutionProfile.SummonCategory) ? command.Definition.ExecutionKind.ToString() : command.Definition.ExecutionProfile.SummonCategory;
        var summonId = new UnitInstanceId($"{actor.Unit.InstanceId.Value}.{category.ToLowerInvariant()}.{ordinal}");
        int maxHealth = command.Definition.ExecutionKind switch { SkillExecutionKind.SummonSkeletonMage => command.Definition.Level >= 2 ? 8 : 6, SkillExecutionKind.SummonSkeleton => command.Definition.Level >= 2 ? 10 : 8, _ => 12 };
        int magicalAttack = command.Definition.ExecutionKind is SkillExecutionKind.SummonFireDemon or SkillExecutionKind.SummonSkeletonMage ? 4 : 0;
        var facts = new UnitState(summonId, definitionId, cell, 3, 10f, actor.Unit.PlayerNumber, ordinal);
        var summon = new BattleUnitState(facts, maxHealth, maxHealth, maxMana: 0, currentMana: 0, physicalAttack: 4, magicalAttack: magicalAttack, summonOwnerId: actor.Unit.InstanceId, canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: category);
        BattleUnitState updatedActor = actor.WithMana(actor.CurrentMana - command.Definition.ManaCost).WithSuccessfulSkillUse(command.Definition.ContentId);
        int maximum = command.Definition.ExecutionProfile.SummonLimit > 0 ? command.Definition.ExecutionProfile.SummonLimit : command.Definition.ExecutionKind switch { SkillExecutionKind.SummonSkeleton => Math.Min(3, command.Definition.Level), SkillExecutionKind.SummonSkeletonMage => Math.Min(2, command.Definition.Level), _ => Math.Max(1, command.Definition.Level) };
        BattleState next = state.WithUnit(updatedActor);
        if (requiresCorpse) next = next.WithoutCorpse(cell);
        next = next.WithSummon(summon, maximum, category);
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, command.Definition.ContentId) };
        if (command.Definition.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, command.Definition.ContentId, command.Definition.ManaCost, updatedActor.CurrentMana));
        if (requiresCorpse) events.Add(new CorpseConsumedEvent(cell, actor.Unit.InstanceId));
        events.Add(new UnitSummonedEvent(actor.Unit.InstanceId, summonId, definitionId, cell));
        events.Add(new SemanticCueEmittedEvent(actor.Unit.InstanceId, summonId, command.Definition.ContentId, "summoned"));
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplySelfDefense(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        BattleUnitState updated = actor.WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        if (skill.ExecutionKind == SkillExecutionKind.BoneShield)
        {
            int points = Math.Max(1, checked((actor.MaxMana / 3) * Math.Max(1,
                skill.ExecutionProfile.ShieldMultiplier) + UnitCombatStatRules.AttributeContribution(
                    actor.Unit.EffectiveAttributes, EffectiveRole(actor, skill), SkillEffectScalingKind.Shield)));
            updated = updated.WithDamageShield(new BattleDamageShieldState(points, skill.ExecutionProfile.ShieldAbsorbsAllDamage || skill.Level >= 2));
            var shieldEvents = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId) };
            if (skill.ManaCost > 0) shieldEvents.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, updated.CurrentMana));
            shieldEvents.Add(new DamageShieldAppliedEvent(actor.Unit.InstanceId, skill.ContentId, points, updated.DamageShield!.AbsorbsAllDamage));
            return new BattleTransition(state.WithUnit(updated), shieldEvents);
        }
        ContentId statusId = skill.StatusContentId ?? new ContentId(skill.ExecutionKind == SkillExecutionKind.IceArmor ? "buff.ice-armor" : "buff.bone-shield");
        var definition = new StatusDefinition(statusId, skill.SourceId, Math.Max(1, skill.StatusDuration), true, StatusPolarity.Beneficial,
            StatusEffectKind.DamageReduction, StatusTriggerTiming.DamageTaken, StatusRefreshStrategy.RefreshDuration,
            damageReductionPercent: skill.ExecutionKind == SkillExecutionKind.IceArmor ? 0.25f : 0f,
            meleeRetaliationStatusId: skill.ExecutionKind == SkillExecutionKind.IceArmor && skill.Level >= 2 ? new ContentId("buff.slow") : null,
            meleeRetaliationDuration: skill.ExecutionKind == SkillExecutionKind.IceArmor && skill.Level >= 2 ? 2 : 0);
        StatusApplicationResult application = _statuses.Apply(updated, definition, actor.Unit.InstanceId, Math.Max(1, skill.StatusDuration));
        updated = application.Unit;
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId) };
        if (skill.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, updated.CurrentMana));
        events.Add(new StatusAppliedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, statusId, application.AppliedStatus.RemainingTurns));
        return new BattleTransition(state.WithUnit(updated), events);
    }

    private BattleTransition ApplyRelocation(BattleState state, BattleUnitState actor, UseSkillCommand command, bool createDecoy)
    {
        SkillDefinition skill = command.Definition;
        GridPoint destination = command.TargetCell;
        int distance = Manhattan(actor.Unit.Position, destination);
        if (!state.Board.Contains(destination) || distance < skill.MinRange || distance > skill.MaxRange) return Reject(state, actor, "destination_out_of_range");
        BoardSnapshot relocationBoard = state.CreateMovementBoard(actor.Unit.InstanceId);
        if (!relocationBoard.GetCell(destination).CanStop(actor.Unit.MovementKind)) return Reject(state, actor, "destination_occupied");
        if (skill.RequiresLineOfSight && !_lineOfSight.Trace(state.Board, actor.Unit.Position, destination,
                LivingBlockers(state, actor.Unit.InstanceId, destination, skill.ExecutionKind)).IsClear) return Reject(state, actor, "line_of_sight_blocked");
        GridPoint origin = actor.Unit.Position;
        BattleUnitState moved = actor.WithPosition(destination, actor.HasMovedThisTurn).WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        BattleState next = state.WithUnit(moved);
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId) };
        if (skill.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, moved.CurrentMana));
        events.Add(new UnitMovedEvent(actor.Unit.InstanceId, origin, destination, new[] { destination }));
        if (createDecoy)
        {
            int ordinal = state.Units.Values.Select(unit => unit.Unit.SpawnOrdinal).DefaultIfEmpty(-1).Max() + 1;
            var id = new UnitInstanceId($"{actor.Unit.InstanceId.Value}.decoy.{ordinal}");
            var facts = new UnitState(id, DecoyDefinitionId, origin, 0, actor.Unit.Initiative, actor.Unit.PlayerNumber, ordinal);
            var decoy = new BattleUnitState(facts, Math.Max(1, actor.MaxHealth / 2), Math.Max(1, actor.MaxHealth / 2), summonOwnerId: actor.Unit.InstanceId, canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: "Decoy");
            if (skill.ExecutionProfile.CleanseHarmful || skill.Level >= 2) moved = _statuses.RemoveHarmful(moved, out _);
            next = next.WithUnit(moved).WithSummon(decoy, 1, "Decoy");
            events.Add(new UnitSummonedEvent(actor.Unit.InstanceId, id, DecoyDefinitionId, origin));
        }
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyRecoverSpear(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        if (!state.TryGetDroppedSpear(actor.Unit.InstanceId, out GridPoint spear)) return Reject(state, actor, "spear_not_dropped");
        if (command.TargetCell != spear || Manhattan(actor.Unit.Position, spear) > command.Definition.MaxRange) return Reject(state, actor, "spear_out_of_range");
        BattleUnitState updated = actor.WithMana(actor.CurrentMana - command.Definition.ManaCost).WithSuccessfulSkillUse(command.Definition.ContentId);
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, command.Definition.ContentId) };
        if (command.Definition.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, command.Definition.ContentId, command.Definition.ManaCost, updated.CurrentMana));
        events.Add(new SpearRecoveredEvent(actor.Unit.InstanceId, spear));
        BattleState next = state.WithUnit(updated).WithoutDroppedSpear(actor.Unit.InstanceId);
        int secondaryDamage = command.Definition.ExecutionProfile.SecondaryDamage;
        if (secondaryDamage > 0)
        {
            foreach (BattleUnitState original in next.Units.Values
                         .Where(unit => unit.IsAlive && IsHostile(state, actor, unit) && Manhattan(unit.Unit.Position, actor.Unit.Position) == 1)
                         .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal).ToArray())
            {
                BattleUnitState target = next.Units[original.Unit.InstanceId];
                int damage = LimitDirectHitDamage(target, secondaryDamage);
                BattleUnitState damaged = target.WithHealth(target.CurrentHealth - damage);
                next = next.WithUnit(damaged);
                events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, command.Definition.ContentId, target.CurrentHealth - damaged.CurrentHealth, damaged.CurrentHealth));
                next = ApplyDefeat(next, actor, target, damaged, events);
            }
        }
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyMultiStab(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        int expected = command.Definition.ExecutionProfile.OrderedTargetCount > 0 ? command.Definition.ExecutionProfile.OrderedTargetCount : command.Definition.Level >= 2 ? 4 : 3;
        if (command.OrderedTargetIds.Count != expected) return Reject(state, actor, "ordered_targets_required");
        BattleState next = state.WithUnit(actor.WithMana(actor.CurrentMana - command.Definition.ManaCost).WithSuccessfulSkillUse(command.Definition.ContentId));
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, command.OrderedTargetIds[0], command.Definition.ContentId) };
        if (command.Definition.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, command.Definition.ContentId, command.Definition.ManaCost, next.Units[actor.Unit.InstanceId].CurrentMana));
        foreach (UnitInstanceId targetId in command.OrderedTargetIds)
        {
            if (!next.TryGetUnit(targetId, out BattleUnitState? target) || target is null || !target.IsAlive || !IsHostile(next, actor, target)) return Reject(state, actor, "invalid_ordered_target");
            var random = new DeterministicRandom(next.RandomState);
            int roll = random.NextInt(100);
            int dodge = UnitCombatStatRules.Dodge(target.Unit.EffectiveAttributes) +
                (target.HasCombatTechniquesLevelOne ? 30 : 0);
            int chance = Math.Clamp((int)Math.Floor((UnitCombatStatRules.Accuracy(actor.Unit.EffectiveAttributes) -
                dodge) * command.Definition.ExecutionProfile.AccuracyFactor), 0, 100);
            bool missed = roll >= chance;
            next = next.WithRandomState(random.State);
            events.Add(new CombatRollResolvedEvent(actor.Unit.InstanceId, targetId, command.Definition.ContentId,
                roll, dodge, missed ? "dodge" : "hit", random.State));
            int before = target.CurrentHealth;
            int contribution = UnitCombatStatRules.AttributeContribution(actor.Unit.EffectiveAttributes,
                EffectiveRole(actor, command.Definition), EffectiveScaling(command.Definition), multiHit: true);
            int damage = LimitDirectHitDamage(target,
                missed ? 0 : checked(command.Definition.Damage + contribution));
            BattleUnitState damaged = target.WithHealth(before - damage);
            next = next.WithUnit(damaged);
            events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, targetId, command.Definition.ContentId, before - damaged.CurrentHealth, damaged.CurrentHealth));
            next = ApplyDefeat(next, actor, target, damaged, events);
        }
        return new BattleTransition(next, events);
    }

    private static IReadOnlyList<GridPoint> CardinalPath(GridPoint origin, GridPoint destination)
    {
        int dx = Math.Sign(destination.X - origin.X);
        int dy = Math.Sign(destination.Y - origin.Y);
        int distance = Manhattan(origin, destination);
        return Enumerable.Range(1, distance)
            .Select(step => new GridPoint(origin.X + dx * step, origin.Y + dy * step)).ToArray();
    }

    private static int LimitDirectHitDamage(BattleUnitState target, int damage) =>
        damage > 0 && (target.Unit.DefinitionId == PoetDecoyDefinitionId ||
                       target.Unit.InstanceId.Value.Contains(".poet-decoy.", StringComparison.Ordinal))
            ? 1
            : damage;

    private static int Manhattan(GridPoint left, GridPoint right) => Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

    /// <summary>
    /// Applies the defeat transaction and then the unified permanent-death assessment for a
    /// possessed demonbound that just defeated a friendly unit. Every damage source submits
    /// its defeat fact through this single exit so no secondary damage branch can bypass the roll.
    /// </summary>
    private static BattleState ApplyDefeat(BattleState state, BattleUnitState actor, BattleUnitState previous,
        BattleUnitState updated, ICollection<BattleEvent> events)
    {
        BattleState afterDefeat = BattleDefeatResolver.Apply(state, previous, updated, events);
        return DemonboundPermanentDeathPostProcessor.Apply(afterDefeat, actor, previous, updated, events);
    }

    /// <summary>Gets whether the unit is a non-acting decoy placeholder that must never be targeted.</summary>
    public static bool IsNonActingDecoy(BattleUnitState unit) =>
        string.Equals(unit.SummonCategory, "Decoy", StringComparison.Ordinal) ||
        unit.Unit.DefinitionId == new ContentId("unit.pure-run.amazon-decoy");

    /// <summary>
    /// Resolves hostile legality with the possessed form's unified target strategy.
    /// Unpossessed actors target only the enemy faction (non-acting decoys remain
    /// attackable so the decoy/bodyguard mechanic keeps working); possessed actors
    /// treat every living formal unit and summon as hostile except themselves and
    /// non-acting decoys — matching the AI candidate pool (TargetRelationshipStrategy).
    /// </summary>
    public static bool IsHostile(BattleState state, BattleUnitState actor, BattleUnitState target)
    {
        if (target.Unit.InstanceId == actor.Unit.InstanceId) return false;
        if (actor.DemonboundState?.IsPossessed == true)
            return target.IsAlive && !IsNonActingDecoy(target);
        return target.IsAlive && target.Unit.PlayerNumber != actor.Unit.PlayerNumber;
    }

    private IEnumerable<BattleUnitState> ResolveTargets(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        if (skill.ExecutionKind == SkillExecutionKind.Bane)
        {
            int dx = Math.Sign(command.TargetCell.X - actor.Unit.Position.X);
            int dy = Math.Sign(command.TargetCell.Y - actor.Unit.Position.Y);
            int selectedDistance = Manhattan(command.TargetCell, actor.Unit.Position);
            if (Math.Abs(dx) + Math.Abs(dy) != 1 || selectedDistance != 1) yield break;
            GridPoint first = new(actor.Unit.Position.X + dx, actor.Unit.Position.Y + dy);
            GridPoint second = new(actor.Unit.Position.X + dx * 2, actor.Unit.Position.Y + dy * 2);
            foreach (BattleUnitState unit in state.Units.Values
                         .Where(unit => unit.IsAlive && IsHostile(state, actor, unit) &&
                             (unit.Unit.Position == first || unit.Unit.Position == second))
                         .OrderBy(unit => Manhattan(unit.Unit.Position, actor.Unit.Position))
                         .ThenBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal))
                yield return unit;
            yield break;
        }
        if (skill.ExecutionKind is SkillExecutionKind.Cleave or SkillExecutionKind.InfernalBlast)
        {
            int dx = Math.Sign(command.TargetCell.X - actor.Unit.Position.X);
            int dy = Math.Sign(command.TargetCell.Y - actor.Unit.Position.Y);
            if (Math.Abs(dx) + Math.Abs(dy) != 1) yield break;
            int depth = skill.ExecutionKind == SkillExecutionKind.Cleave ? Math.Min(2, skill.Level) : 4;
            int halfWidth = skill.ExecutionKind == SkillExecutionKind.Cleave || skill.Level >= 3 ? 1 : 0;
            HashSet<GridPoint> cells = Enumerable.Range(1, depth)
                .SelectMany(step => Enumerable.Range(-halfWidth, halfWidth * 2 + 1)
                    .Select(offset => new GridPoint(actor.Unit.Position.X + dx * step - dy * offset,
                        actor.Unit.Position.Y + dy * step + dx * offset)))
                .Where(state.Board.Contains).ToHashSet();
            foreach (BattleUnitState unit in state.Units.Values
                         .Where(unit => unit.IsAlive && IsHostile(state, actor, unit) &&
                             cells.Contains(unit.Unit.Position))
                         .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal))
                yield return unit;
            yield break;
        }
        if (skill.ExecutionKind == SkillExecutionKind.Hellfire)
        {
            foreach (BattleUnitState unit in state.Units.Values
                         .Where(unit => unit.IsAlive && IsHostile(state, actor, unit) &&
                             Manhattan(unit.Unit.Position, actor.Unit.Position) is >= 1 and <= 2)
                         .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal))
                yield return unit;
            yield break;
        }
        int distance = Math.Abs(actor.Unit.Position.X - command.TargetCell.X) + Math.Abs(actor.Unit.Position.Y - command.TargetCell.Y);
        if (!state.Board.Contains(command.TargetCell) || distance < skill.MinRange || distance > skill.MaxRange) yield break;
        if (skill.RequiresLineOfSight && !_lineOfSight.Trace(state.Board, actor.Unit.Position, command.TargetCell,
                LivingBlockers(state, actor.Unit.InstanceId, command.TargetCell, skill.ExecutionKind)).IsClear) yield break;
        if (skill.AreaRadius > 0 && skill.ExecutionKind is SkillExecutionKind.AreaBlast or SkillExecutionKind.Fireball or SkillExecutionKind.AmplifyDamage or SkillExecutionKind.FearCurse or SkillExecutionKind.PoisonSpear)
        {
            BattleUnitState[] area = state.Units.Values.Where(unit => unit.IsAlive && IsHostile(state, actor, unit))
                .Where(unit => skill.ExecutionProfile.AreaShape == "square"
                    ? Math.Max(Math.Abs(unit.Unit.Position.X-command.TargetCell.X), Math.Abs(unit.Unit.Position.Y-command.TargetCell.Y)) <= skill.AreaRadius
                    : Math.Abs(unit.Unit.Position.X-command.TargetCell.X)+Math.Abs(unit.Unit.Position.Y-command.TargetCell.Y) <= skill.AreaRadius)
                .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal).ToArray();
            if (skill.ExecutionKind == SkillExecutionKind.Fireball && command.TargetId is UnitInstanceId fireballTarget)
            {
                BattleUnitState? primary = area.FirstOrDefault(unit => unit.Unit.InstanceId == fireballTarget);
                if (primary is null) yield break;
                yield return primary;
                foreach (BattleUnitState unit in area.Where(unit => unit.Unit.InstanceId != fireballTarget)) yield return unit;
                yield break;
            }
            foreach (BattleUnitState unit in area) yield return unit;
            yield break;
        }
        if (skill.UsesLineTargeting)
        {
            int dx = command.TargetCell.X - actor.Unit.Position.X;
            int dy = command.TargetCell.Y - actor.Unit.Position.Y;
            if (skill.ExecutionKind == SkillExecutionKind.Thrust && dx != 0 && dy != 0) yield break;
            if (skill.ExecutionKind != SkillExecutionKind.Thrust &&
                (command.TargetId is not UnitInstanceId selectedId ||
                 !state.TryGetUnit(selectedId, out BattleUnitState? selectedUnit) ||
                 selectedUnit is null || !selectedUnit.IsAlive ||
                 !IsHostile(state, actor, selectedUnit) ||
                 selectedUnit.Unit.Position != command.TargetCell))
                yield break;
            int selectedDistance = Math.Abs(dx) + Math.Abs(dy);
            IEnumerable<BattleUnitState> ray = state.Units.Values.Where(unit => unit.IsAlive && IsHostile(state, actor, unit))
                .Where(unit => IsOnSelectedRay(actor.Unit.Position, command.TargetCell, unit.Unit.Position))
                .Where(unit => Math.Abs(unit.Unit.Position.X - actor.Unit.Position.X) + Math.Abs(unit.Unit.Position.Y - actor.Unit.Position.Y) <= selectedDistance)
                .OrderBy(unit => Math.Abs(unit.Unit.Position.X - actor.Unit.Position.X) + Math.Abs(unit.Unit.Position.Y - actor.Unit.Position.Y));
            IEnumerable<BattleUnitState> resolvedRay = skill.ExecutionKind == SkillExecutionKind.Thrust ||
                skill.ExecutionKind == SkillExecutionKind.BoneSpear && skill.ExecutionProfile.PierceAll
                    ? ray
                    : ray.Take(1);
            foreach (BattleUnitState unit in resolvedRay) yield return unit;
            yield break;
        }
        if (command.TargetId is UnitInstanceId id && state.TryGetUnit(id, out BattleUnitState? target) && target is not null && target.Unit.Position == command.TargetCell) yield return target;
    }

    private static bool IsOnSelectedRay(GridPoint origin, GridPoint selected, GridPoint candidate)
    {
        int selectedX = selected.X - origin.X;
        int selectedY = selected.Y - origin.Y;
        int candidateX = candidate.X - origin.X;
        int candidateY = candidate.Y - origin.Y;
        int cross = candidateX * selectedY - candidateY * selectedX;
        int dot = candidateX * selectedX + candidateY * selectedY;
        return cross == 0 && dot > 0;
    }

    public static IReadOnlyDictionary<GridPoint, LineOfSightBlocker> LivingBlockers(BattleState state, UnitInstanceId actorId, GridPoint targetCell, SkillExecutionKind executionKind) =>
        executionKind == SkillExecutionKind.BoneSpear
            ? new Dictionary<GridPoint, LineOfSightBlocker>()
            : state.Units.Values
            .Where(unit => unit.IsAlive && unit.Unit.InstanceId != actorId && unit.Unit.Position != targetCell)
            .ToDictionary(unit => unit.Unit.Position,
                unit => new LineOfSightBlocker(LineOfSightBlockingKind.LivingUnit, unit.Unit.InstanceId));

    private static StatusDefinition StatusFor(SkillDefinition skill, ContentId statusId) => skill.ExecutionKind switch
    {
        SkillExecutionKind.Fireball or SkillExecutionKind.FireDemonAttack => new StatusDefinition(statusId, "Ignite", 2, true, StatusPolarity.Harmful, StatusEffectKind.Burning, StatusTriggerTiming.TurnStart, StatusRefreshStrategy.AddStacks, damagePerTurn: 1, elementKind: StatusElementKind.Fire),
        SkillExecutionKind.IceBolt => new StatusDefinition(statusId, "Slow", 1, true, StatusPolarity.Harmful, StatusEffectKind.Slow, StatusTriggerTiming.None, StatusRefreshStrategy.RefreshDuration, elementKind: StatusElementKind.Ice, initiativeModifier: -4, movementModifier: -1),
        SkillExecutionKind.Lightning or SkillExecutionKind.Hellfire => new StatusDefinition(statusId, "Stun", 1, false, StatusPolarity.Harmful, StatusEffectKind.Stun, StatusTriggerTiming.None, StatusRefreshStrategy.RefreshDuration, elementKind: StatusElementKind.Lightning),
        SkillExecutionKind.AmplifyDamage => new StatusDefinition(statusId, "CurseDamageAmplifier", 5, true, StatusPolarity.Harmful, StatusEffectKind.CurseDamageAmplifier, StatusTriggerTiming.None, StatusRefreshStrategy.RefreshDuration, curseCategory: "damage-taken"),
        SkillExecutionKind.FearCurse => new StatusDefinition(statusId, "Fear", Math.Max(1, skill.StatusDuration), true, StatusPolarity.Harmful, StatusEffectKind.Fear, StatusTriggerTiming.None, StatusRefreshStrategy.RefreshDuration, curseCategory: "fear"),
        SkillExecutionKind.Bane => new StatusDefinition(statusId, "Bane", Math.Max(1, skill.StatusDuration), true,
            StatusPolarity.Harmful, StatusEffectKind.DamageOutputReduction, StatusTriggerTiming.None,
            StatusRefreshStrategy.RefreshDuration),
        _ => throw new InvalidOperationException($"Unsupported status contract for {skill.ExecutionKind}.")
    };

    private static SkillEffectScalingKind EffectiveScaling(SkillDefinition skill)
    {
        if (skill.ExecutionProfile.EffectScaling != SkillEffectScalingKind.None)
            return skill.ExecutionProfile.EffectScaling;
        return skill.ExecutionKind switch
        {
            SkillExecutionKind.MeleeAttack or SkillExecutionKind.Thrust or SkillExecutionKind.MultiStab =>
                SkillEffectScalingKind.MeleePhysical,
            SkillExecutionKind.RangedAttack or SkillExecutionKind.HeavyShot or SkillExecutionKind.PoisonSpear =>
                SkillEffectScalingKind.RangedPhysical,
            SkillExecutionKind.MagicAttack or SkillExecutionKind.Fireball or SkillExecutionKind.IceBolt or
                SkillExecutionKind.Lightning or SkillExecutionKind.BoneSpear or SkillExecutionKind.Bane or
                SkillExecutionKind.Cleave or SkillExecutionKind.InfernalBlast or SkillExecutionKind.Hellfire =>
                SkillEffectScalingKind.Magical,
            SkillExecutionKind.RecoverSpear or SkillExecutionKind.DemonicRegeneration =>
                SkillEffectScalingKind.Healing,
            SkillExecutionKind.IceArmor or SkillExecutionKind.BoneShield => SkillEffectScalingKind.Shield,
            _ => SkillEffectScalingKind.None
        };
    }

    private static SkillRole EffectiveRole(BattleUnitState actor, SkillDefinition skill) =>
        skill.Role == SkillRole.Any ? actor.Unit.CombatRole : skill.Role;

    private static BattleTransition Reject(BattleState state, BattleUnitState actor, string reason) => new(state, new BattleEvent[] { new CommandRejectedEvent(actor.Unit.InstanceId, reason) });

    public static string? UsageFailure(BattleUnitState actor, SkillDefinition skill)
    {
        int uses = actor.SuccessfulUsesOf(skill.ContentId);
        if (skill.ExecutionKind == SkillExecutionKind.PoetMoonDrink &&
            actor.SuccessfulUsesOf(PoetMoonDrinkFamilyUseId) >= 1)
            return "ability_use_limit_reached";
        if (skill.IsBasicAbility && uses >= 1) return "basic_ability_already_used";
        if (!skill.IsBasicAbility && skill.MaxUsesPerTurn > 0 && uses >= skill.MaxUsesPerTurn)
            return "ability_use_limit_reached";
        return null;
    }

    /// <summary>Returns the stable reason why a skill cannot currently be selected before targeting.</summary>
    public static string? AvailabilityFailure(BattleUnitState actor, SkillDefinition skill)
    {
        string? usageFailure = UsageFailure(actor, skill);
        if (usageFailure is not null) return usageFailure;
        if (skill.ExecutionKind == SkillExecutionKind.PoetMoonDrink && actor.CurrentHealth >= actor.MaxHealth &&
            !(skill.Level >= 3 && actor.Statuses.Values.Any(status => status.Polarity == StatusPolarity.Harmful)))
            return "poet_moon_drink_no_effect";
        return actor.CurrentMana < skill.ManaCost ? "insufficient_mana" : null;
    }
}
