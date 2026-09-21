using System.Globalization;
using Godot;
using Tactics.Core.Board;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record GodotStartCampCandidate(string CharacterId, UnitDefinitionResource Definition);
public enum StartAtlasCameraMode { Current, Overview }
public enum StartCampPointerResolution { Candidate, Exit, Tile, Preview, BlockedOverview, BlockedOccupied, BlockedMovement, Miss }

/// <summary>Bounded, structured evidence for the most recent Start Camp left-click routing attempt.</summary>
public sealed record GodotStartCampInputDiagnostic(
    long AttemptId,
    string SourceControl,
    string HoveredControl,
    StartAtlasCameraMode CameraMode,
    Vector2 SourcePoint,
    Vector2 ViewPoint,
    Vector2 AtlasPoint,
    Vector2 MapPoint,
    StartCampPointerResolution Resolution,
    string? TargetId,
    string? NearestCandidateId,
    Rect2? NearestCandidateBounds,
    float? NearestCandidateDistance,
    int SelectedBefore,
    int SelectedAfter,
    string? Outcome)
{
    public string ToLogLine() =>
        $"attempt={AttemptId} source={SourceControl} hovered={HoveredControl} mode={CameraMode} " +
        $"sourcePoint={Format(SourcePoint)} viewPoint={Format(ViewPoint)} atlasPoint={Format(AtlasPoint)} mapPoint={Format(MapPoint)} " +
        $"resolution={Resolution} target={TargetId ?? "none"} nearest={NearestCandidateId ?? "none"} " +
        $"nearestBounds={(NearestCandidateBounds is Rect2 bounds ? Format(bounds) : "none")} " +
        $"nearestDistance={(NearestCandidateDistance?.ToString("0.###", CultureInfo.InvariantCulture) ?? "none")} " +
        $"selected={SelectedBefore}->{SelectedAfter} outcome={Outcome ?? "pending"}";

    private static string Format(Vector2 value) =>
        $"({value.X.ToString("0.###", CultureInfo.InvariantCulture)},{value.Y.ToString("0.###", CultureInfo.InvariantCulture)})";

    private static string Format(Rect2 value) => $"{Format(value.Position)}:{Format(value.Size)}";
}

/// <summary>Full-scale, typed start-camp surface used by party selection.</summary>
public partial class GodotStartCampView : Control
{
    public const int AtlasWorldMaxZIndex = 100;
    public static readonly Rect2 SafeMapArea = new(new Vector2(40, 130), new Vector2(1520, 650));
    public static readonly Rect2 AtlasViewport = SafeMapArea;
    private readonly Dictionary<string, GodotUnitActor> _candidates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GridPoint> _candidateCells = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private AdventureMapTemplateDefinition? _definition;
    private AdventureMapTemplateDefinition? _planningDefinition;
    private Node2D? _atlasWorldRoot;
    private Node2D? _mapRoot;
    private GodotStartCampfireActor? _campfire;
    private GodotStartCampExitActor? _exit;
    private readonly Dictionary<string, Rect2> _routePreviewRects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Transform2D> _nodeTransforms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Vector2 From, Vector2 To)> _connectionEndpoints = new(StringComparer.Ordinal);
    private Transform2D _mapWorldTransform;
    private Rect2 _atlasBounds;
    private Rect2 _localViewBounds;
    private Rect2 _initialOverviewBounds;
    private Vector2 _cameraOrigin;
    private Vector2 _atlasPan;
    private float _atlasZoom = 1f;
    private StartAtlasCameraMode _cameraMode;
    private bool _cameraDragging;
    private Vector2 _cameraDragOrigin;
    private Vector2 _cameraPanOrigin;
    private Control? _inputSurface;
    private bool _inputSurfaceConnected;
    private GodotAdventureMovementPlayer? _movementPlayer;
    private StandardUnitPresentationResource _movementProfile = new();
    private float _movementSpeed = 1f;
    private long _inputAttemptId;

    public event Action<string>? CandidatePressed;
    public event Action? ExitPressed;
    public event Action<GridPoint>? LeaderMoved;
    public event Action<string>? MovementRejected;
    public event Action<string>? PreviewPressed;
    public event Action<StartAtlasCameraMode>? CameraModeChanged;
    public string? LeaderId { get; private set; }
    public GodotAdventureMapInstance MapInstance { get; private set; } = null!;
    public IReadOnlyDictionary<string, GodotUnitActor> CandidateActors => _candidates;
    public IReadOnlyDictionary<string, GridPoint> CandidateCells => _candidateCells;
    public GodotStartCampfireActor Campfire => _campfire ?? throw new InvalidOperationException("Start camp is not configured.");
    public GodotStartCampExitActor Exit => _exit ?? throw new InvalidOperationException("Start camp is not configured.");
    public Rect2 FittedMapBounds { get; private set; }
    public IReadOnlyList<GodotAdventureMapInstance> RoutePreviews { get; private set; } = Array.Empty<GodotAdventureMapInstance>();
    public IReadOnlyDictionary<string, Rect2> AtlasNodeBounds => _routePreviewRects;
    public IReadOnlyDictionary<string, Transform2D> AtlasNodeTransforms => _nodeTransforms;
    public IReadOnlyDictionary<string, (Vector2 From, Vector2 To)> ConnectionEndpoints => _connectionEndpoints;
    public float AtlasZoom => _atlasZoom;
    public Vector2 AtlasPan => _atlasPan;
    public bool IsAtlasOverview => _cameraMode == StartAtlasCameraMode.Overview;
    public StartAtlasCameraMode CameraMode => _cameraMode;
    public Control InputSurface => EnsureInputSurface();
    public GodotStartCampInputDiagnostic? LastInputDiagnostic { get; private set; }
    public GodotAdventureMovementDiagnostic? LastMovementDiagnostic => _movementPlayer?.LastDiagnostic;
    public bool IsMovementLocked => _movementPlayer?.IsLocked == true;

    public override void _EnterTree()
    {
        if (_inputSurface is not null && GodotObject.IsInstanceValid(_inputSurface)) ConnectInputSurface();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        EnsureInputSurface();
        EnsureMovementPlayer();
    }

    public override void _ExitTree()
    {
        if (_inputSurface is not null && _inputSurfaceConnected)
            _inputSurface.GuiInput -= OnInputSurfaceGuiInput;
        _inputSurfaceConnected = false;
        _movementPlayer?.Cancel("start-camp-exit-tree");
    }

    private Control EnsureInputSurface()
    {
        if (_inputSurface is not null && GodotObject.IsInstanceValid(_inputSurface))
        {
            ConnectInputSurface();
            return _inputSurface;
        }
        _inputSurface = new Control
        {
            Name = "StartCampAtlasInput",
            Position = AtlasViewport.Position,
            Size = AtlasViewport.Size,
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.None,
            ZIndex = GodotPlayableRunMain.StartPageUiZIndex - 1
        };
        AddChild(_inputSurface);
        ConnectInputSurface();
        return _inputSurface;
    }

    private void ConnectInputSurface()
    {
        if (_inputSurface is null || _inputSurfaceConnected) return;
        _inputSurface.GuiInput += OnInputSurfaceGuiInput;
        _inputSurfaceConnected = true;
    }

    private GodotAdventureMovementPlayer EnsureMovementPlayer()
    {
        if (_movementPlayer is not null && GodotObject.IsInstanceValid(_movementPlayer)) return _movementPlayer;
        _movementPlayer = new GodotAdventureMovementPlayer { Name = "StartCampMovementPlayer" };
        AddChild(_movementPlayer);
        _movementPlayer.Configure(_movementProfile, _movementSpeed);
        _movementPlayer.CellCentered += cell =>
        {
            if (LeaderId is null || !_candidates.TryGetValue(LeaderId, out GodotUnitActor? leader)) return;
            _candidateCells[LeaderId] = cell;
            leader.ZIndex = 30 + cell.X + cell.Y;
        };
        _movementPlayer.MovementCompleted += completion => LeaderMoved?.Invoke(completion.Destination);
        return _movementPlayer;
    }

    public void ConfigureMovement(StandardUnitPresentationResource profile, float speed = 1f)
    {
        _movementProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        _movementSpeed = speed;
        EnsureMovementPlayer().Configure(profile, speed);
        foreach (GodotUnitActor actor in _candidates.Values) actor.ConfigurePresentation(profile);
    }

    public void SetMovementPaused(bool paused) => EnsureMovementPlayer().SetPaused(paused);
    public void SetMovementSpeed(float speed)
    {
        _movementSpeed = speed;
        EnsureMovementPlayer().SetSpeed(speed);
    }

    private void OnInputSurfaceGuiInput(InputEvent input)
    {
        if (_inputSurface is null || input is not InputEventMouse mouse) return;
        Vector2 canvasPoint = _inputSurface.GetGlobalTransform() * mouse.Position;
        Vector2 viewPoint = GetGlobalTransform().AffineInverse() * canvasPoint;
        HandleAtlasInput(input, viewPoint, _inputSurface, mouse.Position);
    }

    public void Configure(AdventureMapTemplateResource template, IReadOnlyList<GodotStartCampCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(candidates);
        AdventureMapTemplateDefinition definition = template.ToCoreDefinition();
        _definition = definition;
        if (candidates.Select(value => value.CharacterId).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
            throw new ArgumentException("Start camp candidates require unique character ids.", nameof(candidates));
        if (definition.CandidateSlots.Count < candidates.Count)
            throw new InvalidOperationException($"Start camp template has {definition.CandidateSlots.Count} candidate slots for {candidates.Count} candidates.");

        EnsureMovementPlayer().Cancel("start-camp-reconfigure");
        _atlasWorldRoot?.QueueFree();
        _candidates.Clear();
        _candidateCells.Clear();
        _selected.Clear();
        _atlasWorldRoot = new Node2D { Name = "StartCampAtlasWorld", ZIndex = 0 };
        AddChild(_atlasWorldRoot);
        _mapRoot = new Node2D { Name = "StartCampMapRoot", ZIndex = 3 };
        _atlasWorldRoot.AddChild(_mapRoot);
        MapInstance = new GodotAdventureMapInstance { Name = "StartCampMapInstance" };
        _mapRoot.AddChild(MapInstance);
        MapInstance.Configure(template);
        MapInstance.Activate();

        Rect2 boardBounds = GodotBattleBoardFitter.BoardBounds();
        Transform2D fit = GodotBattleBoardFitter.Fit(boardBounds, SafeMapArea);
        Transform2D scaleOnly = new(fit.X, fit.Y, Vector2.Zero);
        Rect2 scaledBounds = GodotBattleBoardFitter.TransformBounds(boardBounds, scaleOnly);
        _mapWorldTransform = new Transform2D(scaleOnly.X, scaleOnly.Y, -scaledBounds.Position);
        _mapRoot.Transform = _mapWorldTransform;
        _atlasBounds = new Rect2(Vector2.Zero, scaledBounds.Size);
        _localViewBounds = _atlasBounds;
        _routePreviewRects.Clear();
        _routePreviewRects[GodotAdventureAtlasLayout.StartNodeId] = _atlasBounds;
        _nodeTransforms.Clear();
        _nodeTransforms[GodotAdventureAtlasLayout.StartNodeId] = _mapWorldTransform;
        ResetAtlasCamera(false);
        FittedMapBounds = GodotBattleBoardFitter.TransformBounds(_atlasBounds, _atlasWorldRoot.Transform);

        AdventureBoardObject campfire = definition.Board.Objects.Single(value => value.Kind == AdventureObjectKind.Campfire);
        _campfire = new GodotStartCampfireActor { Name = "StartCampfire", Position = MapInstance.Surface.CellCenter(campfire.Cell), ZIndex = 20 };
        _mapRoot.AddChild(_campfire);
        AdventureMapExitAnchor exit = definition.Exits.Single();
        _exit = new GodotStartCampExitActor { Name = "StartCampExit", Position = MapInstance.Surface.CellCenter(exit.Cell), ZIndex = 15 };
        _mapRoot.AddChild(_exit);

        for (int index = 0; index < candidates.Count; index++)
        {
            GodotStartCampCandidate candidate = candidates[index];
            AdventureMapSlot slot = definition.CandidateSlots[index];
            GodotUnitActor actor = GodotUnitFactory.InstantiateActor(candidate.Definition);
            actor.Name = $"StartCampCandidate{index + 1}";
            actor.ConfigureInstanceIdentity("start-camp:" + candidate.CharacterId);
            actor.ConfigurePresentation(_movementProfile);
            actor.Position = MapInstance.Surface.CellCenter(slot.Cell);
            actor.Scale = Vector2.One * .34f;
            actor.ZIndex = 30 + slot.Cell.X + slot.Cell.Y;
            _mapRoot.AddChild(actor);
            _candidates.Add(candidate.CharacterId, actor);
            _candidateCells.Add(candidate.CharacterId, slot.Cell);
        }
        SetSelection(Array.Empty<string>(), false);
    }

    public void ConfigureRoutePreviews(AdventureMapTemplateResource planningTemplate, PureRunMapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(planningTemplate);
        ArgumentNullException.ThrowIfNull(map);
        if (_atlasWorldRoot is null || _mapRoot is null || _definition is null)
            throw new InvalidOperationException("Start camp must be configured before route previews.");
        _planningDefinition = planningTemplate.ToCoreDefinition();
        foreach (Node child in _atlasWorldRoot.GetChildren().Where(value => value != _mapRoot).ToArray()) child.QueueFree();
        _routePreviewRects.Clear();
        _nodeTransforms.Clear();
        _connectionEndpoints.Clear();
        Vector2 mapSize = new(_atlasBounds.Size.X, _atlasBounds.Size.Y);
        IReadOnlyDictionary<string, GodotAdventureAtlasNodeLayout> layout = GodotAdventureAtlasLayout.Project(map, mapSize);
        foreach ((string id, GodotAdventureAtlasNodeLayout node) in layout) _routePreviewRects[id] = node.WorldBounds;
        _mapWorldTransform = AtWorldBounds(layout[GodotAdventureAtlasLayout.StartNodeId].WorldBounds);
        _mapRoot.Transform = _mapWorldTransform;
        _nodeTransforms[GodotAdventureAtlasLayout.StartNodeId] = _mapWorldTransform;
        PureRunMapNodeDefinition startNode = map.Nodes.Single(value => value.NodeId == GodotAdventureAtlasLayout.StartNodeId);
        AddAtlasBadge(startNode, layout[startNode.NodeId].WorldBounds);
        var previews = new List<GodotAdventureMapInstance>();
        foreach (PureRunMapNodeDefinition node in map.Nodes.Where(value => value.NodeId != GodotAdventureAtlasLayout.StartNodeId))
        {
            Transform2D transform = AtWorldBounds(layout[node.NodeId].WorldBounds);
            var preview = new GodotAdventureMapInstance { Name = $"PlanningPreview_{node.NodeId}", Transform = transform, ZIndex = 2 };
            _atlasWorldRoot.AddChild(preview);
            preview.Configure(planningTemplate);
            preview.Deactivate();
            previews.Add(preview);
            _nodeTransforms[node.NodeId] = transform;
            AddAtlasBadge(node, layout[node.NodeId].WorldBounds);
        }
        foreach (PureRunMapConnectionDefinition edge in map.Connections)
        {
            Vector2 from = ConnectionPoint(edge.FromNodeId, source: true);
            Vector2 to = ConnectionPoint(edge.ToNodeId, source: false);
            string key = $"{edge.FromNodeId}->{edge.ToNodeId}";
            _connectionEndpoints[key] = (from, to);
            var line = new Line2D { Name = $"Route_{edge.FromNodeId}_{edge.ToNodeId}", Width = 6f,
                DefaultColor = new Color("657b86d0"), Points = [from, to], ZIndex = 1 };
            _atlasWorldRoot.AddChild(line);
        }
        RoutePreviews = previews;
        _atlasBounds = GodotAdventureAtlasLayout.Union(layout.Values.Select(value => value.WorldBounds));
        _localViewBounds = layout[GodotAdventureAtlasLayout.StartNodeId].WorldBounds;
        _initialOverviewBounds = GodotAdventureAtlasLayout.Union(map.Nodes.Where(value => value.Layer <= 2)
            .Select(value => layout[value.NodeId].WorldBounds));
        ResetAtlasCamera(false);
    }

    public void SetSelection(IReadOnlyCollection<string> selected, bool exitUnlocked)
    {
        string? nextLeader = selected.FirstOrDefault();
        if (IsMovementLocked && !string.Equals(LeaderId, nextLeader, StringComparison.Ordinal))
            _movementPlayer?.Cancel("start-camp-leader-changed");
        _selected.Clear();
        LeaderId = nextLeader;
        foreach (string id in selected) _selected.Add(id);
        foreach ((string id, GodotUnitActor actor) in _candidates)
            actor.Modulate = _selected.Contains(id) ? Colors.White : new Color(.62f, .66f, .68f, .82f);
        Exit.SetUnlocked(exitUnlocked);
    }

    public bool TryResolveTarget(string targetKind, string locator, out Vector2 globalPoint)
    {
        globalPoint = Vector2.Zero;
        if (_mapRoot is null) return false;
        if (targetKind == "AdventureActor" && _candidates.TryGetValue(locator, out GodotUnitActor? actor))
        {
            Rect2 bounds = actor.VisualBoundsInParent();
            foreach (float y in new[] { .12f, .28f, .44f, .60f, .76f, .88f })
            foreach (float x in new[] { .5f, .32f, .68f, .18f, .82f })
            {
                Vector2 point = bounds.Position + new Vector2(bounds.Size.X * x, bounds.Size.Y * y);
                if (actor.ContainsOpaquePoint(point) && ResolveCandidateAtMapPoint(point) == locator)
                {
                    globalPoint = _mapRoot.GlobalTransform * point;
                    return true;
                }
            }
            Vector2 fallback = new(bounds.GetCenter().X, bounds.Position.Y + Math.Min(6f, bounds.Size.Y * .12f));
            globalPoint = _mapRoot.GlobalTransform * fallback;
            return true;
        }
        if (targetKind == "AdventureObject" && locator == "start-exit" && _exit is not null)
        {
            globalPoint = _mapRoot.GlobalTransform * _exit.Position;
            return true;
        }
        if (targetKind == "AdventureCell" && TryCell(locator, out GridPoint cell) && _definition?.Board.Contains(cell) == true)
        {
            globalPoint = _mapRoot.GlobalTransform * MapInstance.Surface.CellCenter(cell);
            return true;
        }
        return false;
    }

    public void HandleAtlasInput(InputEvent input, Vector2 viewPoint) =>
        HandleAtlasInput(input, viewPoint, null, viewPoint);

    private void HandleAtlasInput(InputEvent input, Vector2 viewPoint, Control? source, Vector2 sourcePoint)
    {
        if (_mapRoot is null) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right } right)
        {
            _cameraDragging = right.Pressed;
            if (_cameraMode != StartAtlasCameraMode.Overview) return;
            if (right.Pressed) { _cameraDragOrigin = viewPoint; _cameraPanOrigin = _atlasPan; }
            AcceptInput(source); return;
        }
        if (input is InputEventMouseMotion && _cameraDragging && _cameraMode == StartAtlasCameraMode.Overview)
        {
            _atlasPan = ClampAtlasPan(_cameraPanOrigin + viewPoint - _cameraDragOrigin);
            ApplyAtlasCamera(); AcceptInput(source); return;
        }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown })
        { AcceptInput(source); return; }
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
        Vector2 atlasPoint = _atlasWorldRoot is null ? Vector2.Zero : _atlasWorldRoot.Transform.AffineInverse() * viewPoint;
        Vector2 mapPoint = _mapRoot.Transform.AffineInverse() * atlasPoint;
        string? previewNode = _routePreviewRects.FirstOrDefault(value =>
            value.Key != GodotAdventureAtlasLayout.StartNodeId && value.Value.HasPoint(atlasPoint)).Key;
        if (_cameraMode == StartAtlasCameraMode.Overview)
        {
            StartCampPointerResolution resolution = string.IsNullOrEmpty(previewNode)
                ? StartCampPointerResolution.BlockedOverview
                : StartCampPointerResolution.Preview;
            RecordInput(source, sourcePoint, viewPoint, atlasPoint, mapPoint, resolution, previewNode);
            if (!string.IsNullOrEmpty(previewNode)) PreviewPressed?.Invoke(previewNode);
            AcceptInput(source);
            return;
        }
        if (IsMovementLocked && LeaderId is not null && _definition is not null)
        {
            string? blockedCandidate = ResolveCandidateAtMapPoint(mapPoint);
            bool blockedExit = _exit is not null && _exit.Position.DistanceTo(mapPoint) <= 38f;
            if (blockedCandidate is null && !blockedExit && MapInstance.Surface.TryPointToCell(mapPoint, out GridPoint retargetCell) &&
                _definition.Board.IsWalkable(retargetCell) && !_candidateCells.Where(value => value.Key != LeaderId)
                    .Select(value => value.Value).Contains(retargetCell))
            {
                bool accepted = EnsureMovementPlayer().TryRetarget(retargetCell, out string? rejection);
                RecordInput(source, sourcePoint, viewPoint, atlasPoint, mapPoint, StartCampPointerResolution.Tile,
                    $"{retargetCell.X},{retargetCell.Y}", accepted ? "retargeted" : rejection);
                if (!accepted) MovementRejected?.Invoke(rejection ?? "adventure.destination_unreachable");
            }
            else
            {
                string target = blockedCandidate ?? (blockedExit ? "start-exit" : "occupied-or-invalid");
                RecordInput(source, sourcePoint, viewPoint, atlasPoint, mapPoint,
                    StartCampPointerResolution.BlockedMovement, target, "adventure.movement_in_progress");
                MovementRejected?.Invoke("adventure.movement_in_progress");
            }
            AcceptInput(source);
            return;
        }
        string? candidate = ResolveCandidateAtMapPoint(mapPoint);
        if (candidate is not null)
        {
            int subscriberCount = CandidatePressed?.GetInvocationList().Length ?? 0;
            RecordInput(source, sourcePoint, viewPoint, atlasPoint, mapPoint, StartCampPointerResolution.Candidate,
                candidate, $"dispatching:{subscriberCount}");
            try
            {
                CandidatePressed?.Invoke(candidate);
            }
            catch (Exception exception)
            {
                LastInputDiagnostic = LastInputDiagnostic! with
                {
                    Outcome = $"handler-exception:{exception.GetType().Name}:{Sanitize(exception.Message)}"
                };
                GD.PushError("[StartCampSelection] " + LastInputDiagnostic.ToLogLine());
                throw;
            }
            if (LastInputDiagnostic?.Outcome == $"dispatching:{subscriberCount}")
                LastInputDiagnostic = LastInputDiagnostic with { Outcome = "handled-without-outcome" };
            AcceptInput(source);
            return;
        }
        if (_exit is not null && _exit.Position.DistanceTo(mapPoint) <= 38f)
        {
            RecordInput(source, sourcePoint, viewPoint, atlasPoint, mapPoint, StartCampPointerResolution.Exit, "start-exit");
            ExitPressed?.Invoke();
            AcceptInput(source);
            return;
        }
        if (LeaderId is not null && _definition is not null &&
            MapInstance.Surface.TryPointToCell(mapPoint, out GridPoint cell) && _definition.Board.IsWalkable(cell))
        {
            HashSet<GridPoint> occupied = _candidateCells.Where(value => value.Key != LeaderId)
                .Select(value => value.Value).ToHashSet();
            if (occupied.Contains(cell))
            {
                RecordInput(source, sourcePoint, viewPoint, atlasPoint, mapPoint,
                    StartCampPointerResolution.BlockedOccupied, $"{cell.X},{cell.Y}");
                AcceptInput(source);
                return;
            }
            GridPoint origin = _candidateCells[LeaderId];
            GodotUnitActor leader = _candidates[LeaderId];
            bool started = EnsureMovementPlayer().TryStart(leader, LeaderId, origin, cell, _definition.Board, occupied,
                MapInstance.Surface.CellCenter, out string? rejection);
            RecordInput(source, sourcePoint, viewPoint, atlasPoint, mapPoint,
                StartCampPointerResolution.Tile, $"{cell.X},{cell.Y}", started ? "movement-started" : rejection);
            if (!started) MovementRejected?.Invoke(rejection ?? "adventure.destination_unreachable");
            AcceptInput(source);
            return;
        }
        RecordInput(source, sourcePoint, viewPoint, atlasPoint, mapPoint, StartCampPointerResolution.Miss, null);
        AcceptInput(source);
    }

    public void RecordSelectionOutcome(bool accepted, string? errorCode, int selectedAfter)
    {
        if (LastInputDiagnostic is null || LastInputDiagnostic.Resolution != StartCampPointerResolution.Candidate) return;
        LastInputDiagnostic = LastInputDiagnostic with
        {
            SelectedAfter = selectedAfter,
            Outcome = accepted ? "accepted" : "rejected:" + (errorCode ?? "unknown")
        };
#if DEBUG
        GD.Print("[StartCampSelection] " + LastInputDiagnostic.ToLogLine());
#endif
    }

    public bool HandleAtlasKey(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return false;
        if (key.Keycode == Key.M) { ResetAtlasCamera(IsAtlasOverview ? StartAtlasCameraMode.Current : StartAtlasCameraMode.Overview); return true; }
        if (key.Keycode is Key.F or Key.Home) { FocusLeader(); return true; }
        if (_cameraMode != StartAtlasCameraMode.Overview) return false;
        Vector2 pan = key.Keycode switch
        {
            Key.A or Key.Left => new Vector2(60, 0), Key.D or Key.Right => new Vector2(-60, 0),
            Key.W or Key.Up => new Vector2(0, 60), Key.S or Key.Down => new Vector2(0, -60), _ => Vector2.Zero
        };
        if (pan == Vector2.Zero) return false;
        _atlasPan = ClampAtlasPan(_atlasPan + pan);
        ApplyAtlasCamera();
        return true;
    }

    private void ResetAtlasCamera(bool overview) => ResetAtlasCamera(
        overview ? StartAtlasCameraMode.Overview : StartAtlasCameraMode.Current);

    private void ResetAtlasCamera(StartAtlasCameraMode mode)
    {
        _cameraMode = mode;
        _atlasPan = Vector2.Zero;
        Transform2D currentFit = GodotBattleBoardFitter.Fit(_localViewBounds, AtlasViewport);
        if (mode == StartAtlasCameraMode.Current)
        {
            _atlasZoom = currentFit.X.Length();
            _cameraOrigin = currentFit.Origin;
        }
        else
        {
            _atlasZoom = currentFit.X.Length() * .48f;
            _cameraOrigin = AtlasViewport.GetCenter() - _initialOverviewBounds.GetCenter() * _atlasZoom;
        }
        ApplyAtlasCamera();
        CameraModeChanged?.Invoke(_cameraMode);
    }

    private void FocusLeader()
    {
        _cameraMode = StartAtlasCameraMode.Current;
        Transform2D fit = GodotBattleBoardFitter.Fit(_routePreviewRects[GodotAdventureAtlasLayout.StartNodeId], AtlasViewport);
        _atlasZoom = fit.X.Length();
        Vector2 focus = LeaderId is not null && _candidates.TryGetValue(LeaderId, out GodotUnitActor? leader)
            ? _mapWorldTransform * leader.Position
            : _routePreviewRects[GodotAdventureAtlasLayout.StartNodeId].GetCenter();
        _cameraOrigin = AtlasViewport.GetCenter() - focus * _atlasZoom;
        _atlasPan = Vector2.Zero;
        ApplyAtlasCamera();
        CameraModeChanged?.Invoke(_cameraMode);
    }

    private void ApplyAtlasCamera()
    {
        if (_atlasWorldRoot is not null)
        {
            _atlasPan = ClampAtlasPan(_atlasPan);
            _atlasWorldRoot.Transform = new Transform2D(0f, Vector2.One * _atlasZoom, 0f,
                _cameraOrigin + _atlasPan);
        }
        FittedMapBounds = GodotBattleBoardFitter.TransformBounds(
            _routePreviewRects.GetValueOrDefault(GodotAdventureAtlasLayout.StartNodeId, _atlasBounds),
            _atlasWorldRoot?.Transform ?? Transform2D.Identity);
        QueueRedraw();
    }

    private Vector2 ClampAtlasPan(Vector2 value)
    {
        const float visibleMargin = 80f;
        float minimumX = AtlasViewport.Position.X + visibleMargin - _atlasBounds.End.X * _atlasZoom - _cameraOrigin.X;
        float maximumX = AtlasViewport.End.X - visibleMargin - _atlasBounds.Position.X * _atlasZoom - _cameraOrigin.X;
        float minimumY = AtlasViewport.Position.Y + visibleMargin - _atlasBounds.End.Y * _atlasZoom - _cameraOrigin.Y;
        float maximumY = AtlasViewport.End.Y - visibleMargin - _atlasBounds.Position.Y * _atlasZoom - _cameraOrigin.Y;
        return new Vector2(
            Mathf.Clamp(value.X, Math.Min(minimumX, maximumX), Math.Max(minimumX, maximumX)),
            Mathf.Clamp(value.Y, Math.Min(minimumY, maximumY), Math.Max(minimumY, maximumY)));
    }

    private Transform2D AtWorldBounds(Rect2 bounds) => new(
        _mapWorldTransform.X, _mapWorldTransform.Y, _mapWorldTransform.Origin + bounds.Position);

    private void AddAtlasBadge(PureRunMapNodeDefinition node, Rect2 bounds)
    {
        var badge = new Label
        {
            Name = $"PreviewBadge_{node.NodeId}",
            Text = $"{node.Title ?? node.NodeId} · {node.Kind} · L{node.Layer}",
            Position = bounds.Position + new Vector2(0, -48),
            Size = new Vector2(bounds.Size.X, 36),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        badge.AddThemeFontSizeOverride("font_size", 22);
        _atlasWorldRoot!.AddChild(badge);
    }

    internal string? ResolveCandidateAt(Vector2 viewPoint)
    {
        if (_atlasWorldRoot is null || _mapRoot is null) return null;
        Vector2 atlasPoint = _atlasWorldRoot.Transform.AffineInverse() * viewPoint;
        return ResolveCandidateAtMapPoint(_mapRoot.Transform.AffineInverse() * atlasPoint);
    }

    private void AcceptInput(Control? source)
    {
        if (source is not null) source.AcceptEvent();
        else AcceptEvent();
    }

    private void RecordInput(Control? source, Vector2 sourcePoint, Vector2 viewPoint, Vector2 atlasPoint,
        Vector2 mapPoint, StartCampPointerResolution resolution, string? targetId, string? outcome = null)
    {
        (string? nearestId, Rect2? nearestBounds, float? nearestDistance) = NearestCandidate(mapPoint);
        LastInputDiagnostic = new GodotStartCampInputDiagnostic(
            ++_inputAttemptId,
            source?.Name.ToString() ?? Name.ToString(),
            GetViewport()?.GuiGetHoveredControl()?.Name.ToString() ?? "none",
            _cameraMode,
            sourcePoint,
            viewPoint,
            atlasPoint,
            mapPoint,
            resolution,
            targetId,
            nearestId,
            nearestBounds,
            nearestDistance,
            _selected.Count,
            _selected.Count,
            outcome);
#if DEBUG
        GD.Print("[StartCampInput] " + LastInputDiagnostic.ToLogLine());
#endif
    }

    private static bool TryCell(string value, out GridPoint cell)
    {
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
        {
            cell = new GridPoint(x, y);
            return true;
        }
        cell = default;
        return false;
    }

    private static string Sanitize(string value) => value
        .Replace('\r', '_')
        .Replace('\n', '_')
        .Replace('\t', '_')
        .Replace(' ', '_')
        .Replace('=', '_');

    private (string? Id, Rect2? Bounds, float? Distance) NearestCandidate(Vector2 mapPoint)
    {
        (string Id, Rect2 Bounds, float Distance)? nearest = null;
        foreach ((string id, GodotUnitActor actor) in _candidates)
        {
            if (!GodotObject.IsInstanceValid(actor) || !actor.Visible) continue;
            Rect2 bounds = actor.VisualBoundsInParent().Grow(8f);
            Vector2 closest = new(
                Mathf.Clamp(mapPoint.X, bounds.Position.X, bounds.End.X),
                Mathf.Clamp(mapPoint.Y, bounds.Position.Y, bounds.End.Y));
            float distance = closest.DistanceTo(mapPoint);
            if (nearest is null || distance < nearest.Value.Distance ||
                Mathf.IsEqualApprox(distance, nearest.Value.Distance) && string.CompareOrdinal(id, nearest.Value.Id) < 0)
                nearest = (id, bounds, distance);
        }
        return nearest is null ? (null, null, null) : (nearest.Value.Id, nearest.Value.Bounds, nearest.Value.Distance);
    }

    private string? ResolveCandidateAtMapPoint(Vector2 mapPoint) => _candidates
        .Where(value => GodotObject.IsInstanceValid(value.Value) && value.Value.Visible)
        .Select(value => new
        {
            value.Key,
            Actor = value.Value,
            Opaque = value.Value.ContainsOpaquePoint(mapPoint),
            InsideBounds = value.Value.VisualBoundsInParent().Grow(8f).HasPoint(mapPoint)
        })
        .Where(value => value.Opaque || value.InsideBounds)
        .OrderByDescending(value => value.Opaque)
        .ThenByDescending(value => value.Actor.ZIndex)
        .ThenBy(value => value.Key, StringComparer.Ordinal)
        .Select(value => value.Key)
        .FirstOrDefault();

    private Vector2 ConnectionPoint(string nodeId, bool source)
    {
        if (!_nodeTransforms.TryGetValue(nodeId, out Transform2D transform))
            throw new InvalidOperationException($"Atlas connection references missing node '{nodeId}'.");
        AdventureMapTemplateDefinition definition = nodeId == GodotAdventureAtlasLayout.StartNodeId
            ? _definition ?? throw new InvalidOperationException("Start template is missing.")
            : _planningDefinition ?? throw new InvalidOperationException("Planning template is missing.");
        GridPoint cell = source
            ? definition.Exits.SingleOrDefault()?.Cell ?? throw new InvalidOperationException($"Atlas source node '{nodeId}' has no exit anchor.")
            : definition.Entries.SingleOrDefault()?.Cell ?? throw new InvalidOperationException($"Atlas target node '{nodeId}' has no entry anchor.");
        return transform * MapInstance.Surface.CellCenter(cell);
    }

}

public partial class GodotStartCampfireActor : Node2D
{
    public override void _Ready() => QueueRedraw();
    public override void _Draw()
    {
        DrawLine(new Vector2(-22, 10), new Vector2(22, 20), new Color("67402b"), 9, true);
        DrawLine(new Vector2(22, 10), new Vector2(-22, 20), new Color("67402b"), 9, true);
        DrawPolygon([new Vector2(0, -38), new Vector2(-20, 14), new Vector2(20, 14)], [new Color("f06b32")]);
        DrawPolygon([new Vector2(0, -20), new Vector2(-10, 12), new Vector2(10, 12)], [new Color("ffd05a")]);
    }
}

public partial class GodotStartCampExitActor : Node2D
{
    public bool IsUnlocked { get; private set; }
    public void SetUnlocked(bool unlocked) { IsUnlocked = unlocked; QueueRedraw(); }
    public override void _Ready() => QueueRedraw();
    public override void _Draw()
    {
        Color color = IsUnlocked ? new Color("63d98b") : new Color("6d7378");
        DrawPolyline([new Vector2(-38, 0), new Vector2(0, -20), new Vector2(38, 0), new Vector2(0, 20), new Vector2(-38, 0)], color, 5, true);
        DrawPolygon([new Vector2(-8, -9), new Vector2(15, 0), new Vector2(-8, 9)], [color]);
    }
}
