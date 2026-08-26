using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV11Tests
{
    [Test]
    public void RoundTripPreservesNodeLevelFactsAndOmitsSessionAndCameraState()
    {
        PureRunState run = Run("mage");
        string json = RunSaveDocumentV11.Encode(new PureRunSaveSnapshot(run.Revision, run, null));
        RunSaveDecodeResultV11 decoded = RunSaveDocumentV11.Decode(json);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain("actorCells").IgnoreCase);
            Assert.That(json, Does.Not.Contain("camera").IgnoreCase);
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.Snapshot!.ActiveRun!.AdventureState!.LeaderId, Is.EqualTo("mage"));
            Assert.That(decoded.Snapshot.ActiveRun.AdventureState.ActorCells, Is.Empty);
            Assert.That(decoded.Snapshot.ActiveRun.MapState!.NodeIntelStates!["next"],
                Is.EqualTo(PureRunNodeIntelState.TacticalPreview));
            Assert.That(decoded.Snapshot.ActiveRun.MapState.CurrentNodeId, Is.EqualTo("start"));
            Assert.That(decoded.Snapshot.ActiveRun.MapState.ReachableNodeIds, Is.EqualTo(new[] { "next" }));
            Assert.That(decoded.Snapshot.ActiveRun.MapState.VisitedNodeIds, Is.EqualTo(new[] { "start" }));
            Assert.That(decoded.Snapshot.ActiveRun.MapState.TreasureResolution!.Confirmed, Is.True);
            Assert.That(decoded.Snapshot.ActiveRun.Checkpoint!.EncounterContentId,
                Is.EqualTo(new ContentId("encounter.n1")));
        });
    }

    [Test]
    public void V10UpgradeDropsActorCellsAndFallsBackFromIllegalLeader()
    {
        PureRunState source = Run("missing");
        RunAdventureState legacyAdventure = source.AdventureState! with
        {
            ActorCells = [new("missing", new GridPoint(9, 9)), new("mage", new GridPoint(8, 8))]
        };
        PureRunState legacy = CopyWithAdventure(source, legacyAdventure);

        RunSaveDecodeResultV11 decoded = RunSaveDocumentV11.Decode(
            RunSaveDocumentV10.Encode(new PureRunSaveSnapshot(legacy.Revision, legacy, null)));
        AdventureExplorationSession restored = AdventureExplorationSession.Restore(decoded.Snapshot!.ActiveRun!, Template());

        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.MigratedFromSchema, Is.EqualTo(10));
            Assert.That(decoded.Snapshot!.ActiveRun!.AdventureState!.ActorCells, Is.Empty);
            Assert.That(decoded.Snapshot.ActiveRun.AdventureState.LeaderId, Is.EqualTo("mage"));
            Assert.That(restored.ActorCells.Select(value => value.Cell),
                Is.EqualTo(Template().PartyEntrySlots.Select(value => value.Cell)));
        });
    }

    [Test]
    public void DecodeRejectsV11PayloadTampering()
    {
        string encoded = RunSaveDocumentV11.Encode(new PureRunSaveSnapshot(4, Run("mage"), null));
        string tampered = encoded.Replace("encounter.n1", "encounter.n2", StringComparison.Ordinal);

        RunSaveDecodeResultV11 decoded = RunSaveDocumentV11.Decode(tampered);

        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.False);
            Assert.That(decoded.ErrorCode, Is.EqualTo("save.payload_hash_mismatch"));
            Assert.That(decoded.Snapshot, Is.Null);
        });
    }

    [Test]
    public void ExplorationSessionRestoresTemplateSlotsWithoutChangingSaveState()
    {
        PureRunState run = Run("mage");
        AdventureMapTemplateDefinition template = Template();

        AdventureExplorationSession session = AdventureExplorationSession.Restore(run, template);

        Assert.That(session.ActorCells.Select(value => value.Cell),
            Is.EqualTo(template.PartyEntrySlots.Select(value => value.Cell)));
        Assert.That(run.AdventureState!.ActorCells, Is.Empty);
    }

    private static PureRunState Run(string leader)
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        RunCharacterState[] party = new[] { "mage", "necro", "amazon" }.Select((id, index) => new RunCharacterState(
            id, new ContentId("unit." + id), 1, attributes, index == 1 ? 0 : 20, 20, 10, 10, index == 1,
            [new ContentId("skill." + id)])).ToArray();
        var checkpoint = new RunEncounterCheckpoint(new ContentId("encounter.n1"), 0, 3, party, [], []);
        var map = new PureRunMapState(PureRunMapPhase.ResolvingNode, "start", ["next"], ["start"],
            new Dictionary<string, string>(), NodeIntelStates: new Dictionary<string, PureRunNodeIntelState>
            {
                ["start"] = PureRunNodeIntelState.Completed,
                ["next"] = PureRunNodeIntelState.TacticalPreview
            }, TreasureResolution: new("start", 3, null, null, null, "mage", true));
        var adventure = new RunAdventureState(RunAdventureLifecycle.MapActive, new ContentId("adventure-map.test"), leader,
            [], RunAdventureEventContextKind.None, null, null, 2, 1, 1, 1, 1);
        return new PureRunState("run", 7, 4, PureRunPhase.Ready, 0, new ContentId("encounter.n1"), party,
            checkpoint: checkpoint, mapState: map, adventureState: adventure);
    }

    private static AdventureMapTemplateDefinition Template() => new(new ContentId("template.test"),
        new(new ContentId("board.test"), 10, 10, [], [], [], new(1, 5), new(8, 5)),
        [new("candidate", new(2, 2))],
        [new("party-1", new(1, 4)), new("party-2", new(1, 5)), new("party-3", new(1, 6))],
        [new("player", new(2, 4))], [new("enemy", new(7, 4))],
        [new("entry", new(1, 5))], [new("exit", new(8, 5), "next", "entry")],
        [new("connection", new(9, 5))], new("camera", new(5, 5)), new("bounds", new(9, 9)),
        AdventureMapStateLayers.Required);

    private static PureRunState CopyWithAdventure(PureRunState run, RunAdventureState adventure) => new(
        run.RunId, run.Seed, run.Revision, run.Phase, run.EncounterIndex, run.EncounterContentId, run.Party,
        run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression, run.AppliedTransactionKeys,
        run.Gold, run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems, run.Checkpoint, run.MapState,
        run.NodeTransaction, run.EscortState, adventure);
}
