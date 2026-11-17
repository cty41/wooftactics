using Tactics.Application.Runs;
using Tactics.Core.Board;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Exploration movement: clicking a cell walks the leader there, clicking a target object walks the leader to the
/// best free neighbour of the object and then interacts. The move is committed first (Core stays authoritative); the
/// board then animates the path at a constant speed and the page re-renders before the interaction runs.
/// </summary>
public partial class GodotPlayableRunMain
{
    internal const string AdventureTargetUnreachable = "adventure.target_unreachable";

    /// <summary>Re-render callback of the current exploration page (set by <see cref="WireAdventureMovement"/>).</summary>
    private Action<PureRunState>? _adventureRerender;

    /// <summary>
    /// Runs <paramref name="interact"/> once the leader stands next to <paramref name="target"/>: immediately when it
    /// already does, otherwise after walking to the best reachable neighbour.
    /// </summary>
    private void ApproachThen(PureRunState run, AdventureBoardDefinition definition, GridPoint target, Action<PureRunState> interact)
    {
        if (_adventureBoard?.IsWalking == true) return;
        RunAdventureActorCell leader = run.AdventureState!.ActorCells.Single(value => value.ActorId == run.AdventureState.LeaderId);
        if (RunAdventureTransitionService.IsAdjacent(leader.Cell, target)) { interact(run); return; }
        if (BestApproachCell(run, definition, target) is not GridPoint approach) { SetStatus(AdventureTargetUnreachable); return; }
        WalkLeaderThen(run, definition, approach, interact);
    }

    /// <summary>
    /// The free neighbour of <paramref name="target"/> with the shortest leader path; ties prefer the neighbour nearer
    /// the target on screen, then <c>(x, y)</c> order. Null when no neighbour is reachable.
    /// </summary>
    internal static GridPoint? BestApproachCell(PureRunState run, AdventureBoardDefinition definition, GridPoint target)
    {
        RunAdventureState state = run.AdventureState!;
        RunAdventureActorCell leader = state.ActorCells.Single(value => value.ActorId == state.LeaderId);
        GridPoint[] occupied = state.ActorCells.Where(value => value.ActorId != state.LeaderId).Select(value => value.Cell).ToArray();
        GridPoint[] blockedByObjects = definition.Objects.Select(value => value.Cell).ToArray();
        return new[] { new GridPoint(target.X + 1, target.Y), new GridPoint(target.X - 1, target.Y),
                new GridPoint(target.X, target.Y + 1), new GridPoint(target.X, target.Y - 1) }
            .Where(cell => cell.X >= 0 && cell.Y >= 0 && cell.X < definition.Width && cell.Y < definition.Height &&
                           definition.IsWalkable(cell) && !occupied.Contains(cell) && !blockedByObjects.Contains(cell))
            .Select(cell => (Cell: cell, Path: AdventureBoardPathfinder.FindPath(definition, leader.Cell, cell, occupied)))
            .Where(value => value.Path.Count > 0)
            .OrderBy(value => value.Path.Count)
            .ThenBy(value => IsometricGridProjection.GridToScreen(value.Cell).DistanceSquaredTo(IsometricGridProjection.GridToScreen(target)))
            .ThenBy(value => value.Cell.X).ThenBy(value => value.Cell.Y)
            .Select(value => (GridPoint?)value.Cell)
            .FirstOrDefault();
    }

    /// <summary>Commits the leader move, animates it, re-renders the page and then runs <paramref name="arrived"/>.</summary>
    private void WalkLeaderThen(PureRunState run, AdventureBoardDefinition definition, GridPoint destination,
        Action<PureRunState>? arrived)
    {
        if (_adventureBoard is not { } board || board.IsWalking) return;
        RunAdventureState state = run.AdventureState!;
        RunAdventureActorCell leader = state.ActorCells.Single(value => value.ActorId == state.LeaderId);
        GridPoint[] occupied = state.ActorCells.Where(value => value.ActorId != state.LeaderId).Select(value => value.Cell).ToArray();
        IReadOnlyList<GridPoint> path = AdventureBoardPathfinder.FindPath(definition, leader.Cell, destination, occupied);
        RunSessionResult changed = _run!.ApplyMutation(current =>
        {
            try { return new RunMutationResult(true, null, new RunAdventureTransitionService().MoveLeader(current, definition, destination)); }
            catch (InvalidOperationException error) { return new RunMutationResult(false, error.Message, current); }
        });
        if (!changed.Succeeded) { SetStatus(changed.ErrorCode); return; }
        PureRunState moved = changed.Snapshot!.ActiveRun!;
        Action<PureRunState>? rerender = _adventureRerender;
        board.AnimateActorPath(leader.ActorId, path, () =>
        {
            // The page may have changed during the walk (the old board is only queue-freed, so its tween can still
            // finish this frame): a board that is no longer current must not re-render or interact.
            if (!ReferenceEquals(_adventureBoard, board)) return;
            rerender?.Invoke(moved);
            arrived?.Invoke(moved);
        });
    }
}
