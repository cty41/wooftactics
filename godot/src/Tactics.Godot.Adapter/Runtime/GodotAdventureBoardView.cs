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
    private AdventureBoardDefinition? _definition;

    public event Action<GridPoint>? CellPressed;
    public event Action<string>? ActorPressed;
    public event Action<string>? ObjectPressed;
    public TileMapLayer TileLayer => _tiles ?? throw new InvalidOperationException("Adventure board is not ready.");
    public AdventureBoardDefinition Definition => _definition ?? throw new InvalidOperationException("Adventure board is not configured.");
    public IReadOnlyDictionary<string, GridPoint> ActorCells => _actorCells;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        TileSet tileSet = ResourceLoader.Load<TileSet>(SharedTileSetPath)
            ?? throw new InvalidOperationException($"Generated adventure TileSet is missing: {SharedTileSetPath}.");
        _tiles = new TileMapLayer { Name = "AdventureTileMapLayer", TileSet = tileSet, Position = BoardOffset };
        AddChild(_tiles);
        GuiInput += OnGuiInput;
    }

    public override void _ExitTree() => GuiInput -= OnGuiInput;

    public void SetBoard(AdventureBoardDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        _definition = definition;
        _actorCells.Clear();
        foreach (AdventureActorPlacement actor in definition.Actors) _actorCells[actor.ActorId] = actor.Cell;
        TileLayer.Clear();
        foreach (int y in Enumerable.Range(0, definition.Height))
        foreach (int x in Enumerable.Range(0, definition.Width))
        {
            GridPoint cell = new(x, y);
            TileLayer.SetCell(new Vector2I(x, y), 0, definition.IsWalkable(cell) ? new Vector2I(0, 0) : new Vector2I(1, 0));
        }
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

    public Vector2 CellCenter(GridPoint cell) => TileLayer.Position + TileLayer.MapToLocal(new Vector2I(cell.X, cell.Y));

    public bool TryPointToCell(Vector2 point, out GridPoint cell)
    {
        Vector2I mapped = TileLayer.LocalToMap(point - TileLayer.Position);
        cell = new GridPoint(mapped.X, mapped.Y);
        return _definition?.Contains(cell) == true;
    }

    private void OnGuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button ||
            _definition is null || !TryPointToCell(button.Position, out GridPoint cell)) return;
        string? actorId = _actorCells.FirstOrDefault(value => value.Value == cell).Key;
        if (actorId is not null) { ActorPressed?.Invoke(actorId); AcceptEvent(); return; }
        AdventureBoardObject? value = _definition.Objects.FirstOrDefault(candidate => candidate.Cell == cell);
        if (value is not null) { ObjectPressed?.Invoke(value.ObjectId); AcceptEvent(); return; }
        CellPressed?.Invoke(cell);
        AcceptEvent();
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
