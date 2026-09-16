using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunSessionServiceTests
{
    private static IEnumerable<TestCaseData> DemonboundFixedSeedCases()
    {
        string[][] parties =
        [
            ["mage", "amazon", "demonbound"],
            ["mage", "necromancer", "demonbound"],
            ["necromancer", "amazon", "demonbound"]
        ];
        foreach (string[] party in parties)
        {
            for (int seed = 0; seed < 10; seed++)
                yield return new TestCaseData(party, seed).SetName(
                    $"DemonboundFixedSeed_{string.Join('_', party.Take(2))}_{seed:D2}");
        }
    }

    [TestCaseSource(nameof(DemonboundFixedSeedCases))]
    public void DemonboundFixedSeedSetupSamples_AreStableAcrossThreePartyCombinations(string[] party, int seed)
    {
        var store = new MemoryRunStore();
        PureRunDefinition definition = DefinitionWithDemonbound();
        var service = new PureRunSessionService(definition, store);

        Assert.That(service.BeginNewRunSetup(seed).Succeeded, Is.True);
        Assert.That(service.ChooseParty(party).Succeeded, Is.True);
        Dictionary<string, PureRunPartyTemplate> templates = definition.Party.ToDictionary(value => value.CharacterId, StringComparer.Ordinal);
        foreach (PureRunPartyTemplate template in party.Select(value => templates[value]).Where(value => value.CharacterId != "demonbound"))
        {
            Assert.That(service.ChooseStartingSkill(template.CharacterId, template.StartingSkillContentId).Succeeded,
                Is.True);
        }

        RunCharacterState demonbound = store.Snapshot!.ActiveRun!.Party.Single(value =>
            value.CharacterId == "demonbound");
        ContentId expected = definition.Party.Single(value => value.CharacterId == "demonbound")
            .EffectiveStartingSkillChoices
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ElementAt((int)((uint)PureRunSettlementService.DeriveSeed(seed, "starting-skill:demonbound") % 3U));
        Assert.Multiple(() =>
        {
            Assert.That(demonbound.StartingSkillContentId, Is.EqualTo(expected));
            Assert.That(demonbound.LearnedSkills, Does.Contain(new ContentId("skill.demonbound.meditation")));
            Assert.That(store.Snapshot.ActiveRun.Party.Select(value => value.CharacterId),
                Is.EqualTo(definition.Party.Where(value => party.Contains(value.CharacterId, StringComparer.Ordinal))
                    .Select(value => value.CharacterId)));
        });
    }

    [Test]
    public void FourCandidateSetup_ChoosesThreeAndResolvesSeededDemonboundSkill()
    {
        var store = new MemoryRunStore();
        PureRunDefinition definition = DefinitionWithDemonbound();
        var service = new PureRunSessionService(definition, store);

        RunSessionResult begun = service.BeginNewRunSetup(37);
        Assert.Multiple(() =>
        {
            Assert.That(begun.Succeeded, Is.True);
            Assert.That(begun.Snapshot!.PendingRunSetup!.SelectedCharacterIds, Is.Empty);
            Assert.That(begun.Snapshot.PendingRunSetup.CurrentCharacterId, Is.Null);
        });

        RunSessionResult selected = service.ChooseParty(["amazon", "demonbound", "mage"]);
        Assert.Multiple(() =>
        {
            Assert.That(selected.Succeeded, Is.True);
            Assert.That(selected.Snapshot!.PendingRunSetup!.SelectedCharacterIds,
                Is.EqualTo(new[] { "amazon", "demonbound", "mage" }));
            Assert.That(selected.Snapshot.PendingRunSetup.CurrentCharacterId, Is.EqualTo("amazon"));
        });

        Assert.That(service.ChooseStartingSkill("amazon", new ContentId("skill.poison-spear.lv1")).Succeeded, Is.True);
        Assert.That(service.ChooseStartingSkill("mage", new ContentId("skill.mage.fireball.lv1")).Succeeded, Is.True);
        PureRunSaveSnapshot completed = store.Snapshot!;
        RunCharacterState demonbound = completed.ActiveRun!.Party.Single(value => value.CharacterId == "demonbound");
        Assert.Multiple(() =>
        {
            Assert.That(completed.PendingRunSetup, Is.Null);
            Assert.That(completed.ActiveRun.Party.Select(value => value.CharacterId),
                Is.EqualTo(new[] { "amazon", "demonbound", "mage" }));
            Assert.That(demonbound.LearnedSkills, Does.Contain(new ContentId("skill.demonbound.meditation")));
            Assert.That(demonbound.StartingSkillContentId,
                Is.AnyOf(new ContentId("skill.demonbound.bane.lv1"),
                    new ContentId("skill.demonbound.infernal-blast.lv1"),
                    new ContentId("skill.demonbound.mindfulness.lv1")));
        });
    }

    [Test]
    public void FiveCandidateSetup_ChoosesExactlyThreeAndSupportsSeededPoet()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(DefinitionWithPoet(), store);

        Assert.That(service.BeginNewRunSetup(41).Succeeded, Is.True);
        Assert.That(service.ChooseParty(["amazon", "poet", "mage"]).Succeeded, Is.True);
        Assert.That(service.ChooseStartingSkill("amazon", new ContentId("skill.poison-spear.lv1")).Succeeded,
            Is.True);
        Assert.That(service.ChooseStartingSkill("mage", new ContentId("skill.mage.fireball.lv1")).Succeeded,
            Is.True);

        PureRunState run = store.Snapshot!.ActiveRun!;
        RunCharacterState poet = run.Party.Single(character => character.CharacterId == "poet");
        Assert.Multiple(() =>
        {
            Assert.That(run.Party.Select(character => character.CharacterId),
                Is.EqualTo(new[] { "amazon", "poet", "mage" }));
            Assert.That(poet.StartingSkillContentId, Is.AnyOf(
                new ContentId("skill.poet.xiake-xing.lv1"),
                new ContentId("skill.poet.jiang-jin-jiu.lv1"),
                new ContentId("skill.poet.moon-drink.lv1")));
        });
    }

    [Test]
    public void FourCandidateSetup_RejectsInvalidPartySelection()
    {
        var service = new PureRunSessionService(DefinitionWithDemonbound(), new MemoryRunStore());
        service.BeginNewRunSetup(1);
        Assert.Multiple(() =>
        {
            Assert.That(service.ChooseParty(["mage", "mage", "amazon"]).ErrorCode,
                Is.EqualTo("run_setup.party_size_invalid"));
            Assert.That(service.ChooseParty(["mage", "amazon", "unknown"]).ErrorCode,
                Is.EqualTo("run_setup.party_character_invalid"));
        });
    }

    [Test]
    public void FourCandidateSetup_PersistsImmutableLeaderFirstSelectionAndRejectsOverflow()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(DefinitionWithDemonbound(), store);
        Assert.That(service.BeginNewRunSetup(41).Succeeded, Is.True);

        Assert.That(service.SelectPartyMember("demonbound").Succeeded, Is.True);
        Assert.That(service.SelectPartyMember("mage").Succeeded, Is.True);
        Assert.That(service.SelectPartyMember("amazon").Succeeded, Is.True);
        PureRunSaveSnapshot selected = store.Snapshot!;

        Assert.Multiple(() =>
        {
            Assert.That(selected.PendingRunSetup!.SelectedCharacterIds,
                Is.EqualTo(new[] { "demonbound", "mage", "amazon" }));
            Assert.That(service.SelectPartyMember("demonbound").ErrorCode,
                Is.EqualTo("run_setup.party_character_immutable"));
            Assert.That(service.SelectPartyMember("necromancer").ErrorCode,
                Is.EqualTo("run_setup.party_full"));
            Assert.That(service.ChooseParty(["mage", "demonbound", "amazon"]).ErrorCode,
                Is.EqualTo("run_setup.party_order_immutable"));
        });

        IReadOnlyList<string> persistedOrder = selected.PendingRunSetup!.SelectedCharacterIds;
        RunSessionResult finalized = service.ChooseParty(persistedOrder);
        Assert.That(finalized.Succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(finalized.Snapshot!.PendingRunSetup!.SelectedCharacterIds[0], Is.EqualTo("demonbound"));
            Assert.That(finalized.Snapshot.PendingRunSetup.CurrentCharacterId, Is.EqualTo("mage"));
        });
    }

    [Test]
    public void AbandonPendingSetup_CreatesAbandonedTerminalSummary()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(DefinitionWithDemonbound(), store);
        service.BeginNewRunSetup(42);
        service.SelectPartyMember("mage");

        RunSessionResult result = service.AbandonRun();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot!.PendingRunSetup, Is.Null);
            Assert.That(result.Snapshot.ActiveRun, Is.Null);
            Assert.That(result.Snapshot.TerminalSummary!.Outcome, Is.EqualTo(PureRunOutcome.Abandoned));
        });
    }

    [Test]
    public void NewRunSetup_RequiresThreeCanonicalChoicesBeforeReplacingActiveRun()
    {
        var store = new MemoryRunStore();
        PureRunDefinition definition = DefinitionWithChoices();
        var service = new PureRunSessionService(definition, store);
        Assert.That(service.StartNewRun(3).Succeeded, Is.True);
        string oldRunId = store.Snapshot!.ActiveRun!.RunId;

        RunSessionResult begun = service.BeginNewRunSetup(9);
        Assert.Multiple(() =>
        {
            Assert.That(begun.Succeeded, Is.True);
            Assert.That(begun.Snapshot!.ActiveRun!.RunId, Is.EqualTo(oldRunId));
            Assert.That(begun.Snapshot.PendingRunSetup!.CurrentCharacterId, Is.EqualTo("mage"));
        });

        Assert.That(service.ChooseStartingSkill("mage", new ContentId("skill.mage.fireball.lv1")).Succeeded, Is.True);
        Assert.That(service.ChooseStartingSkill("necromancer", new ContentId("skill.necromancer.bone-spear.lv1")).Succeeded, Is.True);
        RunSessionResult completed = service.ChooseStartingSkill("amazon", new ContentId("skill.poison-spear.lv1"));

        Assert.Multiple(() =>
        {
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(completed.Snapshot!.PendingRunSetup, Is.Null);
            Assert.That(completed.Snapshot.ActiveRun!.RunId, Is.Not.EqualTo(oldRunId));
            Assert.That(completed.Snapshot.ActiveRun.Party.SelectMany(value => value.LearnedSkills),
                Is.EquivalentTo(new[]
                {
                    new ContentId("skill.mage.fireball.lv1"),
                    new ContentId("skill.necromancer.bone-spear.lv1"),
                    new ContentId("skill.poison-spear.lv1")
                }));
            RunCharacterState amazon = completed.Snapshot.ActiveRun.Party.Single(value =>
                value.CharacterId == "amazon");
            Assert.That(amazon.LearnedSkillStates.Single().BranchId,
                Is.EqualTo("amazon.poison-spear"));
            Assert.That(amazon.StartingSkillContentId,
                Is.EqualTo(new ContentId("skill.poison-spear.lv1")));
        });
    }

    [Test]
    public void NewRunSetup_RejectsCrossClassChoiceAndCancelPreservesOldRun()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(DefinitionWithChoices(), store);
        service.StartNewRun(4);
        string oldRunId = store.Snapshot!.ActiveRun!.RunId;
        service.BeginNewRunSetup(10);

        RunSessionResult rejected = service.ChooseStartingSkill("mage", new ContentId("skill.amazon.thrust.lv1"));
        RunSessionResult canceled = service.CancelNewRunSetup();

        Assert.Multiple(() =>
        {
            Assert.That(rejected.ErrorCode, Is.EqualTo("run_setup.skill_not_offered"));
            Assert.That(canceled.Succeeded, Is.True);
            Assert.That(canceled.Snapshot!.PendingRunSetup, Is.Null);
            Assert.That(canceled.Snapshot.ActiveRun!.RunId, Is.EqualTo(oldRunId));
        });
    }

    [Test]
    public void PendingNewRunSetupRejectsStaleRunMutationAndPreservesSetup()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(DefinitionWithChoices(), store);
        service.StartNewRun(5);
        service.BeginNewRunSetup(11);

        RunSessionResult encounter = service.BeginEncounter();
        RunSessionResult legacyStart = service.StartNewRun(99);
        RunSessionResult restartSetup = service.BeginNewRunSetup(99);
        RunSessionResult mutation = service.ApplyMutation(state =>
            new RunMutationResult(true, null, new PureRunState(state.RunId, state.Seed, state.Revision + 1,
                state.Phase, state.EncounterIndex, state.EncounterContentId, state.Party)));

        Assert.Multiple(() =>
        {
            Assert.That(encounter.ErrorCode, Is.EqualTo("run_setup.pending"));
            Assert.That(legacyStart.ErrorCode, Is.EqualTo("run_setup.pending"));
            Assert.That(restartSetup.ErrorCode, Is.EqualTo("run_setup.pending"));
            Assert.That(mutation.ErrorCode, Is.EqualTo("run_setup.pending"));
            Assert.That(store.Snapshot!.PendingRunSetup, Is.Not.Null);
            Assert.That(store.Snapshot.PendingRunSetup!.CurrentCharacterId, Is.EqualTo("mage"));
        });
    }
    [Test]
    public void BeginEncounter_PersistsCheckpointBeforeReturningRequest()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        Assert.That(service.StartNewRun(42).Succeeded, Is.True);

        RunSessionResult result = service.BeginEncounter();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(store.Snapshot!.ActiveRun!.Phase, Is.EqualTo(PureRunPhase.PendingBattle));
            Assert.That(result.EncounterRequest!.CheckpointRevision,
                Is.EqualTo(store.Snapshot.ActiveRun.Checkpoint!.Revision));
        });
    }

    [Test]
    public void BeginEncounter_ExcludesPermanentlyDeadMemberFromCheckpoint()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        Assert.That(service.StartNewRun(42).Succeeded, Is.True);

        RunSessionResult mutated = service.ApplyMutation(state => new RunMutationResult(true, null, new PureRunState(
            state.RunId, state.Seed, state.Revision + 1, state.Phase, state.EncounterIndex, state.EncounterContentId,
            state.Party.Select((member, index) => index == 1
                ? new RunCharacterState(member.CharacterId, member.UnitContentId, member.Level, member.Attributes,
                    0, member.MaxHealth, 0, member.MaxMana, true, member.LearnedSkills,
                    member.Equipment, member.CarriedConsumables, member.LearnedSkillStates,
                    member.StartingSkillContentId)
                : member).ToArray())));
        Assert.That(mutated.Succeeded, Is.True);
        RunCharacterState[] roster = store.Snapshot!.ActiveRun!.Party.ToArray();
        Assert.That(roster.Count(value => value.IsDead), Is.EqualTo(1));

        RunSessionResult result = service.BeginEncounter();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.EncounterRequest!.Party.Select(value => value.CharacterId),
                Does.Not.Contain(roster.Single(value => value.IsDead).CharacterId));
            Assert.That(result.EncounterRequest!.Party, Has.Count.EqualTo(roster.Length - 1));
            // Full roster (with tombstone) is retained on the pending state for settlement/Summary.
            Assert.That(store.Snapshot!.ActiveRun!.Party, Has.Count.EqualTo(roster.Length));
        });
    }

    [Test]
    public void ResumePendingBattle_ReissuesSameCheckpoint()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        service.StartNewRun(9);
        EncounterRequest first = service.BeginEncounter().EncounterRequest!;

        EncounterRequest resumed = service.ResumeRun().EncounterRequest!;

        Assert.That(resumed, Is.EqualTo(first));
    }

    [Test]
    public void ThreeVictories_ProduceTerminalSummaryAndNoActiveRun()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        service.StartNewRun(11);
        for (int index = 0; index < 3; index++)
        {
            EncounterRequest request = service.BeginEncounter().EncounterRequest!;
            PureRunState run = store.Snapshot!.ActiveRun!;
            RunSessionResult settled = service.ApplyBattleResult(Victory(run));
            Assert.That(settled.Succeeded, Is.True);
        }

        Assert.Multiple(() =>
        {
            Assert.That(store.Snapshot!.ActiveRun, Is.Null);
            Assert.That(store.Snapshot.TerminalSummary!.Outcome, Is.EqualTo(PureRunOutcome.SliceCompleted));
            Assert.That(store.Snapshot.TerminalSummary.BattlesCompleted, Is.EqualTo(3));
        });
    }

    [Test]
    public void TwoVictoriesAndCompletedProgression_UnlockN3AndBeginItsBattle()
    {
        var store = new MemoryRunStore();
        PureRunDefinition definition = Definition();
        var service = new PureRunSessionService(definition, store);
        var progression = new RunInventoryProgressionService();
        service.StartNewRun(31);

        for (int index = 0; index < 2; index++)
        {
            Assert.That(service.BeginEncounter().Succeeded, Is.True);
            RunSessionResult settled = service.ApplyBattleResult(Victory(store.Snapshot!.ActiveRun!));
            Assert.That(settled.Succeeded, Is.True);
            PendingProgression pending = settled.Snapshot!.ActiveRun!.PendingProgression.Single();
            RunCharacterState character = settled.Snapshot.ActiveRun.Party.Single(value =>
                value.CharacterId == pending.CharacterId);
            UnitAttributes raised = new(character.Attributes.Strength + 1, character.Attributes.Agility,
                character.Attributes.Constitution, character.Attributes.Intelligence,
                character.Attributes.Charisma, character.Attributes.Luck);
            RunSessionResult completed = service.ApplyMutation(state => progression.CompleteProgression(
                state, state.Revision, pending.TransactionKey, raised, null,
                new Dictionary<ContentId, Tactics.Core.Skills.SkillDefinition>(), definition));
            Assert.That(completed.Succeeded, Is.True);
        }

        PureRunState ready = store.Snapshot!.ActiveRun!;
        RunSessionResult n3 = service.BeginEncounter();
        Assert.Multiple(() =>
        {
            Assert.That(ready.Phase, Is.EqualTo(PureRunPhase.Ready));
            Assert.That(ready.EncounterIndex, Is.EqualTo(2));
            Assert.That(ready.EncounterContentId, Is.EqualTo(new ContentId("encounter.pure-run.n3")));
            Assert.That(ready.PendingProgression, Is.Empty);
            Assert.That(n3.Succeeded, Is.True);
            Assert.That(n3.EncounterRequest!.EncounterContentId,
                Is.EqualTo(new ContentId("encounter.pure-run.n3")));
        });
    }

    [Test]
    public void StoreRejectsStaleRevision()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        service.StartNewRun(1);
        RunStoreResult stale = store.Save(store.Snapshot!, expectedRevision: 0);
        Assert.That(stale.ErrorCode, Is.EqualTo("save.revision_conflict"));
    }

    [Test]
    public void DuplicateSettlementAfterAdvance_DoesNotApplyRewardTwice()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        service.StartNewRun(17);
        service.BeginEncounter();
        PureRunBattleResult result = Victory(store.Snapshot!.ActiveRun!);
        RunSessionResult first = service.ApplyBattleResult(result);
        int gold = first.Snapshot!.ActiveRun!.Gold;

        RunSessionResult duplicate = service.ApplyBattleResult(result);

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Succeeded, Is.True);
            Assert.That(duplicate.WasDuplicate, Is.True);
            Assert.That(duplicate.Snapshot!.ActiveRun!.Gold, Is.EqualTo(gold));
        });
    }

    [Test]
    public void DuplicateTerminalSettlement_ReturnsExistingSummaryWithoutRewrite()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        service.StartNewRun(23);
        PureRunBattleResult? finalResult = null;
        for (int index = 0; index < 3; index++)
        {
            service.BeginEncounter();
            finalResult = Victory(store.Snapshot!.ActiveRun!);
            Assert.That(service.ApplyBattleResult(finalResult).Succeeded, Is.True);
        }
        long terminalRevision = store.Snapshot!.Revision;

        RunSessionResult duplicate = service.ApplyBattleResult(finalResult!);

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Succeeded, Is.True);
            Assert.That(duplicate.WasDuplicate, Is.True);
            Assert.That(duplicate.Snapshot!.Revision, Is.EqualTo(terminalRevision));
            Assert.That(duplicate.Snapshot.TerminalSummary!.Outcome, Is.EqualTo(PureRunOutcome.SliceCompleted));
        });
    }

    [Test]
    public void ResumeRun_RepairsLegacyAllZeroAttributesAndPersistsOnNextTransaction()
    {
        PureRunDefinition definition = Definition();
        var store = new MemoryRunStore();
        var zero = new UnitAttributes(0,0,0,0,0,0);
        RunCharacterState[] party = definition.Party.Select(template => new RunCharacterState(template.CharacterId,
            template.UnitContentId,1,zero,20,20,5,18,false,new[]{template.StartingSkillContentId})).ToArray();
        store.Snapshot = new PureRunSaveSnapshot(1,new PureRunState("run-legacy",7,1,PureRunPhase.Ready,0,
            definition.Encounters[0],party),null);
        var service = new PureRunSessionService(definition,store);

        RunSessionResult resumed = service.ResumeRun();
        RunSessionResult begun = service.BeginEncounter();

        Assert.Multiple(() =>
        {
            Assert.That(resumed.Succeeded,Is.True);
            Assert.That(resumed.Diagnostics,Does.Contain("save.attributes_repaired_from_run_definition"));
            Assert.That(resumed.Snapshot!.ActiveRun!.Party[0].Attributes.Strength,Is.EqualTo(5));
            Assert.That(begun.Succeeded,Is.True);
            Assert.That(store.Snapshot!.ActiveRun!.Party.All(member=>member.Attributes.Strength>0),Is.True);
        });
    }

    [Test]
    public void ResumeRun_RejectsAmbiguousLegacyStartingSkillInsteadOfUsingTemplateDefault()
    {
        PureRunDefinition definition = DefinitionWithChoices();
        var store = new MemoryRunStore();
        PureRunPartyTemplate template = definition.Party[0];
        RunCharacterState mage = new(template.CharacterId, template.UnitContentId, 2, template.Attributes,
            20, 20, 5, 15, false,
            [new ContentId("skill.mage.ice-bolt.lv1"), new ContentId("skill.mage.fireball.lv1")]);
        RunCharacterState[] party = [mage, .. definition.Party.Skip(1).Select(item => new RunCharacterState(
            item.CharacterId, item.UnitContentId, 1, item.Attributes, 20, 20, 5, 15, false,
            [item.StartingSkillContentId], startingSkillContentId: item.StartingSkillContentId))];
        PureRunSaveSnapshot original = new(1, new PureRunState("legacy-ambiguous", 9, 1,
            PureRunPhase.Ready, 0, definition.Encounters[0], party), null);
        store.Snapshot = original;

        RunSessionResult result = new PureRunSessionService(definition, store).ResumeRun();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("save.starting_skill_ambiguous"));
            Assert.That(store.Snapshot, Is.SameAs(original));
        });
    }

    [TestCase("skill.poison-spear.lv1")]
    [TestCase("skill.mage.lightning.lv1")]
    public void ResumeRun_RejectsExplicitStartingSkillThatIsCrossRoleOrNotLearned(string invalidStartingSkill)
    {
        PureRunDefinition definition = DefinitionWithChoices();
        var store = new MemoryRunStore();
        PureRunPartyTemplate template = definition.Party[0];
        RunCharacterState mage = new(template.CharacterId, template.UnitContentId, 1, template.Attributes,
            20, 20, 5, 15, false, [new ContentId("skill.mage.fireball.lv1")],
            startingSkillContentId: new ContentId(invalidStartingSkill));
        RunCharacterState[] party = [mage, .. definition.Party.Skip(1).Select(item => new RunCharacterState(
            item.CharacterId, item.UnitContentId, 1, item.Attributes, 20, 20, 5, 15, false,
            [item.StartingSkillContentId], startingSkillContentId: item.StartingSkillContentId))];
        PureRunSaveSnapshot original = new(1, new PureRunState("invalid-starting-skill", 9, 1,
            PureRunPhase.Ready, 0, definition.Encounters[0], party), null);
        store.Snapshot = original;

        RunSessionResult result = new PureRunSessionService(definition, store).ResumeRun();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("save.starting_skill_invalid"));
            Assert.That(store.Snapshot, Is.SameAs(original));
        });
    }

    [Test]
    public void ResumeRun_AcceptsExplicitStartingSkillAfterThatBranchWasUpgraded()
    {
        PureRunDefinition definition = DefinitionWithChoices();
        var store = new MemoryRunStore();
        PureRunPartyTemplate template = definition.Party[1];
        var startingSkill = new ContentId("skill.necromancer.bone-spear.lv1");
        var upgradedSkill = new ContentId("skill.necromancer.bone-spear.lv2");
        RunCharacterState necromancer = new(template.CharacterId, template.UnitContentId, 2,
            template.Attributes, 20, 20, 5, 15, false, [upgradedSkill],
            learnedSkillStates: [new RunLearnedSkillState("necromancer.bone-spear", 2, upgradedSkill)],
            startingSkillContentId: startingSkill);
        RunCharacterState[] party =
        [
            new RunCharacterState(definition.Party[0].CharacterId, definition.Party[0].UnitContentId, 1,
                definition.Party[0].Attributes, 20, 20, 5, 15, false,
                [definition.Party[0].StartingSkillContentId],
                startingSkillContentId: definition.Party[0].StartingSkillContentId),
            necromancer,
            new RunCharacterState(definition.Party[2].CharacterId, definition.Party[2].UnitContentId, 1,
                definition.Party[2].Attributes, 20, 20, 5, 15, false,
                [definition.Party[2].StartingSkillContentId],
                startingSkillContentId: definition.Party[2].StartingSkillContentId)
        ];
        store.Snapshot = new PureRunSaveSnapshot(1, new PureRunState("upgraded-starting-skill", 9, 1,
            PureRunPhase.Ready, 2, definition.Encounters[2], party), null);

        RunSessionResult result = new PureRunSessionService(definition, store).ResumeRun();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot!.ActiveRun!.Party[1].StartingSkillContentId,
                Is.EqualTo(startingSkill));
            Assert.That(result.Snapshot.ActiveRun.Party[1].LearnedSkillStates.Single().Level,
                Is.EqualTo(2));
        });
    }

    [Test]
    public void ResumeRun_RepairsMissingStartingSkillFromAnUpgradedBranch()
    {
        PureRunDefinition definition = DefinitionWithChoices();
        var store = new MemoryRunStore();
        PureRunPartyTemplate template = definition.Party[1];
        var upgradedSkill = new ContentId("skill.necromancer.bone-spear.lv2");
        RunCharacterState necromancer = new(template.CharacterId, template.UnitContentId, 2,
            template.Attributes, 20, 20, 5, 15, false, [upgradedSkill],
            learnedSkillStates: [new RunLearnedSkillState("necromancer.bone-spear", 2, upgradedSkill)]);
        RunCharacterState[] party = definition.Party.Select(item => item.CharacterId == template.CharacterId
            ? necromancer
            : new RunCharacterState(item.CharacterId, item.UnitContentId, 1, item.Attributes,
                20, 20, 5, 15, false, [item.StartingSkillContentId],
                startingSkillContentId: item.StartingSkillContentId)).ToArray();
        store.Snapshot = new PureRunSaveSnapshot(1, new PureRunState("legacy-upgraded-starting", 9, 1,
            PureRunPhase.Ready, 2, definition.Encounters[2], party), null);

        RunSessionResult result = new PureRunSessionService(definition, store).ResumeRun();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Snapshot!.ActiveRun!.Party[1].StartingSkillContentId,
            Is.EqualTo(new ContentId("skill.necromancer.bone-spear.lv1")));
    }

    [Test]
    public void ResumeRun_RejectsLearnedSkillWhoseBranchLevelAndDefinitionDisagree()
    {
        PureRunDefinition definition = DefinitionWithChoices();
        var store = new MemoryRunStore();
        PureRunPartyTemplate template = definition.Party[1];
        var startingSkill = new ContentId("skill.necromancer.bone-spear.lv1");
        var unrelatedDefinition = new ContentId("skill.mage.fireball.lv2");
        RunCharacterState necromancer = new(template.CharacterId, template.UnitContentId, 2,
            template.Attributes, 20, 20, 5, 15, false, [unrelatedDefinition],
            learnedSkillStates: [new RunLearnedSkillState("necromancer.bone-spear", 99, unrelatedDefinition)],
            startingSkillContentId: startingSkill);
        RunCharacterState[] party = definition.Party.Select(item => item.CharacterId == template.CharacterId
            ? necromancer
            : new RunCharacterState(item.CharacterId, item.UnitContentId, 1, item.Attributes,
                20, 20, 5, 15, false, [item.StartingSkillContentId],
                startingSkillContentId: item.StartingSkillContentId)).ToArray();
        store.Snapshot = new PureRunSaveSnapshot(1, new PureRunState("invalid-learned-identity", 9, 1,
            PureRunPhase.Ready, 2, definition.Encounters[2], party), null);

        RunSessionResult result = new PureRunSessionService(definition, store).ResumeRun();

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("save.starting_skill_invalid"));
    }

    [Test]
    public void ResumeRun_RejectsLearnedSkillWithoutCanonicalLevelSuffix()
    {
        PureRunDefinition definition = DefinitionWithChoices();
        var store = new MemoryRunStore();
        PureRunPartyTemplate template = definition.Party[1];
        var startingSkill = new ContentId("skill.necromancer.bone-spear.lv1");
        var malformedDefinition = new ContentId("skill.necromancer.bone-spear");
        RunCharacterState necromancer = new(template.CharacterId, template.UnitContentId, 1,
            template.Attributes, 20, 20, 5, 15, false, [malformedDefinition],
            learnedSkillStates: [new RunLearnedSkillState("necromancer.bone-spear", 1, malformedDefinition)],
            startingSkillContentId: startingSkill);
        RunCharacterState[] party = definition.Party.Select(item => item.CharacterId == template.CharacterId
            ? necromancer
            : new RunCharacterState(item.CharacterId, item.UnitContentId, 1, item.Attributes,
                20, 20, 5, 15, false, [item.StartingSkillContentId],
                startingSkillContentId: item.StartingSkillContentId)).ToArray();
        store.Snapshot = new PureRunSaveSnapshot(1, new PureRunState("malformed-learned-id", 9, 1,
            PureRunPhase.Ready, 2, definition.Encounters[2], party), null);

        RunSessionResult result = new PureRunSessionService(definition, store).ResumeRun();

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("save.starting_skill_invalid"));
    }

    [TestCase("necromancer.bone-spear.lv2")]
    [TestCase("skill.necromancer.bone-spear.lv02")]
    public void ResumeRun_RejectsNonCanonicalLearnedSkillId(string invalidDefinitionId)
    {
        PureRunDefinition definition = DefinitionWithChoices();
        var store = new MemoryRunStore();
        PureRunPartyTemplate template = definition.Party[1];
        var startingSkill = new ContentId("skill.necromancer.bone-spear.lv1");
        var invalidDefinition = new ContentId(invalidDefinitionId);
        RunCharacterState necromancer = new(template.CharacterId, template.UnitContentId, 2,
            template.Attributes, 20, 20, 5, 15, false, [invalidDefinition],
            learnedSkillStates: [new RunLearnedSkillState("necromancer.bone-spear", 2, invalidDefinition)],
            startingSkillContentId: startingSkill);
        RunCharacterState[] party = definition.Party.Select(item => item.CharacterId == template.CharacterId
            ? necromancer
            : new RunCharacterState(item.CharacterId, item.UnitContentId, 1, item.Attributes,
                20, 20, 5, 15, false, [item.StartingSkillContentId],
                startingSkillContentId: item.StartingSkillContentId)).ToArray();
        store.Snapshot = new PureRunSaveSnapshot(1, new PureRunState("noncanonical-learned-id", 9, 1,
            PureRunPhase.Ready, 2, definition.Encounters[2], party), null);

        RunSessionResult result = new PureRunSessionService(definition, store).ResumeRun();

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("save.starting_skill_invalid"));
    }

    [Test]
    public void ResumeRun_RepairsLegacyLayerFourStateWithoutChangingStoredSnapshot()
    {
        PureRunDefinition definition = DefinitionWithMap();
        PureRunMapDefinition map = LayerFourMap();
        var store = new MemoryRunStore();
        PureRunState legacy = new("legacy-layer-four", 7, 12, PureRunPhase.AwaitingLayerFourChoice,
            2, definition.Encounters[2], definition.Party.Select(template => new RunCharacterState(
                template.CharacterId, template.UnitContentId, 1, template.Attributes, 20, 20, 10, 15, false,
                [template.StartingSkillContentId], startingSkillContentId: template.StartingSkillContentId)).ToArray(),
            battlesCompleted: 3);
        store.Snapshot = new PureRunSaveSnapshot(12, legacy, null);

        RunSessionResult resumed = new PureRunSessionService(definition, store, mapDefinition: map).ResumeRun();

        Assert.Multiple(() =>
        {
            Assert.That(resumed.Succeeded, Is.True);
            Assert.That(resumed.Snapshot!.ActiveRun!.MapState!.Phase, Is.EqualTo(PureRunMapPhase.ChoosingLayerFour));
            Assert.That(resumed.Snapshot.ActiveRun.MapState.ReachableNodeIds, Has.Count.EqualTo(4));
            Assert.That(store.Snapshot!.ActiveRun!.MapState, Is.Null);
        });
    }

    [Test]
    public void ApplyFullRunTransition_PersistsBossTerminalAtANewRevisionExactlyOnce()
    {
        PureRunDefinition definition = DefinitionWithChoices();
        var store = new MemoryRunStore();
        RunCharacterState[] party = definition.Party.Select(template => new RunCharacterState(
            template.CharacterId, template.UnitContentId, 2, template.Attributes, 20, 20, 10, 15, false,
            [template.StartingSkillContentId], startingSkillContentId: template.StartingSkillContentId)).ToArray();
        var encounter = new ContentId("encounter.pure-run.special");
        var checkpoint = new RunEncounterCheckpoint(encounter, 6, 148, party, [], []);
        var pending = new PureRunState("boss-run", 7, 148, PureRunPhase.PendingBattle, 6, encounter,
            party, checkpoint: checkpoint, battlesCompleted: 4);
        store.Snapshot = new PureRunSaveSnapshot(148, pending, null);
        var battle = new PureRunBattleResult(pending.RunId, checkpoint.Revision, encounter, true, 11, 1,
            party.Select(member => new BattlePartyResult(member.CharacterId, member.CurrentHealth,
                member.CurrentMana, member.IsDead, member.CarriedConsumables)).ToArray());
        var session = new PureRunSessionService(definition, store);

        RunSessionResult completed = session.ApplyFullRunTransition(state =>
            new PureRunFullRunService().CompleteBoss(state, battle));
        long persistedRevision = store.Snapshot!.Revision;
        RunSessionResult duplicate = session.ApplyFullRunTransition(state =>
            new PureRunFullRunService().CompleteBoss(state, battle));

        Assert.Multiple(() =>
        {
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(completed.Snapshot!.Revision, Is.EqualTo(149));
            Assert.That(completed.Snapshot.ActiveRun, Is.Null);
            Assert.That(completed.Snapshot.TerminalSummary!.Outcome, Is.EqualTo(PureRunOutcome.BossVictory));
            Assert.That(store.Snapshot.Revision, Is.EqualTo(persistedRevision));
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.ErrorCode, Is.EqualTo("run.no_active_run"));
        });
    }

    private static PureRunBattleResult Victory(PureRunState run) => new(
        run.RunId, run.Checkpoint!.Revision, run.EncounterContentId, true, 3, 3,
        run.Party.Select(member => new BattlePartyResult(
            member.CharacterId, member.MaxHealth, member.MaxMana, false,
            Array.Empty<Tactics.Core.Items.BattleConsumableState>())).ToArray());

    private static PureRunDefinition Definition() => new(
        new ContentId("run.pure-run.three-encounter-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }.Select(value => new ContentId(value)),
        new[]
        {
            new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5,5,5,6,5,5)),
            new PureRunPartyTemplate("necro", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5,5,5,5,6,5)),
            new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5,6,5,5,5,5))
        });

    private static PureRunDefinition DefinitionWithChoices() => new(
        new ContentId("run.pure-run.three-encounter-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }.Select(value => new ContentId(value)),
        new[]
        {
            new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5,5,5,6,5,5), 1,
                [new ContentId("skill.mage.fireball.lv1"), new ContentId("skill.mage.ice-bolt.lv1"), new ContentId("skill.mage.lightning.lv1")]),
            new PureRunPartyTemplate("necromancer", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5,5,5,5,6,5), 1,
                [new ContentId("skill.necromancer.summon-skeleton.lv1"), new ContentId("skill.necromancer.amplify-damage.lv1"), new ContentId("skill.necromancer.bone-spear.lv1")]),
            new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5,6,5,5,5,5), 1,
                [new ContentId("skill.amazon.thrust.lv1"), new ContentId("skill.poison-spear.lv1"), new ContentId("skill.amazon.combat-techniques.lv1")])
        });

    private static PureRunDefinition DefinitionWithMap() => new(
        new ContentId("run.pure-run.three-encounter-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }.Select(value => new ContentId(value)),
        Definition().Party,
        new ContentId("run-map.pure-run.layer4-v1"));

    private static PureRunDefinition DefinitionWithDemonbound() => new(
        new ContentId("run.pure-run.four-candidate-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }
            .Select(value => new ContentId(value)),
        DefinitionWithChoices().Party.Append(new PureRunPartyTemplate(
            "demonbound", new ContentId("unit.pure-run.demonbound"),
            new ContentId("skill.demonbound.bane.lv1"), new UnitAttributes(5, 5, 5, 5, 6, 5), 1,
            [new ContentId("skill.demonbound.bane.lv1"),
                new ContentId("skill.demonbound.infernal-blast.lv1"),
                new ContentId("skill.demonbound.mindfulness.lv1")],
            SeededStartingSkill: true,
            InherentSkills: [new ContentId("skill.demonbound.meditation")])));

    private static PureRunDefinition DefinitionWithPoet() => new(
        new ContentId("run.pure-run.five-candidate-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }
            .Select(value => new ContentId(value)),
        DefinitionWithDemonbound().Party.Append(new PureRunPartyTemplate(
            "poet", new ContentId("unit.pure-run.poet"),
            new ContentId("skill.poet.xiake-xing.lv1"), new UnitAttributes(6, 5, 5, 4, 6, 4), 1,
            [new ContentId("skill.poet.xiake-xing.lv1"),
                new ContentId("skill.poet.jiang-jin-jiu.lv1"),
                new ContentId("skill.poet.moon-drink.lv1")],
            SeededStartingSkill: true)));

    private static PureRunMapDefinition LayerFourMap() => new(new ContentId("run-map.pure-run.layer4-v1"), 2,
    [
        new PureRunMapNodeDefinition("layer_04_battle", 4, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n4")),
        new PureRunMapNodeDefinition("layer_04_rest", 4, PureRunNodeKind.Rest, new ContentId("rest.standard")),
        new PureRunMapNodeDefinition("layer_04_store", 4, PureRunNodeKind.Store, new ContentId("store.standard")),
        new PureRunMapNodeDefinition("layer_04_event", 4, PureRunNodeKind.Mystery, new ContentId("event.mystery"))
    ]);

    private sealed class MemoryRunStore : IRunSaveStore
    {
        public PureRunSaveSnapshot? Snapshot { get; set; }

        public RunStoreResult Load() => new(true, null, Snapshot ?? new PureRunSaveSnapshot(0, null, null));

        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
        {
            long current = Snapshot?.Revision ?? 0;
            if (current != expectedRevision)
                return new RunStoreResult(false, "save.revision_conflict", Snapshot);
            Snapshot = snapshot;
            return new RunStoreResult(true, null, Snapshot);
        }
    }
}
