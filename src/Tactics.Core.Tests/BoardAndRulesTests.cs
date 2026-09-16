using NUnit.Framework;
using Tactics.Core.Board;
using Tactics.Core.Battle;
using Tactics.Core.Combat;
using Tactics.Core.Content;
using Tactics.Core.Pathfinding;
using Tactics.Core.Presentation;
using Tactics.Core.Turns;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class BoardAndRulesTests
{
    [Test]
    public void LegacyBaseSpeedOverrideDoesNotChangeAttributeDerivedActionStats()
    {
        var facts = new UnitState(new UnitInstanceId("unit-speed"), new ContentId("unit.speed"),
            new GridPoint(1, 1), 2, 10f, 1, 0);
        var unit = new BattleUnitState(facts, 20, 20, baseSpeed: 5f);

        BattleUnitState adjusted = unit.WithBaseSpeed(6f);

        Assert.Multiple(() =>
        {
            Assert.That(adjusted.BaseSpeed, Is.EqualTo(6f));
            Assert.That(adjusted.Unit.Initiative, Is.EqualTo(10f));
            Assert.That(adjusted.Unit.MoveRange, Is.EqualTo(2));
        });
    }
    [TestCase("Skill.Poison-Spear.Lv1")]
    [TestCase("skill_poison_spear")]
    [TestCase("skill..poison")]
    [TestCase("skill.poison-")]
    public void ContentId_RejectsNonCanonicalBusinessIds(string value)
    {
        Assert.Throws<ArgumentException>(() => _ = new ContentId(value));
    }

    [Test]
    public void BoardSpec_UsesFixedTenByTenLocalBounds()
    {
        Assert.That(BoardSpec.CellCount, Is.EqualTo(100));
        Assert.Multiple(() =>
        {
            Assert.That(BoardSpec.Contains(new GridPoint(0, 0)), Is.True);
            Assert.That(BoardSpec.Contains(new GridPoint(9, 9)), Is.True);
            Assert.That(BoardSpec.Contains(new GridPoint(-1, 0)), Is.False);
            Assert.That(BoardSpec.Contains(new GridPoint(10, 9)), Is.False);
        });
    }

    [Test]
    public void AStar_UsesFourNeighboursAndSkipsBlockedCells()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        cells[new GridPoint(1, 0)] = new CellState(blocksMovement: true);

        var path = new DeterministicDijkstraPathfinder().FindPath(
            new BoardSnapshot(cells),
            new GridPoint(0, 0),
            new GridPoint(2, 0));

        Assert.That(path, Is.EqualTo(new[]
        {
            new GridPoint(0, 1),
            new GridPoint(1, 1),
            new GridPoint(2, 1),
            new GridPoint(2, 0)
        }));
    }

    [Test]
    public void MovementPolicy_ChargesLandForShallowWaterAndLetsAirFlyOverButNotStopOnOccupiers()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        cells[new GridPoint(1, 0)] = new CellState(terrain: TerrainKind.ShallowWater);
        BoardSnapshot board = new BoardSnapshot(cells).WithOccupancy(new[] { new GridPoint(2, 0) });
        var pathfinder = new DeterministicDijkstraPathfinder();

        IReadOnlyList<GridPoint> land = pathfinder.FindPath(board, new GridPoint(0, 0), new GridPoint(1, 0), movementKind: UnitMovementKind.Land);
        IReadOnlyList<GridPoint> air = pathfinder.FindPath(board, new GridPoint(0, 0), new GridPoint(3, 0), movementKind: UnitMovementKind.Air);

        Assert.Multiple(() =>
        {
            Assert.That(DeterministicDijkstraPathfinder.MovementPointCost(board, land, UnitMovementKind.Land), Is.EqualTo(2));
            Assert.That(air, Does.Contain(new GridPoint(2, 0)));
            Assert.That(board.GetCell(new GridPoint(2, 0)).CanStop(UnitMovementKind.Air), Is.False);
            Assert.That(board.GetCell(new GridPoint(1, 0)).MovementPointCost(UnitMovementKind.Swim), Is.EqualTo(1));
        });
    }

    [Test]
    public void Pathfinder_AllowOccupiedDestinationDoesNotAllowObstacleDestination()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        GridPoint occupiedDestination = new(1, 0);
        GridPoint obstacleDestination = new(0, 1);
        cells[obstacleDestination] = new CellState(obstacle: MovementObstacleKind.Absolute);
        BoardSnapshot board = new BoardSnapshot(cells).WithOccupancy([occupiedDestination]);
        var pathfinder = new DeterministicDijkstraPathfinder();

        Assert.Multiple(() =>
        {
            Assert.That(pathfinder.FindPath(board, new GridPoint(0, 0), occupiedDestination,
                allowOccupiedDestination: true), Is.EqualTo(new[] { occupiedDestination }));
            Assert.That(pathfinder.FindPath(board, new GridPoint(0, 0), obstacleDestination,
                allowOccupiedDestination: true, movementKind: UnitMovementKind.Air), Is.Empty);
        });
    }

    [Test]
    public void ShadowConeLos_AllowsDiagonalTerrainCornerTangency()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        cells[new GridPoint(1, 0)] = new CellState(blocksLineOfSight: true);

        bool visible = new ShadowConeLineOfSight().HasLineOfSight(
            new BoardSnapshot(cells),
            new GridPoint(0, 0),
            new GridPoint(2, 2));

        Assert.That(visible, Is.True);
    }

    [Test]
    public void ShadowConeLos_AllowsDiagonalLivingUnitCornerTangency()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var board = new BoardSnapshot(cells);
        var occupied = new HashSet<GridPoint> { new(1, 0) };

        Assert.Multiple(() =>
        {
            Assert.That(new ShadowConeLineOfSight().HasLineOfSight(board, new GridPoint(0, 0), new GridPoint(2, 2), occupied), Is.True);
            Assert.That(new ShadowConeLineOfSight().HasLineOfSight(board, new GridPoint(0, 0), new GridPoint(2, 2)), Is.True);
        });
    }

    [Test]
    public void ShadowConeLos_TraceReportsTheBlockingCellAndKind()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var board = new BoardSnapshot(cells);
        var blockerId = new UnitInstanceId("living-blocker");
        var blockers = new Dictionary<GridPoint, LineOfSightBlocker>
        {
            [new GridPoint(1, 0)] = new(LineOfSightBlockingKind.LivingUnit, blockerId)
        };

        LineOfSightResult result = new ShadowConeLineOfSight().Trace(
            board, new GridPoint(0, 0), new GridPoint(2, 0), blockers);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsClear, Is.False);
            Assert.That(result.BlockingCell, Is.EqualTo(new GridPoint(1, 0)));
            Assert.That(result.BlockingKind, Is.EqualTo(LineOfSightBlockingKind.LivingUnit));
            Assert.That(result.BlockingUnitId, Is.EqualTo(blockerId));
            Assert.That(result.RayCells, Is.EqualTo(new[] { new GridPoint(1, 0) }));
        });
    }

    [Test]
    public void ShadowConeLos_BlocksTheNearestCellWhoseOpenInteriorCrossesANonAxialRay()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var board = new BoardSnapshot(cells);
        var nearId = new UnitInstanceId("near-blocker");
        var farId = new UnitInstanceId("far-blocker");
        var blockers = new Dictionary<GridPoint, LineOfSightBlocker>
        {
            [new GridPoint(2, 1)] = new(LineOfSightBlockingKind.LivingUnit, nearId),
            [new GridPoint(4, 2)] = new(LineOfSightBlockingKind.LivingUnit, farId)
        };

        LineOfSightResult result = new ShadowConeLineOfSight().Trace(
            board, new GridPoint(0, 0), new GridPoint(6, 3), blockers);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsClear, Is.False);
            Assert.That(result.BlockingCell, Is.EqualTo(new GridPoint(2, 1)));
            Assert.That(result.BlockingUnitId, Is.EqualTo(nearId));
            Assert.That(result.RayCells, Does.Contain(new GridPoint(4, 2)));
        });
    }

    [Test]
    public void MovementActionState_AllowsOneMoveWithoutRemainingPointAccounting()
    {
        var movement = new MovementActionState(moveRange: 3);

        Assert.Multiple(() =>
        {
            Assert.That(movement.CanUseMove(3), Is.True);
            Assert.That(movement.TryUseMove(2), Is.True);
            Assert.That(movement.HasMovedThisTurn, Is.True);
            Assert.That(movement.CanUseMove(1), Is.False);
        });

        movement.PrepareForTurn();
        Assert.That(movement.TryUseMove(3), Is.True);
    }

    [Test]
    public void InitiativeOrder_UsesFrozenInitiativePlayerAndSpawnOrdinalTieBreak()
    {
        var result = InitiativeOrder.Sort(new[]
        {
            new InitiativeEntry(new UnitInstanceId("enemy.zombie.0"), 8, 1, 2),
            new InitiativeEntry(new UnitInstanceId("enemy.mage.0"), 10, 1, 5),
            new InitiativeEntry(new UnitInstanceId("party.amazon.0"), 8, 0, 9),
            new InitiativeEntry(new UnitInstanceId("enemy.zombie.1"), 8, 1, 1)
        });

        Assert.That(result.Select(entry => entry.UnitId.Value), Is.EqualTo(new[]
        {
            "enemy.mage.0", "party.amazon.0", "enemy.zombie.1", "enemy.zombie.0"
        }));
    }

    [Test]
    public void UnitFacingResolver_UsesCardinalDominantAxisAndStableTieRules()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UnitFacingResolver.Initial(0), Is.EqualTo(UnitFacing.East));
            Assert.That(UnitFacingResolver.Initial(1), Is.EqualTo(UnitFacing.West));
            Assert.That(UnitFacingResolver.Resolve(new GridPoint(0, 0), new GridPoint(1, 3), UnitFacing.East),
                Is.EqualTo(UnitFacing.North));
            Assert.That(UnitFacingResolver.Resolve(new GridPoint(2, 2), new GridPoint(1, 1), UnitFacing.West),
                Is.EqualTo(UnitFacing.West));
            Assert.That(UnitFacingResolver.Resolve(new GridPoint(2, 2), new GridPoint(2, 2), UnitFacing.South),
                Is.EqualTo(UnitFacing.South));
            Assert.That(UnitFacingResolver.BackwardStep(new GridPoint(4, 4), UnitFacing.East),
                Is.EqualTo(new GridPoint(3, 4)));
        });
    }

    [Test]
    public void PoisonSpear_ResolvesDamageAndPoisonWithoutPresentationState()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var board = new BoardSnapshot(cells);
        var caster = new UnitState(
            new UnitInstanceId("party.caster.0"),
            new ContentId("unit.caster"),
            new GridPoint(1, 1),
            3,
            10,
            0,
            0);
        var target = new UnitState(
            new UnitInstanceId("enemy.target.0"),
            new ContentId("unit.target"),
            new GridPoint(3, 2),
            3,
            8,
            1,
            1);

        var result = new PoisonSpearResolver().Resolve(
            board,
            caster,
            target,
            new PoisonSpearDefinition(new ContentId("skill.poison-spear.lv1"), 6, 8, 3));

        Assert.That(result, Is.EqualTo(new ActionResult(true, 8, 3)));
    }

    [Test]
    public void PresentationPlan_RejectsMissingChildrenAndCycles()
    {
        var missingChild = new PresentationExecutionPlan(
            1,
            "root",
            new[] { new PresentationNode("root", "sequence", PresentationNodeKind.Sequence, new[] { "missing" }) });
        Assert.Throws<InvalidOperationException>(() => missingChild.Validate());

        var cycle = new PresentationExecutionPlan(
            1,
            "root",
            new[]
            {
                new PresentationNode("root", "sequence", PresentationNodeKind.Sequence, new[] { "leaf" }),
                new PresentationNode("leaf", "sequence", PresentationNodeKind.Sequence, new[] { "root" })
            });
        Assert.Throws<InvalidOperationException>(() => cycle.Validate());
    }
}
