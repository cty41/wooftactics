using System.Globalization;
using Godot;
using Tactics.Core.Board;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record GodotAdventureMovementCompletion(
    string ActorId,
    GridPoint Origin,
    GridPoint Destination,
    IReadOnlyList<GridPoint> TraversedCells,
    int ReplanCount);

public sealed record GodotAdventureMovementDiagnostic(
    long AttemptId,
    string ActorId,
    GridPoint Origin,
    GridPoint CurrentCell,
    GridPoint DesiredDestination,
    int RemainingSteps,
    int ReplanCount,
    string Outcome)
{
    public string ToLogLine() => string.Join(' ',
        $"attempt={AttemptId.ToString(CultureInfo.InvariantCulture)}",
        $"actor={ActorId}",
        $"origin={Origin.X},{Origin.Y}",
        $"current={CurrentCell.X},{CurrentCell.Y}",
        $"desired={DesiredDestination.X},{DesiredDestination.Y}",
        $"remaining={RemainingSteps.ToString(CultureInfo.InvariantCulture)}",
        $"replans={ReplanCount.ToString(CultureInfo.InvariantCulture)}",
        $"outcome={Outcome}");
}

/// <summary>Owns transient one-cell Adventure movement Tweens and replans only at cell centers.</summary>
public partial class GodotAdventureMovementPlayer : Node
{
    private StandardUnitPresentationResource _profile = new();
    private Tween? _activeTween;
    private GodotUnitActor? _actor;
    private AdventureBoardDefinition? _board;
    private Func<GridPoint, Vector2>? _project;
    private IReadOnlyCollection<GridPoint> _occupiedCells = Array.Empty<GridPoint>();
    private readonly List<GridPoint> _remainingPath = new();
    private readonly List<GridPoint> _traversedCells = new();
    private string _actorId = string.Empty;
    private GridPoint _origin;
    private GridPoint _currentCell;
    private GridPoint _desiredDestination;
    private long _attemptId;
    private long _generation;
    private int _replanCount;
    private float _speed = 1f;
    private bool _paused;

    public bool IsMoving { get; private set; }
    public bool IsLocked => IsMoving;
    public GridPoint CurrentCell => _currentCell;
    public GridPoint DesiredDestination => _desiredDestination;
    public int ReplanCount => _replanCount;
    public GodotAdventureMovementDiagnostic? LastDiagnostic { get; private set; }

    public event Action<GridPoint>? CellCentered;
    public event Action<GodotAdventureMovementCompletion>? MovementCompleted;

    public void Configure(StandardUnitPresentationResource profile, float speed = 1f)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        SetSpeed(speed);
    }

    public bool TryStart(GodotUnitActor actor, string actorId, GridPoint origin, GridPoint destination,
        AdventureBoardDefinition board, IReadOnlyCollection<GridPoint> occupiedCells,
        Func<GridPoint, Vector2> project, out string? rejection)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(occupiedCells);
        ArgumentNullException.ThrowIfNull(project);
        if (IsMoving) return TryRetarget(destination, out rejection);
        if (origin == destination)
        {
            rejection = "adventure.destination_is_current";
            return false;
        }
        IReadOnlyList<GridPoint> path = PathFrom(board, origin, destination, occupiedCells);
        if (path.Count == 0)
        {
            rejection = "adventure.destination_unreachable";
            return false;
        }

        _generation++;
        _attemptId++;
        _actor = actor;
        _actorId = actorId;
        _origin = origin;
        _currentCell = origin;
        _desiredDestination = destination;
        _board = board;
        _occupiedCells = occupiedCells.ToArray();
        _project = project;
        _remainingPath.Clear();
        _remainingPath.AddRange(path);
        _traversedCells.Clear();
        _replanCount = 0;
        IsMoving = true;
        actor.Position = project(origin);
        actor.RestoreTransientBodyPose();
        actor.SetAirMoveOverlay(true);
        Record("started");
        PlayNextSegment(_generation);
        rejection = null;
        return true;
    }

    public bool TryRetarget(GridPoint destination, out string? rejection)
    {
        if (!IsMoving || _board is null)
        {
            rejection = "adventure.movement_not_active";
            return false;
        }
        if (destination == _desiredDestination)
        {
            rejection = null;
            return true;
        }
        if (destination != _currentCell && PathFrom(_board, _currentCell, destination, _occupiedCells).Count == 0)
        {
            rejection = "adventure.destination_unreachable";
            Record("retarget-rejected-unreachable");
            return false;
        }
        _desiredDestination = destination;
        _replanCount++;
        Record("retargeted");
        rejection = null;
        return true;
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        if (_activeTween is null || !GodotObject.IsInstanceValid(_activeTween)) return;
        if (paused) _activeTween.Pause(); else _activeTween.Play();
        Record(paused ? "paused" : "resumed");
    }

    public void SetSpeed(float speed)
    {
        if (!GodotBattlePresentationPlayer.IsSupportedSpeed(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
        _speed = speed;
        if (_activeTween is not null && GodotObject.IsInstanceValid(_activeTween)) _activeTween.SetSpeedScale(speed);
    }

    public void Cancel(string reason = "cancelled")
    {
        _generation++;
        if (_activeTween is not null && GodotObject.IsInstanceValid(_activeTween)) _activeTween.Kill();
        _activeTween = null;
        if (_actor is not null && GodotObject.IsInstanceValid(_actor))
        {
            _actor.SetAirMoveOverlay(false);
            _actor.RestoreTransientBodyPose();
        }
        if (IsMoving) Record(Sanitize(reason));
        IsMoving = false;
        _remainingPath.Clear();
    }

    public override void _ExitTree() => Cancel("exit-tree");

    private void PlayNextSegment(long generation)
    {
        if (!IsCurrent(generation)) return;
        if (_currentCell == _desiredDestination)
        {
            PlaySettle(generation);
            return;
        }
        if (_board is null || _project is null || _actor is null) { Cancel("invalid-state"); return; }

        IReadOnlyList<GridPoint> replanned = PathFrom(_board, _currentCell, _desiredDestination, _occupiedCells);
        if (replanned.Count == 0)
        {
            Record("replan-failed");
            _desiredDestination = _currentCell;
            PlaySettle(generation);
            return;
        }
        _remainingPath.Clear();
        _remainingPath.AddRange(replanned);
        GridPoint next = _remainingPath[0];
        GridPoint from = _currentCell;
        GodotUnitActor actor = _actor;
        Tween tween = CreateTween().SetSpeedScale(_speed);
        _activeTween = tween;
        if (_paused) tween.Pause();
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsCurrent(generation)) actor.SetFacing(GodotPresentationFacingResolver.Resolve(from, next, actor.PresentationFacing));
        }));
        GodotUnitMoveTweenBuilder.AppendSegment(tween, actor, _project(from), _project(next), _profile);
        tween.Finished += () =>
        {
            if (!IsCurrent(generation)) return;
            _activeTween = null;
            actor.Position = _project(next);
            _currentCell = next;
            _traversedCells.Add(next);
            if (_remainingPath.Count > 0) _remainingPath.RemoveAt(0);
            CellCentered?.Invoke(next);
            Record("cell-centered");
            PlayNextSegment(generation);
        };
    }

    private void PlaySettle(long generation)
    {
        if (!IsCurrent(generation) || _actor is null) return;
        GodotUnitActor actor = _actor;
        Tween tween = CreateTween().SetSpeedScale(_speed);
        _activeTween = tween;
        if (_paused) tween.Pause();
        GodotUnitMoveTweenBuilder.AppendSettle(tween, actor, _profile);
        tween.Finished += () =>
        {
            if (!IsCurrent(generation)) return;
            _activeTween = null;
            if (_currentCell != _desiredDestination)
            {
                PlayNextSegment(generation);
                return;
            }
            actor.SetAirMoveOverlay(false);
            actor.RestoreTransientBodyPose();
            IsMoving = false;
            _remainingPath.Clear();
            Record("completed");
            MovementCompleted?.Invoke(new GodotAdventureMovementCompletion(_actorId, _origin, _currentCell,
                _traversedCells.ToArray(), _replanCount));
        };
    }

    private bool IsCurrent(long generation) => IsMoving && generation == _generation &&
        _actor is not null && GodotObject.IsInstanceValid(_actor);

    private void Record(string outcome)
    {
        LastDiagnostic = new GodotAdventureMovementDiagnostic(_attemptId, _actorId, _origin, _currentCell,
            _desiredDestination, _remainingPath.Count, _replanCount, outcome);
#if DEBUG
        GD.Print("[AdventureMovement] " + LastDiagnostic.ToLogLine());
#endif
    }

    private static IReadOnlyList<GridPoint> PathFrom(AdventureBoardDefinition board, GridPoint origin,
        GridPoint destination, IReadOnlyCollection<GridPoint> occupiedCells)
    {
        IReadOnlyList<GridPoint> path = AdventureBoardPathfinder.FindPath(board, origin, destination, occupiedCells);
        return path.Count > 0 && path[0] == origin ? path.Skip(1).ToArray() : path;
    }

    private static string Sanitize(string value) => value.Replace('\r', '_').Replace('\n', '_').Replace(' ', '_').Replace('=', '_');
}
