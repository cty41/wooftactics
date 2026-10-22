using Godot;
using Tactics.Core.Board;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Production-input surface backed by a real Godot TileMapLayer and transient leader movement.</summary>
public partial class GodotAdventureBoardView : Control
{
    private const string SharedTileSetPath = "res://content/adventure_maps/AdventureMapTileSetV1.tres";
    public static readonly Rect2 CanonicalGameplaySafeArea = new(40, 40, 1520, 820);
    private readonly Dictionary<string, GodotUnitActor> _actors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _objects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GridPoint> _actorCells = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UnitDefinitionResource> _actorDefinitions = new(StringComparer.Ordinal);
    private TileMapLayer? _tiles;
    private GodotActWorldHost? _residentHost;
    private GodotNodeActivationLease? _residentLease;
    private GodotIsometricTileMapSurface? _residentSurface;
    private AdventureBoardDefinition? _definition;
    private GodotAdventureMovementPlayer? _movementPlayer;
    private StandardUnitPresentationResource _movementProfile = new();
    private string? _leaderId;
    private float _movementSpeed = 1f;
    private Rect2 _localBoardBounds;
    private Transform2D _boardTransform = Transform2D.Identity;
    private float _boardScale = 1f;

    public event Action<GridPoint>? CellPressed;
    public event Action<string>? ActorPressed;
    public event Action<string>? ObjectPressed;
    public event Action<string>? MovementRejected;
    public event Action? ManualMovementRequested;
    public event Action<GodotAdventureMovementCompletion>? MovementCompleted;
    public event Action<GridPoint>? MovementInterruptedAtCell;
    public TileMapLayer TileLayer => _residentSurface?.TerrainLayer ?? _tiles ??
        throw new InvalidOperationException("Adventure board is not ready.");
    public bool UsesResidentSurface => _residentSurface is not null;
    public GodotIsometricTileMapSurface? ResidentSurface => _residentSurface;
    public AdventureBoardDefinition Definition => _definition ?? throw new InvalidOperationException("Adventure board is not configured.");
    public IReadOnlyDictionary<string, GridPoint> ActorCells => _actorCells;
    public IReadOnlyDictionary<string, GodotUnitActor> ActorNodes => _actors;
    public bool IsMovementLocked => _movementPlayer?.IsLocked == true;
    private bool IsResidentLeaseCurrent => _residentHost is null ||
        (_residentLease is { } lease && _residentHost.IsLeaseCurrent(lease));
    public GodotAdventureMovementDiagnostic? LastMovementDiagnostic => _movementPlayer?.LastDiagnostic;
    public GridPoint? LastPointerCell { get; private set; }
    public Rect2 FittedBoardBounds => _tiles is null
        ? default
        : GodotBattleBoardFitter.TransformBounds(_localBoardBounds, _boardTransform);
    public float BoardScale => _boardScale;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        TileSet tileSet = ResourceLoader.Load<TileSet>(SharedTileSetPath)
            ?? throw new InvalidOperationException($"Generated adventure TileSet is missing: {SharedTileSetPath}.");
        _tiles = new TileMapLayer { Name = "AdventureTileMapLayer", TileSet = tileSet };
        AddChild(_tiles);
        EnsureMovementPlayer();
        GuiInput += OnGuiInput;
    }

    public override void _ExitTree()
    {
        GuiInput -= OnGuiInput;
        _movementPlayer?.Cancel("adventure-board-exit-tree");
    }

    public override void _Notification(int what)
    {
        if (what != NotificationResized || _definition is null || _tiles is null || !IsNodeReady() ||
            !IsResidentLeaseCurrent) return;
        GridPoint? interruptedCell = _movementPlayer?.IsMoving == true ? _movementPlayer.CurrentCell : null;
        _movementPlayer?.Cancel("adventure-board-viewport-resized");
        if (interruptedCell is GridPoint cell) MovementInterruptedAtCell?.Invoke(cell);
        FitBoardToViewport();
        RelayoutMarkers();
    }

    public void ConfigureActors(IReadOnlyDictionary<string, UnitDefinitionResource> definitions, string leaderId,
        StandardUnitPresentationResource profile, float speed = 1f)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaderId);
        _actorDefinitions.Clear();
        foreach ((string actorId, UnitDefinitionResource definition) in definitions) _actorDefinitions[actorId] = definition;
        _leaderId = leaderId;
        _movementProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        _movementSpeed = speed;
        EnsureMovementPlayer().Configure(profile, speed);
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
        definition.Validate();
        foreach (AdventureActorPlacement actor in definition.Actors)
            if (!_actorDefinitions.ContainsKey(actor.ActorId))
                throw new InvalidOperationException($"Adventure actor '{actor.ActorId}' has no injected UnitDefinitionResource.");
        if (definition.Actors.Count > 0 && (_leaderId is null || !definition.Actors.Any(value => value.ActorId == _leaderId)))
            throw new InvalidOperationException($"Adventure leader '{_leaderId ?? "none"}' is not present on board '{definition.ContentId.Value}'.");

        EnsureMovementPlayer().Cancel("adventure-board-reset");
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
        if (IsMovementLocked) throw new InvalidOperationException("Adventure actors cannot be repositioned during movement.");
        if (!Definition.IsWalkable(cell)) throw new ArgumentException("Adventure actor destination is not walkable.", nameof(cell));
        if (_actorCells.Any(value => value.Key != actorId && value.Value == cell))
            throw new ArgumentException("Adventure actor destination is occupied.", nameof(cell));
        _actorCells[actorId] = cell;
        if (_actors.TryGetValue(actorId, out GodotUnitActor? actor))
        {
            actor.Position = CellCenter(cell);
            actor.ZIndex = ActorZIndex(cell);
        }
    }

    public void SetMovementPaused(bool paused) => EnsureMovementPlayer().SetPaused(paused);
    public void SetMovementSpeed(float speed)
    {
        _movementSpeed = speed;
        EnsureMovementPlayer().SetSpeed(speed);
    }
    public void CancelMovement(string reason) => _movementPlayer?.Cancel(reason);

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

    private void OnGuiInput(InputEvent input)
    {
        if (!IsResidentLeaseCurrent) return;
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button ||
            _definition is null || !TryPointToCell(button.Position, out GridPoint cell)) return;
        LastPointerCell = cell;
        string? actorId = _actorCells.FirstOrDefault(value => value.Value == cell).Key;
        AdventureBoardObject? value = _definition.Objects.FirstOrDefault(candidate => candidate.Cell == cell);
        if (IsMovementLocked)
        {
            if (actorId is not null || value is not null)
                MovementRejected?.Invoke("adventure.movement_in_progress");
            else
            {
                ManualMovementRequested?.Invoke();
                RequestLeaderMovement(cell);
            }
            AcceptEvent();
            return;
        }
        if (actorId is not null) { ActorPressed?.Invoke(actorId); AcceptEvent(); return; }
        if (value is not null) { ObjectPressed?.Invoke(value.ObjectId); AcceptEvent(); return; }
        CellPressed?.Invoke(cell);
        ManualMovementRequested?.Invoke();
        RequestLeaderMovement(cell);
        AcceptEvent();
    }

    private void RequestLeaderMovement(GridPoint destination)
    {
        if (!TryRequestLeaderMovement(destination, out string? rejection))
            MovementRejected?.Invoke(rejection ?? "adventure.destination_unreachable");
    }

    public bool TryRequestLeaderMovement(GridPoint destination, out string? rejection)
    {
        if (_definition is null || _leaderId is null || !_actorCells.TryGetValue(_leaderId, out GridPoint origin) ||
            !_actors.TryGetValue(_leaderId, out GodotUnitActor? actor))
        {
            rejection = "adventure.leader_not_found";
            return false;
        }
        GridPoint[] occupied = _actorCells.Where(value => value.Key != _leaderId).Select(value => value.Value).ToArray();
        return IsMovementLocked
            ? EnsureMovementPlayer().TryRetarget(destination, out rejection)
            : EnsureMovementPlayer().TryStart(actor, _leaderId, origin, destination, _definition, occupied, CellCenter, out rejection);
    }

    private GodotAdventureMovementPlayer EnsureMovementPlayer()
    {
        if (_movementPlayer is not null && GodotObject.IsInstanceValid(_movementPlayer)) return _movementPlayer;
        _movementPlayer = new GodotAdventureMovementPlayer { Name = "AdventureMovementPlayer" };
        AddChild(_movementPlayer);
        _movementPlayer.Configure(_movementProfile, _movementSpeed);
        _movementPlayer.CellCentered += cell =>
        {
            if (!IsResidentLeaseCurrent || _leaderId is null ||
                !_actors.TryGetValue(_leaderId, out GodotUnitActor? actor)) return;
            _actorCells[_leaderId] = cell;
            actor.ZIndex = ActorZIndex(cell);
        };
        _movementPlayer.MovementCompleted += completion =>
        {
            if (IsResidentLeaseCurrent) MovementCompleted?.Invoke(completion);
        };
        return _movementPlayer;
    }

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

    private void RebuildMarkers()
    {
        foreach (Node marker in _actors.Values.Cast<Node>().Concat(_objects.Values)) marker.QueueFree();
        _actors.Clear();
        _objects.Clear();
        foreach (AdventureActorPlacement placement in Definition.Actors)
        {
            UnitDefinitionResource resource = _actorDefinitions[placement.ActorId];
            GodotUnitActor actor = GodotUnitFactory.InstantiateActor(resource);
            actor.Name = "AdventureActor_" + placement.ActorId;
            actor.ConfigureInstanceIdentity("adventure:" + placement.ActorId);
            actor.ConfigurePresentation(_movementProfile);
            actor.Position = CellCenter(placement.Cell);
            actor.Scale = Vector2.One * (.34f * _boardScale);
            actor.ZIndex = ActorZIndex(placement.Cell);
            AddChild(actor);
            _actors[placement.ActorId] = actor;
        }
        foreach (AdventureBoardObject value in Definition.Objects)
            _objects[value.ObjectId] = AddObjectMarker(value.ObjectId, value.Cell, new Color("ffb347"));
    }

    private void RelayoutMarkers()
    {
        foreach ((string actorId, GodotUnitActor actor) in _actors)
        {
            GridPoint cell = _actorCells[actorId];
            actor.Position = CellCenter(cell);
            actor.Scale = Vector2.One * (.34f * _boardScale);
            actor.ZIndex = ActorZIndex(cell);
        }
        foreach (AdventureBoardObject value in Definition.Objects)
            if (_objects.TryGetValue(value.ObjectId, out Label? marker))
                marker.Position = CellCenter(value.Cell) - new Vector2(42, 34);
    }

    private Label AddObjectMarker(string id, GridPoint cell, Color color)
    {
        var marker = new Label { Name = id, Text = id, Modulate = color, MouseFilter = MouseFilterEnum.Ignore,
            Position = CellCenter(cell) - new Vector2(42, 34), Size = new Vector2(84, 28), HorizontalAlignment = HorizontalAlignment.Center,
            ZIndex = 60 + cell.X + cell.Y };
        AddChild(marker);
        return marker;
    }

    private static int ActorZIndex(GridPoint cell) => 30 + cell.X + cell.Y;

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
