using NUnit.Framework;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Pathfinding;
using Tactics.Core.Runs;

namespace Tactics.Core.Tests;

[TestFixture]
public sealed class AdventureBoardDefinitionTests
{
    [Test]
    public void FixedBoardValidatesAndFindsDeterministicPathAroundObjects()
    {
        AdventureBoardDefinition board = Board();

        board.Validate();
        IReadOnlyList<GridPoint> path = AdventureBoardPathfinder.FindPath(board, new GridPoint(1, 5), new GridPoint(8, 5));

        Assert.That(path.First(), Is.EqualTo(new GridPoint(1, 5)));
        Assert.That(path.Last(), Is.EqualTo(new GridPoint(8, 5)));
        Assert.That(path, Does.Not.Contain(new GridPoint(4, 5)));
        Assert.That(path, Is.EqualTo(AdventureBoardPathfinder.FindPath(board, new GridPoint(1, 5), new GridPoint(8, 5))));
    }

    [Test]
    public void FindsTheShortestStableReachableApproachCell()
    {
        AdventureBoardDefinition board = Board();

        AdventureApproachRoute? direct = AdventureBoardPathfinder.FindApproach(board, new GridPoint(5, 5), new GridPoint(8, 5));
        AdventureApproachRoute? occupied = AdventureBoardPathfinder.FindApproach(board, new GridPoint(5, 5), new GridPoint(8, 5), [new GridPoint(7, 5)]);

        Assert.That(direct?.Destination, Is.EqualTo(new GridPoint(7, 5)));
        Assert.That(direct?.Path.Last(), Is.EqualTo(new GridPoint(7, 5)));
        Assert.That(occupied?.Destination, Is.EqualTo(new GridPoint(8, 6)));
    }

    [Test]
    public void RejectsWrongSizeAndBlockedExit()
    {
        AdventureBoardDefinition wrongSize = Board() with { Width = 9 };
        AdventureBoardDefinition blockedExit = Board() with { BlockedCells = [new GridPoint(8, 5)] };

        Assert.Throws<ArgumentException>(() => wrongSize.Validate());
        Assert.Throws<ArgumentException>(() => blockedExit.Validate());
    }

    [Test]
    public void RejectsActorOverlapBlockedCellsAndOccupiedEntry()
    {
        AdventureBoardDefinition board = Board();
        Assert.Throws<ArgumentException>(() => (board with
        {
            Actors = [new("a", new GridPoint(2, 5)), new("b", new GridPoint(2, 5))]
        }).Validate());
        Assert.Throws<ArgumentException>(() => (board with
        {
            Actors = [new("a", new GridPoint(4, 5))]
        }).Validate());
        Assert.Throws<ArgumentException>(() => (board with
        {
            Actors = [new("a", board.EntryCell)]
        }).Validate());
    }

    [Test]
    public void OccupiedPartyCellsBlockTraversalAndDestination()
    {
        AdventureBoardDefinition board = Board();
        GridPoint teammate = new(3, 5);
        IReadOnlyList<GridPoint> path = AdventureBoardPathfinder.FindPath(board, new GridPoint(2, 5),
            new GridPoint(5, 5), [teammate]);
        Assert.That(path, Does.Not.Contain(teammate));
        Assert.That(AdventureBoardPathfinder.FindPath(board, new GridPoint(2, 5), teammate, [teammate]), Is.Empty);
    }

    [Test]
    public void UniformAdventurePolicyMatchesBattlePathAndKeepsPublicOrigin()
    {
        AdventureBoardDefinition board = Board();
        GridPoint origin = new(1, 5);
        GridPoint destination = new(8, 5);

        IReadOnlyList<GridPoint> adventure = AdventureBoardPathfinder.FindPath(board, origin, destination);
        IReadOnlyList<GridPoint> battle = new DeterministicDijkstraPathfinder().FindPath(
            BattleBoard(board), origin, destination);

        Assert.Multiple(() =>
        {
            Assert.That(adventure[0], Is.EqualTo(origin));
            Assert.That(adventure.Skip(1), Is.EqualTo(battle));
            Assert.That(AdventureBoardPathfinder.FindPath(board, origin, origin), Is.EqualTo(new[] { origin }));
        });
    }

    [Test]
    public void EqualCostTieIsStableAndInitiallyMatchesBattleNeighbourPolicy()
    {
        AdventureBoardDefinition board = Board() with { Objects = Array.Empty<AdventureBoardObject>() };
        GridPoint origin = new(4, 4);
        GridPoint destination = new(5, 5);

        IReadOnlyList<GridPoint> first = AdventureBoardPathfinder.FindPath(board, origin, destination);
        IReadOnlyList<GridPoint> replay = AdventureBoardPathfinder.FindPath(board, origin, destination);
        IReadOnlyList<GridPoint> battle = new DeterministicDijkstraPathfinder().FindPath(
            BattleBoard(board), origin, destination);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(replay));
            Assert.That(first.Skip(1), Is.EqualTo(battle));
            Assert.That(first.Count, Is.EqualTo(3));
        });
    }

    [Test]
    public void BattleAndAdventurePoliciesCanDivergeWithoutChangingSearchMechanics()
    {
        AdventureBoardDefinition board = Board() with { Objects = Array.Empty<AdventureBoardObject>() };
        GridPoint origin = new(1, 1);
        GridPoint destination = new(3, 1);
        GridPoint battleOnlyObstacle = new(2, 1);
        BoardSnapshot battleBoard = BattleBoard(board, battleOnlyObstacle);

        IReadOnlyList<GridPoint> adventure = AdventureBoardPathfinder.FindPath(board, origin, destination);
        IReadOnlyList<GridPoint> battle = new DeterministicDijkstraPathfinder().FindPath(
            battleBoard, origin, destination);

        Assert.Multiple(() =>
        {
            Assert.That(adventure, Does.Contain(battleOnlyObstacle));
            Assert.That(battle, Does.Not.Contain(battleOnlyObstacle));
            Assert.That(adventure.Skip(1), Is.Not.EqualTo(battle));
        });
    }

    private static BoardSnapshot BattleBoard(AdventureBoardDefinition board, GridPoint? absoluteObstacle = null)
    {
        var cells = Enumerable.Range(0, board.Width)
            .SelectMany(x => Enumerable.Range(0, board.Height).Select(y => new GridPoint(x, y)))
            .ToDictionary(point => point, point => new CellState(blocksMovement: !board.IsWalkable(point)));
        if (absoluteObstacle is GridPoint obstacle)
            cells[obstacle] = new CellState(obstacle: MovementObstacleKind.Absolute);
        return new BoardSnapshot(cells);
    }

    private static AdventureBoardDefinition Board() => new(
        new ContentId("adventure-board.test.camp"), 10, 10,
        Enumerable.Range(0, 10).SelectMany(value => new[] { new GridPoint(value, 0), new GridPoint(value, 9) })
            .Concat(Enumerable.Range(1, 8).SelectMany(value => new[] { new GridPoint(0, value), new GridPoint(9, value) }))
            .Distinct().ToArray(),
        [new AdventureBoardObject("campfire", AdventureObjectKind.Campfire, new GridPoint(4, 5), true, false)],
        [new AdventureActorPlacement("party-mage", new GridPoint(2, 5))],
        new GridPoint(1, 5), new GridPoint(8, 5));
}
