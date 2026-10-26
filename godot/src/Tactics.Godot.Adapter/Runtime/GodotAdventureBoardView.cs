using Godot;
using Tactics.Core.Board;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Production-input surface backed by a real Godot TileMapLayer.</summary>
public partial class GodotAdventureBoardView : Control
{
    private const string SharedTileSetPath = "res://content/adventure_maps/AdventureMapTileSetV1.tres";
    private static readonly Vector2 BoardOffset = new(400, 400);
    private readonly Dictionary<string, Label> _actors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _objects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GridPoint> _actorCells = new(StringComparer.Ordinal);
    private TileMapLayer? _tiles;
    private GodotActWorldHost? _residentHost;
    private GodotNodeActivationLease? _residentLease;
    private GodotIsometricTileMapSurface? _residentSurface;
    private AdventureBoardDefinition? _definition;
    private bool _inputConnected;
    private Rect2 _localBoardBounds;
    private Transform2D _boardTransform = Transform2D.Identity;
    private float _boardScale = 1f;

    public event Action<GridPoint>? CellPressed;
    public event Action<string>? ActorPressed;
    public event Action<string>? ObjectPressed;
    public TileMapLayer TileLayer => _residentSurface?.TerrainLayer ?? _tiles ??
        throw new InvalidOperationException("Adventure board is not ready.");
    public bool UsesResidentSurface => _residentSurface is not null;
    public GodotIsometricTileMapSurface? ResidentSurface => _residentSurface;
    public AdventureBoardDefinition Definition => _definition ?? throw new InvalidOperationException("Adventure board is not configured.");
    public IReadOnlyDictionary<string, GridPoint> ActorCells => _actorCells;
    private bool IsResidentLeaseCurrent => _residentHost is null ||
        (_residentLease is { } lease && _residentHost.IsLeaseCurrent(lease));
    public Rect2 FittedBoardBounds => _tiles is null
        ? default
        : GodotBattleBoardFitter.TransformBounds(_localBoardBounds, _boardTransform);
    public float BoardScale => _boardScale;
    public void CancelMovement(string reason) { }

    public override void _Ready() => EnsureInitialized();

    public override void _ExitTree()
    {
        if (!_inputConnected) return;
        GuiInput -= OnGuiInput;
        _inputConnected = false;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationResized || _definition is null || _tiles is null || !IsNodeReady() ||
            !IsResidentLeaseCurrent) return;
        FitBoardToViewport();
        RelayoutMarkers();
    }

    public void BindResidentSurface(GodotActWorldHost host, GodotNodeActivationLease lease,
        GodotIsometricTileMapSurface surface)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(surface);
        if (!host.IsPrepared || !host.IsLeaseCurrent(lease) || !GodotObject.IsInstanceValid(surface) || surface.IsQueuedForDeletion())
            throw new InvalidOperationException("adventure.resident_surface_unavailable");
        _residentHost = host;
        _residentLease = lease;
        _residentSurface = surface;
        if (_tiles is not null) _tiles.Visible = false;
    }

    public void SetBoard(AdventureBoardDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        EnsureInitialized();
        definition.Validate();
        _definition = definition;
        _actorCells.Clear();
        foreach (AdventureActorPlacement actor in definition.Actors) _actorCells[actor.ActorId] = actor.Cell;
        if (_residentSurface is null)
        {
            TileLayer.Clear();
            foreach (int y in Enumerable.Range(0, definition.Height))
            foreach (int x in Enumerable.Range(0, definition.Width))
            {
                GridPoint cell = new(x, y);
                TileLayer.SetCell(new Vector2I(x, y), 0,
                    definition.IsWalkable(cell) ? new Vector2I(0, 0) : new Vector2I(1, 0));
            }
        }
        FitBoardToViewport();
        RebuildMarkers();
    }

    public bool TryResolveTarget(string targetKind, string locator, out Vector2 point)
    {
        point = Vector2.Zero;
        if (_definition is null) return false;
        if (targetKind == "AdventureCell" && TryCell(locator, out GridPoint cell))
        { point = CellCenter(cell); return _definition.Contains(cell); }
        if (targetKind == "AdventureActor" && _actorCells.TryGetValue(locator, out GridPoint actorCell))
        { point = CellCenter(actorCell); return true; }
        if (targetKind == "AdventureObject" && _definition.Objects.FirstOrDefault(value => value.ObjectId == locator) is { } value)
        { point = CellCenter(value.Cell); return true; }
        if (targetKind == "RouteNode" && _definition.Objects.FirstOrDefault(value => value.ObjectId == locator &&
                value.Kind is AdventureObjectKind.Rest or AdventureObjectKind.Store or AdventureObjectKind.Treasure or
                    AdventureObjectKind.Battle or AdventureObjectKind.Event or AdventureObjectKind.Escort) is { } route)
        { point = CellCenter(route.Cell); return true; }
        return false;
    }

    public void MoveActor(string actorId, GridPoint cell)
    {
        if (!_actorCells.ContainsKey(actorId)) throw new ArgumentException("Unknown adventure actor.", nameof(actorId));
        if (!Definition.IsWalkable(cell)) throw new ArgumentException("Adventure actor destination is not walkable.", nameof(cell));
        _actorCells[actorId] = cell;
        if (_actors.TryGetValue(actorId, out Label? marker))
            marker.Position = CellCenter(cell) - new Vector2(42, 34);
    }

    public Vector2 CellCenter(GridPoint cell)
    {
        if (_residentSurface is null)
            return TileLayer.Transform * TileLayer.MapToLocal(new Vector2I(cell.X, cell.Y));
        return GetGlobalTransformWithCanvas().AffineInverse() * _residentSurface.GetGlobalTransformWithCanvas() *
            _residentSurface.CellCenter(cell);
    }

    public bool TryPointToCell(Vector2 point, out GridPoint cell)
    {
        Vector2I mapped;
        if (_residentSurface is null)
        {
            Vector2 localPoint = TileLayer.Transform.AffineInverse() * point;
            mapped = TileLayer.LocalToMap(localPoint);
        }
        else
        {
            Vector2 viewportPoint = GetGlobalTransformWithCanvas() * point;
            Vector2 surfacePoint = _residentSurface.GetGlobalTransformWithCanvas().AffineInverse() * viewportPoint;
            if (!_residentSurface.TryPointToCell(surfacePoint, out cell)) return false;
            return _definition?.Contains(cell) == true;
        }
        cell = new GridPoint(mapped.X, mapped.Y);
        return _definition?.Contains(cell) == true;
    }

    private void EnsureInitialized()
    {
        if (_tiles is null)
        {
            MouseFilter = MouseFilterEnum.Stop;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            TileSet tileSet = ResourceLoader.Load<TileSet>(SharedTileSetPath)
                ?? throw new InvalidOperationException($"Generated adventure TileSet is missing: {SharedTileSetPath}.");
            _tiles = new TileMapLayer { Name = "AdventureTileMapLayer", TileSet = tileSet, Position = BoardOffset };
            AddChild(_tiles);
        }
        if (_inputConnected) return;
        GuiInput += OnGuiInput;
        _inputConnected = true;
    }

    private void OnGuiInput(InputEvent input)
    {
        if (!IsResidentLeaseCurrent) return;
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button ||
            _definition is null || !TryPointToCell(button.Position, out GridPoint cell)) return;
        string? actorId = _actorCells.FirstOrDefault(value => value.Value == cell).Key;
        if (actorId is not null) { ActorPressed?.Invoke(actorId); AcceptEvent(); return; }
        AdventureBoardObject? value = _definition.Objects.FirstOrDefault(candidate => candidate.Cell == cell);
        if (value is not null) { ObjectPressed?.Invoke(value.ObjectId); AcceptEvent(); return; }
        CellPressed?.Invoke(cell);
        AcceptEvent();
    }

    public static readonly Rect2 CanonicalGameplaySafeArea = new(40, 40, 1520, 820);

    public static Rect2 GameplaySafeArea(Vector2 viewportSize)
    {
        if (viewportSize.X <= 0 || viewportSize.Y <= 0) return CanonicalGameplaySafeArea;
        return new Rect2(viewportSize.X * .025f, viewportSize.Y * (40f / 900f),
            viewportSize.X * .95f, viewportSize.Y * (820f / 900f));
    }

    private void FitBoardToViewport()
    {
        Vector2[] centers = Enumerable.Range(0, Definition.Height)
            .SelectMany(y => Enumerable.Range(0, Definition.Width).Select(x => _residentSurface is null
                ? TileLayer.MapToLocal(new Vector2I(x, y))
                : _residentSurface.CellCenter(new GridPoint(x, y))))
            .ToArray();
        Vector2 halfTile = new Vector2(TileLayer.TileSet.TileSize.X, TileLayer.TileSet.TileSize.Y) * .5f;
        float minX = centers.Min(value => value.X) - halfTile.X;
        float minY = centers.Min(value => value.Y) - halfTile.Y;
        float maxX = centers.Max(value => value.X) + halfTile.X;
        float maxY = centers.Max(value => value.Y) + halfTile.Y;
        _localBoardBounds = new Rect2(minX, minY, maxX - minX, maxY - minY);
        _boardTransform = GodotBattleBoardFitter.Fit(_localBoardBounds, GameplaySafeArea(Size));
        _boardScale = _boardTransform.X.Length();
        if (_residentSurface is null)
        {
            TileLayer.Transform = _boardTransform;
            return;
        }
        GodotActWorldHost host = _residentHost ?? throw new InvalidOperationException("adventure.resident_host_missing");
        Transform2D hostViewport = host.GetGlobalTransformWithCanvas();
        Transform2D parentViewport = hostViewport * host.Transform.AffineInverse();
        Transform2D surfaceToHost = hostViewport.AffineInverse() * _residentSurface.GetGlobalTransformWithCanvas();
        host.Transform = parentViewport.AffineInverse() * GetGlobalTransformWithCanvas() * _boardTransform *
            surfaceToHost.AffineInverse();
    }

    private void RelayoutMarkers()
    {
        foreach ((string actorId, Label marker) in _actors)
        {
            if (!_actorCells.TryGetValue(actorId, out GridPoint cell)) continue;
            marker.Position = CellCenter(cell) - new Vector2(42, 34);
        }
        foreach (AdventureBoardObject value in Definition.Objects)
            if (_objects.TryGetValue(value.ObjectId, out Label? marker))
                marker.Position = CellCenter(value.Cell) - new Vector2(42, 34);
    }

    private void RebuildMarkers()
    {
        foreach (Label marker in _actors.Values.Concat(_objects.Values)) marker.QueueFree();
        _actors.Clear(); _objects.Clear();
        foreach (AdventureActorPlacement actor in Definition.Actors)
            _actors[actor.ActorId] = AddMarker(actor.ActorId, actor.Cell, new Color("6ec8ff"));
        foreach (AdventureBoardObject value in Definition.Objects)
            _objects[value.ObjectId] = AddMarker(value.ObjectId, value.Cell, new Color("ffb347"));
    }

    private Label AddMarker(string id, GridPoint cell, Color color)
    {
        var marker = new Label { Name = id, Text = id, Modulate = color, MouseFilter = MouseFilterEnum.Ignore,
            Position = CellCenter(cell) - new Vector2(42, 34), Size = new Vector2(84, 28), HorizontalAlignment = HorizontalAlignment.Center };
        AddChild(marker);
        return marker;
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
}
