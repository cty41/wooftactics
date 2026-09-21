using Tactics.Core.Board;

namespace Tactics.Core.Pathfinding;

public interface IPathfinder
{
    IReadOnlyList<GridPoint> FindPath(
        BoardSnapshot board,
        GridPoint origin,
        GridPoint destination,
        bool allowOccupiedDestination = false,
        Units.UnitMovementKind movementKind = Units.UnitMovementKind.Land);
}

/// <summary>
/// Deterministic four-neighbour Dijkstra pathfinding matching the frozen Unity movement contract.
/// </summary>
/// <remarks>
/// Neighbours are discovered in the stable order owned by <see cref="BoardSnapshot"/>. Equal-cost
/// frontier entries use the same binary-heap comparisons as the final Unity runtime. The returned path
/// excludes the origin and includes the destination.
/// </remarks>
public sealed class DeterministicDijkstraPathfinder : IPathfinder
{
    public IReadOnlyList<GridPoint> FindPath(
        BoardSnapshot board,
        GridPoint origin,
        GridPoint destination,
        bool allowOccupiedDestination = false,
        Units.UnitMovementKind movementKind = Units.UnitMovementKind.Land)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (!board.Contains(origin) || !board.Contains(destination))
            return Array.Empty<GridPoint>();
        return DeterministicGridPathSearch.FindPath(
            origin,
            destination,
            board.GetNeighbours,
            (point, isDestination) =>
            {
                CellState cell = board.GetCell(point);
                return isDestination
                    ? cell.Obstacle == MovementObstacleKind.None && (!cell.IsOccupied || allowOccupiedDestination)
                    : cell.CanTraverse(movementKind);
            },
            point => board.GetCell(point).MovementPointCost(movementKind));
    }

    public static int MovementPointCost(BoardSnapshot board, IReadOnlyList<GridPoint> path,
        Units.UnitMovementKind movementKind) => path.Sum(point => board.GetCell(point).MovementPointCost(movementKind));

}
