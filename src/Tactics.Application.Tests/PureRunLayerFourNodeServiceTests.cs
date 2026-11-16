using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunLayerFourNodeServiceTests
{
    private readonly PureRunLayerFourNodeService _service = new();

    [Test]
    public void SelectingRouteLocksOthersAndRestCommitsOnce()
    {
        PureRunState selected = Select(CreateRun(), "layer_04_rest").State;
        Assert.That(selected.MapState!.ReachableNodeIds, Is.EqualTo(new[] { "layer_04_rest" }));
        LayerFourNodeResolution result = _service.ConfirmRest(selected);
        Assert.Multiple(() =>
        {
            Assert.That(result.State.Party[0].CurrentHealth, Is.EqualTo(14));
            Assert.That(result.State.Party[0].CurrentMana, Is.EqualTo(8));
            Assert.That(result.State.Phase, Is.EqualTo(PureRunPhase.ReadyForLayerFive));
            Assert.That(result.State.MapState!.NodeLifecycle, Is.EqualTo(RunNodeLifecycle.Committed));
            Assert.That(result.State.MapState.ReachableNodeIds, Is.Empty);
            Assert.That(result.State.PendingProgression, Is.Empty);
        });
        Assert.That(_service.ConfirmRest(result.State).WasDuplicate, Is.True);
    }

    [Test]
    public void StoreOffersAndPurchasesPersistDeterministically()
    {
        PureRunState run = Select(CreateRun(gold: 20), "layer_04_store").State;
        RunStoreOffer[] items = [new(new ContentId("item.a"), 2, true), new(new ContentId("item.b"), 3, true)];
        RunStoreOffer[] gear = [new(new ContentId("item.c"), 4, false), new(new ContentId("item.d"), 5, false)];
        LayerFourNodeResolution opened = _service.OpenStore(run, gear, items);
        LayerFourNodeResolution replay = _service.OpenStore(opened.State, gear.Reverse(), items.Reverse());
        Assert.That(replay.StoreOffers, Is.EqualTo(opened.StoreOffers));
        Assert.That(opened.StoreOffers, Has.Count.EqualTo(3));
        Assert.That(opened.StoreOffers!.Count(value => value.IsConsumable), Is.GreaterThanOrEqualTo(1));
        RunStoreOfferState offer = opened.StoreOffers!.First(value => value.IsConsumable);
        var definitions = new Dictionary<ContentId, ConsumableDefinition>
        {
            [offer.ContentId] = new(offer.ContentId, "source", "Item", "", ItemRarity.Common, offer.Price, 1,
                ConsumableEffectKind.RestoreHealth, 1, 1, ConsumableTargetMode.Self)
        };
        LayerFourNodeResolution bought = _service.Purchase(opened.State, offer.InstanceId, definitions,
            new Dictionary<ContentId, EquipmentDefinition>());
        Assert.That(bought.State.Gold, Is.EqualTo(20 - offer.Price));
        Assert.That(bought.State.BackpackConsumables.Single().InstanceId, Is.EqualTo(offer.InstanceId));
        Assert.That(_service.Purchase(bought.State, offer.InstanceId, definitions,
            new Dictionary<ContentId, EquipmentDefinition>()).RejectionCode, Is.EqualTo("store.offer_already_purchased"));
        LayerFourNodeResolution left = _service.LeaveStore(bought.State);
        Assert.That(left.State.Phase, Is.EqualTo(PureRunPhase.ReadyForLayerFive));
        Assert.That(left.State.PendingProgression, Is.Empty);
    }

    [Test]
    public void MysteryRollIsPersistedAndDamageIsAppliedOnce()
    {
        PureRunState run = Select(CreateRun(allAtOneHealth: true), "layer_04_event").State;
        string eventId = run.MapState!.MysteryEventAssignments["layer_04_event"];
        LayerFourNodeResolution assigned = _service.AssignMysteryAdjudicator(run, eventId);
        string adjudicator = assigned.State.MapState!.MysteryAdjudicatorAssignments!["layer_04_event"];
        LayerFourNodeResolution resolved = _service.ResolveMystery(assigned.State, eventId, "risk", RunEventAttribute.Intelligence, 0,
            "Nothing", 0, null, "Damage", 99, null);
        LayerFourNodeResolution replay = _service.ResolveMystery(resolved.State, eventId, "other", RunEventAttribute.Charisma, 100,
            "Gold", 50, null, "Nothing", 0, null);
        Assert.That(replay.EventOutcome, Is.EqualTo(resolved.EventOutcome));
        LayerFourNodeResolution confirmed = _service.ConfirmMystery(resolved.State,
            new Dictionary<ContentId, ConsumableDefinition>());
        Assert.Multiple(() =>
        {
            Assert.That(confirmed.State.Party.Single(value => value.CharacterId == adjudicator).IsDead, Is.True);
            Assert.That(confirmed.State.Phase, Is.EqualTo(PureRunPhase.ReadyForLayerFive));
            Assert.That(confirmed.State.MapState!.MysteryResolution!.Confirmed, Is.True);
            Assert.That(confirmed.State.PendingProgression, Is.Empty);
        });
    }

    [Test]
    public void MysteryNodeCanBeginAnEventBattleWithoutChangingItsTransactionKind()
    {
        PureRunState run = Select(CreateRun(), "layer_04_event").State;

        LayerFourNodeResolution begun = _service.BeginEventBattle(run, new ContentId("encounter.pure-run.n4"));

        Assert.Multiple(() =>
        {
            Assert.That(begun.Succeeded, Is.True);
            Assert.That(begun.State.Phase, Is.EqualTo(PureRunPhase.PendingBattle));
            Assert.That(begun.State.NodeTransaction!.Kind, Is.EqualTo(PureRunNodeKind.Mystery));
            Assert.That(begun.State.Checkpoint!.EncounterContentId, Is.EqualTo(new ContentId("encounter.pure-run.n4")));
        });
    }

    [Test]
    public void MysteryAdjudicator_IsChosenOnceFromLivingPartyAndReusedAcrossOptions()
    {
        PureRunState run = Select(CreateRun(), "layer_04_event").State;
        string eventId = run.MapState!.MysteryEventAssignments["layer_04_event"];

        LayerFourNodeResolution first = _service.AssignMysteryAdjudicator(run, eventId);
        LayerFourNodeResolution replay = _service.AssignMysteryAdjudicator(first.State, eventId);
        string selected = first.State.MapState!.MysteryAdjudicatorAssignments!["layer_04_event"];
        RunSaveDecodeResultV5 restored = RunSaveDocumentV5.Decode(RunSaveDocumentV5.Encode(
            new PureRunSaveSnapshot(first.State.Revision, first.State, null)));

        Assert.Multiple(() =>
        {
            Assert.That(first.Succeeded, Is.True);
            Assert.That(replay.State.MapState!.MysteryAdjudicatorAssignments!["layer_04_event"], Is.EqualTo(selected));
            Assert.That(first.State.Party.Single(value => value.CharacterId == selected).IsDead, Is.False);
            Assert.That(restored.Succeeded, Is.True);
            Assert.That(restored.Snapshot!.ActiveRun!.MapState!.MysteryAdjudicatorAssignments!["layer_04_event"],
                Is.EqualTo(selected));
        });
    }

    [Test]
    public void MysteryOptionWithoutAttribute_SucceedsAutomatically()
    {
        PureRunState run = Select(CreateRun(), "layer_04_event").State;
        string eventId = run.MapState!.MysteryEventAssignments["layer_04_event"];
        run = _service.AssignMysteryAdjudicator(run, eventId).State;

        LayerFourNodeResolution result = _service.ResolveMystery(run, eventId, "leave", RunEventAttribute.None, 0,
            "Nothing", 0, null, "Damage", 99, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.EventOutcome!.Succeeded, Is.True);
            Assert.That(result.EventOutcome.SuccessRate, Is.EqualTo(100));
            Assert.That(result.EventOutcome.Roll, Is.EqualTo(0));
        });
    }

    [Test]
    public void MysteryDamageThatKillsLastLivingCharacterEntersDefeated()
    {
        PureRunState initial = CreateRun(allAtOneHealth: true);
        RunCharacterState[] party = initial.Party.Select((value, index) => index == 0 ? value :
            new RunCharacterState(value.CharacterId, value.UnitContentId, value.Level, value.Attributes, 0,
                value.MaxHealth, value.CurrentMana, value.MaxMana, true, value.LearnedSkills)).ToArray();
        var wounded = new PureRunState(initial.RunId, initial.Seed, initial.Revision, initial.Phase,
            initial.EncounterIndex, initial.EncounterContentId, party, gold: initial.Gold);
        PureRunState run = Select(wounded, "layer_04_event").State;
        string eventId = run.MapState!.MysteryEventAssignments["layer_04_event"];
        run = _service.AssignMysteryAdjudicator(run, eventId).State;
        LayerFourNodeResolution resolved = _service.ResolveMystery(run, eventId, "risk", RunEventAttribute.Intelligence, 0,
            "Nothing", 0, null, "Damage", 99, null);
        Assert.That(_service.ConfirmMystery(resolved.State,
            new Dictionary<ContentId, ConsumableDefinition>()).State.Phase, Is.EqualTo(PureRunPhase.Defeated));
    }

    [Test]
    public void TreasureResolutionIsStableAndConfirmationAppliesRewardsOnce()
    {
        PureRunState run = Select(CreateRun(), "layer_04_treasure").State;
        var definition = new PureRunTreasureDefinition(new ContentId("treasure.pure-run.standard-v1"), 2, 5,
            [new WeightedContentDefinition(new ContentId("item.equipment.staff-01"), 1)],
            [new WeightedContentDefinition(new ContentId("item.consumable.life-potion"), 1)],
            [new WeightedContentDefinition(new ContentId("buff.event-damage-reduction"), 1)]);
        var equipment = new Dictionary<ContentId, EquipmentDefinition>
        {
            [new ContentId("item.equipment.staff-01")] = new(new ContentId("item.equipment.staff-01"), "source",
                "Staff", EquipmentSlot.Weapon, ItemRarity.Common, 1, new UnitAttributes(0, 0, 0, 1, 0, 0))
        };
        var consumables = new Dictionary<ContentId, ConsumableDefinition>
        {
            [new ContentId("item.consumable.life-potion")] = new(new ContentId("item.consumable.life-potion"),
                "source", "Potion", "", ItemRarity.Common, 1, 1, ConsumableEffectKind.RestoreHealth, 4, 1,
                ConsumableTargetMode.Self)
        };

        LayerFourNodeResolution first = _service.ResolveTreasure(run, definition);
        LayerFourNodeResolution replay = _service.ResolveTreasure(first.State, definition);
        Assert.That(replay.TreasureOutcome, Is.EqualTo(first.TreasureOutcome));

        LayerFourNodeResolution committed = _service.ConfirmTreasure(first.State, definition, equipment, consumables);
        Assert.Multiple(() =>
        {
            Assert.That(committed.Succeeded, Is.True);
            Assert.That(committed.State.Gold, Is.InRange(2, 5));
            Assert.That(committed.State.BackpackEquipment, Has.Count.EqualTo(1));
            Assert.That(committed.State.BackpackConsumables, Has.Count.EqualTo(1));
            Assert.That(committed.State.MapState!.PendingStatuses, Has.Count.EqualTo(1));
            Assert.That(committed.State.AppliedTransactionKeys, Does.Contain("node:layer_04_treasure:resolve"));
            Assert.That(committed.State.PendingProgression, Is.Empty);
        });
        LayerFourNodeResolution duplicate = _service.ConfirmTreasure(committed.State, definition, equipment, consumables);
        Assert.That(duplicate.WasDuplicate, Is.True);
    }

    [Test]
    public void TreasureConfirmationCanOweTheStandardLevelUpWithoutASeparateSettlement()
    {
        PureRunState run = Select(CreateRun(), "layer_04_treasure").State;
        var definition = new PureRunTreasureDefinition(new ContentId("treasure.pure-run.standard-v1"), 2, 5,
            [new WeightedContentDefinition(new ContentId("item.equipment.staff-01"), 1)],
            [new WeightedContentDefinition(new ContentId("item.consumable.life-potion"), 1)],
            [new WeightedContentDefinition(new ContentId("buff.event-damage-reduction"), 1)]);
        var equipment = new Dictionary<ContentId, EquipmentDefinition>
        {
            [new ContentId("item.equipment.staff-01")] = new(new ContentId("item.equipment.staff-01"), "source",
                "Staff", EquipmentSlot.Weapon, ItemRarity.Common, 1, new UnitAttributes(0, 0, 0, 1, 0, 0))
        };
        var consumables = new Dictionary<ContentId, ConsumableDefinition>
        {
            [new ContentId("item.consumable.life-potion")] = new(new ContentId("item.consumable.life-potion"),
                "source", "Potion", "", ItemRarity.Common, 1, 1, ConsumableEffectKind.RestoreHealth, 4, 1,
                ConsumableTargetMode.Self)
        };
        LayerFourNodeResolution resolved = _service.ResolveTreasure(run, definition);

        LayerFourNodeResolution committed = _service.ConfirmTreasure(resolved.State, definition, equipment, consumables, grantsGrowth: true);

        PendingProgression growth = committed.State.PendingProgression.Single();
        Assert.Multiple(() =>
        {
            Assert.That(growth.TransactionKey, Is.EqualTo("progression:node:layer_04_treasure:resolve"));
            Assert.That(growth.CharacterId, Is.EqualTo("mage"));
            Assert.That(committed.State.BackpackEquipment, Has.Count.EqualTo(1));
        });
    }

    private LayerFourNodeResolution Select(PureRunState run, string nodeId) => _service.SelectNode(run, Map(), nodeId);

    private static PureRunMapDefinition Map() => new(new ContentId("run-map.pure-run.layer4-v1"), 2,
    [
        new("layer_04_battle", 4, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n4")),
        new("layer_04_rest", 4, PureRunNodeKind.Rest, new ContentId("rest.pure-run.standard-v1")),
        new("layer_04_store", 4, PureRunNodeKind.Store, new ContentId("store.pure-run.standard-v1")),
        new("layer_04_event", 4, PureRunNodeKind.Mystery, new ContentId("event.pure-run.cursed-chest")),
        new("layer_04_treasure", 4, PureRunNodeKind.Treasure, new ContentId("treasure.pure-run.standard-v1"))
    ]);

    private static PureRunState CreateRun(int gold = 0, bool allAtOneHealth = false)
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        var party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(id,
            new ContentId($"unit.{id}"), 1, attributes, allAtOneHealth ? 1 : 6, 24, 3, 15, false,
            [new ContentId($"skill.{id}")])).ToArray();
        return new PureRunState("run", 42, 4, PureRunPhase.AwaitingLayerFourChoice, 2,
            new ContentId("encounter.pure-run.n3"), party, gold: gold);
    }

    [Test]
    public void BeginNodeBattle_ExcludesDeadCharactersFromCheckpointAndEncounterRequest()
    {
        PureRunState run = CreateRun();
        RunCharacterState[] party = run.Party.Select((value, index) => index == 0
            ? new RunCharacterState(value.CharacterId, value.UnitContentId, value.Level, value.Attributes, 0,
                value.MaxHealth, value.CurrentMana, value.MaxMana, true, value.LearnedSkills)
            : value).ToArray();
        run = new PureRunState(run.RunId, run.Seed, run.Revision, run.Phase, run.EncounterIndex,
            run.EncounterContentId, party, gold: run.Gold);
        PureRunState selected = Select(run, "layer_04_battle").State;

        LayerFourNodeResolution begun = _service.BeginN4(selected,
            new ContentId("encounter.pure-run.n4"));

        Assert.Multiple(() =>
        {
            Assert.That(begun.Succeeded, Is.True);
            Assert.That(begun.EncounterRequest, Is.Not.Null);
            Assert.That(begun.EncounterRequest!.Party.Select(value => value.CharacterId), Is.EqualTo(new[] { "necro", "amazon" }));
            Assert.That(begun.State.Checkpoint!.Party.Select(value => value.CharacterId), Is.EqualTo(new[] { "necro", "amazon" }));
            Assert.That(begun.State.Party.Count, Is.EqualTo(3), "Full roster with tombstones stays on the run state.");
        });
    }
}
