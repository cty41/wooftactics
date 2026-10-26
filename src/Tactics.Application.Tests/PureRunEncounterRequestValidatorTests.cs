using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunEncounterRequestValidatorTests
{
    [Test]
    public void BoundActualEventVariantUsesPersistedTemplateAndRejectsAnAlteredRequestIdentity()
    {
        PureRunState legacy = PureRunPendingSceneRecoveryTests.ActualEvent("layer_04_event", RunAdventureEventContextKind.CursedChestMimic);
        var recovered = PureRunPendingSceneRecovery.Recover(new(legacy.Revision, legacy, null), PureRunNodeSceneProjectorTests.DemoMap());
        Assert.That(recovered.Succeeded, Is.True, recovered.ErrorCode);
        PureRunState run = recovered.Snapshot.ActiveRun!;
        var request = new EncounterRequest(run.RunId, run.Checkpoint!.Revision, run.Checkpoint.EncounterContentId,
            run.Checkpoint.Party, run.AdventureState!.Revision, run.Checkpoint.SceneIdentity);
        Assert.That(Validate(recovered.Snapshot, request).Succeeded, Is.True);
        var defaultBinding = PureRunActBlueprint.Resolve(PureRunNodeSceneProjectorTests.DemoMap(), run.Seed).Bindings[request.SceneIdentity!.NodeId];
        var wrong = new RunNodeSceneIdentity(request.SceneIdentity.ActId, request.SceneIdentity.DefinitionVersion,
            request.SceneIdentity.NodeId, defaultBinding.TemplateId);
        Assert.That(Validate(recovered.Snapshot, request with { SceneIdentity = wrong }).Succeeded, Is.False);
    }

    [Test]
    public void ActualSessionRequestAndASeparateSaveRoundTripPartyBothValidateWithoutWrites()
    {
        var (snapshot, request, store) = Pending();
        int saves = store.SaveCount;
        Assert.That(Validate(snapshot, request).Succeeded, Is.True);
        var decoded = RunSaveDocumentV11.Decode(RunSaveDocumentV11.Encode(snapshot));
        Assert.That(decoded.Succeeded, Is.True);
        var party = decoded.Snapshot!.ActiveRun!.Checkpoint!.Party;
        Assert.That(party, Is.Not.SameAs(request.Party));
        Assert.That(Validate(snapshot, request with { Party = party }).Succeeded, Is.True);
        Assert.That(store.SaveCount, Is.EqualTo(saves));
    }

    [TestCase("run")]
    [TestCase("checkpoint")]
    [TestCase("encounter")]
    [TestCase("adventure")]
    [TestCase("null-party")]
    [TestCase("null-member")]
    [TestCase("empty-party")]
    [TestCase("reordered-party")]
    [TestCase("hp")]
    [TestCase("skill")]
    [TestCase("attribute")]
    [TestCase("mana")]
    [TestCase("starting-skill")]
    public void ForgedRequestIsRejectedWithoutMutatingTheCheckpoint(string change)
    {
        var (snapshot, original, _) = Pending();
        string before = RunSaveDocumentV11.Encode(snapshot);
        EncounterRequest request = change switch
        {
            "run" => original with { RunId = "other-run" },
            "checkpoint" => original with { CheckpointRevision = original.CheckpointRevision + 1 },
            "encounter" => original with { EncounterContentId = new ContentId("encounter.other") },
            "adventure" => original with { AdventureRevision = original.AdventureRevision + 1 },
            "null-party" => original with { Party = null! },
            "null-member" => original with { Party = new RunCharacterState[] { null!, original.Party[1], original.Party[2] } },
            "empty-party" => original with { Party = [] },
            "reordered-party" => original with { Party = original.Party.Reverse().ToArray() },
            _ => original with { Party = new[] { Mutate(original.Party[0], change) }.Concat(original.Party.Skip(1)).ToArray() }
        };
        Assert.That(Validate(snapshot, request).Succeeded, Is.False, change);
        Assert.That(RunSaveDocumentV11.Encode(snapshot), Is.EqualTo(before));
    }

    [TestCase("no-run")]
    [TestCase("setup")]
    [TestCase("envelope")]
    [TestCase("zero-checkpoint")]
    [TestCase("negative-adventure")]
    [TestCase("run-encounter")]
    [TestCase("null-checkpoint")]
    [TestCase("ready")]
    public void InvalidPersistedBoundaryIsRejected(string change)
    {
        var (source, request, _) = Pending();
        PureRunState run = source.ActiveRun!;
        PureRunSaveSnapshot snapshot = change switch
        {
            "no-run" => source with { ActiveRun = null },
            "setup" => source with { PendingRunSetup = new PendingRunSetup(41, 0, []) },
            "envelope" => source with { Revision = source.Revision + 1 },
            "zero-checkpoint" => source with { ActiveRun = Copy(run, run.AdventureState, run.Checkpoint! with { Revision = 0 }) },
            "negative-adventure" => source with { ActiveRun = Copy(run, run.AdventureState! with { Revision = -1 }, run.Checkpoint) },
            "run-encounter" => source with { ActiveRun = Copy(run, run.AdventureState, run.Checkpoint, encounter: new ContentId("encounter.other")) },
            "null-checkpoint" => source with { ActiveRun = Copy(run, run.AdventureState, null) },
            _ => source with { ActiveRun = Copy(run, run.AdventureState, run.Checkpoint, phase: PureRunPhase.Ready) }
        };
        if (change == "zero-checkpoint") request = request with { CheckpointRevision = 0 };
        if (change == "negative-adventure") request = request with { AdventureRevision = -1 };
        Assert.That(Validate(snapshot, request).Succeeded, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MatchingButStructurallyInvalidPartiesAreRejected(bool duplicate)
    {
        var (source, request, _) = Pending();
        RunCharacterState[] party = duplicate ? [request.Party[0], request.Party[0], request.Party[2]] : [];
        var run = source.ActiveRun!;
        var snapshot = source with { ActiveRun = Copy(run, run.AdventureState, run.Checkpoint! with { Party = party }) };
        Assert.That(Validate(snapshot, request with { Party = party }).Succeeded, Is.False);
    }

    [Test]
    public void LegacyWithoutAdventureUsesZeroAndDoesNotDemandTheNewTopology()
    {
        var (source, request, _) = Pending();
        var snapshot = source with { ActiveRun = Copy(source.ActiveRun!, null, source.ActiveRun!.Checkpoint) };
        var unsupported = new PureRunMapDefinition(new ContentId("map.other"), 2,
            [new PureRunMapNodeDefinition("only", 0, PureRunNodeKind.Rest, new ContentId("rest.other"))]);
        Assert.That(PureRunEncounterRequestValidator.Validate(snapshot, request with { AdventureRevision = 0 }, unsupported).Succeeded, Is.True);
        Assert.That(Validate(snapshot, request with { AdventureRevision = 1 }).Succeeded, Is.False);
    }

    [Test]
    public void FullyBoundV12RoundTripIsAcceptedButUnsupportedTopologyReturnsFailure()
    {
        var (source, _, _) = Pending();
        var recovery = PureRunPendingSceneRecovery.Recover(source, Map());
        Assert.That(recovery.Succeeded, Is.True, recovery.ErrorCode);
        var decoded = RunSaveDocumentV12.Decode(RunSaveDocumentV12.Encode(recovery.Snapshot));
        var snapshot = decoded.Snapshot!;
        var request = Request(snapshot.ActiveRun!);
        Assert.That(Validate(snapshot, request).Succeeded, Is.True);
        var map = Map();
        var unsupported = new PureRunMapDefinition(map.ContentId, map.LayoutVersion, map.Nodes, map.Connections.Skip(1));
        var result = PureRunEncounterRequestValidator.Validate(snapshot, request, unsupported);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("encounter.scene_definition_invalid"));
    }

    [TestCase("current-missing")]
    [TestCase("checkpoint-missing")]
    [TestCase("request-missing")]
    [TestCase("current-wrong")]
    [TestCase("checkpoint-wrong")]
    [TestCase("request-wrong")]
    [TestCase("all-wrong")]
    public void IdentitiesMustBeCompleteEqualAndBelongToTheActualNode(string change)
    {
        var (source, _, _) = Pending();
        var snapshot = PureRunPendingSceneRecovery.Recover(source, Map()).Snapshot;
        var run = snapshot.ActiveRun!;
        var wrong = new RunNodeSceneIdentity(new ContentId("act-01"), 1, "layer_02_battle",
            new ContentId("adventure-map-template.pure-run.node.layer-02-battle"));
        var adventure = run.AdventureState!;
        var checkpoint = run.Checkpoint!;
        var request = Request(run);
        if (change.StartsWith("current") || change == "all-wrong")
            adventure = adventure with { SceneIdentity = change.EndsWith("missing") ? null : wrong };
        if (change.StartsWith("checkpoint") || change == "all-wrong")
            checkpoint = checkpoint with { SceneIdentity = change.EndsWith("missing") ? null : wrong };
        if (change.StartsWith("request") || change == "all-wrong")
            request = request with { SceneIdentity = change.EndsWith("missing") ? null : wrong };
        snapshot = snapshot with { ActiveRun = Copy(run, adventure, checkpoint) };
        Assert.That(Validate(snapshot, request).Succeeded, Is.False, change);
    }

    private static RunCharacterState Mutate(RunCharacterState value, string change) => new(value.CharacterId,
        value.UnitContentId, value.Level,
        change == "attribute" ? new UnitAttributes(value.Attributes.Strength + 1, value.Attributes.Agility,
            value.Attributes.Constitution, value.Attributes.Intelligence, value.Attributes.Charisma, value.Attributes.Luck) : value.Attributes,
        change == "hp" ? value.CurrentHealth - 1 : value.CurrentHealth, value.MaxHealth,
        change == "mana" ? value.CurrentMana - 1 : value.CurrentMana, value.MaxMana, value.IsDead,
        change == "skill" ? value.LearnedSkills.Concat([new ContentId("skill.other.lv1")]).ToArray() : value.LearnedSkills,
        value.Equipment, value.CarriedConsumables, value.LearnedSkillStates,
        change == "starting-skill" ? new ContentId("skill.other.lv1") : value.StartingSkillContentId);

    private static PureRunEncounterRequestValidationResult Validate(PureRunSaveSnapshot snapshot, EncounterRequest request) =>
        PureRunEncounterRequestValidator.Validate(snapshot, request, Map());
    [Test]
    public void LivingCheckpointPartyValidatesWhenTheFullRosterStillContainsDeadMembers()
    {
        var (snapshot, _, _) = Pending();
        PureRunState run = snapshot.ActiveRun!;
        RunCharacterState[] party = run.Party.Select(member => member.CharacterId == "amazon"
            ? member
            : new RunCharacterState(member.CharacterId, member.UnitContentId, member.Level, member.Attributes,
                0, member.MaxHealth, member.CurrentMana, member.MaxMana, true, member.LearnedSkills,
                member.Equipment, member.CarriedConsumables, member.LearnedSkillStates,
                member.StartingSkillContentId)).ToArray();
        RunCharacterState[] living = party.Where(member => !member.IsDead).ToArray();
        RunEncounterCheckpoint checkpoint = run.Checkpoint! with { Party = living };
        PureRunState pending = new(run.RunId, run.Seed, run.Revision, run.Phase, run.EncounterIndex,
            run.EncounterContentId, party, run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression,
            run.AppliedTransactionKeys, run.Gold, run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems,
            checkpoint, run.MapState, run.NodeTransaction, run.EscortState, run.AdventureState);
        var reduced = snapshot with { ActiveRun = pending };
        var request = new EncounterRequest(pending.RunId, checkpoint.Revision, checkpoint.EncounterContentId,
            living, pending.AdventureState?.Revision ?? 0, checkpoint.SceneIdentity);
        Assert.That(Validate(reduced, request).Succeeded, Is.True);
    }

    private static PureRunMapDefinition Map() => PureRunNodeSceneProjectorTests.DemoMap();
    private static EncounterRequest Request(PureRunState run) => new(run.RunId, run.Checkpoint!.Revision,
        run.Checkpoint.EncounterContentId, run.Checkpoint.Party, run.AdventureState?.Revision ?? 0, run.Checkpoint.SceneIdentity);
    private static PureRunState Copy(PureRunState run, RunAdventureState? adventure, RunEncounterCheckpoint? checkpoint,
        PureRunPhase? phase = null, ContentId? encounter = null) => new(run.RunId, run.Seed, run.Revision,
        phase ?? run.Phase, run.EncounterIndex, encounter ?? run.EncounterContentId, run.Party, run.BackpackConsumables,
        run.BackpackEquipment, run.PendingProgression, run.AppliedTransactionKeys, run.Gold, run.BattlesCompleted,
        run.EnemiesDefeated, run.AcquiredItems, checkpoint, run.MapState, run.NodeTransaction, run.EscortState, adventure);

    private static (PureRunSaveSnapshot Snapshot, EncounterRequest Request, MemoryStore Store) Pending()
    {
        var store = new MemoryStore();
        var definition = new PureRunDefinition(new ContentId("run.pure-run.three-encounter-v1"),
            new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }.Select(id => new ContentId(id)),
            [new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5,5,5,6,5,5)),
             new PureRunPartyTemplate("necro", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5,5,5,5,6,5)),
             new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5,6,5,5,5,5))]);
        var service = new PureRunSessionService(definition, store);
        Assert.That(service.StartNewRun(41).Succeeded, Is.True);
        var begun = service.BeginEncounter();
        Assert.That(begun.Succeeded, Is.True, begun.ErrorCode);
        return (begun.Snapshot!, begun.EncounterRequest!, store);
    }
    private sealed class MemoryStore : IRunSaveStore
    {
        public PureRunSaveSnapshot Snapshot { get; private set; } = new(0, null, null);
        public int SaveCount { get; private set; }
        public RunStoreResult Load() => new(true, null, Snapshot);
        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
        {
            SaveCount++;
            if (Snapshot.Revision != expectedRevision) return new(false, "save.revision_conflict", Snapshot);
            Snapshot = snapshot;
            return new(true, null, Snapshot);
        }
    }
}
