using System.Text.Json;
using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunPendingSceneRecoveryTests
{
    [Test]
    public void LegacyDocumentCanNormalizeInMemoryRoundTripV12AndResumeWithoutChangingRandomState()
    {
        string legacyJson = RunSaveDocumentV11.Encode(LegacyPending());
        RunSaveDecodeResultV12 legacy = RunSaveDocumentV12.Decode(legacyJson);
        Assert.That(legacy.RequiresNodeMigration, Is.True);
        PureRunSaveSnapshot source = legacy.Snapshot!;
        PureRunState before = source.ActiveRun!;
        PureRunPendingSceneRecoveryResult recovered = PureRunPendingSceneRecovery.Recover(source, Map());
        Assert.That(recovered.Succeeded, Is.True, recovered.ErrorCode);
        Assert.That(recovered.WasMigrated, Is.True);
        PureRunState after = recovered.Snapshot.ActiveRun!;
        Assert.Multiple(() =>
        {
            Assert.That(recovered.Snapshot.Revision, Is.EqualTo(source.Revision));
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.AdventureState!.Revision, Is.EqualTo(before.AdventureState!.Revision));
            Assert.That(after.AdventureState.SceneRevision, Is.EqualTo(before.AdventureState.SceneRevision));
            Assert.That(after.AdventureState.LeaderRevision, Is.EqualTo(before.AdventureState.LeaderRevision));
            Assert.That(after.AdventureState.InteractionRevision, Is.EqualTo(before.AdventureState.InteractionRevision));
            Assert.That(after.AdventureState.ExitRevision, Is.EqualTo(before.AdventureState.ExitRevision));
            Assert.That(after.AdventureState.SceneIdentity!.NodeId, Is.EqualTo("layer_01_battle"));
            Assert.That(after.Checkpoint!.SceneIdentity, Is.EqualTo(after.AdventureState.SceneIdentity));
            Assert.That(RunAdventureTransitionService.CurrentNodeId(after), Is.EqualTo("layer_01_battle"));
            // Legacy serialization ignores only the added metadata, comparing every old checkpoint field by value.
            Assert.That(JsonSerializer.Serialize(after.Checkpoint), Is.EqualTo(JsonSerializer.Serialize(before.Checkpoint)));
            Assert.That(after.Seed, Is.EqualTo(before.Seed));
            Assert.That(after.EncounterContentId, Is.EqualTo(before.EncounterContentId));
            Assert.That(before.AdventureState.SceneIdentity, Is.Null);
            Assert.That(RunSaveDocumentV11.Encode(source), Is.EqualTo(legacyJson));
        });
        string v12 = RunSaveDocumentV12.Encode(recovered.Snapshot);
        RunSaveDecodeResultV12 decoded = RunSaveDocumentV12.Decode(v12);
        Assert.That(decoded.Succeeded, Is.True, decoded.ErrorCode);
        var store = new MemoryStore { Snapshot = decoded.Snapshot! };
        RunSessionResult resumed = new PureRunSessionService(Definition(), store).ResumeRun();
        Assert.That(resumed.Succeeded, Is.True, resumed.ErrorCode);
        var oldRequest = new EncounterRequest(before.RunId, before.Checkpoint!.Revision,
            before.Checkpoint.EncounterContentId, before.Checkpoint.Party, before.AdventureState!.Revision);
        Assert.Multiple(() =>
        {
            Assert.That(resumed.EncounterRequest!.EffectiveRandomState, Is.EqualTo(oldRequest.EffectiveRandomState));
            Assert.That(resumed.EncounterRequest.SceneIdentity, Is.EqualTo(after.Checkpoint!.SceneIdentity));
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(RunSaveDocumentV12.Encode(decoded.Snapshot!), Is.EqualTo(v12));
        });
        PureRunPendingSceneRecoveryResult repeated = PureRunPendingSceneRecovery.Recover(decoded.Snapshot!, Map());
        Assert.That(repeated.Succeeded, Is.True, repeated.ErrorCode);
        Assert.That(repeated.WasMigrated, Is.False);
        Assert.That(repeated.Snapshot, Is.SameAs(decoded.Snapshot));
    }

    [Test]
    public void LegacyCellsAreNotCarriedIntoTheNewSceneAndTheSourceIsNotMutated()
    {
        PureRunSaveSnapshot source = LegacyPending();
        Assert.That(source.ActiveRun!.AdventureState!.ActorCells, Has.Count.EqualTo(3));
        var recovered = PureRunPendingSceneRecovery.Recover(source, Map());
        Assert.That(recovered.Succeeded, Is.True, recovered.ErrorCode);
        Assert.That(recovered.Snapshot.ActiveRun!.AdventureState!.ActorCells, Is.Empty);
        Assert.That(source.ActiveRun.AdventureState.ActorCells, Has.Count.EqualTo(3));
        Assert.That(source.ActiveRun.AdventureState.Lifecycle, Is.EqualTo(RunAdventureLifecycle.InitialExploration));
        Assert.That(source.ActiveRun.Checkpoint!.SceneIdentity, Is.Null);
    }

    [TestCase("revision")]
    [TestCase("no-run")]
    [TestCase("setup")]
    public void InvalidRecoveryPreservesTheExactInputSnapshot(string invalid)
    {
        PureRunSaveSnapshot original = LegacyPending();
        PureRunSaveSnapshot source = invalid switch
        {
            "revision" => original with { Revision = original.Revision + 1 },
            "no-run" => original with { ActiveRun = null },
            _ => original with { PendingRunSetup = new PendingRunSetup(41, 0, []) }
        };
        var recovered = PureRunPendingSceneRecovery.Recover(source, Map());
        Assert.That(recovered.Succeeded, Is.False);
        Assert.That(recovered.WasMigrated, Is.False);
        Assert.That(recovered.Snapshot, Is.SameAs(source));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UnsupportedButWellFormedMapReturnsFailureAndPreservesTheSource(bool invalidConnections)
    {
        PureRunMapDefinition canonical = Map();
        PureRunMapDefinition unsupported = invalidConnections
            ? new(canonical.ContentId, canonical.LayoutVersion, canonical.Nodes, canonical.Connections.Skip(1))
            : new(new ContentId("map.other"), 2,
                [new PureRunMapNodeDefinition("only-node", 0, PureRunNodeKind.Rest, new ContentId("rest.other"))]);
        PureRunSaveSnapshot source = LegacyPending();
        var result = PureRunPendingSceneRecovery.Recover(source, unsupported);
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("run.scene_definition_invalid"));
            Assert.That(result.WasMigrated, Is.False);
            Assert.That(result.Snapshot, Is.SameAs(source));
        });
    }

    [TestCase("layer_04_event", RunAdventureEventContextKind.CursedChestMimic)]
    [TestCase("layer_06_event", RunAdventureEventContextKind.FallenAltarGuardian)]
    [TestCase("layer_06_event", RunAdventureEventContextKind.LostVillagerEscort)]
    public void ActualPendingVariantRoundTripsWithoutChangingDefaultAssignmentsOrBusinessState(string node, RunAdventureEventContextKind context)
    {
        PureRunState run = ActualEvent(node, context);
        var source = new PureRunSaveSnapshot(run.Revision, run, null);
        string oldWire = RunSaveDocumentV11.Encode(source);
        var result = PureRunPendingSceneRecovery.Recover(source, Map());
        Assert.That(result.Succeeded, Is.True, result.ErrorCode);
        PureRunState migrated = result.Snapshot.ActiveRun!;
        Assert.Multiple(() =>
        {
            Assert.That(migrated.MapState, Is.SameAs(run.MapState));
            Assert.That(migrated.EscortState, Is.SameAs(run.EscortState));
            Assert.That(migrated.NodeTransaction, Is.SameAs(run.NodeTransaction));
            Assert.That(migrated.Revision, Is.EqualTo(run.Revision));
            Assert.That(JsonSerializer.Serialize(migrated.Checkpoint), Is.EqualTo(JsonSerializer.Serialize(run.Checkpoint)));
            Assert.That(migrated.AdventureState!.Revision, Is.EqualTo(run.AdventureState!.Revision));
            Assert.That(migrated.AdventureState.PendingEventContext, Is.EqualTo(context));
            Assert.That(RunSaveDocumentV11.Encode(source), Is.EqualTo(oldWire));
        });
        string encoded = RunSaveDocumentV12.Encode(result.Snapshot);
        var decoded = RunSaveDocumentV12.Decode(encoded);
        Assert.That(decoded.Succeeded, Is.True, decoded.ErrorCode);
        var again = PureRunPendingSceneRecovery.Recover(decoded.Snapshot!, Map());
        Assert.That(again.Succeeded, Is.True, again.ErrorCode);
        Assert.That(again.WasMigrated, Is.False);
        Assert.That(again.Snapshot, Is.SameAs(decoded.Snapshot));
        PureRunState restored = again.Snapshot.ActiveRun!;
        var request = new EncounterRequest(restored.RunId, restored.Checkpoint!.Revision, restored.Checkpoint.EncounterContentId,
            restored.Checkpoint.Party, restored.AdventureState!.Revision, restored.Checkpoint.SceneIdentity);
        Assert.That(request.EffectiveRandomState, Is.EqualTo(new EncounterRequest(run.RunId, run.Checkpoint!.Revision,
            run.Checkpoint.EncounterContentId, run.Checkpoint.Party, run.AdventureState!.Revision).EffectiveRandomState));
        var validated = PureRunEncounterRequestValidator.Validate(again.Snapshot, request, Map());
        Assert.That(validated.Succeeded, Is.True, validated.ErrorCode);
        Assert.That(RunSaveDocumentV12.Encode(again.Snapshot), Is.EqualTo(encoded));
    }

    [TestCase("missing")]
    [TestCase("destination")]
    [TestCase("lifecycle")]
    [TestCase("dead")]
    public void EscortCannotBeInventedOrRetargetedByRecovery(string fault)
    {
        PureRunState run = ActualEvent("layer_06_event", RunAdventureEventContextKind.LostVillagerEscort);
        run = PureRunNodeSceneProjectorTests.Copy(run, dropEscort: fault == "missing", escort: run.EscortState! with
        {
            DestinationNodeId = fault == "destination" ? "layer_04_event" : "layer_06_event",
            Lifecycle = fault == "lifecycle" ? RunEscortLifecycle.Traveling : RunEscortLifecycle.BattlePending,
            ProtectedNpcAlive = fault != "dead"
        });
        AssertEscortRecoveryRejectedWithoutMutation(run);
    }

    [TestCase("layer_04_event", RunAdventureEventContextKind.CursedChestMimic)]
    [TestCase("layer_06_event", RunAdventureEventContextKind.FallenAltarGuardian)]
    [TestCase("layer_01_battle", RunAdventureEventContextKind.None)]
    public void BattlePendingEscortCannotSpawnIntoAnUnrelatedBattle(string node, RunAdventureEventContextKind context)
    {
        PureRunState run = context == RunAdventureEventContextKind.None
            ? PureRunNodeSceneProjectorTests.Fixture(node).Item1 : ActualEvent(node, context);
        run = PureRunNodeSceneProjectorTests.Copy(run,
            escort: new RunEscortState("saved-escort", RunEscortLifecycle.BattlePending, true, "start", node, 7));
        AssertEscortRecoveryRejectedWithoutMutation(run);
    }

    [TestCase(RunEscortLifecycle.Completed, "layer_01_battle")]
    [TestCase(RunEscortLifecycle.Traveling, "layer_01_battle")]
    [TestCase(RunEscortLifecycle.Completed, "layer_04_event")]
    [TestCase(RunEscortLifecycle.Traveling, "layer_04_event")]
    public void NonPendingEscortHistoryDoesNotPreventOtherNodeRecovery(RunEscortLifecycle lifecycle, string node)
    {
        PureRunState run = node == "layer_01_battle" ? PureRunNodeSceneProjectorTests.Fixture(node).Item1
            : ActualEvent(node, RunAdventureEventContextKind.CursedChestMimic);
        var escort = new RunEscortState("saved-escort", lifecycle, true, "start", "layer_06_event", 7);
        run = PureRunNodeSceneProjectorTests.Copy(run, escort: escort);
        var result = PureRunPendingSceneRecovery.Recover(new(run.Revision, run, null), Map());
        Assert.That(result.Succeeded, Is.True, result.ErrorCode);
        Assert.That(result.Snapshot.ActiveRun!.EscortState, Is.SameAs(escort));
    }

    private static void AssertEscortRecoveryRejectedWithoutMutation(PureRunState run)
    {
        var source = new PureRunSaveSnapshot(run.Revision, run, null);
        string before = RunSaveDocumentV11.Encode(source);
        ulong RandomState() => new EncounterRequest(run.RunId, run.Checkpoint!.Revision, run.Checkpoint.EncounterContentId,
            run.Checkpoint.Party, run.AdventureState!.Revision).EffectiveRandomState;
        ulong random = RandomState();
        var result = PureRunPendingSceneRecovery.Recover(source, Map());
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("scene.escort_state_conflict"));
        Assert.That(result.WasMigrated, Is.False);
        Assert.That(result.Snapshot, Is.SameAs(source));
        Assert.That(RunSaveDocumentV11.Encode(source), Is.EqualTo(before));
        Assert.That(RandomState(), Is.EqualTo(random));
    }

    [TestCase("marker")]
    [TestCase("assignment")]
    [TestCase("transaction")]
    [TestCase("map-version")]
    [TestCase("identity-version")]
    [TestCase("identity-template")]
    [TestCase("partial-identity")]
    public void ActualVariantCannotMaskConflictingEvidence(string fault)
    {
        PureRunState run = ActualEvent("layer_04_event", RunAdventureEventContextKind.CursedChestMimic);
        switch (fault)
        {
            case "marker": run = PureRunNodeSceneProjectorTests.Copy(run, adventure: run.AdventureState! with { PendingEventNodeId = "layer_06_event" }); break;
            case "assignment": run = PureRunNodeSceneProjectorTests.Copy(run, map: run.MapState! with { MysteryEventAssignments = new Dictionary<string, string>() }); break;
            case "transaction": run = PureRunNodeSceneProjectorTests.Copy(run, transaction: run.NodeTransaction! with { Committed = true }); break;
            case "map-version": run = PureRunNodeSceneProjectorTests.Copy(run, map: run.MapState! with { MapLayoutVersion = 999 }); break;
            default:
                var blueprint = PureRunPendingSceneRecovery.ResolveBlueprint(run, Map());
                var identity = new RunNodeSceneIdentity(blueprint.ActDefinition.ActId,
                    fault == "identity-version" ? 999 : blueprint.ActDefinition.DefinitionVersion, "layer_04_event",
                    fault == "identity-template" ? new ContentId("template.unknown") : blueprint.Bindings["layer_04_event"].TemplateId);
                run = PureRunNodeSceneProjectorTests.Copy(run, adventure: run.AdventureState! with { SceneIdentity = identity },
                    checkpoint: run.Checkpoint! with { SceneIdentity = fault == "partial-identity" ? null : identity });
                break;
        }
        var snapshot = new PureRunSaveSnapshot(run.Revision, run, null);
        var result = PureRunPendingSceneRecovery.Recover(snapshot, Map());
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.WasMigrated, Is.False);
        Assert.That(result.Snapshot, Is.SameAs(snapshot));
    }

    internal static PureRunState ActualEvent(string node, RunAdventureEventContextKind context)
    {
        int seed = Enumerable.Range(0, 512).First(value => PureRunActBlueprint.Resolve(Map(), value).Bindings[node].EventContext != context);
        var (run, _) = PureRunNodeSceneProjectorTests.Fixture(node, seed);
        return PureRunNodeSceneProjectorTests.Copy(run, adventure: run.AdventureState! with
        {
            PendingEventContext = context,
            PendingEventObjectId = context switch
            {
                RunAdventureEventContextKind.CursedChestMimic => "cursed-chest",
                RunAdventureEventContextKind.FallenAltarGuardian => "fallen-altar",
                RunAdventureEventContextKind.LostVillagerEscort => "lost-villager",
                _ => throw new ArgumentOutOfRangeException(nameof(context))
            },
            BoardContentId = new ContentId($"adventure-board.pure-run.event.{context.ToString().ToLowerInvariant()}")
        }, dropEscort: context != RunAdventureEventContextKind.LostVillagerEscort,
        escort: context == RunAdventureEventContextKind.LostVillagerEscort
            ? new RunEscortState("saved-escort", RunEscortLifecycle.BattlePending, true, "layer_04_event", node, 7) : null);
    }

    private static PureRunMapDefinition Map() => PureRunNodeSceneProjectorTests.DemoMap();

    private static PureRunSaveSnapshot LegacyPending()
    {
        var store = new MemoryStore();
        var service = new PureRunSessionService(Definition(), store);
        Assert.That(service.StartNewRun(41).Succeeded, Is.True);
        RunSessionResult result = service.BeginEncounter();
        Assert.That(result.Succeeded, Is.True, result.ErrorCode);
        return result.Snapshot!;
    }

    private static PureRunDefinition Definition() => new(new ContentId("run.pure-run.three-encounter-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }.Select(id => new ContentId(id)),
        [new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5, 5, 5, 6, 5, 5)),
         new PureRunPartyTemplate("necro", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5, 5, 5, 5, 6, 5)),
         new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5, 6, 5, 5, 5, 5))]);

    private sealed class MemoryStore : IRunSaveStore
    {
        public PureRunSaveSnapshot Snapshot { get; set; } = new(0, null, null);
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
