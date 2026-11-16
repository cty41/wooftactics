using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class NodeCompletionTests
{
    [Test]
    public void BattleVictory_OwesLowestCharacterStillStanding()
    {
        RunCharacterState[] party = [Member("mage", 1), Member("necro", 2), Member("amazon", 2)];
        PureRunBattleResult result = Victory(party, down: "mage");
        RunCharacterState[] recovered = party.Select(member => member.IsDead ? Living(member) : member).ToArray();

        PendingProgression? offer = new BattleVictoryCompletion(result, "node:layer_04_event:resolve").OfferGrowth(recovered);

        Assert.Multiple(() =>
        {
            Assert.That(offer, Is.Not.Null);
            Assert.That(offer!.CharacterId, Is.EqualTo("necro"));
            Assert.That(offer.TransactionKey, Is.EqualTo("progression:node:layer_04_event:resolve"));
            Assert.That(offer.EncounterId, Is.EqualTo("encounter.pure-run.n1"));
        });
    }

    [Test]
    public void BattleVictory_OwesNothingWhenNobodyQualifies()
    {
        RunCharacterState[] capped = [Member("mage", 12), Member("necro", 12), Member("amazon", 12)];
        RunCharacterState[] wiped = [Member("mage", 1, dead: true), Member("necro", 1, dead: true), Member("amazon", 1, dead: true)];

        Assert.Multiple(() =>
        {
            Assert.That(new BattleVictoryCompletion(Victory(capped), "encounter.pure-run.n1").OfferGrowth(capped), Is.Null);
            Assert.That(new BattleVictoryCompletion(Victory(wiped, "mage", "necro", "amazon"), "encounter.pure-run.n1")
                .OfferGrowth(wiped), Is.Null);
            Assert.That(new BossVictoryCompletion().OfferGrowth(capped.Select(Living).ToArray()), Is.Null);
        });
    }

    [Test]
    public void NonBattleCompletions_DefaultToNoGrowthUnlessTheChestSaysSo()
    {
        RunCharacterState[] party = [Member("mage", 2, dead: true), Member("necro", 1), Member("amazon", 3)];

        Assert.Multiple(() =>
        {
            Assert.That(new RestCompletion().OfferGrowth(party), Is.Null);
            Assert.That(new StoreCompletion().OfferGrowth(party), Is.Null);
            Assert.That(new MysteryChoiceCompletion().OfferGrowth(party), Is.Null);
            Assert.That(new TreasureCompletion("node:layer_04_treasure:resolve", grantsGrowth: false).OfferGrowth(party), Is.Null);
            PendingProgression? chest = new TreasureCompletion("node:layer_04_treasure:resolve", grantsGrowth: true).OfferGrowth(party);
            Assert.That(chest!.CharacterId, Is.EqualTo("necro"));
            Assert.That(chest.TransactionKey, Is.EqualTo("progression:node:layer_04_treasure:resolve"));
        });
    }

    [Test]
    public void Apply_IgnoresARepeatedOfferForTheSameStep()
    {
        var offer = new PendingProgression("progression:node:layer_04_event:resolve", "encounter.pure-run.n1", "necro");
        PendingProgression[] once = NodeCompletion.Apply(Array.Empty<PendingProgression>(), offer);
        PendingProgression[] twice = NodeCompletion.Apply(once, offer);

        Assert.That(twice, Is.EqualTo(once));
    }

    private static PureRunBattleResult Victory(IReadOnlyList<RunCharacterState> party, params string[] down) => new(
        "run", 2, new ContentId("encounter.pure-run.n1"), true, 3, 3,
        party.Select(member => new BattlePartyResult(member.CharacterId, down.Contains(member.CharacterId) ? 0 : 10, 1,
            down.Contains(member.CharacterId), Array.Empty<BattleConsumableState>())).ToArray());

    private static RunCharacterState Member(string id, int level, bool dead = false) => new(id,
        new ContentId($"unit.{id}"), level, new UnitAttributes(5, 5, 5, 5, 5, 5), dead ? 0 : 10, 20, dead ? 0 : 5, 15,
        dead, [new ContentId($"skill.{id}")]);

    private static RunCharacterState Living(RunCharacterState member) => new(member.CharacterId, member.UnitContentId,
        member.Level, member.Attributes, 10, member.MaxHealth, 5, member.MaxMana, false, member.LearnedSkills);
}