using NUnit.Framework;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Combat;
using Tactics.Core.Content;
using Tactics.Core.Randomness;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class BattleTransitionTests
{
    [Test]
    public void FireDemonAttack_DealsFrozenDamageAppliesIgniteAndCannotCrit()
    {
        BattleState state = CreateBattleState().WithRandomState(6UL);
        BattleUnitState target = state.Units[new UnitInstanceId("enemy.target.0")];
        var skill = new SkillDefinition(new ContentId("skill.summon.fire-demon-attack"), "unity.fire-demon",
            SkillRole.Any, SkillKind.Basic, 1, 0, 1, 3, SkillExecutionKind.FireDemonAttack, 4,
            SkillDamageKind.Magical, new ContentId("buff.ignite"), 1, isBasicAbility: true, canCrit: false);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, target.Unit.InstanceId, target.Unit.Position, skill));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Events.OfType<DamageAppliedEvent>().Single().Amount, Is.EqualTo(4));
            Assert.That(result.Events.OfType<CombatRollResolvedEvent>().Single().Outcome, Is.Not.EqualTo("critical"));
            Assert.That(result.Events.OfType<CombatRollResolvedEvent>().Single().Roll, Is.EqualTo(92));
            Assert.That(result.Events.OfType<StatusAppliedEvent>().Single().StatusId,
                Is.EqualTo(new ContentId("buff.ignite")));
        });
    }

    [Test]
    public void SuccessfulMovementAndTargetedSkillCommitCoreFacing()
    {
        BattleState state = CreateBattleState();
        BattleTransition moved = new BattleTransitionService().Apply(state,
            new MoveUnitCommand(state.ActiveUnitId, new GridPoint(1, 3)));
        BattleUnitState movedActor = moved.State.Units[state.ActiveUnitId];
        BattleUnitState target = moved.State.Units[new UnitInstanceId("enemy.target.0")];
        var skill = new SkillDefinition(new ContentId("skill.test.attack"), "test.attack", SkillRole.Mage,
            SkillKind.Active, 1, 0, 1, 5, SkillExecutionKind.Fireball, 1, SkillDamageKind.Magical);
        BattleTransition attacked = new BattleTransitionService().Apply(moved.State,
            new UseSkillCommand(state.ActiveUnitId, target.Unit.InstanceId, target.Unit.Position, skill));

        Assert.Multiple(() =>
        {
            Assert.That(moved.Succeeded, Is.True);
            Assert.That(movedActor.Unit.Facing, Is.EqualTo(UnitFacing.North));
            Assert.That(attacked.Succeeded, Is.True);
            Assert.That(attacked.State.Units[state.ActiveUnitId].Unit.Facing, Is.EqualTo(UnitFacing.East));
        });
    }

    [Test]
    public void LivingIntermediateUnit_BlocksSkillLosButDefeatedUnitDoesNot()
    {
        BattleState baseline = CreateBattleState();
        var blockerId = new UnitInstanceId("party.blocker.0");
        var blocker = new BattleUnitState(new UnitState(blockerId, new ContentId("unit.blocker"), new GridPoint(2, 1), 3, 9, 0, 2), 20, 20);
        BattleState blocked = new(baseline.Board, baseline.Units.Values.Append(blocker),
            new[] { baseline.ActiveUnitId, blockerId, new UnitInstanceId("enemy.target.0") }, randomState: baseline.RandomState);
        BattleUnitState target = blocked.Units[new UnitInstanceId("enemy.target.0")];
        var skill = new SkillDefinition(new ContentId("skill.mage.fireball.lv1"), "unity.fireball", SkillRole.Mage,
            SkillKind.Active, 1, 5, 1, 4, SkillExecutionKind.Fireball, 6, SkillDamageKind.Magical);
        var command = new UseSkillCommand(blocked.ActiveUnitId, target.Unit.InstanceId, target.Unit.Position, skill);

        BattleTransition rejected = new BattleTransitionService().Apply(blocked, command);
        BattleState defeatedBlocker = blocked.WithUnit(blocker.WithHealth(0));
        BattleTransition accepted = new BattleTransitionService().Apply(defeatedBlocker, command);

        Assert.Multiple(() =>
        {
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Events.Single(), Is.EqualTo(new CommandRejectedEvent(blocked.ActiveUnitId, "line_of_sight_blocked")));
            Assert.That(rejected.State, Is.SameAs(blocked));
            Assert.That(accepted.Succeeded, Is.True);
        });
    }

    [Test]
    public void CorpseAndDroppedSpear_DoNotBlockSkillLos()
    {
        BattleState baseline = CreateBattleState();
        UnitInstanceId targetId = new("enemy.target.0");
        BattleState state = new(baseline.Board, baseline.Units.Values, baseline.TurnOrder,
            randomState: baseline.RandomState,
            droppedSpears: new Dictionary<UnitInstanceId, GridPoint> { [targetId] = new(3, 1) },
            corpses: new HashSet<GridPoint> { new(2, 1) });
        BattleUnitState target = state.Units[targetId];
        var skill = new SkillDefinition(new ContentId("skill.mage.fireball.lv1"), "unity.fireball", SkillRole.Mage,
            SkillKind.Active, 1, 5, 1, 4, SkillExecutionKind.Fireball, 6, SkillDamageKind.Magical);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, targetId, target.Unit.Position, skill));

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public void PoisonSpear_UsesLivingUnitLosBlockers()
    {
        BattleState baseline = CreateBattleState();
        var blockerId = new UnitInstanceId("party.blocker.0");
        var blocker = new BattleUnitState(new UnitState(blockerId, new ContentId("unit.blocker"), new GridPoint(2, 1), 3, 9, 0, 2), 20, 20);
        UnitInstanceId targetId = new("enemy.target.0");
        BattleState state = new(baseline.Board, baseline.Units.Values.Append(blocker),
            new[] { baseline.ActiveUnitId, blockerId, targetId }, randomState: baseline.RandomState);
        var definition = new PoisonSpearDefinition(new ContentId("skill.poison-spear.lv1"), 5, 8, 3);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UsePoisonSpearCommand(state.ActiveUnitId, targetId, definition));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Events.Single(), Is.EqualTo(new CommandRejectedEvent(state.ActiveUnitId, "line_of_sight_blocked")));
        });
    }

    [Test]
    public void CommandSequence_ProducesImmutableStateAndOrderedGameplayEvents()
    {
        BattleState initial = CreateBattleState();
        var service = new BattleTransitionService();

        BattleTransition movement = service.Apply(
            initial,
            new MoveUnitCommand(new UnitInstanceId("party.caster.0"), new GridPoint(2, 1)));
        BattleTransition skill = service.Apply(
            movement.State,
            new UsePoisonSpearCommand(
                new UnitInstanceId("party.caster.0"),
                new UnitInstanceId("enemy.target.0"),
                new PoisonSpearDefinition(
                    new ContentId("skill.poison-spear.lv1"),
                    5,
                    8,
                    3,
                    poisonDamagePerTurn: 2,
                    manaCost: 6)));
        BattleTransition turn = service.Apply(skill.State, new EndTurnCommand(new UnitInstanceId("party.caster.0")));

        Assert.Multiple(() =>
        {
            Assert.That(movement.Succeeded, Is.True);
            Assert.That(movement.Events, Has.Count.EqualTo(1));
            Assert.That(movement.Events[0], Is.TypeOf<UnitMovedEvent>());
            Assert.That(skill.Succeeded, Is.True);
            Assert.That(skill.Events.Select(item => item.GetType()), Is.EqualTo(new[]
            {
                typeof(SkillUsedEvent),
                typeof(ManaSpentEvent),
                typeof(DamageAppliedEvent),
                typeof(StatusAppliedEvent),
                typeof(SpearDroppedEvent)
            }));
            Assert.That(turn.Succeeded, Is.True);
            Assert.That(turn.Events.Select(item => item.GetType()), Is.EqualTo(new[]
            {
                typeof(TurnAdvancedEvent),
                typeof(StatusTickedEvent)
            }));
        });

        BattleUnitState originalCaster = initial.Units[new UnitInstanceId("party.caster.0")];
        BattleUnitState finalCaster = turn.State.Units[new UnitInstanceId("party.caster.0")];
        BattleUnitState finalTarget = turn.State.Units[new UnitInstanceId("enemy.target.0")];
        Assert.Multiple(() =>
        {
            Assert.That(originalCaster.Unit.Position, Is.EqualTo(new GridPoint(1, 1)));
            Assert.That(originalCaster.HasMovedThisTurn, Is.False);
            Assert.That(finalCaster.Unit.Position, Is.EqualTo(new GridPoint(2, 1)));
            Assert.That(finalCaster.HasMovedThisTurn, Is.True);
            Assert.That(finalCaster.CurrentMana, Is.EqualTo(14));
            Assert.That(finalTarget.CurrentHealth, Is.EqualTo(10));
            Assert.That(finalTarget.StatusDurations[new ContentId("buff.poison")], Is.EqualTo(3));
            Assert.That(finalTarget.Statuses[new ContentId("buff.poison")].DamagePerTurn, Is.EqualTo(2));
            Assert.That(turn.State.Round, Is.EqualTo(1));
            Assert.That(turn.State.ActiveUnitId, Is.EqualTo(new UnitInstanceId("enemy.target.0")));
            Assert.That(turn.State.RandomState, Is.EqualTo(42UL));
            Assert.That(turn.State.DroppedSpears[new UnitInstanceId("party.caster.0")],
                Is.EqualTo(new GridPoint(5, 1)));
        });
    }

    [Test]
    public void PoisonSpear_ReapplicationAddsDurationAndCapturesLatestSource()
    {
        var casterId = new UnitInstanceId("party.caster.0");
        var poisonId = new ContentId("buff.poison");
        BattleState initial = CreateBattleState(new BattleStatusState(poisonId, casterId, 2, 2));

        BattleTransition transition = new BattleTransitionService().Apply(
            initial,
            new UsePoisonSpearCommand(
                casterId,
                new UnitInstanceId("enemy.target.0"),
                new PoisonSpearDefinition(
                    new ContentId("skill.poison-spear.lv1"),
                    5,
                    8,
                    3,
                    poisonId,
                    poisonDamagePerTurn: 2,
                    manaCost: 6)));

        BattleStatusState poison = transition.State.Units[new UnitInstanceId("enemy.target.0")].Statuses[poisonId];
        Assert.Multiple(() =>
        {
            Assert.That(transition.Succeeded, Is.True);
            Assert.That(poison.RemainingTurns, Is.EqualTo(5));
            Assert.That(poison.DamagePerTurn, Is.EqualTo(2));
            Assert.That(poison.SourceId, Is.EqualTo(casterId));
            Assert.That(transition.Events.OfType<StatusAppliedEvent>().Single().RemainingTurns, Is.EqualTo(5));
        });
    }

    [Test]
    public void PoisonSpear_RejectsASecondUseUntilTheDroppedSpearIsRecovered()
    {
        var casterId = new UnitInstanceId("party.caster.0");
        var targetId = new UnitInstanceId("enemy.target.0");
        var definition = new PoisonSpearDefinition(
            new ContentId("skill.poison-spear.lv1"),
            5,
            8,
            3,
            manaCost: 6);
        var service = new BattleTransitionService();
        BattleTransition first = service.Apply(
            CreateBattleState(),
            new UsePoisonSpearCommand(casterId, targetId, definition));
        BattleTransition second = service.Apply(
            first.State,
            new UsePoisonSpearCommand(casterId, targetId, definition));

        Assert.Multiple(() =>
        {
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.Events.OfType<SpearDroppedEvent>().Single().Cell,
                Is.EqualTo(new GridPoint(5, 1)));
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.State, Is.SameAs(first.State));
            Assert.That(second.Events.Single(), Is.EqualTo(
                new CommandRejectedEvent(casterId, "spear_not_held")));
        });
    }

    [Test]
    public void EndTurn_ExpiresOutgoingStatusBeforeAdvancing()
    {
        var casterId = new UnitInstanceId("party.caster.0");
        var targetId = new UnitInstanceId("enemy.target.0");
        var poisonId = new ContentId("buff.poison");
        BattleState initial = CreateBattleState(
            new BattleStatusState(poisonId, casterId, 1, 2),
            activeIndex: 1);

        BattleTransition transition = new BattleTransitionService().Apply(
            initial,
            new EndTurnCommand(targetId));

        Assert.Multiple(() =>
        {
            Assert.That(transition.Events.Select(item => item.GetType()), Is.EqualTo(new[]
            {
                typeof(StatusExpiredEvent),
                typeof(TurnAdvancedEvent)
            }));
            Assert.That(transition.State.Units[targetId].Statuses, Is.Empty);
            Assert.That(transition.State.ActiveUnitId, Is.EqualTo(casterId));
            Assert.That(transition.State.Round, Is.EqualTo(2));
        });
    }

    [Test]
    public void AdvanceTurn_RemovesConsecutiveDefeatedUnitsAndRebuildsTheNextRoundOnce()
    {
        BattleState baseline = CreateBattleState(activeIndex: 1);
        BattleUnitState caster = baseline.Units[new UnitInstanceId("party.caster.0")];
        BattleUnitState target = baseline.Units[new UnitInstanceId("enemy.target.0")];
        var secondDeadId = new UnitInstanceId("enemy.dead.1");
        var secondDead = new BattleUnitState(new UnitState(secondDeadId, new ContentId("unit.dead"), new GridPoint(5, 1), 3, 7, 1, 2), 20, 0);
        BattleState state = new(baseline.Board,
            new[] { caster, target.WithHealth(0), secondDead },
            new[] { caster.Unit.InstanceId, target.Unit.InstanceId, secondDeadId },
            round: 3, activeIndex: 1, randomState: baseline.RandomState);

        BattleState advanced = state.AdvanceTurn();

        Assert.Multiple(() =>
        {
            Assert.That(advanced.ActiveUnitId, Is.EqualTo(caster.Unit.InstanceId));
            Assert.That(advanced.Round, Is.EqualTo(4));
            Assert.That(advanced.TurnOrder, Does.Not.Contain(target.Unit.InstanceId));
            Assert.That(advanced.TurnOrder, Does.Not.Contain(secondDeadId));
            Assert.That(advanced.InitiativeRound.Remaining, Is.Empty);
        });
    }

    [Test]
    public void PoisonSpear_RejectsWithoutManaAndLeavesStateUnchanged()
    {
        BattleState initial = CreateBattleState(casterMana: 5);
        BattleTransition transition = new BattleTransitionService().Apply(
            initial,
            new UsePoisonSpearCommand(
                new UnitInstanceId("party.caster.0"),
                new UnitInstanceId("enemy.target.0"),
                new PoisonSpearDefinition(
                    new ContentId("skill.poison-spear.lv1"),
                    5,
                    8,
                    3,
                    manaCost: 6)));

        Assert.Multiple(() =>
        {
            Assert.That(transition.Succeeded, Is.False);
            Assert.That(transition.State, Is.SameAs(initial));
            Assert.That(transition.Events.Single(), Is.EqualTo(
                new CommandRejectedEvent(new UnitInstanceId("party.caster.0"), "insufficient_mana")));
        });
    }

    [Test]
    public void RejectedCommand_ReturnsOriginalStateAndSingleStableReason()
    {
        BattleState initial = CreateBattleState();

        BattleTransition transition = new BattleTransitionService().Apply(
            initial,
            new EndTurnCommand(new UnitInstanceId("enemy.target.0")));

        Assert.Multiple(() =>
        {
            Assert.That(transition.Succeeded, Is.False);
            Assert.That(transition.State, Is.SameAs(initial));
            Assert.That(transition.Events, Is.EqualTo(new BattleEvent[]
            {
                new CommandRejectedEvent(new UnitInstanceId("enemy.target.0"), "not_active_unit")
            }));
        });
    }

    [Test]
    public void DeterministicRandom_UsesVersionedSplitMix64Sequence()
    {
        var random = new DeterministicRandom(42UL);

        Assert.Multiple(() =>
        {
            Assert.That(random.NextUInt64(), Is.EqualTo(13679457532755275413UL));
            Assert.That(random.NextInt(100), Is.EqualTo(91));
            Assert.That(random.NextInt(7), Is.EqualTo(0));
            Assert.That(random.NextUInt64(), Is.EqualTo(6349198060258255764UL));
            Assert.That(random.State, Is.EqualTo(8709371129873690750UL));
        });
    }

    [Test]
    public void BattleState_AllowsDistinctInstancesOfTheSameUnitDefinition()
    {
        BattleState baseline = CreateBattleState();
        var secondTargetId = new UnitInstanceId("enemy.target.1");
        var secondTarget = new BattleUnitState(new UnitState(
            secondTargetId,
            new ContentId("unit.target"),
            new GridPoint(5, 1),
            3,
            8,
            1,
            2), 20, 20);
        var state = new BattleState(
            baseline.Board,
            baseline.Units.Values.Append(secondTarget),
            baseline.TurnOrder.Append(secondTargetId).ToArray(),
            randomState: baseline.RandomState);

        Assert.Multiple(() =>
        {
            Assert.That(state.Units, Has.Count.EqualTo(3));
            Assert.That(
                state.Units.Values.Count(unit => unit.Unit.DefinitionId == new ContentId("unit.target")),
                Is.EqualTo(2));
            Assert.That(state.Units.ContainsKey(new UnitInstanceId("enemy.target.0")), Is.True);
            Assert.That(state.Units.ContainsKey(new UnitInstanceId("enemy.target.1")), Is.True);
        });
    }

    [Test]
    public void InitiativeChange_ReordersOnlyPendingBattleStateSuffix()
    {
        BattleState baseline = CreateBattleState();
        BattleUnitState target = baseline.Units[new UnitInstanceId("enemy.target.0")];
        var earlierId = new UnitInstanceId("enemy.earlier.0");
        var earlier = new BattleUnitState(new UnitState(
            earlierId,
            new ContentId("unit.target"),
            new GridPoint(5, 1),
            3,
            8,
            1,
            0), 20, 20);
        var state = new BattleState(
            baseline.Board,
            baseline.Units.Values.Append(earlier),
            new[] { baseline.ActiveUnitId, earlierId, target.Unit.InstanceId },
            randomState: baseline.RandomState);

        BattleUnitState acceleratedTarget = new(
            target.Unit with { Initiative = 12 },
            target.MaxHealth,
            target.CurrentHealth,
            target.HasMovedThisTurn,
            maxMana: target.MaxMana,
            currentMana: target.CurrentMana,
            statuses: target.Statuses);
        BattleState reordered = state.WithInitiativeChanged(acceleratedTarget);
        BattleUnitState slowedCurrent = new(
            reordered.Units[reordered.ActiveUnitId].Unit with { Initiative = 1 },
            20,
            20);
        BattleState currentUnchanged = reordered.WithInitiativeChanged(slowedCurrent);

        Assert.Multiple(() =>
        {
            Assert.That(reordered.TurnOrder.Select(id => id.Value), Is.EqualTo(new[]
            {
                "party.caster.0", "enemy.target.0", "enemy.earlier.0"
            }));
            Assert.That(currentUnchanged.TurnOrder, Is.EqualTo(reordered.TurnOrder));
            Assert.That(currentUnchanged.ActiveUnitId, Is.EqualTo(state.ActiveUnitId));
        });
    }

    [Test]
    public void InitiativeChange_OnCurrentUnitAffectsFreshlySortedNextRoundWithoutSecondAction()
    {
        BattleState baseline = CreateBattleState();
        BattleUnitState caster = baseline.Units[baseline.ActiveUnitId];
        BattleUnitState target = baseline.Units[new UnitInstanceId("enemy.target.0")];
        var thirdId = new UnitInstanceId("enemy.third.0");
        var third = new BattleUnitState(new UnitState(thirdId, new ContentId("unit.target"),
            new GridPoint(6, 1), 3, 6, 1, 2), 20, 20);
        var state = new BattleState(baseline.Board, baseline.Units.Values.Append(third),
            new[] { caster.Unit.InstanceId, target.Unit.InstanceId, thirdId }, randomState: baseline.RandomState);

        BattleUnitState slowedCaster = state.Units[caster.Unit.InstanceId]
            .WithUnitFacts(caster.Unit with { Initiative = 1 });
        state = state.WithUnit(slowedCaster);

        BattleState targetTurn = state.AdvanceTurn();
        BattleState thirdTurn = targetTurn.AdvanceTurn();
        BattleState nextRound = thirdTurn.AdvanceTurn();

        Assert.Multiple(() =>
        {
            Assert.That(targetTurn.ActiveUnitId, Is.EqualTo(target.Unit.InstanceId));
            Assert.That(thirdTurn.ActiveUnitId, Is.EqualTo(thirdId));
            Assert.That(nextRound.Round, Is.EqualTo(2));
            Assert.That(nextRound.ActiveUnitId, Is.EqualTo(target.Unit.InstanceId));
            Assert.That(nextRound.InitiativeRound.Acted.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void SummonReplacement_PreservesCurrentActorAndNewInstanceCanActThisRound()
    {
        BattleState baseline = CreateBattleState();
        BattleUnitState owner = baseline.Units[baseline.ActiveUnitId];
        BattleUnitState enemy = baseline.Units[new UnitInstanceId("enemy.target.0")];
        var oldId = new UnitInstanceId("party.summon.old");
        var oldSummon = new BattleUnitState(new UnitState(oldId, new ContentId("unit.summon"),
            new GridPoint(2, 2), 3, 20, 0, 1), 8, 8, summonOwnerId: owner.Unit.InstanceId,
            canProduceCorpse: false, summonCategory: "FireDemon");
        var state = new BattleState(baseline.Board, new[] { oldSummon, owner, enemy },
            new[] { oldId, owner.Unit.InstanceId, enemy.Unit.InstanceId }, activeIndex: 1,
            randomState: baseline.RandomState);
        var newId = new UnitInstanceId("party.summon.new");
        var replacement = new BattleUnitState(new UnitState(newId, new ContentId("unit.summon"),
            new GridPoint(3, 2), 3, 30, 0, 2), 8, 8, summonOwnerId: owner.Unit.InstanceId,
            canProduceCorpse: false, summonCategory: "FireDemon");

        BattleState replaced = state.WithSummon(replacement, 1, "FireDemon");
        BattleState advanced = replaced.AdvanceTurn();

        Assert.Multiple(() =>
        {
            Assert.That(replaced.ActiveUnitId, Is.EqualTo(owner.Unit.InstanceId));
            Assert.That(replaced.Units.ContainsKey(oldId), Is.False);
            Assert.That(replaced.InitiativeRound.Remaining.Select(entry => entry.UnitId),
                Does.Contain(newId));
            Assert.That(advanced.ActiveUnitId, Is.EqualTo(newId));
            Assert.That(advanced.Round, Is.EqualTo(1));
        });
    }

    [Test]
    public void NonActingDecoy_DoesNotEnterInitiativePartition()
    {
        BattleState state = CreateBattleState();
        BattleUnitState owner = state.Units[state.ActiveUnitId];
        var decoyId = new UnitInstanceId("party.decoy.0");
        var decoy = new BattleUnitState(new UnitState(decoyId, new ContentId("unit.decoy"),
            new GridPoint(2, 2), 0, 99, 0, 4), 1, 1, summonOwnerId: owner.Unit.InstanceId,
            canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: "Decoy");

        BattleState updated = state.WithSummon(decoy, 1, "Decoy");

        Assert.Multiple(() =>
        {
            Assert.That(updated.Units.ContainsKey(decoyId), Is.True);
            Assert.That(updated.TurnOrder, Does.Not.Contain(decoyId));
            Assert.That(updated.InitiativeRound.Remaining.Select(entry => entry.UnitId), Does.Not.Contain(decoyId));
        });
    }

    private static BattleState CreateBattleState(
        BattleStatusState? targetStatus = null,
        int activeIndex = 0,
        int casterMana = 20)
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var casterId = new UnitInstanceId("party.caster.0");
        var targetId = new UnitInstanceId("enemy.target.0");
        return new BattleState(
            new BoardSnapshot(cells),
            new[]
            {
                new BattleUnitState(new UnitState(
                    casterId,
                    new ContentId("unit.caster"),
                    new GridPoint(1, 1),
                    3,
                    10,
                    0,
                    0),
                    20,
                    20,
                    maxMana: 20,
                    currentMana: casterMana),
                new BattleUnitState(new UnitState(
                    targetId,
                    new ContentId("unit.target"),
                    new GridPoint(4, 1),
                    3,
                    8,
                    1,
                    1),
                    20,
                    20,
                    statuses: targetStatus is null
                        ? null
                        : new Dictionary<ContentId, BattleStatusState>
                        {
                            [targetStatus.ContentId] = targetStatus
                        })
            },
            new[] { casterId, targetId },
            activeIndex: activeIndex,
            randomState: 42UL);
    }
}
