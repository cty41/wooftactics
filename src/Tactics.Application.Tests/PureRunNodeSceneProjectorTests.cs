using System.Text.Json;
using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunNodeSceneProjectorTests
{
    public static IEnumerable<TestCaseData> PendingCases =>
        new[] { "layer_01_battle", "layer_02_battle", "layer_03_battle", "layer_04_battle", "layer_04_event",
            "layer_05_battle", "layer_06_battle", "layer_06_event", "layer_07_battle" }
        .SelectMany(node => new[] { -17, 0, 41 }.Select(seed => new TestCaseData(node, seed)));

    [TestCaseSource(nameof(PendingCases))]
    public void LegacyAndBoundSnapshotsResolveWithoutChangingAnyRunData(string node, int seed)
    {
        var (legacy, blueprint) = Fixture(node, seed);
        foreach (string mode in new[] { "legacy", "bound", "legacy-initial" })
        {
            bool bound = mode == "bound";
            PureRunState run = bound ? Bind(legacy, blueprint) : legacy;
            if (mode == "legacy-initial") run = Copy(run, adventure: run.AdventureState! with
            { Lifecycle = RunAdventureLifecycle.InitialExploration, BoardContentId = RunAdventureTransitionService.InitialBoardContentId });
            string before = JsonSerializer.Serialize(run);
            RunEncounterCheckpoint checkpoint = run.Checkpoint!;
            RunAdventureState adventure = run.AdventureState!;
            ulong random = Request(run).EffectiveRandomState;
            var result = PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint);
            Assert.That(result.Succeeded, Is.True, result.ErrorCode);
            Assert.Multiple(() =>
            {
                Assert.That(result.Identity!.NodeId, Is.EqualTo(node));
                Assert.That(result.Identity.TemplateId, Is.EqualTo(blueprint.Bindings[node].TemplateId));
                Assert.That(result.RequiresMigration, Is.EqualTo(!bound));
                Assert.That(result.BoardContentId!.Value.Value, Is.EqualTo(Board(node).Value));
                Assert.That(JsonSerializer.Serialize(run), Is.EqualTo(before));
                Assert.That(run.Checkpoint, Is.SameAs(checkpoint));
                Assert.That(run.AdventureState, Is.SameAs(adventure));
                Assert.That(run.Checkpoint!.Party, Is.SameAs(checkpoint.Party));
                Assert.That(Request(run).EffectiveRandomState, Is.EqualTo(random));
                if (bound) Assert.That(result.Identity, Is.SameAs(adventure.SceneIdentity));
            });
        }
    }

    [Test]
    public void ReusedN1EventsHaveDistinctLayerIdentitiesAndRetainLegacyEncounterIndex()
    {
        var (four, blueprint) = Fixture("layer_04_event");
        var (six, _) = Fixture("layer_06_event");
        Assert.That(four.Checkpoint!.EncounterContentId, Is.EqualTo(six.Checkpoint!.EncounterContentId));
        Assert.That(six.EncounterIndex, Is.Not.EqualTo(six.Checkpoint.EncounterIndex));
        Assert.That(PureRunNodeSceneProjector.ResolvePendingBattle(four, blueprint).Identity!.NodeId, Is.EqualTo("layer_04_event"));
        Assert.That(PureRunNodeSceneProjector.ResolvePendingBattle(six, blueprint).Identity!.NodeId, Is.EqualTo("layer_06_event"));
    }

    [TestCase("act")]
    [TestCase("version")]
    [TestCase("node")]
    [TestCase("template")]
    [TestCase("single-adventure")]
    [TestCase("single-checkpoint")]
    [TestCase("different-ends")]
    public void BoundIdentityCannotOverrideDefinitionOrPartialMetadata(string fault)
    {
        var (run, blueprint) = Fixture("layer_01_battle");
        RunNodeSceneIdentity good = Identity(blueprint, "layer_01_battle");
        RunNodeSceneIdentity wrong = fault switch
        {
            "act" => new(new ContentId("act-unknown"), 1, good.NodeId, good.TemplateId),
            "version" => new(good.ActId, 999, good.NodeId, good.TemplateId),
            "node" => new(good.ActId, 1, "unknown-node", good.TemplateId),
            _ => new(good.ActId, 1, good.NodeId, new ContentId("template.unknown"))
        };
        RunNodeSceneIdentity? adventure = fault == "single-checkpoint" ? null : fault == "single-adventure" ? good : wrong;
        RunNodeSceneIdentity? checkpoint = fault == "single-adventure" ? null : fault is "single-checkpoint" or "different-ends" ? good : wrong;
        Reject(Copy(run, adventure: run.AdventureState! with { SceneIdentity = adventure },
            checkpoint: run.Checkpoint! with { SceneIdentity = checkpoint }), blueprint);
    }

    [TestCase("selected")]
    [TestCase("pending")]
    [TestCase("key")]
    [TestCase("committed")]
    [TestCase("kind")]
    [TestCase("lifecycle")]
    [TestCase("phase")]
    [TestCase("event-node")]
    [TestCase("event-context")]
    [TestCase("assignment")]
    [TestCase("map-id")]
    [TestCase("map-version")]
    [TestCase("map-current")]
    [TestCase("wrong-layer")]
    [TestCase("missing-transaction")]
    public void EventResolutionRejectsConflictingPersistedEvidence(string fault)
    {
        var (run, blueprint) = Fixture("layer_04_event");
        PureRunMapState map = run.MapState!;
        RunNodeTransaction tx = run.NodeTransaction!;
        RunAdventureState adventure = run.AdventureState!;
        switch (fault)
        {
            case "selected": map = map with { SelectedNodeId = "layer_04_battle" }; break;
            case "pending": map = map with { PendingNodeId = null }; break;
            case "key": map = map with { PendingTransactionKey = "wrong" }; break;
            case "committed": tx = tx with { Committed = true }; break;
            case "kind": tx = tx with { Kind = PureRunNodeKind.Rest }; break;
            case "lifecycle": map = map with { NodeLifecycle = RunNodeLifecycle.Selected }; break;
            case "phase": map = map with { Phase = PureRunMapPhase.ChoosingLayerFour }; break;
            case "event-node": adventure = adventure with { PendingEventNodeId = "layer_06_event" }; break;
            case "event-context": adventure = adventure with { PendingEventContext = RunAdventureEventContextKind.None }; break;
            case "assignment": map = map with { MysteryEventAssignments = new Dictionary<string, string>() }; break;
            case "map-id": map = map with { MapContentId = new ContentId("run-map.unknown") }; break;
            case "map-version": map = map with { MapLayoutVersion = 999 }; break;
            case "map-current": map = map with { CurrentNodeId = "layer_06_event" }; break;
            case "wrong-layer": tx = tx with { NodeId = "layer_06_event" }; break;
        }
        Reject(Copy(run, adventure: adventure, map: map, transaction: tx, dropTransaction: fault == "missing-transaction"), blueprint);
    }

    [TestCase("layer_04_event", RunAdventureEventContextKind.CursedChestMimic)]
    [TestCase("layer_06_event", RunAdventureEventContextKind.FallenAltarGuardian)]
    public void LegacyHardCodedEventVariantIsExplicitlyUnsupportedWithoutRewritingTheSnapshot(
        string node, RunAdventureEventContextKind actualContext)
    {
        int seed = Enumerable.Range(0, 512).First(value =>
            PureRunActBlueprint.Resolve(DemoMap(), value).Bindings[node].EventContext != actualContext);
        var (run, blueprint) = Fixture(node, seed);
        // Mirrors the old Main's L4 cursed-chest / L6 altar trigger, including its actual event board.
        run = Copy(run, adventure: run.AdventureState! with
        {
            PendingEventContext = actualContext,
            BoardContentId = new ContentId($"adventure-board.pure-run.event.{actualContext.ToString().ToLowerInvariant()}")
        });
        var snapshot = new PureRunSaveSnapshot(run.Revision, run, null);
        string before = RunSaveDocumentV11.Encode(snapshot);
        RunEncounterCheckpoint checkpoint = run.Checkpoint!;
        RunAdventureState adventure = run.AdventureState!;
        ulong random = Request(run).EffectiveRandomState;
        var result = PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint);
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("scene.event_variant_incompatible"));
            Assert.That(result.Identity, Is.Null);
            Assert.That(result.BoardContentId, Is.Null);
            Assert.That(result.RequiresMigration, Is.False);
            Assert.That(snapshot.ActiveRun, Is.SameAs(run));
            Assert.That(run.Checkpoint, Is.SameAs(checkpoint));
            Assert.That(run.AdventureState, Is.SameAs(adventure));
            Assert.That(run.AdventureState!.PendingEventContext, Is.EqualTo(actualContext));
            Assert.That(Request(run).EffectiveRandomState, Is.EqualTo(random));
            Assert.That(RunSaveDocumentV11.Encode(snapshot), Is.EqualTo(before));
        });
    }

    [TestCase(RunAdventureEventContextKind.CursedChestMimic)]
    [TestCase(RunAdventureEventContextKind.FallenAltarGuardian)]
    [TestCase(RunAdventureEventContextKind.LostVillagerEscort)]
    public void EventObjectIdentityMustMatchItsContextExactly(RunAdventureEventContextKind context)
    {
        int seed = Enumerable.Range(-2048, 4097).First(value =>
            PureRunActBlueprint.Resolve(DemoMap(), value).Bindings["layer_04_event"].EventContext == context);
        var (source, blueprint) = Fixture("layer_04_event", seed);
        string expected = EventObjectId(context)!;

        var valid = PureRunNodeSceneProjector.ResolvePendingBattle(source, blueprint);
        Assert.That(valid.Succeeded, Is.True, valid.ErrorCode);
        foreach (string invalid in new[] { "wrong-event-object", expected.ToUpperInvariant(), $" {expected}", $"{expected} " })
        {
            PureRunState run = Copy(source, adventure: source.AdventureState! with { PendingEventObjectId = invalid });
            RunEncounterCheckpoint checkpoint = run.Checkpoint!;
            RunAdventureState adventure = run.AdventureState!;
            ulong random = Request(run).EffectiveRandomState;
            string before = JsonSerializer.Serialize(run);
            var result = PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint);
            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo("scene.event_conflict"));
                Assert.That(result.Identity, Is.Null);
                Assert.That(result.BoardContentId, Is.Null);
                Assert.That(run.Checkpoint, Is.SameAs(checkpoint));
                Assert.That(run.AdventureState, Is.SameAs(adventure));
                Assert.That(Request(run).EffectiveRandomState, Is.EqualTo(random));
                Assert.That(JsonSerializer.Serialize(run), Is.EqualTo(before));
            });
        }
    }

    [TestCase("all", "scene.event_state_incomplete")]
    [TestCase("context", "scene.event_conflict")]
    [TestCase("node", "scene.event_conflict")]
    [TestCase("object", "scene.event_conflict")]
    [TestCase("wrong-node", "scene.event_conflict")]
    public void MissingEventMarkersAreNotASeedDerivedMigration(string missing, string error)
    {
        var (run, blueprint) = Fixture("layer_04_event");
        RunAdventureState adventure = run.AdventureState!;
        adventure = adventure with
        {
            PendingEventContext = missing is "all" or "context" ? RunAdventureEventContextKind.None : adventure.PendingEventContext,
            PendingEventNodeId = missing is "all" or "node" ? null : missing == "wrong-node" ? "layer_06_event" : adventure.PendingEventNodeId,
            PendingEventObjectId = missing is "all" or "object" ? null : adventure.PendingEventObjectId
        };
        run = Copy(run, adventure: adventure);
        var result = PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint);
        Assert.That(result.ErrorCode, Is.EqualTo(error));
        Reject(run, blueprint);
    }

    [TestCase("layer_01_battle", "start")]
    [TestCase("layer_02_battle", "layer_01_battle")]
    [TestCase("layer_04_battle", "layer_03_battle")]
    [TestCase("layer_05_battle", "layer_04_rest")]
    [TestCase("layer_07_battle", "layer_06_rest")]
    public void OnlyLegacyMayRetainAnExplicitDirectPredecessorBoard(string node, string prior)
    {
        var (run, blueprint) = Fixture(node);
        run = Copy(run, adventure: run.AdventureState! with { BoardContentId = new ContentId(Board(prior).Value + ".resolved") });
        Assert.That(PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint).Succeeded, Is.True);
        Reject(Bind(run, blueprint), blueprint);
        Reject(Copy(run, adventure: run.AdventureState! with { BoardContentId = Board(node == "layer_07_battle" ? "layer_01_battle" : "layer_07_battle") }), blueprint);
    }

    [TestCase("layer_04_event")]
    [TestCase("layer_06_event")]
    public void ExplicitLegacyEventAliasUsesTransactionIdentityNotTheSharedEncounter(string node)
    {
        var (run, blueprint) = Fixture(node);
        string alias = $"adventure-board.pure-run.event.{blueprint.Bindings[node].EventContext.ToString().ToLowerInvariant()}";
        run = Copy(run, adventure: run.AdventureState! with { BoardContentId = new ContentId(alias) });
        var result = PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint);
        Assert.That(result.Succeeded, Is.True, result.ErrorCode);
        Assert.That(result.Identity!.NodeId, Is.EqualTo(node));
        Assert.That(result.BoardContentId, Is.EqualTo(Board(node)));
        var bound = PureRunNodeSceneProjector.ResolvePendingBattle(Bind(run, blueprint), blueprint);
        Assert.That(bound.Succeeded, Is.True, bound.ErrorCode);
        Assert.That(bound.RequiresMigration, Is.False);
    }

    [Test]
    public void ResolvedEventPredecessorNeedsMatchingCommittedTransactionAndMapCurrent()
    {
        var (run, blueprint) = Fixture("layer_05_battle");
        var selector = new PureRunMapService(blueprint.ActDefinition.Map);
        var selected = selector.BeginNode(selector.UnlockLayerFour(run.Seed), "layer_04_event");
        var committed = selector.CommitNode(selected.State, selected.Transaction!, []);
        string alias = $"adventure-board.pure-run.event.{blueprint.Bindings["layer_04_event"].EventContext.ToString().ToLowerInvariant()}.resolved";
        run = Copy(run, map: committed.State, transaction: committed.Transaction,
            adventure: run.AdventureState! with { BoardContentId = new ContentId(alias) });
        Assert.That(PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint).Succeeded, Is.True);
        Reject(Bind(run, blueprint), blueprint);
        Reject(Copy(run, transaction: run.NodeTransaction! with { NodeId = "layer_04_rest" }), blueprint);
    }

    [TestCase("layer_05_battle", "layer_04_event")]
    [TestCase("layer_07_battle", "layer_06_event")]
    public void LegacyResolvedActualEventAliasMayDifferFromTheDefaultPredecessorVariant(string node, string predecessor)
    {
        var (run, blueprint) = Fixture(node);
        var selector = new PureRunMapService(DemoMap());
        var available = selector.UnlockLayerFour(run.Seed);
        if (predecessor == "layer_06_event") available = selector.UnlockLayerSix(available, run.Seed);
        var selected = selector.BeginNode(available, predecessor);
        var committed = selector.CommitNode(selected.State, selected.Transaction!, []);
        var variant = PureRunActBlueprint.EnumerateContentVariants(DemoMap()).First(value =>
            value.NodeId == predecessor && value.EventContext != blueprint.Bindings[predecessor].EventContext);
        run = Copy(run, map: committed.State, transaction: committed.Transaction, adventure: run.AdventureState! with
        { BoardContentId = new ContentId($"adventure-board.pure-run.event.{variant.EventContext.ToString().ToLowerInvariant()}.resolved") });
        var result = PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint);
        Assert.That(result.Succeeded, Is.True, result.ErrorCode);
        Assert.That(result.Identity!.NodeId, Is.EqualTo(node));
        Assert.That(blueprint.PersistedSceneIdentities, Is.Empty);
        Reject(Bind(run, blueprint), blueprint);
        Reject(Copy(run, adventure: run.AdventureState! with { BoardContentId = new ContentId("adventure-board.pure-run.event.unknown.resolved") }), blueprint);
    }

    [Test]
    public void LegacyInitialIsUnknownSceneMetadataButCannotCarryAnotherBoardOrBoundIdentity()
    {
        var (run, blueprint) = Fixture("layer_01_battle");
        RunAdventureState initial = new RunAdventureTransitionService().CreateInitial(run.Party);
        run = Copy(run, adventure: initial);
        Assert.That(PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint).RequiresMigration, Is.True);
        Reject(Bind(run, blueprint), blueprint);
        var (later, _) = Fixture("layer_02_battle");
        Assert.That(PureRunNodeSceneProjector.ResolvePendingBattle(Copy(later, adventure: initial), blueprint).Succeeded, Is.True);
        Reject(Copy(later, adventure: initial with { BoardContentId = Board("layer_01_battle") }), blueprint);
    }

    [Test]
    public void RealLegacySessionRetainsInitialAcrossThreeOrdinaryPendingCheckpoints()
    {
        var store = new MemoryStore();
        var definition = new PureRunDefinition(new ContentId("run.pure-run.three-encounter-v1"),
            new[] { "n1", "n2", "n3" }.Select(id => new ContentId($"encounter.pure-run.{id}")),
            [new("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5,5,5,6,5,5)),
             new("necro", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5,5,5,5,6,5)),
             new("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5,6,5,5,5,5))]);
        var service = new PureRunSessionService(definition, store);
        Assert.That(service.StartNewRun(41).Succeeded, Is.True);
        PureRunActBlueprint blueprint = PureRunActBlueprint.Resolve(DemoMap(), 41);
        for (int index = 0; index < 3; index++)
        {
            Assert.That(service.BeginEncounter().Succeeded, Is.True);
            PureRunState pending = store.Snapshot.ActiveRun!;
            Assert.That(pending.AdventureState!.Lifecycle, Is.EqualTo(RunAdventureLifecycle.InitialExploration));
            Assert.That(pending.BattlesCompleted, Is.EqualTo(index));
            int writes = store.SaveCount;
            RunEncounterCheckpoint checkpoint = pending.Checkpoint!;
            var projected = PureRunNodeSceneProjector.ResolvePendingBattle(pending, blueprint);
            Assert.That(projected.Succeeded, Is.True, projected.ErrorCode);
            Assert.That(projected.Identity!.NodeId, Is.EqualTo($"layer_{index + 1:00}_battle"));
            Assert.That(pending.Checkpoint, Is.SameAs(checkpoint));
            Assert.That(store.SaveCount, Is.EqualTo(writes));
            if (index == 2) break;
            var victory = new PureRunBattleResult(pending.RunId, checkpoint.Revision, pending.EncounterContentId,
                true, 3, 3, pending.Party.Select(member => new BattlePartyResult(member.CharacterId,
                    member.MaxHealth, member.MaxMana, false, member.CarriedConsumables)).ToArray());
            Assert.That(service.ApplyBattleResult(victory).Succeeded, Is.True);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RealDirectLateBattleApiRetainsInitialWithoutOverridingTheCheckpoint(bool boss)
    {
        string node = boss ? "layer_07_battle" : "layer_05_battle";
        var (run, blueprint) = Fixture(node);
        PureRunState ready = Copy(run, phase: boss ? PureRunPhase.ReadyForBoss : PureRunPhase.ReadyForLayerFive,
            dropCheckpoint: true, adventure: run.AdventureState! with
            { Lifecycle = RunAdventureLifecycle.InitialExploration, BoardContentId = RunAdventureTransitionService.InitialBoardContentId });
        var service = new PureRunFullRunService();
        FullRunTransitionResult started = boss ? service.BeginBoss(ready, blueprint.ActDefinition.Map)
            : service.BeginLayerFive(ready, blueprint.ActDefinition.Map);
        Assert.That(started.Succeeded, Is.True, started.RejectionCode);
        Assert.That(started.State.AdventureState, Is.SameAs(ready.AdventureState));
        var projected = PureRunNodeSceneProjector.ResolvePendingBattle(started.State, blueprint);
        Assert.That(projected.Succeeded, Is.True, projected.ErrorCode);
        Assert.That(projected.Identity!.NodeId, Is.EqualTo(node));
        Assert.That(projected.RequiresMigration, Is.True);
    }

    [TestCase("missing-checkpoint")]
    [TestCase("missing-adventure")]
    [TestCase("ready")]
    [TestCase("encounter")]
    [TestCase("binding-encounter")]
    [TestCase("index")]
    [TestCase("revision")]
    [TestCase("zero-checkpoint-revision")]
    [TestCase("negative-adventure-revision")]
    [TestCase("unknown-board")]
    [TestCase("uncommitted-transaction")]
    [TestCase("pending-event")]
    public void FixedCheckpointRejectsMissingOrConflictingEvidence(string fault)
    {
        var (run, blueprint) = Fixture("layer_01_battle");
        RunAdventureState adventure = run.AdventureState!;
        RunEncounterCheckpoint checkpoint = run.Checkpoint!;
        if (fault is "encounter" or "binding-encounter") checkpoint = checkpoint with { EncounterContentId = new ContentId("encounter.wrong") };
        if (fault == "index") checkpoint = checkpoint with { EncounterIndex = 99 };
        if (fault == "revision") checkpoint = checkpoint with { Revision = run.Revision + 1 };
        if (fault == "zero-checkpoint-revision") checkpoint = checkpoint with { Revision = 0 };
        if (fault == "negative-adventure-revision") adventure = adventure with { Revision = -1 };
        if (fault == "unknown-board") adventure = adventure with { BoardContentId = new ContentId("adventure-board.unknown") };
        if (fault == "pending-event") adventure = adventure with { PendingEventNodeId = "layer_04_event" };
        run = Copy(run, adventure: adventure, checkpoint: checkpoint,
            phase: fault == "ready" ? PureRunPhase.Ready : null,
            transaction: fault == "uncommitted-transaction" ? new("pending", "layer_04_event", PureRunNodeKind.Mystery) : null,
            dropCheckpoint: fault == "missing-checkpoint", dropAdventure: fault == "missing-adventure",
            encounter: fault == "binding-encounter" ? checkpoint.EncounterContentId : null);
        Reject(run, blueprint);
    }

    [Test]
    public void WrongSeedBlueprintCannotBeUsedEvenForTheSharedFirstEncounter()
    {
        var (run, blueprint) = Fixture("layer_01_battle", 41);
        PureRunActBlueprint other = Enumerable.Range(42, 100).Select(seed => PureRunActBlueprint.Resolve(blueprint.ActDefinition.Map, seed))
            .First(candidate => candidate.Bindings.Any(pair => pair.Value != blueprint.Bindings[pair.Key]));
        Reject(run, other);
    }

    [Test]
    public void CurrentPendingCannotAuthorizeAnOverrideForAnotherNode()
    {
        var (run, defaults) = Fixture("layer_04_event");
        PureRunActBlueprint restored = PureRunActBlueprint.Restore(DemoMap(), run.Seed,
            [Identity(defaults, "layer_06_event")]);
        var result = PureRunNodeSceneProjector.ResolvePendingBattle(run, restored);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("scene.override_unproven"));
    }

    private static void Reject(PureRunState run, PureRunActBlueprint blueprint)
    {
        var result = PureRunNodeSceneProjector.ResolvePendingBattle(run, blueprint);
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Empty);
            Assert.That(result.Identity, Is.Null);
            Assert.That(result.BoardContentId, Is.Null);
            Assert.That(result.RequiresMigration, Is.False);
        });
    }

    internal static (PureRunState, PureRunActBlueprint) Fixture(string nodeId, int seed = 41)
    {
        PureRunMapDefinition mapDefinition = DemoMap();
        PureRunActBlueprint blueprint = PureRunActBlueprint.Resolve(mapDefinition, seed);
        PureRunNodeContentBinding binding = blueprint.Bindings[nodeId];
        int index = mapDefinition.Nodes.Single(node => node.NodeId == nodeId).Layer - 1;
        var party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(id,
            new ContentId($"unit.{id}"), 1, new UnitAttributes(5, 5, 5, 5, 5, 5), 20, 20, 10, 10, false,
            [new ContentId($"skill.{id}.lv1")])).ToArray();
        PureRunMapState? map = null;
        RunNodeTransaction? transaction = null;
        var selector = new PureRunMapService(mapDefinition);
        if (index >= 3)
        {
            map = selector.UnlockLayerFour(seed);
            if (index >= 5) map = selector.UnlockLayerSix(map, seed);
            string selected = index is 4 or 6 ? $"layer_{index:00}_rest" : nodeId;
            PureRunMapResult selection = selector.BeginNode(map, selected);
            map = selection.State;
            transaction = selection.Transaction!;
            if (index is 4 or 6)
            {
                PureRunMapResult committed = selector.CommitNode(map, transaction, []);
                map = committed.State;
                transaction = committed.Transaction;
            }
            else map = map with { NodeLifecycle = RunNodeLifecycle.Pending };
        }
        RunAdventureState adventure = new RunAdventureTransitionService().CreateInitial(party) with
        {
            Lifecycle = RunAdventureLifecycle.MapActive, BoardContentId = Board(nodeId), Revision = 9,
            PendingEventContext = binding.EventContext,
            PendingEventNodeId = binding.EventContext == RunAdventureEventContextKind.None ? null : nodeId,
            PendingEventObjectId = EventObjectId(binding.EventContext)
        };
        var checkpoint = new RunEncounterCheckpoint(binding.EncounterId!.Value, index, 30, party, [], []);
        return (new PureRunState("run-test", seed, 31, PureRunPhase.PendingBattle,
            index is 3 or 5 ? index - 1 : index, binding.EncounterId.Value, party,
            checkpoint: checkpoint, mapState: map, nodeTransaction: transaction,
            escortState: binding.EventContext == RunAdventureEventContextKind.LostVillagerEscort
                ? new RunEscortState("escort-test", RunEscortLifecycle.BattlePending, true, "start", nodeId, 4) : null,
            adventureState: adventure), blueprint);
    }

    private static PureRunState Bind(PureRunState run, PureRunActBlueprint blueprint)
    {
        string node = run.Checkpoint!.EncounterIndex is 3 or 5 ? run.NodeTransaction!.NodeId
            : $"layer_{run.Checkpoint.EncounterIndex + 1:00}_battle";
        RunNodeSceneIdentity identity = Identity(blueprint, node);
        return Copy(run, adventure: run.AdventureState! with { SceneIdentity = identity },
            checkpoint: run.Checkpoint! with { SceneIdentity = identity });
    }

    private static RunNodeSceneIdentity Identity(PureRunActBlueprint blueprint, string node) =>
        new(blueprint.ActDefinition.ActId, blueprint.ActDefinition.DefinitionVersion, node, blueprint.Bindings[node].TemplateId);
    private static string? EventObjectId(RunAdventureEventContextKind context) => context switch
    {
        RunAdventureEventContextKind.CursedChestMimic => "cursed-chest",
        RunAdventureEventContextKind.FallenAltarGuardian => "fallen-altar",
        RunAdventureEventContextKind.LostVillagerEscort => "lost-villager",
        _ => null
    };
    private static ContentId Board(string node) => new($"adventure-board.pure-run.node.{node.Replace('_', '-')}");
    private static EncounterRequest Request(PureRunState run) => new(run.RunId, run.Checkpoint!.Revision,
        run.Checkpoint.EncounterContentId, run.Checkpoint.Party, run.AdventureState!.Revision);
    internal static PureRunState Copy(PureRunState run, RunAdventureState? adventure = null,
        RunEncounterCheckpoint? checkpoint = null, PureRunMapState? map = null, RunNodeTransaction? transaction = null,
        PureRunPhase? phase = null, bool dropCheckpoint = false, bool dropAdventure = false, bool dropTransaction = false,
        ContentId? encounter = null, RunEscortState? escort = null, bool dropEscort = false) =>
        new(run.RunId, run.Seed, run.Revision, phase ?? run.Phase, run.EncounterIndex, encounter ?? run.EncounterContentId,
            run.Party, run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression, run.AppliedTransactionKeys,
            run.Gold, run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems,
            dropCheckpoint ? null : checkpoint ?? run.Checkpoint, map ?? run.MapState,
            dropTransaction ? null : transaction ?? run.NodeTransaction, dropEscort ? null : escort ?? run.EscortState,
            dropAdventure ? null : adventure ?? run.AdventureState);

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

    // Same canonical 16-node fixture as PureRunActBlueprintTests / MapTreasureAssetFactory; no Godot dependency.
    internal static PureRunMapDefinition DemoMap()
    {
        var nodes = new List<PureRunMapNodeDefinition>
        {
            new("start", 0, PureRunNodeKind.Battle, new ContentId("run.pure-run.three-encounter-v1")),
            new("layer_01_battle", 1, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n1")),
            new("layer_02_battle", 2, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n2")),
            new("layer_03_battle", 3, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n3")),
            new("layer_05_battle", 5, PureRunNodeKind.Elite, new ContentId("encounter.pure-run.e1")),
            new("layer_07_battle", 7, PureRunNodeKind.Boss, new ContentId("encounter.pure-run.special"))
        };
        var edges = new List<PureRunMapConnectionDefinition>
        { new("start", "layer_01_battle"), new("layer_01_battle", "layer_02_battle"), new("layer_02_battle", "layer_03_battle") };
        foreach (int layer in new[] { 4, 6 })
        {
            nodes.Add(new($"layer_{layer:00}_battle", layer, PureRunNodeKind.Battle, new ContentId(layer == 4 ? "encounter.pure-run.n4" : "encounter.pure-run.e1")));
            nodes.Add(new($"layer_{layer:00}_rest", layer, PureRunNodeKind.Rest, new ContentId("rest.pure-run.standard-v1")));
            nodes.Add(new($"layer_{layer:00}_store", layer, PureRunNodeKind.Store, new ContentId("store.pure-run.standard-v1")));
            nodes.Add(new($"layer_{layer:00}_event", layer, PureRunNodeKind.Mystery, new ContentId("event.pure-run.cursed-chest")));
            nodes.Add(new($"layer_{layer:00}_treasure", layer, PureRunNodeKind.Treasure, new ContentId("treasure.pure-run.standard-v1")));
        }
        foreach (string suffix in new[] { "battle", "rest", "store", "event", "treasure" })
        {
            edges.Add(new("layer_03_battle", $"layer_04_{suffix}")); edges.Add(new($"layer_04_{suffix}", "layer_05_battle"));
            edges.Add(new("layer_05_battle", $"layer_06_{suffix}")); edges.Add(new($"layer_06_{suffix}", "layer_07_battle"));
        }
        return new(new ContentId("run-map.pure-run.layer4-v1"), 3, nodes, edges);
    }
}
