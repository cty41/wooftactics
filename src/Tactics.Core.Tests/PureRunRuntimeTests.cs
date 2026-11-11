using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class PureRunRuntimeTests
{
    [Test]
    public void Settlement_VictoryRecoversLivingAndRecordsProgressionWithoutLeveling()
    {
        PureRunDefinition definition = TestDefinition();
        PureRunState pending = PendingState(definition, seed: 19);
        PureRunBattleResult result = Result(pending, victory: true, rounds: 3,
            health: new[] { 10, 0, 8 }, mana: new[] { 2, 0, 1 });

        PureRunSettlementResult settled = new PureRunSettlementService().Apply(
            definition, pending, result, Array.Empty<ContentId>());

        Assert.Multiple(() =>
        {
            Assert.That(settled.Succeeded, Is.True);
            Assert.That(settled.ActiveRun!.EncounterContentId.Value, Is.EqualTo("encounter.pure-run.n2"));
            Assert.That(settled.ActiveRun.Gold, Is.EqualTo(8));
            Assert.That(settled.ActiveRun.Party[0].CurrentHealth, Is.EqualTo(20));
            Assert.That(settled.ActiveRun.Party[1].IsDead, Is.False);
            Assert.That(settled.ActiveRun.Party[1].CurrentHealth, Is.EqualTo(10));
            Assert.That(settled.ActiveRun.Party[2].CurrentMana, Is.EqualTo(6));
            Assert.That(settled.ActiveRun.PendingProgression.Single().CharacterId, Is.EqualTo("mage"));
            Assert.That(settled.ActiveRun.Party.Select(member => member.Level), Is.All.EqualTo(1));
        });
    }

    /// <summary>
    /// "Living" for progression means standing when the battle ended: a character who was down is recovered by the
    /// victory but is not levelled, even when it is the lowest level or first in party order.
    /// </summary>
    [TestCase(new[] { 1, 2, 2 }, new[] { 0, 9, 8 }, "necro", TestName = "Progression_LowestLevelDownAtEndIsSkipped")]
    [TestCase(new[] { 1, 1, 1 }, new[] { 0, 9, 8 }, "necro", TestName = "Progression_SameLevelDownFirstInOrderIsSkipped")]
    [TestCase(new[] { 2, 1, 1 }, new[] { 9, 0, 8 }, "amazon", TestName = "Progression_DownMiddleMemberIsSkipped")]
    [TestCase(new[] { 1, 1, 1 }, new[] { 9, 9, 8 }, "mage", TestName = "Progression_NobodyDownKeepsLowestLevelThenPartyOrder")]
    [TestCase(new[] { 2, 2, 1 }, new[] { 9, 9, 8 }, "amazon", TestName = "Progression_NobodyDownPicksLowestLevel")]
    public void Settlement_ProgressionTargetsOnlyCharactersStandingAtBattleEnd(int[] levels, int[] health, string expected)
    {
        PureRunDefinition definition = TestDefinition();
        PureRunState pending = PendingState(definition, seed: 19, levels: levels);
        PureRunBattleResult result = Result(pending, victory: true, rounds: 3, health, mana: new[] { 1, 1, 1 });

        PureRunSettlementResult first = new PureRunSettlementService().Apply(
            definition, pending, result, Array.Empty<ContentId>());
        PureRunSettlementResult late = new PureRunSettlementService().ApplyFixedLateBattle(
            pending, result, Array.Empty<ContentId>(), PureRunPhase.Ready, boss: false);

        Assert.Multiple(() =>
        {
            foreach (PureRunSettlementResult settled in new[] { first, late })
            {
                Assert.That(settled.Succeeded, Is.True);
                Assert.That(settled.ActiveRun!.PendingProgression.Single().CharacterId, Is.EqualTo(expected));
                // The down member still gets the normal post-victory recovery and keeps its level.
                Assert.That(settled.ActiveRun.Party.Any(member => member.IsDead), Is.False);
                Assert.That(settled.ActiveRun.Party.Select(member => member.Level), Is.EqualTo(levels));
                for (int index = 0; index < health.Length; index++)
                    Assert.That(settled.ActiveRun.Party[index].CurrentHealth, Is.EqualTo(Math.Min(20, health[index] + 10)));
            }
        });
    }

    [Test]
    public void Settlement_N3ProducesSliceCompletedInsteadOfBossVictory()
    {
        PureRunDefinition definition = TestDefinition();
        PureRunState pending = PendingState(definition, 7, encounterIndex: 2);
        PureRunSettlementResult result = new PureRunSettlementService().Apply(
            definition, pending, Result(pending, true, 6, new[] { 20, 20, 20 }, new[] { 5, 5, 5 }),
            Array.Empty<ContentId>());

        Assert.Multiple(() =>
        {
            Assert.That(result.ActiveRun, Is.Null);
            Assert.That(result.TerminalSummary!.Outcome, Is.EqualTo(PureRunOutcome.SliceCompleted));
            Assert.That(result.TerminalSummary.BattlesCompleted, Is.EqualTo(1));
        });
    }

    [Test]
    public void Settlement_DefeatDoesNotRecoverOrReviveParty()
    {
        PureRunDefinition definition=TestDefinition();PureRunState pending=PendingState(definition,13);
        PureRunSettlementResult result=new PureRunSettlementService().Apply(definition,pending,
            Result(pending,false,4,new[]{0,7,8},new[]{0,1,2}),Array.Empty<ContentId>());
        Assert.Multiple(() =>
        {
            Assert.That(result.ActiveRun,Is.Null);
            Assert.That(result.TerminalSummary!.Outcome,Is.EqualTo(PureRunOutcome.Defeated));
            Assert.That(result.TerminalSummary.DeadCharacters,Does.Contain("mage"));
        });
    }

    [Test]
    public void Settlement_VictoryDoesNotReviveRunPermanentlyDeadPartyMember()
    {
        PureRunDefinition definition = TestDefinition();
        PureRunState pending = PendingState(definition, 23);
        PureRunBattleResult battle = Result(pending, true, 4,
            new[] { 0, 7, 8 }, new[] { 0, 1, 2 });
        battle = battle with
        {
            Party = battle.Party.Select((member, index) =>
                index == 0 ? member with { RunPermanentlyDead = true } : member).ToArray()
        };

        PureRunSettlementResult result = new PureRunSettlementService().Apply(
            definition, pending, battle, Array.Empty<ContentId>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ActiveRun!.Party[0].CurrentHealth, Is.Zero);
            Assert.That(result.ActiveRun.Party[0].IsDead, Is.True);
            Assert.That(result.ActiveRun.Party[1].CurrentHealth, Is.GreaterThan(7),
                "Ordinary Down party members still use normal post-victory recovery.");
        });
    }

    [Test]
    public void Settlement_RejectsStaleRevisionWithoutMutation()
    {
        PureRunDefinition definition = TestDefinition();
        PureRunState pending = PendingState(definition, 7);
        PureRunBattleResult stale = Result(pending, true, 2, new[] { 20, 20, 20 }, new[] { 5, 5, 5 })
            with { CheckpointRevision = pending.Checkpoint!.Revision - 1 };

        PureRunSettlementResult result = new PureRunSettlementService().Apply(
            definition, pending, stale, Array.Empty<ContentId>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RejectionCode, Is.EqualTo("run.result_revision_mismatch"));
            Assert.That(result.ActiveRun, Is.SameAs(pending));
        });
    }

    [Test]
    public void DeriveSeed_MatchesFrozenUnityFnvContract()
    {
        Assert.That(PureRunSettlementService.DeriveSeed(1203, "battle-drop:encounter.pure-run.n1"),
            Is.EqualTo(unchecked((int)0x759961a2)));
    }

    internal static PureRunDefinition TestDefinition() => new(
        new ContentId("run.pure-run.three-encounter-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }.Select(value => new ContentId(value)),
        new[]
        {
            new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5,5,5,6,5,5)),
            new PureRunPartyTemplate("necro", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5,5,5,5,6,5)),
            new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5,6,5,5,5,5))
        });

    internal static PureRunState PendingState(PureRunDefinition definition, int seed, int encounterIndex = 0, int[]? levels = null)
    {
        RunCharacterState[] party = definition.Party.Select((template, index) => new RunCharacterState(
            template.CharacterId, template.UnitContentId, levels?[index] ?? 1, template.Attributes,
            20, 20, 5, 18, false, new[] { template.StartingSkillContentId })).ToArray();
        ContentId encounter = definition.Encounters[encounterIndex];
        var checkpoint = new RunEncounterCheckpoint(encounter, encounterIndex, 2, party,
            Array.Empty<Tactics.Core.Items.BattleConsumableState>(), Array.Empty<RunEquipmentState>());
        return new PureRunState("run-test", seed, 2, PureRunPhase.PendingBattle, encounterIndex, encounter, party,
            checkpoint: checkpoint);
    }

    [Test]
    public void Settlement_PreservesDeceasedRosterTombstoneAlongsideActivePartyOutcomes()
    {
        // A permanently dead "mage" was already removed from the battle party but must stay
        // on the roster as a tombstone while the surviving members settle normally.
        PureRunDefinition definition = TestDefinition();
        var deadMage = new RunCharacterState("mage", new ContentId("unit.pure-run.mage"), 1,
            new UnitAttributes(5, 5, 5, 6, 5, 5), 0, 20, 0, 18, true,
            new[] { new ContentId("skill.mage.fireball.lv1") });
        RunCharacterState[] active = definition.Party.Skip(1).Select(template => new RunCharacterState(
            template.CharacterId, template.UnitContentId, 1, template.Attributes,
            20, 20, 5, 18, false, new[] { template.StartingSkillContentId })).ToArray();
        ContentId encounter = definition.Encounters[0];
        var checkpoint = new RunEncounterCheckpoint(encounter, 0, 2, active,
            Array.Empty<Tactics.Core.Items.BattleConsumableState>(), Array.Empty<RunEquipmentState>());
        var pending = new PureRunState("run-test", 31, 2, PureRunPhase.PendingBattle, 0, encounter,
            new[] { deadMage, active[0], active[1] }, checkpoint: checkpoint);
        PureRunBattleResult battle = new("run-test", 2, encounter, true, 3, 3,
            new[]
            {
                new BattlePartyResult("necro", 12, 2, false, Array.Empty<Tactics.Core.Items.BattleConsumableState>()),
                new BattlePartyResult("amazon", 9, 1, false, Array.Empty<Tactics.Core.Items.BattleConsumableState>())
            });

        PureRunSettlementResult result = new PureRunSettlementService().Apply(
            definition, pending, battle, Array.Empty<ContentId>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ActiveRun!.Party.Count, Is.EqualTo(3));
            Assert.That(result.ActiveRun.Party.Single(value => value.CharacterId == "mage").IsDead, Is.True);
            Assert.That(result.ActiveRun.Party.Single(value => value.CharacterId == "necro").CurrentHealth, Is.EqualTo(20));
            Assert.That(result.ActiveRun.Party.Single(value => value.CharacterId == "amazon").CurrentHealth, Is.EqualTo(19));
        });
    }

    internal static PureRunBattleResult Result(
        PureRunState state, bool victory, int rounds, int[] health, int[] mana) => new(
            state.RunId, state.Checkpoint!.Revision, state.EncounterContentId, victory, rounds, 3,
            state.Party.Select((member, index) => new BattlePartyResult(
                member.CharacterId, health[index], mana[index], health[index] == 0,
                Array.Empty<Tactics.Core.Items.BattleConsumableState>())).ToArray());
}
