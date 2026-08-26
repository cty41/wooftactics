using Godot;
using Tactics.Core.Board;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record GodotStartCampCandidate(string CharacterId, UnitDefinitionResource Definition);

/// <summary>Full-scale, typed start-camp surface used by party selection.</summary>
public partial class GodotStartCampView : Control
{
    public static readonly Rect2 SafeMapArea = new(new Vector2(40, 130), new Vector2(1520, 590));
    private readonly Dictionary<string, GodotUnitActor> _candidates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GridPoint> _candidateCells = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private AdventureMapTemplateDefinition? _definition;
    private Node2D? _mapRoot;
    private GodotStartCampfireActor? _campfire;
    private GodotStartCampExitActor? _exit;
    private Node2D? _routePreviewRoot;
    private readonly Dictionary<string, Rect2> _routePreviewRects = new(StringComparer.Ordinal);
    private Transform2D _mapBaseTransform;
    private Vector2 _routeBasePosition;
    private Vector2 _atlasPan;
    private float _atlasZoom = 1f;
    private bool _atlasOverview;
    private bool _cameraDragging;
    private Vector2 _cameraDragOrigin;
    private Vector2 _cameraPanOrigin;

    public event Action<string>? CandidatePressed;
    public event Action? ExitPressed;
    public event Action<GridPoint>? LeaderMoved;
    public event Action<string>? PreviewPressed;
    public string? LeaderId { get; private set; }
    public GodotAdventureMapInstance MapInstance { get; private set; } = null!;
    public IReadOnlyDictionary<string, GodotUnitActor> CandidateActors => _candidates;
    public IReadOnlyDictionary<string, GridPoint> CandidateCells => _candidateCells;
    public GodotStartCampfireActor Campfire => _campfire ?? throw new InvalidOperationException("Start camp is not configured.");
    public GodotStartCampExitActor Exit => _exit ?? throw new InvalidOperationException("Start camp is not configured.");
    public Rect2 FittedMapBounds { get; private set; }
    public IReadOnlyList<GodotAdventureMapInstance> RoutePreviews { get; private set; } = Array.Empty<GodotAdventureMapInstance>();
    public float AtlasZoom => _atlasZoom;
    public Vector2 AtlasPan => _atlasPan;
    public bool IsAtlasOverview => _atlasOverview;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GuiInput += OnGuiInput;
    }

    public override void _ExitTree() => GuiInput -= OnGuiInput;

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

        _mapRoot?.QueueFree();
        _candidates.Clear();
        _candidateCells.Clear();
        _selected.Clear();
        _mapRoot = new Node2D { Name = "StartCampMapRoot" };
        AddChild(_mapRoot);
        MapInstance = new GodotAdventureMapInstance { Name = "StartCampMapInstance" };
        _mapRoot.AddChild(MapInstance);
        MapInstance.Configure(template);
        MapInstance.Activate();

        Transform2D fit = GodotBattleBoardFitter.Fit(GodotBattleBoardFitter.BoardBounds(), SafeMapArea);
        _mapRoot.Transform = fit;
        _mapBaseTransform = fit;
        FittedMapBounds = GodotBattleBoardFitter.TransformBounds(GodotBattleBoardFitter.BoardBounds(), fit);

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
        _routePreviewRoot?.QueueFree();
        _routePreviewRoot = new Node2D { Name = "StartCampRoutePreviewRoot", Position = new Vector2(760, 385), ZIndex = 80 };
        _routeBasePosition = _routePreviewRoot.Position;
        _routePreviewRects.Clear();
        AddChild(_routePreviewRoot);
        var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        foreach (PureRunMapNodeDefinition node in map.Nodes)
        {
            Vector2 position = new((node.Lane + 1.8f) * 165f, (7 - node.Layer) * 105f);
            positions[node.NodeId] = position;
            _routePreviewRects[node.NodeId] = new Rect2(position - new Vector2(8, 8), new Vector2(110, 86));
        }
        foreach (PureRunMapConnectionDefinition edge in map.Connections)
        {
            if (!positions.TryGetValue(edge.FromNodeId, out Vector2 from) || !positions.TryGetValue(edge.ToNodeId, out Vector2 to)) continue;
            var line = new Line2D { Name = $"Route_{edge.FromNodeId}_{edge.ToNodeId}", Width = 3f,
                DefaultColor = new Color("657b86a0"), Points = [from + new Vector2(46, 24), to + new Vector2(46, 24)] };
            _routePreviewRoot.AddChild(line);
        }
        var previews = new List<GodotAdventureMapInstance>();
        foreach (PureRunMapNodeDefinition node in map.Nodes)
        {
            var preview = new GodotAdventureMapInstance { Name = $"PlanningPreview_{node.NodeId}", Position = positions[node.NodeId], Scale = Vector2.One * .095f, ZIndex = 2 };
            _routePreviewRoot.AddChild(preview);
            preview.Configure(planningTemplate);
            preview.Deactivate();
            previews.Add(preview);
            var badge = new Label { Name = $"PreviewBadge_{node.NodeId}", Text = $"{node.Kind} L{node.Layer}", Position = positions[node.NodeId] + new Vector2(-4, 52),
                MouseFilter = MouseFilterEnum.Ignore };
            badge.AddThemeFontSizeOverride("font_size", 13);
            _routePreviewRoot.AddChild(badge);
        }
        RoutePreviews = previews;
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

    private void OnGuiInput(InputEvent input)
    {
        if (_mapRoot is null) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right } right)
        {
            _cameraDragging = right.Pressed;
            if (right.Pressed) { _cameraDragOrigin = right.Position; _cameraPanOrigin = _atlasPan; GrabFocus(); }
            AcceptEvent(); return;
        }
        if (input is InputEventMouseMotion motion && _cameraDragging)
        {
            _atlasPan = ClampAtlasPan(_cameraPanOrigin + motion.Position - _cameraDragOrigin);
            ApplyAtlasCamera(); AcceptEvent(); return;
        }
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheelUp)
        { ZoomAtlasAt(wheelUp.Position, 1.1f); AcceptEvent(); return; }
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheelDown)
        { ZoomAtlasAt(wheelDown.Position, 1f / 1.1f); AcceptEvent(); return; }
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button) return;
        GrabFocus();
        Vector2 routePoint = _routePreviewRoot is null ? Vector2.Zero : _routePreviewRoot.Transform.AffineInverse() * button.Position;
        string? previewNode = _routePreviewRects.FirstOrDefault(value => value.Value.HasPoint(routePoint)).Key;
        if (previewNode is not null)
        {
            PreviewPressed?.Invoke(previewNode);
            AcceptEvent();
            return;
        }
        Vector2 mapPoint = _mapRoot.Transform.AffineInverse() * button.Position;
        string? candidate = _candidateCells.FirstOrDefault(value =>
            MapInstance.Surface.CellCenter(value.Value).DistanceTo(mapPoint) <= 34f).Key;
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

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key || !HasFocus()) return;
        Vector2 pan = key.Keycode switch
        {
            Key.A or Key.Left => new Vector2(60, 0), Key.D or Key.Right => new Vector2(-60, 0),
            Key.W or Key.Up => new Vector2(0, 60), Key.S or Key.Down => new Vector2(0, -60), _ => Vector2.Zero
        };
        if (pan != Vector2.Zero) { _atlasPan = ClampAtlasPan(_atlasPan + pan); ApplyAtlasCamera(); AcceptEvent(); return; }
        if (key.Keycode == Key.M) { ResetAtlasCamera(!_atlasOverview); AcceptEvent(); }
        else if (key.Keycode is Key.F or Key.Home) { FocusLeader(); AcceptEvent(); }
    }

    private void ResetAtlasCamera(bool overview)
    {
        _atlasZoom = overview ? .78f : 1f;
        _atlasOverview = overview;
        _atlasPan = Vector2.Zero;
        ApplyAtlasCamera();
    }

    private void FocusLeader()
    {
        _atlasOverview = false;
        _atlasZoom = 1f;
        if (LeaderId is null || !_candidates.TryGetValue(LeaderId, out GodotUnitActor? leader))
        {
            _atlasPan = Vector2.Zero;
        }
        else
        {
            Vector2 baseLeaderPoint = _mapBaseTransform * leader.Position;
            _atlasPan = ClampAtlasPan(Size * .5f - baseLeaderPoint);
        }
        ApplyAtlasCamera();
    }

    private void ZoomAtlasAt(Vector2 pointer, float factor)
    {
        float next = Mathf.Clamp(_atlasZoom * factor, .7f, 1.35f);
        Vector2 center = Size * .5f;
        Vector2 logical = (pointer - center - _atlasPan) / _atlasZoom;
        _atlasZoom = next;
        _atlasOverview = false;
        _atlasPan = ClampAtlasPan(pointer - center - logical * next);
        ApplyAtlasCamera();
    }

    private void ApplyAtlasCamera()
    {
        if (_mapRoot is not null)
            _mapRoot.Transform = new Transform2D(_mapBaseTransform.X * _atlasZoom, _mapBaseTransform.Y * _atlasZoom,
                Size * .5f + (_mapBaseTransform.Origin - Size * .5f) * _atlasZoom + _atlasPan);
        if (_routePreviewRoot is not null)
        {
            _routePreviewRoot.Scale = Vector2.One * _atlasZoom;
            _routePreviewRoot.Position = Size * .5f + (_routeBasePosition - Size * .5f) * _atlasZoom + _atlasPan;
        }
        QueueRedraw();
    }

    private Vector2 ClampAtlasPan(Vector2 value) => new(Mathf.Clamp(value.X, -520, 520), Mathf.Clamp(value.Y, -300, 300));

    public override void _Draw()
    {
        const string hints = "[LMB] select/move/inspect   [RMB hold] drag   [Wheel] zoom   [M] overview   [F/Home] leader";
        Font font = ThemeDB.FallbackFont;
        Vector2 textSize = font.GetStringSize(hints, HorizontalAlignment.Left, -1, 14);
        Vector2 origin = new(Size.X - textSize.X - 22, Size.Y - 20);
        DrawRect(new Rect2(origin - new Vector2(10, 17), textSize + new Vector2(20, 23)), new Color(0, 0, 0, .42f));
        DrawString(font, origin, hints, HorizontalAlignment.Left, -1, 14, new Color("c4d0d3b8"));
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
