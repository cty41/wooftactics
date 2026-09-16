using NUnit.Framework;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Combat;
using Tactics.Core.Skills;
using Tactics.Core.Statuses;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

[TestFixture]
public sealed class PoetSkillRuntimeTests
{
    [Test]
    public void PoetUsesStrengthForMeleeAndHalfStrengthForRangedScaling()
    {
        var attributes = new UnitAttributes(7, 5, 5, 4, 6, 4);

        Assert.Multiple(() =>
        {
            Assert.That(UnitCombatStatRules.AttributeContribution(attributes, SkillRole.Poet,
                SkillEffectScalingKind.MeleePhysical), Is.EqualTo(7));
            Assert.That(UnitCombatStatRules.AttributeContribution(attributes, SkillRole.Poet,
                SkillEffectScalingKind.RangedPhysical), Is.EqualTo(3));
        });
    }

    [Test]
    public void PoetChargeStopsBeforeFirstEnemyDealsStrengthDamageAndLevelThreeRefundsKillMana()
    {
        BattleState state = State(new GridPoint(1, 1), 6,
            new[] { Unit("enemy.target", new GridPoint(4, 1), 1, health: 8) }, currentMana: 12);
        SkillDefinition skill = Skill("skill.poet.charge.lv3", SkillExecutionKind.PoetCharge,
            level: 3, mana: 6, minRange: 1, maxRange: 5, damage: 1,
            profile: new SkillExecutionProfile(KillManaRefund: 6), canCrit: false);

        BattleTransition result = Apply(state, skill, new UnitInstanceId("enemy.target"), new GridPoint(4, 1));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Units[state.ActiveUnitId].Unit.Position, Is.EqualTo(new GridPoint(3, 1)));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.target")].IsAlive, Is.False);
            Assert.That(result.State.Units[state.ActiveUnitId].CurrentMana, Is.EqualTo(12));
            Assert.That(result.Events.OfType<UnitMovedEvent>().Single().Destination, Is.EqualTo(new GridPoint(3, 1)));
            Assert.That(result.Events.OfType<ManaRestoredEvent>().Single().Amount, Is.EqualTo(6));
        });
    }

    [Test]
    public void PoetChargeRejectsAllyBlockAtomically()
    {
        BattleUnitState ally = Unit("party.ally", new GridPoint(2, 1), 0);
        BattleUnitState enemy = Unit("enemy.target", new GridPoint(4, 1), 1);
        BattleState state = State(new GridPoint(1, 1), 6, new[] { ally, enemy });
        SkillDefinition skill = Skill("skill.poet.charge.lv1", SkillExecutionKind.PoetCharge,
            level: 1, mana: 6, minRange: 1, maxRange: 3, damage: 1, canCrit: false);

        BattleTransition result = Apply(state, skill, enemy.Unit.InstanceId, enemy.Unit.Position);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.State, Is.SameAs(state));
            Assert.That(result.State.Units[state.ActiveUnitId].CurrentMana, Is.EqualTo(state.Units[state.ActiveUnitId].CurrentMana));
            Assert.That(result.Events.OfType<CommandRejectedEvent>().Single().Reason, Is.EqualTo("poet_charge_path_blocked"));
        });
    }

    [Test]
    public void PoetChargeIgnoresCorpsesAndDroppedSpearsWhileTraversing()
    {
        BattleUnitState enemy = Unit("enemy.target", new GridPoint(4, 1), 1);
        BattleState state = State(new GridPoint(1, 1), 6, new[] { enemy })
            .WithCorpse(new GridPoint(2, 1))
            .WithDroppedSpear(new UnitInstanceId("party.poet"), new GridPoint(3, 1));
        SkillDefinition skill = Skill("skill.poet.charge.lv1", SkillExecutionKind.PoetCharge,
            level: 1, mana: 6, minRange: 1, maxRange: 4, damage: 1, canCrit: false);

        BattleTransition result = Apply(state, skill, enemy.Unit.InstanceId, enemy.Unit.Position);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Units[state.ActiveUnitId].Unit.Position, Is.EqualTo(new GridPoint(3, 1)));
        });
    }

    [Test]
    public void SwordRainHitsOnlyHostilesInDiamondAndConfiguredRepeatRerollsEverySurvivor()
    {
        BattleUnitState enemyOne = Unit("enemy.one", new GridPoint(4, 2), 1);
        BattleUnitState enemyTwo = Unit("enemy.two", new GridPoint(5, 3), 1);
        BattleUnitState outside = Unit("enemy.outside", new GridPoint(6, 3), 1);
        BattleUnitState ally = Unit("party.ally", new GridPoint(4, 3), 0);
        BattleState state = State(new GridPoint(1, 2), 6, new[] { enemyOne, enemyTwo, outside, ally });
        SkillDefinition skill = Skill("skill.poet.sword-rain.lv2", SkillExecutionKind.PoetSwordRain,
            level: 2, mana: 12, minRange: 1, maxRange: 4, damage: 3,
            profile: new SkillExecutionProfile(AreaRadius: 2, RepeatChancePercent: 100,
                RepeatDamagePercent: 50), canCrit: false);

        BattleTransition result = Apply(state, skill, null, new GridPoint(4, 2));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Events.OfType<DamageAppliedEvent>().Count(), Is.EqualTo(4));
            Assert.That(result.Events.OfType<CombatRollResolvedEvent>()
                .Any(evt => evt.Outcome == "poet-sword-rain-repeat"), Is.True);
            Assert.That(result.State.Units[enemyOne.Unit.InstanceId].CurrentHealth, Is.LessThan(enemyOne.CurrentHealth));
            Assert.That(result.State.Units[enemyTwo.Unit.InstanceId].CurrentHealth, Is.LessThan(enemyTwo.CurrentHealth));
            Assert.That(result.State.Units[outside.Unit.InstanceId].CurrentHealth, Is.EqualTo(outside.CurrentHealth));
            Assert.That(result.State.Units[ally.Unit.InstanceId].CurrentHealth, Is.EqualTo(ally.CurrentHealth));
        });
    }

    [Test]
    public void WineHealFreezesThreeTickTotalAndLevelThreeCleansesOneHighestPriorityHarmfulStatus()
    {
        ContentId poisonId = new("status.poison");
        ContentId stunId = new("status.stun");
        var statuses = new Dictionary<ContentId, BattleStatusState>
        {
            [poisonId] = new(poisonId, new UnitInstanceId("enemy.source"), 2, 1,
                polarity: StatusPolarity.Harmful, effectKind: StatusEffectKind.Poison),
            [stunId] = new(stunId, new UnitInstanceId("enemy.source"), 1, 0,
                polarity: StatusPolarity.Harmful, effectKind: StatusEffectKind.Stun)
        };
        BattleUnitState ally = Unit("party.ally", new GridPoint(2, 2), 0, statuses: statuses);
        BattleState state = State(new GridPoint(1, 1), 6, new[] { ally });
        ContentId healStatusId = new("status.poet.wine-heal");
        SkillDefinition skill = Skill("skill.poet.wine.lv3", SkillExecutionKind.PoetWineHeal,
            level: 3, mana: 5, minRange: 0, maxRange: 0, damage: 0,
            statusId: healStatusId, statusDuration: 3,
            profile: new SkillExecutionProfile(AreaRadius: 2, HealingTickCount: 3, HealingBase: 6));

        BattleTransition result = Apply(state, skill, state.ActiveUnitId, state.Units[state.ActiveUnitId].Unit.Position);
        BattleUnitState updatedAlly = result.State.Units[ally.Unit.InstanceId];

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(updatedAlly.Statuses[healStatusId].FrozenTotalHealingRemaining, Is.EqualTo(12));
            Assert.That(updatedAlly.Statuses[healStatusId].RemainingTurns, Is.EqualTo(3));
            Assert.That(updatedAlly.Statuses.ContainsKey(stunId), Is.False);
            Assert.That(updatedAlly.Statuses.ContainsKey(poisonId), Is.True);
            Assert.That(result.Events.OfType<StatusesCleansedEvent>().Any(evt =>
                evt.TargetId == ally.Unit.InstanceId && evt.RemovedStatusIds.SequenceEqual(new[] { stunId })), Is.True);
        });
    }

    [Test]
    public void AgilityVerseAppliesAuthoredModifierToEveryNearbyAlly()
    {
        BattleUnitState ally = Unit("party.ally", new GridPoint(2, 1), 0);
        BattleUnitState farAlly = Unit("party.far", new GridPoint(5, 1), 0);
        BattleState state = State(new GridPoint(1, 1), 6, new[] { ally, farAlly });
        ContentId statusId = new("status.poet.agility-verse");
        SkillDefinition skill = Skill("skill.poet.agility.lv2", SkillExecutionKind.PoetAgilityVerse,
            level: 2, mana: 8, minRange: 0, maxRange: 0, damage: 0,
            statusId: statusId, statusDuration: 1,
            profile: new SkillExecutionProfile(AreaRadius: 2, AttributeModifier: 4));

        BattleTransition result = Apply(state, skill, state.ActiveUnitId, state.Units[state.ActiveUnitId].Unit.Position);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Units[state.ActiveUnitId].Statuses[statusId].AttributeModifiers.Agility, Is.EqualTo(4));
            Assert.That(result.State.Units[ally.Unit.InstanceId].Statuses[statusId].AttributeModifiers.Agility, Is.EqualTo(4));
            Assert.That(result.State.Units[farAlly.Unit.InstanceId].Statuses.ContainsKey(statusId), Is.False);
        });
    }

    [Test]
    public void MoonDrinkUsesPrecastThresholdCleansesAllAtLevelThreeAndEnforcesSuccessfulTurnUse()
    {
        ContentId poisonId = new("status.poison");
        var statuses = new Dictionary<ContentId, BattleStatusState>
        {
            [poisonId] = new(poisonId, new UnitInstanceId("enemy.source"), 2, 1,
                polarity: StatusPolarity.Harmful, effectKind: StatusEffectKind.Poison)
        };
        BattleState state = State(new GridPoint(1, 1), 6, Array.Empty<BattleUnitState>(),
            poetHealth: 8, statuses: statuses);
        SkillDefinition skill = Skill("skill.poet.moon-drink.lv3", SkillExecutionKind.PoetMoonDrink,
            level: 3, mana: 4, minRange: 0, maxRange: 0, damage: 0);

        BattleTransition first = Apply(state, skill, state.ActiveUnitId, state.Units[state.ActiveUnitId].Unit.Position);
        BattleTransition second = Apply(first.State, skill, state.ActiveUnitId, first.State.Units[state.ActiveUnitId].Unit.Position);

        Assert.Multiple(() =>
        {
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.State.Units[state.ActiveUnitId].CurrentHealth, Is.EqualTo(16));
            Assert.That(first.State.Units[state.ActiveUnitId].Statuses.Values.Any(status =>
                status.Polarity == StatusPolarity.Harmful), Is.False);
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.State, Is.SameAs(first.State));
            Assert.That(second.Events.OfType<CommandRejectedEvent>().Single().Reason,
                Is.EqualTo("ability_use_limit_reached"));
        });
    }

    [Test]
    public void DecoyRetreatUsesPrecastFacingReplacesCloneAndConsumesOneChargePerDirectHit()
    {
        BattleUnitState enemy = Unit("enemy.attacker", new GridPoint(4, 1), 1, strength: 8);
        BattleState state = State(new GridPoint(2, 1), 6, new[] { enemy }, facing: UnitFacing.East);
        SkillDefinition retreat = Skill("skill.poet.decoy.lv2", SkillExecutionKind.PoetDecoyRetreat,
            level: 2, mana: 5, minRange: 0, maxRange: 0, damage: 0,
            profile: new SkillExecutionProfile(DirectHitCharges: 2));

        BattleTransition created = Apply(state, retreat, state.ActiveUnitId, state.Units[state.ActiveUnitId].Unit.Position);
        BattleUnitState firstDecoy = created.State.Units.Values.Single(unit => unit.SummonOwnerId == state.ActiveUnitId);
        BattleTransition replaced = Apply(created.State, retreat, state.ActiveUnitId,
            created.State.Units[state.ActiveUnitId].Unit.Position);
        BattleUnitState decoy = replaced.State.Units.Values.Single(unit => unit.SummonOwnerId == state.ActiveUnitId);
        SkillDefinition attack = new(new ContentId("skill.test.direct"), "direct", SkillRole.Any,
            SkillKind.Active, 1, 0, 1, 5, SkillExecutionKind.DirectAttack, 99,
            SkillDamageKind.Physical, canCrit: false);
        var runtime = new SkillRuntimeService();
        BattleTransition firstHit = runtime.Apply(replaced.State, enemy,
            new UseSkillCommand(enemy.Unit.InstanceId, decoy.Unit.InstanceId, decoy.Unit.Position, attack));
        BattleUnitState currentEnemy = firstHit.State.Units[enemy.Unit.InstanceId];
        BattleTransition secondHit = runtime.Apply(firstHit.State, currentEnemy,
            new UseSkillCommand(enemy.Unit.InstanceId, decoy.Unit.InstanceId, decoy.Unit.Position, attack));

        Assert.Multiple(() =>
        {
            Assert.That(created.Succeeded, Is.True);
            Assert.That(created.State.Units[state.ActiveUnitId].Unit.Position, Is.EqualTo(new GridPoint(1, 1)));
            Assert.That(created.State.Units[state.ActiveUnitId].Unit.Facing, Is.EqualTo(UnitFacing.East));
            Assert.That(firstDecoy.Unit.Position, Is.EqualTo(new GridPoint(2, 1)));
            Assert.That(replaced.Succeeded, Is.True);
            Assert.That(replaced.State.Units.ContainsKey(firstDecoy.Unit.InstanceId), Is.False);
            Assert.That(replaced.State.Units.Values.Count(unit => unit.SummonOwnerId == state.ActiveUnitId), Is.EqualTo(1));
            Assert.That(decoy.Unit.Position, Is.EqualTo(new GridPoint(1, 1)));
            Assert.That(decoy.MaxHealth, Is.EqualTo(2));
            Assert.That(decoy.CanReceiveStandardHealing, Is.False);
            Assert.That(firstHit.State.Units[decoy.Unit.InstanceId].CurrentHealth, Is.EqualTo(1));
            Assert.That(secondHit.State.Units[decoy.Unit.InstanceId].IsAlive, Is.False);
            Assert.That(firstHit.Events.OfType<DamageAppliedEvent>().Single().Amount, Is.EqualTo(1));
            Assert.That(secondHit.Events.OfType<DamageAppliedEvent>().Single().Amount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ProductionTransition_PreservesRetreatFacingRejectsForgedSelfTargetAndAllowsReplacement()
    {
        BattleUnitState enemy = Unit("enemy.attacker", new GridPoint(4, 1), 1);
        BattleState state = State(new GridPoint(2, 1), 6, new[] { enemy }, facing: UnitFacing.East);
        SkillDefinition retreat = Skill("skill.poet.decoy.lv2", SkillExecutionKind.PoetDecoyRetreat,
            level: 2, mana: 5, minRange: 0, maxRange: 0, damage: 0,
            profile: new SkillExecutionProfile(DirectHitCharges: 2));
        var transitions = new BattleTransitionService();

        BattleTransition forged = transitions.Apply(state, new UseSkillCommand(state.ActiveUnitId,
            state.ActiveUnitId, new GridPoint(3, 3), retreat));
        BattleTransition created = transitions.Apply(state, new UseSkillCommand(state.ActiveUnitId,
            state.ActiveUnitId, state.Units[state.ActiveUnitId].Unit.Position, retreat));
        BattleUnitState firstDecoy = created.State.Units.Values.Single(unit =>
            unit.SummonOwnerId == state.ActiveUnitId);
        BattleTransition replaced = transitions.Apply(created.State, new UseSkillCommand(state.ActiveUnitId,
            state.ActiveUnitId, created.State.Units[state.ActiveUnitId].Unit.Position, retreat));
        BattleUnitState secondDecoy = replaced.State.Units.Values.Single(unit =>
            unit.SummonOwnerId == state.ActiveUnitId);

        Assert.Multiple(() =>
        {
            Assert.That(forged.Succeeded, Is.False);
            Assert.That(forged.State, Is.SameAs(state));
            Assert.That(created.State.Units[state.ActiveUnitId].Unit.Facing, Is.EqualTo(UnitFacing.East));
            Assert.That(firstDecoy.Unit.Facing, Is.EqualTo(UnitFacing.East));
            Assert.That(replaced.Succeeded, Is.True);
            Assert.That(replaced.State.Units[state.ActiveUnitId].Unit.Facing, Is.EqualTo(UnitFacing.East));
            Assert.That(replaced.State.Units.ContainsKey(firstDecoy.Unit.InstanceId), Is.False);
            Assert.That(secondDecoy.Unit.Facing, Is.EqualTo(UnitFacing.East));
        });
    }

    [Test]
    public void PoisonSpearConsumesOnlyOneDirectHitChargeAndPoisonTickConsumesNone()
    {
        BattleUnitState enemy = Unit("enemy.attacker", new GridPoint(4, 1), 1, strength: 8);
        BattleState state = State(new GridPoint(2, 1), 6, new[] { enemy }, facing: UnitFacing.East);
        SkillDefinition retreat = Skill("skill.poet.decoy.lv2", SkillExecutionKind.PoetDecoyRetreat,
            level: 2, mana: 5, minRange: 0, maxRange: 0, damage: 0,
            profile: new SkillExecutionProfile(DirectHitCharges: 2));
        var transitions = new BattleTransitionService();
        BattleState created = transitions.Apply(state, new UseSkillCommand(state.ActiveUnitId,
            state.ActiveUnitId, state.Units[state.ActiveUnitId].Unit.Position, retreat)).State;
        BattleUnitState decoy = created.Units.Values.Single(unit => unit.SummonOwnerId == state.ActiveUnitId);
        BattleState enemyTurn = transitions.Apply(created, new EndTurnCommand(state.ActiveUnitId)).State;
        var poison = new PoisonSpearDefinition(new ContentId("skill.poison-spear.test"), 5, 99, 2,
            poisonDamagePerTurn: 9);

        BattleTransition hit = transitions.Apply(enemyTurn,
            new UsePoisonSpearCommand(enemy.Unit.InstanceId, decoy.Unit.InstanceId, poison));
        BattleUnitState afterHit = hit.State.Units[decoy.Unit.InstanceId];
        BattleState poetTurn = transitions.Apply(hit.State, new EndTurnCommand(enemy.Unit.InstanceId)).State;
        BattleState afterPoisonTick = transitions.Apply(poetTurn,
            new EndTurnCommand(state.ActiveUnitId)).State;

        Assert.Multiple(() =>
        {
            Assert.That(hit.Succeeded, Is.True);
            Assert.That(hit.Events.OfType<DamageAppliedEvent>().Single().Amount, Is.EqualTo(1));
            Assert.That(afterHit.CurrentHealth, Is.EqualTo(1));
            Assert.That(afterPoisonTick.Units[decoy.Unit.InstanceId].CurrentHealth, Is.EqualTo(1));
        });
    }

    [Test]
    public void MissDoesNotConsumePoetDecoyDirectHitCharge()
    {
        BattleUnitState enemy = Unit("enemy.attacker", new GridPoint(4, 1), 1);
        BattleState state = State(new GridPoint(2, 1), 6, new[] { enemy }, facing: UnitFacing.East);
        SkillDefinition retreat = Skill("skill.poet.decoy.lv2", SkillExecutionKind.PoetDecoyRetreat,
            level: 2, mana: 5, minRange: 0, maxRange: 0, damage: 0,
            profile: new SkillExecutionProfile(DirectHitCharges: 2));
        BattleTransition created = Apply(state, retreat, state.ActiveUnitId,
            state.Units[state.ActiveUnitId].Unit.Position);
        BattleUnitState decoy = created.State.Units.Values.Single(unit => unit.SummonOwnerId == state.ActiveUnitId);
        SkillDefinition attack = new(new ContentId("skill.test.miss"), "miss", SkillRole.Any,
            SkillKind.Active, 1, 0, 1, 5, SkillExecutionKind.DirectAttack, 99,
            SkillDamageKind.Physical, executionProfile: new SkillExecutionProfile(AccuracyFactor: 0.001m),
            canCrit: false);

        BattleTransition missed = new SkillRuntimeService().Apply(created.State, enemy,
            new UseSkillCommand(enemy.Unit.InstanceId, decoy.Unit.InstanceId, decoy.Unit.Position, attack));

        Assert.Multiple(() =>
        {
            Assert.That(missed.Succeeded, Is.True);
            Assert.That(missed.Events.OfType<CombatRollResolvedEvent>().Single().Outcome, Is.EqualTo("dodge"));
            Assert.That(missed.Events.OfType<DamageAppliedEvent>().Single().Amount, Is.Zero);
            Assert.That(missed.State.Units[decoy.Unit.InstanceId].CurrentHealth, Is.EqualTo(2));
        });
    }

    [TestCase(2, false)]
    [TestCase(1, true)]
    public void StatusDetonationIsOneLimitedDirectSegmentAndPreservesDefeatEvents(
        int decoyHealth, bool expectedDefeat)
    {
        ContentId igniteId = new("buff.ignite");
        var statuses = new Dictionary<ContentId, BattleStatusState>
        {
            [igniteId] = new(igniteId, new UnitInstanceId("party.poet"), 2, 1, stackCount: 9,
                effectKind: StatusEffectKind.Burning, triggerTiming: StatusTriggerTiming.TurnStart)
        };
        var decoyId = new UnitInstanceId("enemy.poet-decoy.0");
        var decoy = new BattleUnitState(new UnitState(decoyId,
                new ContentId("unit.pure-run.poet-decoy"), new GridPoint(2, 1), 0, 2, 1, 1),
            2, decoyHealth, statuses: statuses, summonCategory: "Decoy");
        BattleState state = State(new GridPoint(1, 1), 6, new[] { decoy });
        SkillDefinition skill = Skill("skill.test.detonation", SkillExecutionKind.Fireball,
            level: 1, mana: 0, minRange: 1, maxRange: 4, damage: 0,
            profile: new SkillExecutionProfile(DetonateStatusContentId: igniteId), canCrit: false);

        BattleTransition result = Apply(state, skill, decoyId, decoy.Unit.Position);
        DamageAppliedEvent detonation = result.Events.OfType<DamageAppliedEvent>()
            .Single(evt => evt.Amount > 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(detonation.Amount, Is.EqualTo(1));
            Assert.That(result.State.Units[decoyId].CurrentHealth, Is.EqualTo(decoyHealth - 1));
            Assert.That(result.State.Units[decoyId].Statuses, Does.Not.ContainKey(igniteId));
            Assert.That(result.Events.OfType<StatusExpiredEvent>().Single().StatusId, Is.EqualTo(igniteId));
            Assert.That(result.Events.OfType<UnitDefeatedEvent>().Any(evt => evt.UnitId == decoyId),
                Is.EqualTo(expectedDefeat));
        });
    }

    [Test]
    public void LightningAppliesProductionStunAsIncapacitating()
    {
        ContentId stunId = new("buff.stun");
        BattleUnitState enemy = Unit("enemy.target", new GridPoint(3, 1), 1);
        BattleState state = State(new GridPoint(1, 1), 6, new[] { enemy });
        SkillDefinition lightning = Skill("skill.mage.lightning.lv1", SkillExecutionKind.Lightning,
            level: 1, mana: 0, minRange: 1, maxRange: 4, damage: 1,
            statusId: stunId, statusDuration: 1, canCrit: false);

        BattleTransition result = Apply(state, lightning, enemy.Unit.InstanceId, enemy.Unit.Position);

        BattleStatusState stun = result.State.Units[enemy.Unit.InstanceId].Statuses[stunId];
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(stun.EffectKind, Is.EqualTo(StatusEffectKind.Stun));
            Assert.That(stun.CanAct, Is.False);
        });
    }

    [Test]
    public void DirectAttackClassificationIsTheExactSingleTargetDirectExecutionSet()
    {
        var expected = new HashSet<SkillExecutionKind>
        {
            SkillExecutionKind.MagicAttack,
            SkillExecutionKind.MeleeAttack,
            SkillExecutionKind.Fireball,
            SkillExecutionKind.IceBolt,
            SkillExecutionKind.Lightning,
            SkillExecutionKind.BoneSpear,
            SkillExecutionKind.RangedAttack,
            SkillExecutionKind.ChargeStrike,
            SkillExecutionKind.HeavyShot,
            SkillExecutionKind.FireDemonAttack,
            SkillExecutionKind.MultiStab,
            SkillExecutionKind.Thrust,
            SkillExecutionKind.PoisonSpear,
            SkillExecutionKind.DirectAttack,
            SkillExecutionKind.PoetCharge
        };

        SkillExecutionKind[] actual = Enum.GetValues<SkillExecutionKind>()
            .Where(kind => new SkillDefinition(new ContentId($"skill.test.{(int)kind}"), kind.ToString(),
                SkillRole.Any, SkillKind.Active, 1, 0, 0, 5, kind, 0, SkillDamageKind.None,
                executionProfile: kind switch
                {
                    SkillExecutionKind.PoetWineHeal => new SkillExecutionProfile(HealingTickCount: 1),
                    SkillExecutionKind.PoetAgilityVerse => new SkillExecutionProfile(AttributeModifier: 1),
                    SkillExecutionKind.PoetDecoyRetreat => new SkillExecutionProfile(DirectHitCharges: 1),
                    _ => null
                }).IsDirectAttack)
            .ToArray();

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void DecoyRetreatRejectsBlockedBackwardCellWithoutSpendingOrReplacing()
    {
        BattleUnitState blocker = Unit("party.blocker", new GridPoint(1, 1), 0);
        BattleState state = State(new GridPoint(2, 1), 6, new[] { blocker }, facing: UnitFacing.East);
        SkillDefinition retreat = Skill("skill.poet.decoy.lv1", SkillExecutionKind.PoetDecoyRetreat,
            level: 1, mana: 5, minRange: 0, maxRange: 0, damage: 0,
            profile: new SkillExecutionProfile(DirectHitCharges: 1, RetreatDistance: 1));

        BattleTransition result = Apply(state, retreat, state.ActiveUnitId, state.Units[state.ActiveUnitId].Unit.Position);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.State, Is.SameAs(state));
            Assert.That(result.State.Units.Values.Any(unit => unit.SummonOwnerId == state.ActiveUnitId), Is.False);
            Assert.That(result.Events.OfType<CommandRejectedEvent>().Single().Reason, Is.EqualTo("poet_retreat_blocked"));
        });
    }

    private static BattleTransition Apply(BattleState state, SkillDefinition skill,
        UnitInstanceId? targetId, GridPoint targetCell) =>
        new SkillRuntimeService().Apply(state, state.Units[state.ActiveUnitId],
            new UseSkillCommand(state.ActiveUnitId, targetId, targetCell, skill));

    private static SkillDefinition Skill(string id, SkillExecutionKind execution, int level, int mana,
        int minRange, int maxRange, int damage, ContentId? statusId = null, int statusDuration = 0,
        SkillExecutionProfile? profile = null, bool canCrit = true) =>
        new(new ContentId(id), id, SkillRole.Poet, SkillKind.Active, level, mana, minRange, maxRange,
            execution, damage, damage > 0 ? SkillDamageKind.Physical : SkillDamageKind.None,
            statusId, statusDuration, executionProfile: profile, canCrit: canCrit);

    private static BattleState State(GridPoint poetCell, int strength, IEnumerable<BattleUnitState> others,
        int currentMana = 30, int poetHealth = 20,
        IReadOnlyDictionary<ContentId, BattleStatusState>? statuses = null,
        UnitFacing facing = UnitFacing.East)
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary();
        var attributes = new UnitAttributes(strength, 25, 5, 4, 10, 4);
        var poetId = new UnitInstanceId("party.poet");
        var poet = new BattleUnitState(new UnitState(poetId, new ContentId("unit.pure-run.poet"),
            poetCell, 4, 50, 0, 0, effectiveAttributes: attributes, baseMoveRange: 4,
            baseInitiative: 50, combatRole: SkillRole.Poet, facing: facing, baseAttributes: attributes),
            20, poetHealth, maxMana: 30, currentMana: currentMana, statuses: statuses,
            physicalAttack: 2, magicalAttack: 2, manaRecoveryPerTurn: 4);
        BattleUnitState[] units = new[] { poet }.Concat(others).ToArray();
        return new BattleState(new BoardSnapshot(cells), units,
            units.Select(unit => unit.Unit.InstanceId).ToArray(), randomState: 7);
    }

    private static BattleUnitState Unit(string id, GridPoint position, int playerNumber,
        int health = 20, int strength = 5,
        IReadOnlyDictionary<ContentId, BattleStatusState>? statuses = null)
    {
        var attributes = new UnitAttributes(strength, playerNumber == 1 ? 25 : 5, 5, 5, 5, 5);
        return new BattleUnitState(new UnitState(new UnitInstanceId(id), new ContentId($"unit.{id}"),
            position, 3, playerNumber == 1 ? 2 : 10, playerNumber, playerNumber + 1,
            effectiveAttributes: attributes, baseAttributes: attributes),
            20, health, maxMana: 15, currentMana: 15, statuses: statuses);
    }
}
