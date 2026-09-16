using NUnit.Framework;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Statuses;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class StatusItemRuntimeTests
{
    [Test]
    public void Apply_UsesFrozenRefreshOverridesCurseReplacementAndBaseSpeed()
    {
        var service = new StatusRuntimeService();
        BattleUnitState unit = Unit("party.actor.0", player: 0, speed: 5f);
        UnitInstanceId source = new("enemy.source.0");
        StatusDefinition poison = Status("buff.poison", StatusEffectKind.Poison, 3, StatusRefreshStrategy.RefreshDuration);
        StatusDefinition burning = Status("buff.ignite", StatusEffectKind.Burning, 2, StatusRefreshStrategy.AddDuration);
        StatusDefinition slow = Status("buff.slow", StatusEffectKind.Slow, 2, StatusRefreshStrategy.AddDuration);
        StatusDefinition curseA = Status(
            "buff.fear", StatusEffectKind.Fear, 1, StatusRefreshStrategy.RefreshDuration, curseCategory: "Curse");
        StatusDefinition curseB = Status(
            "buff.curse-damage-amplifier", StatusEffectKind.CurseDamageAmplifier, 5,
            StatusRefreshStrategy.RefreshDuration, curseCategory: "Curse");

        unit = service.Apply(unit, poison, source).Unit;
        unit = service.Apply(unit, poison, source, 2).Unit;
        unit = service.Apply(unit, burning, source).Unit;
        unit = service.Apply(unit, burning, source).Unit;
        unit = service.Apply(unit, slow, source).Unit;
        unit = service.Apply(unit, slow, source, 4).Unit;
        unit = service.Apply(unit, curseA, source).Unit;
        StatusApplicationResult curseResult = service.Apply(unit, curseB, source);

        Assert.Multiple(() =>
        {
            Assert.That(unit.Statuses[new ContentId("buff.poison")].RemainingTurns, Is.EqualTo(5));
            Assert.That(unit.Statuses[new ContentId("buff.ignite")].StackCount, Is.EqualTo(4));
            Assert.That(unit.Statuses[new ContentId("buff.slow")].RemainingTurns, Is.EqualTo(4));
            Assert.That(unit.BaseSpeed, Is.EqualTo(5f));
            Assert.That(unit.Unit.MoveRange, Is.EqualTo(2));
            Assert.That(unit.Unit.Initiative, Is.EqualTo(6f));
            Assert.That(curseResult.ReplacedStatusIds, Is.EqualTo(new[] { new ContentId("buff.fear") }));
            Assert.That(curseResult.Unit.Statuses.ContainsKey(new ContentId("buff.fear")), Is.False);
        });

        BattleUnitState restored = service.Remove(curseResult.Unit, new ContentId("buff.slow"));
        Assert.Multiple(() =>
        {
            Assert.That(restored.Unit.MoveRange, Is.EqualTo(3));
            Assert.That(restored.Unit.Initiative, Is.EqualTo(10f));
        });
    }

    [Test]
    public void TemporaryAgilityModifier_UpdatesAccuracyAndInitiativeThenRestoresBaseline()
    {
        var service = new StatusRuntimeService();
        BattleUnitState unit = Unit("party.actor.0", player: 0, speed: 5f);
        var definition = new StatusDefinition(new ContentId("buff.poet.agility"), "poet.agility", 1,
            true, StatusPolarity.Beneficial, StatusEffectKind.None, StatusTriggerTiming.None,
            StatusRefreshStrategy.RefreshDuration,
            attributeModifiers: new UnitAttributeModifiers(Agility: 2));

        BattleUnitState buffed = service.Apply(unit, definition, unit.Unit.InstanceId).Unit;
        BattleUnitState restored = service.Remove(buffed, definition.ContentId);

        Assert.Multiple(() =>
        {
            Assert.That(buffed.Unit.EffectiveAttributes.Agility, Is.EqualTo(7));
            Assert.That(buffed.Unit.Initiative, Is.EqualTo(14));
            Assert.That(UnitCombatStatRules.Accuracy(buffed.Unit.EffectiveAttributes), Is.EqualTo(110));
            Assert.That(restored.Unit.EffectiveAttributes.Agility, Is.EqualTo(5));
            Assert.That(restored.Unit.Initiative, Is.EqualTo(10));
        });
    }

    [Test]
    public void TemporaryAttributeRefresh_KeepsStrongestModifierAndRefreshesDuration()
    {
        var service = new StatusRuntimeService();
        BattleUnitState unit = Unit("party.actor.0", player: 0, speed: 5f);
        StatusDefinition stronger = new(new ContentId("buff.poet.agility"), "poet.agility", 1,
            true, StatusPolarity.Beneficial, StatusEffectKind.None, StatusTriggerTiming.None,
            StatusRefreshStrategy.RefreshDuration,
            attributeModifiers: new UnitAttributeModifiers(Agility: 4));
        StatusDefinition weaker = new(new ContentId("buff.poet.agility"), "poet.agility", 3,
            true, StatusPolarity.Beneficial, StatusEffectKind.None, StatusTriggerTiming.None,
            StatusRefreshStrategy.RefreshDuration,
            attributeModifiers: new UnitAttributeModifiers(Agility: 2));

        BattleUnitState buffed = service.Apply(unit, stronger, unit.Unit.InstanceId).Unit;
        BattleUnitState refreshed = service.Apply(buffed, weaker, unit.Unit.InstanceId).Unit;

        Assert.Multiple(() =>
        {
            Assert.That(refreshed.Statuses[stronger.ContentId].RemainingTurns, Is.EqualTo(3));
            Assert.That(refreshed.Statuses[stronger.ContentId].AttributeModifiers.Agility, Is.EqualTo(4));
            Assert.That(refreshed.Unit.EffectiveAttributes.Agility, Is.EqualTo(9));
            Assert.That(refreshed.Unit.Initiative, Is.EqualTo(18));
        });
    }

    [Test]
    public void HealingOverTime_TicksThreeFutureTurnStartsAndDistributesRemainderEarly()
    {
        var statusService = new StatusRuntimeService();
        BattleUnitState source = Unit("party.source.0", player: 0, speed: 5f);
        BattleUnitState target = Unit("party.target.0", player: 0, speed: 4f,
            position: new GridPoint(3, 1)).WithHealth(5);
        var healing = new StatusDefinition(new ContentId("buff.poet.healing"), "poet.healing", 3,
            true, StatusPolarity.Beneficial, StatusEffectKind.None, StatusTriggerTiming.TurnStart,
            StatusRefreshStrategy.RefreshDuration, frozenTotalHealing: 10);
        target = statusService.Apply(target, healing, source.Unit.InstanceId).Unit;
        BattleState state = State(source, target);
        var transitions = new BattleTransitionService(statusRuntime: statusService);

        state = transitions.Apply(state, new EndTurnCommand(source.Unit.InstanceId)).State;
        int first = state.Units[target.Unit.InstanceId].CurrentHealth;
        state = transitions.Apply(state, new EndTurnCommand(target.Unit.InstanceId)).State;
        state = transitions.Apply(state, new EndTurnCommand(source.Unit.InstanceId)).State;
        int second = state.Units[target.Unit.InstanceId].CurrentHealth;
        state = transitions.Apply(state, new EndTurnCommand(target.Unit.InstanceId)).State;
        state = transitions.Apply(state, new EndTurnCommand(source.Unit.InstanceId)).State;
        BattleUnitState completed = state.Units[target.Unit.InstanceId];

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(9));
            Assert.That(second, Is.EqualTo(12));
            Assert.That(completed.CurrentHealth, Is.EqualTo(15));
            Assert.That(completed.Statuses.ContainsKey(healing.ContentId), Is.False);
        });
    }

    [Test]
    public void EndTurn_TicksStatusesInContentIdOrderAndBurningConsumesStacks()
    {
        var statusService = new StatusRuntimeService();
        BattleUnitState actor = Unit("party.actor.0", player: 0, speed: 5f);
        BattleUnitState target = Unit("enemy.target.0", player: 1, speed: 4f, position: new GridPoint(3, 1));
        UnitInstanceId source = actor.Unit.InstanceId;
        target = statusService.Apply(
            target,
            Status("buff.poison", StatusEffectKind.Poison, 3, StatusRefreshStrategy.AddDuration, damagePerTurn: 2),
            source).Unit;
        target = statusService.Apply(
            target,
            Status("buff.ignite", StatusEffectKind.Burning, 2, StatusRefreshStrategy.AddStacks, damagePerTurn: 2),
            source).Unit;
        BattleState state = State(actor, target);

        BattleTransition transition = new BattleTransitionService(statusRuntime: statusService).Apply(
            state,
            new EndTurnCommand(actor.Unit.InstanceId));

        Assert.Multiple(() =>
        {
            Assert.That(transition.Events.Select(item => item.GetType()), Is.EqualTo(new[]
            {
                typeof(TurnAdvancedEvent),
                typeof(StatusTickedEvent),
                typeof(StatusStackChangedEvent),
                typeof(StatusTickedEvent)
            }));
            Assert.That(transition.Events.OfType<StatusTickedEvent>().Select(item => item.StatusId.Value),
                Is.EqualTo(new[] { "buff.ignite", "buff.poison" }));
            Assert.That(transition.State.Units[target.Unit.InstanceId].CurrentHealth, Is.EqualTo(16));
            Assert.That(transition.State.Units[target.Unit.InstanceId].Statuses[new ContentId("buff.ignite")].StackCount,
                Is.EqualTo(1));
        });
    }

    [TestCase(StatusEffectKind.Frozen)]
    [TestCase(StatusEffectKind.Stun)]
    public void BlockingStatus_RejectsNonEndTurnButAllowsEndTurn(StatusEffectKind effectKind)
    {
        var statusService = new StatusRuntimeService();
        BattleUnitState actor = statusService.Apply(
            Unit("party.actor.0", player: 0, speed: 5f),
            Status($"buff.{effectKind.ToString().ToLowerInvariant()}", effectKind, 1,
                StatusRefreshStrategy.RefreshDuration, canAct: false),
            new UnitInstanceId("enemy.source.0")).Unit;
        BattleUnitState target = Unit("enemy.target.0", player: 1, speed: 4f, position: new GridPoint(3, 1));
        BattleState state = State(actor, target);
        var service = new BattleTransitionService(statusRuntime: statusService);

        BattleTransition move = service.Apply(state, new MoveUnitCommand(actor.Unit.InstanceId, new GridPoint(2, 1)));
        BattleTransition end = service.Apply(state, new EndTurnCommand(actor.Unit.InstanceId));

        Assert.Multiple(() =>
        {
            Assert.That(move.Events.Single(), Is.EqualTo(new CommandRejectedEvent(actor.Unit.InstanceId, "status_prevents_action")));
            Assert.That(end.Succeeded, Is.True);
        });
    }

    [Test]
    public void Consumable_ValidZeroRecoveryConsumesChargeAndEnforcesOneUsePerRound()
    {
        ItemInstanceId itemId = new("life-potion.0");
        ConsumableDefinition potion = Consumable(
            "item.consumable.life-potion", ConsumableEffectKind.RestoreHealth, magnitude: 8, maxCharges: 2);
        BattleUnitState actor = Unit("party.actor.0", player: 0, speed: 5f)
            .WithConsumable(new BattleConsumableState(itemId, potion.ContentId, 2, 2));
        BattleUnitState target = Unit("party.target.0", player: 0, speed: 4f, position: new GridPoint(2, 1));
        BattleState state = State(actor, target);
        var command = new UseConsumableCommand(actor.Unit.InstanceId, target.Unit.InstanceId, itemId, potion);
        var service = new BattleTransitionService();

        BattleTransition first = service.Apply(state, command);
        BattleTransition second = service.Apply(first.State, command);

        Assert.Multiple(() =>
        {
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.Events.OfType<HealthRestoredEvent>().Single().Amount, Is.Zero);
            Assert.That(first.State.Units[actor.Unit.InstanceId].Consumables[itemId].RemainingCharges, Is.EqualTo(1));
            Assert.That(second.Events.Single(), Is.EqualTo(
                new CommandRejectedEvent(actor.Unit.InstanceId, "consumable_already_used_this_round")));
        });
    }

    [Test]
    public void Consumable_InvalidTargetDoesNotConsumeAndCleansingRemovesOnlyHarmful()
    {
        var statusService = new StatusRuntimeService();
        ItemInstanceId itemId = new("cleanse.0");
        ConsumableDefinition cleanse = Consumable(
            "item.consumable.cleansing-potion", ConsumableEffectKind.RemoveHarmfulBuffs, magnitude: 0);
        BattleUnitState actor = Unit("party.actor.0", player: 0, speed: 5f)
            .WithConsumable(new BattleConsumableState(itemId, cleanse.ContentId, 1, 1));
        BattleUnitState ally = Unit("party.target.0", player: 0, speed: 4f, position: new GridPoint(2, 1));
        ally = statusService.Apply(
            ally,
            Status("buff.slow", StatusEffectKind.Slow, 2, StatusRefreshStrategy.RefreshDuration),
            actor.Unit.InstanceId).Unit;
        ally = statusService.Apply(
            ally,
            Status("buff.ice-armor", StatusEffectKind.DamageReduction, 2,
                StatusRefreshStrategy.AddDuration, polarity: StatusPolarity.Beneficial),
            actor.Unit.InstanceId).Unit;
        BattleUnitState enemy = Unit("enemy.target.0", player: 1, speed: 4f, position: new GridPoint(1, 2));
        BattleState state = State(actor, ally, enemy);
        var service = new BattleTransitionService(statusRuntime: statusService);

        BattleTransition rejected = service.Apply(state, new UseConsumableCommand(
            actor.Unit.InstanceId, enemy.Unit.InstanceId, itemId, cleanse));
        BattleTransition accepted = service.Apply(state, new UseConsumableCommand(
            actor.Unit.InstanceId, ally.Unit.InstanceId, itemId, cleanse));

        Assert.Multiple(() =>
        {
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.State, Is.SameAs(state));
            Assert.That(accepted.State.Units[ally.Unit.InstanceId].Statuses.Keys.Select(id => id.Value),
                Is.EqualTo(new[] { "buff.ice-armor" }));
            Assert.That(accepted.Events.OfType<StatusesCleansedEvent>().Single().RemovedStatusIds,
                Is.EqualTo(new[] { new ContentId("buff.slow") }));
        });
    }

    [Test]
    public void EquipmentProjection_RejectsDuplicateSlotsAndReusesDerivedContract()
    {
        UnitAttributes baseline = new(5, 5, 5, 5, 5, 5);
        EquipmentDefinition sword = Equipment("item.equipment.sword-01", EquipmentSlot.Weapon, strength: 5);
        EquipmentDefinition armor = Equipment(
            "item.equipment.leather-armor-01", EquipmentSlot.Armor, agility: 2, constitution: 3);

        EquipmentStatProjection result = EquipmentStatProjector.Project(baseline, 5f, new[] { sword, armor });

        Assert.Multiple(() =>
        {
            Assert.That(result.Attributes, Is.EqualTo(new UnitAttributes(10, 7, 8, 5, 5, 5)));
            Assert.That(result.DerivedStats, Is.EqualTo(UnitDerivedStatRules.Calculate(result.Attributes, 5f)));
            Assert.Throws<ArgumentException>(() => EquipmentStatProjector.Project(
                baseline,
                5f,
                new[] { sword, Equipment("item.equipment.staff-01", EquipmentSlot.Weapon) }));
        });
    }

    [Test]
    public void PolicyOutputs_AreTypedWithoutAttachingSkillOrAiSideEffects()
    {
        var service = new StatusRuntimeService();
        BattleUnitState defender = Unit("party.actor.0", player: 0, speed: 5f);
        BattleUnitState attacker = Unit("enemy.target.0", player: 1, speed: 4f, position: new GridPoint(2, 1));
        defender = service.Apply(defender,
            Status("buff.mark", StatusEffectKind.Marked, 2, StatusRefreshStrategy.AddDuration,
                trigger: StatusTriggerTiming.BeforeAttacked), attacker.Unit.InstanceId).Unit;
        defender = service.Apply(defender,
            Status("buff.counter", StatusEffectKind.None, 1, StatusRefreshStrategy.AddDuration,
                polarity: StatusPolarity.Beneficial, trigger: StatusTriggerTiming.DamageTaken),
            defender.Unit.InstanceId).Unit;
        defender = service.Apply(defender,
            Status("buff.ice-armor.lv2", StatusEffectKind.DamageReduction, 2,
                StatusRefreshStrategy.RefreshDuration, polarity: StatusPolarity.Beneficial,
                reduction: 0.25f, retaliation: new ContentId("buff.slow"), retaliationDuration: 2),
            defender.Unit.InstanceId).Unit;

        StatusBeforeAttackPolicy before = service.EvaluateBeforeAttack(defender);
        StatusDamagePolicy damage = service.EvaluateDamageTaken(defender, attacker, isRangedDamage: false);

        Assert.Multiple(() =>
        {
            Assert.That(before.ForceCritical, Is.True);
            Assert.That(damage.DamageMultiplier, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(damage.CounterRequested, Is.True);
            Assert.That(damage.Retaliations.Single().AppliedStatusId, Is.EqualTo(new ContentId("buff.slow")));
        });
    }

    private static StatusDefinition Status(
        string id,
        StatusEffectKind effect,
        int duration,
        StatusRefreshStrategy refresh,
        bool canAct = true,
        StatusPolarity polarity = StatusPolarity.Harmful,
        string curseCategory = "",
        int damagePerTurn = 0,
        StatusTriggerTiming trigger = StatusTriggerTiming.None,
        float reduction = 0f,
        ContentId? retaliation = null,
        int retaliationDuration = 0) => new(
            new ContentId(id),
            id,
            duration,
            canAct,
            polarity,
            effect,
            trigger,
            refresh,
            curseCategory,
            damagePerTurn,
            damageReductionPercent: reduction,
            meleeRetaliationStatusId: retaliation,
            meleeRetaliationDuration: retaliationDuration);

    private static ConsumableDefinition Consumable(
        string id,
        ConsumableEffectKind effect,
        int magnitude,
        int maxCharges = 1) => new(
        new ContentId(id), id, id, string.Empty, ItemRarity.Common, 1, maxCharges, effect, magnitude, 1,
        ConsumableTargetMode.AllyIncludingSelf);

    private static EquipmentDefinition Equipment(
        string id,
        EquipmentSlot slot,
        int strength = 0,
        int agility = 0,
        int constitution = 0) => new(
            new ContentId(id), id, id, slot, ItemRarity.Common, 1,
            new UnitAttributes(strength, agility, constitution, 0, 0, 0));

    private static BattleUnitState Unit(
        string id,
        int player,
        float speed,
        GridPoint? position = null) => new(
            new UnitState(
                new UnitInstanceId(id),
                new ContentId(player == 0 ? "unit.party" : "unit.enemy"),
                position ?? new GridPoint(1, 1),
                (int)Math.Clamp(Math.Ceiling(speed * 0.5d), 1d, 4d),
                speed * 2f,
                player,
                player),
            20,
            20,
            maxMana: 10,
            currentMana: 10,
            baseSpeed: speed);

    private static BattleState State(params BattleUnitState[] units)
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        return new BattleState(
            new BoardSnapshot(cells),
            units,
            units.Select(unit => unit.Unit.InstanceId).ToArray());
    }
}
