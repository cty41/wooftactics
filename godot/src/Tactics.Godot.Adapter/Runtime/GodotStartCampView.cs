using Godot;
using Tactics.Core.Board;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record GodotStartCampCandidate(string CharacterId, UnitDefinitionResource Definition);
public enum StartAtlasCameraMode { Current, Overview }

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

    public event Action<string>? CandidatePressed;
    public event Action? ExitPressed;
    public event Action<GridPoint>? LeaderMoved;
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

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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
        _selected.Clear();
        LeaderId = selected.FirstOrDefault();
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
            globalPoint = _mapRoot.GlobalTransform * actor.Position;
            return true;
        }
        if (targetKind == "AdventureObject" && locator == "start-exit" && _exit is not null)
        {
            globalPoint = _mapRoot.GlobalTransform * _exit.Position;
            return true;
        }
        return false;
    }

    public void HandleAtlasInput(InputEvent input, Vector2 viewPoint)
    {
        if (_mapRoot is null) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right } right)
        {
            _cameraDragging = right.Pressed;
            if (_cameraMode != StartAtlasCameraMode.Overview) return;
            if (right.Pressed) { _cameraDragOrigin = viewPoint; _cameraPanOrigin = _atlasPan; }
            AcceptEvent(); return;
        }
        if (input is InputEventMouseMotion && _cameraDragging && _cameraMode == StartAtlasCameraMode.Overview)
        {
            _atlasPan = ClampAtlasPan(_cameraPanOrigin + viewPoint - _cameraDragOrigin);
            ApplyAtlasCamera(); AcceptEvent(); return;
        }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown })
        { AcceptEvent(); return; }
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button) return;
        Vector2 atlasPoint = _atlasWorldRoot is null ? Vector2.Zero : _atlasWorldRoot.Transform.AffineInverse() * viewPoint;
        string? previewNode = _routePreviewRects.FirstOrDefault(value =>
            value.Key != GodotAdventureAtlasLayout.StartNodeId && value.Value.HasPoint(atlasPoint)).Key;
        if (_cameraMode == StartAtlasCameraMode.Overview)
        {
            if (!string.IsNullOrEmpty(previewNode)) PreviewPressed?.Invoke(previewNode);
            AcceptEvent();
            return;
        }
        Vector2 mapPoint = _mapRoot.Transform.AffineInverse() * atlasPoint;
        string? candidate = ResolveCandidateAtMapPoint(mapPoint);
        if (candidate is not null)
        {
            CandidatePressed?.Invoke(candidate);
            AcceptEvent();
            return;
        }
        if (_exit is not null && _exit.Position.DistanceTo(mapPoint) <= 38f)
        {
            ExitPressed?.Invoke();
            AcceptEvent();
            return;
        }
        if (LeaderId is not null && _definition is not null &&
            MapInstance.Surface.TryPointToCell(mapPoint, out GridPoint cell) && _definition.Board.IsWalkable(cell))
        {
            HashSet<GridPoint> occupied = _candidateCells.Where(value => value.Key != LeaderId)
                .Select(value => value.Value).ToHashSet();
            if (occupied.Contains(cell)) { AcceptEvent(); return; }
            _candidateCells[LeaderId] = cell;
            GodotUnitActor leader = _candidates[LeaderId];
            leader.Position = MapInstance.Surface.CellCenter(cell);
            leader.ZIndex = 30 + cell.X + cell.Y;
            LeaderMoved?.Invoke(cell);
            AcceptEvent();
        }
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

    private string? ResolveCandidateAtMapPoint(Vector2 mapPoint) => _candidates
        .Where(value => GodotObject.IsInstanceValid(value.Value) && value.Value.Visible &&
            (value.Value.ContainsOpaquePoint(mapPoint) || value.Value.VisualBoundsInParent().Grow(8f).HasPoint(mapPoint)))
        .OrderByDescending(value => value.Value.ZIndex)
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
