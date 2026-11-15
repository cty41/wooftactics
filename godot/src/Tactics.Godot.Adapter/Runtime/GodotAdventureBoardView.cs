using Godot;
using Tactics.Core.Board;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Production-input surface backed by a real Godot TileMapLayer.</summary>
public partial class GodotAdventureBoardView : Control
{
    private const string SharedTileSetPath = "res://content/adventure_maps/AdventureMapTileSetV1.tres";
    private static readonly Vector2 BoardOffset = new(400, 400);
    // Resident maps show the same GodotUnitActor presentation as battle; other boards keep text markers.
    private readonly Dictionary<string, CanvasItem> _actors = new(StringComparer.Ordinal);
    private const float UnitActorScale = .34f;
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
    /// <summary>Extra surface-local points (for example visible successor maps) the camera must keep in frame.</summary>
    public IReadOnlyList<Vector2> FrameExtraPoints { get; set; } = Array.Empty<Vector2>();
    /// <summary>Smallest board scale <see cref="FrameExtraPoints"/> may force; below it only nearer points are framed (needs manual review).</summary>
    public const float MinFrameScale = .6f;
    public bool UsesResidentSurface => _residentSurface is not null;

    /// <summary>
    /// Exit objects on a resident map are drawn on the outer top-right rim of the diamond (half a cell beyond the
    /// exit cell along +X), not at the centre of an interior tile. Returns false for non-exit or non-resident use.
    /// </summary>
    public bool TryGetExitRimPosition(string objectId, out Vector2 position)
    {
        position = default;
        if (_residentSurface is null || _definition is null) return false;
        AdventureBoardObject? exit = _definition.Objects.FirstOrDefault(value => value.ObjectId == objectId && value.Kind == AdventureObjectKind.Exit);
        if (exit is null) return false;
        position = RimPosition(exit.Cell);
        return true;
    }

    private Vector2 RimPosition(GridPoint cell)
    {
        Vector2 center = _residentSurface!.CellCenter(cell);
        Vector2 inner = _residentSurface.CellCenter(new GridPoint(cell.X - 1, cell.Y));
        Vector2 local = center + (center - inner) * .5f;
        return GetGlobalTransformWithCanvas().AffineInverse() * _residentSurface.GetGlobalTransformWithCanvas() * local;
    }

    private Vector2 MarkerPosition(AdventureBoardObject value) =>
        value.Kind == AdventureObjectKind.Exit && _residentSurface is not null ? RimPosition(value.Cell) : CellCenter(value.Cell);
    /// <summary>
    /// Unit presentation per actor id (character id). Used only with a resident surface: set before
    /// <see cref="SetBoard"/> so party members are drawn with their battle sprites instead of text labels.
    /// </summary>
    public IReadOnlyDictionary<string, UnitDefinitionResource> ActorUnitDefinitions { get; set; } =
        new Dictionary<string, UnitDefinitionResource>();
    /// <summary>True when the actor is drawn with its battle <see cref="GodotUnitActor"/>.</summary>
    public bool IsActorUnit(string actorId) => _actors.TryGetValue(actorId, out CanvasItem? node) && node is GodotUnitActor;
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
        // The open tile just inside an exit, so specs name the exit instead of hardcoding its approach cell.
        if (targetKind == "AdventureExitApproach" && _definition.Objects.FirstOrDefault(value =>
                value.ObjectId == locator && value.Kind == AdventureObjectKind.Exit) is { } exitObject)
        {
            var inner = new GridPoint(exitObject.Cell.X - 1, exitObject.Cell.Y);
            point = CellCenter(inner);
            return _definition.IsWalkable(inner);
        }
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
        if (_actors.TryGetValue(actorId, out CanvasItem? marker)) PlaceActorNode(marker, cell);
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
            _definition is null) return;
        if (!TryPointToCell(button.Position, out GridPoint cell))
        {
            // The rim marker is the visible exit and sits just outside the board; only clicks that miss every
            // board cell may hit it, so it never steals clicks meant for the walkable cells beside it.
            if (_residentSurface is not null)
                foreach (AdventureBoardObject exit in _definition.Objects.Where(value => value.Kind == AdventureObjectKind.Exit))
                    if (RimPosition(exit.Cell).DistanceTo(button.Position) <= 32f)
                    { ObjectPressed?.Invoke(exit.ObjectId); AcceptEvent(); return; }
            return;
        }
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
        // Keep the current map readable: add extra points nearest-first and stop before the fit would shrink
        // the world below MinFrameScale (far successors then fall outside the frame instead of shrinking the map).
        Vector2 mapCentre = new((minX + maxX) / 2f, (minY + maxY) / 2f);
        Rect2 safeArea = GameplaySafeArea(Size);
        foreach (Vector2 extra in FrameExtraPoints.OrderBy(value => value.DistanceSquaredTo(mapCentre)))
        {
            float nextMinX = Math.Min(minX, extra.X), nextMinY = Math.Min(minY, extra.Y);
            float nextMaxX = Math.Max(maxX, extra.X), nextMaxY = Math.Max(maxY, extra.Y);
            var candidate = new Rect2(nextMinX, nextMinY, nextMaxX - nextMinX, nextMaxY - nextMinY);
            if (GodotBattleBoardFitter.Fit(candidate, safeArea).X.Length() < MinFrameScale) break;
            minX = nextMinX; minY = nextMinY; maxX = nextMaxX; maxY = nextMaxY;
        }
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
        foreach ((string actorId, CanvasItem marker) in _actors)
        {
            if (!_actorCells.TryGetValue(actorId, out GridPoint cell)) continue;
            PlaceActorNode(marker, cell);
        }
        foreach (AdventureBoardObject value in Definition.Objects)
            if (_objects.TryGetValue(value.ObjectId, out Label? marker))
                marker.Position = MarkerPosition(value) - new Vector2(42, 34);
    }

    private void PlaceActorNode(CanvasItem node, GridPoint cell)
    {
        if (node is GodotUnitActor unit)
        {
            unit.Position = CellCenter(cell);
            unit.Scale = Vector2.One * UnitActorScale * _boardScale;
            unit.ZIndex = 30 + cell.X + cell.Y;
        }
        else if (node is Label label) label.Position = CellCenter(cell) - new Vector2(42, 34);
    }

    private void RebuildMarkers()
    {
        foreach (CanvasItem marker in _actors.Values.Concat<CanvasItem>(_objects.Values)) marker.QueueFree();
        _actors.Clear(); _objects.Clear();
        foreach (AdventureActorPlacement actor in Definition.Actors)
        {
            if (_residentSurface is not null && ActorUnitDefinitions.TryGetValue(actor.ActorId, out UnitDefinitionResource? unitDefinition))
            {
                GodotUnitActor unit = GodotUnitFactory.InstantiateActor(unitDefinition);
                unit.Name = actor.ActorId;
                unit.ConfigureInstanceIdentity("adventure:" + actor.ActorId);
                AddChild(unit);
                PlaceActorNode(unit, actor.Cell);
                _actors[actor.ActorId] = unit;
            }
            else _actors[actor.ActorId] = AddMarker(actor.ActorId, actor.Cell, new Color("6ec8ff"));
        }
        foreach (AdventureBoardObject value in Definition.Objects)
            _objects[value.ObjectId] = AddMarker(value.ObjectId, value.Cell, new Color("ffb347"), MarkerPosition(value));
    }

    private Label AddMarker(string id, GridPoint cell, Color color, Vector2? at = null)
    {
        var marker = new Label { Name = id, Text = id, Modulate = color, MouseFilter = MouseFilterEnum.Ignore,
            Position = (at ?? CellCenter(cell)) - new Vector2(42, 34), Size = new Vector2(84, 28), HorizontalAlignment = HorizontalAlignment.Center };
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
