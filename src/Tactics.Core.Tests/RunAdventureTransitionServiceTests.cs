using NUnit.Framework;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class RunAdventureTransitionServiceTests
{
    [Test]
    public void ImmediateExitAndEventTransitionsArePersistedAndMonotonic()
    {
        var service = new RunAdventureTransitionService();
        PureRunState run = Run(service);
        PureRunMapDefinition map = Map();
        run = service.CommitExit(run, map, "layer_01_battle");
        run = service.EnterBoard(run, new ContentId("adventure-board.node.layer-01-battle"));
        run = service.BeginEventBattle(run, RunAdventureEventContextKind.CursedChestMimic, "node-event", "cursed-chest");

        long pendingRevision = run.Revision;
        Assert.That(run.AdventureState!.PendingEventContext, Is.EqualTo(RunAdventureEventContextKind.CursedChestMimic));
        run = service.ResolveEventBattle(run);
        Assert.Multiple(() =>
        {
            Assert.That(run.Revision, Is.GreaterThan(pendingRevision));
            Assert.That(run.AdventureState!.PendingEventContext, Is.EqualTo(RunAdventureEventContextKind.None));
            Assert.That(run.AdventureState.PendingEventNodeId, Is.Null);
            Assert.That(run.AdventureState.PendingEventObjectId, Is.Null);
        });
        Assert.That(service.ResolveEventBattle(run), Is.SameAs(run));
    }

    [Test]
    public void ExitOnlyAcceptsImmediateSuccessorAndRequiresResolvedNode()
    {
        var service = new RunAdventureTransitionService();
        PureRunState run = Run(service);
        PureRunMapDefinition map = Map();
        Assert.Throws<InvalidOperationException>(() => service.CommitExit(run, map, "layer_02_battle"));
        PureRunState entered = service.EnterBoard(service.CommitExit(run, map, "layer_01_battle"),
            new ContentId("adventure-board.node.layer-01-battle"));
        Assert.Throws<InvalidOperationException>(() => service.CommitExit(entered, map, "layer_02_battle"));
        PureRunState resolved = service.ResolveBoard(entered);
        Assert.DoesNotThrow(() => service.CommitExit(resolved, map, "layer_02_battle"));
    }

    [Test]
    public void LeaderMovementTreatsIdlePartyAsObstacles()
    {
        var service = new RunAdventureTransitionService();
        PureRunState run = Run(service);
        AdventureBoardDefinition board = Board(run.AdventureState!);
        Assert.Throws<InvalidOperationException>(() => service.MoveLeader(run, board, new GridPoint(1, 4)));
        PureRunState moved = service.MoveLeader(run, board, new GridPoint(3, 5));
        Assert.That(moved.AdventureState!.ActorCells.Single(value => value.ActorId == "mage").Cell,
            Is.EqualTo(new GridPoint(3, 5)));
    }

    [Test]
    public void MovingLeaderToCurrentCellIsNoOpWithoutRevisionChange()
    {
        var service = new RunAdventureTransitionService();
        PureRunState run = Run(service);
        AdventureBoardDefinition board = Board(run.AdventureState!);
        GridPoint current = run.AdventureState!.ActorCells.Single(value => value.ActorId == run.AdventureState.LeaderId).Cell;

        PureRunState unchanged = service.MoveLeader(run, board, current);

        Assert.Multiple(() =>
        {
            Assert.That(unchanged, Is.SameAs(run));
            Assert.That(unchanged.Revision, Is.EqualTo(run.Revision));
            Assert.That(unchanged.AdventureState!.InteractionRevision,
                Is.EqualTo(run.AdventureState.InteractionRevision));
        });
    }

    [Test]
    public void ExplorationSessionRestoresSaveStrippedCellsAndHydratesOneFinalTransition()
    {
        var service = new RunAdventureTransitionService();
        PureRunState source = Run(service);
        RunAdventureState strippedAdventure = source.AdventureState! with { ActorCells = Array.Empty<RunAdventureActorCell>() };
        var stripped = new PureRunState(source.RunId, source.Seed, source.Revision, source.Phase, source.EncounterIndex,
            source.EncounterContentId, source.Party, source.BackpackConsumables, source.BackpackEquipment,
            source.PendingProgression, source.AppliedTransactionKeys, source.Gold, source.BattlesCompleted,
            source.EnemiesDefeated, source.AcquiredItems, source.Checkpoint, source.MapState, source.NodeTransaction,
            source.EscortState, strippedAdventure);
        AdventureExplorationSession session = AdventureExplorationSession.Restore(stripped,
            [new GridPoint(2, 5), new GridPoint(1, 4), new GridPoint(1, 6)]);

        session.SetLeader("necro");
        session.SetCell("necro", new GridPoint(4, 5));
        PureRunState hydrated = session.Hydrate(stripped);
        var otherRun = new PureRunState("other-run", stripped.Seed, stripped.Revision, stripped.Phase, stripped.EncounterIndex,
            stripped.EncounterContentId, stripped.Party, stripped.BackpackConsumables, stripped.BackpackEquipment,
            stripped.PendingProgression, stripped.AppliedTransactionKeys, stripped.Gold, stripped.BattlesCompleted,
            stripped.EnemiesDefeated, stripped.AcquiredItems, stripped.Checkpoint, stripped.MapState, stripped.NodeTransaction,
            stripped.EscortState, stripped.AdventureState);

        Assert.Multiple(() =>
        {
            Assert.That(session.Matches(stripped), Is.True);
            Assert.That(session.Matches(otherRun), Is.False);
            Assert.That(hydrated.AdventureState!.LeaderId, Is.EqualTo("necro"));
            Assert.That(hydrated.AdventureState.ActorCells.Single(value => value.ActorId == "necro").Cell,
                Is.EqualTo(new GridPoint(4, 5)));
            Assert.That(stripped.AdventureState!.ActorCells, Is.Empty);
        });
    }

    private static PureRunState Run(RunAdventureTransitionService service)
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        RunCharacterState[] party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(
            id, new ContentId("unit." + id), 1, attributes, 20, 20, 10, 10, false, [new ContentId("skill." + id)])).ToArray();
        return new PureRunState("run", 7, 1, PureRunPhase.Ready, 0, new ContentId("encounter.n1"), party,
            adventureState: service.CreateInitial(party));
    }

    private static AdventureBoardDefinition Board(RunAdventureState state) => new(
        state.BoardContentId, 10, 10, [], [], state.ActorCells.Select(value => new AdventureActorPlacement(value.ActorId, value.Cell)).ToArray(),
        new GridPoint(0, 0), new GridPoint(9, 9));

    private static PureRunMapDefinition Map() => new(new ContentId("map"), 2,
    [
        new("start", 0, PureRunNodeKind.Rest, new ContentId("start")),
        new("layer_01_battle", 1, PureRunNodeKind.Battle, new ContentId("n1")),
        new("layer_02_battle", 2, PureRunNodeKind.Battle, new ContentId("n2"))
    ],
    [
        new("start", "layer_01_battle"),
        new("layer_01_battle", "layer_02_battle")
    ]);
}
