using Godot;
using Tactics.Core.Board;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Presentation-only board surface; legal sets are supplied by the Application snapshot.</summary>
[GlobalClass]
public partial class GodotIsometricBattleBoard : Control
{
    private readonly Dictionary<GridPoint, Color> _colors = new();
    private readonly HashSet<GridPoint> _blocked = new();
    private readonly HashSet<GridPoint> _shallowWater = new();
    private GridPoint? _hovered;
    private GodotUnitActor? _activeActor;
    private Vector2? _lastActivePosition;
    private GodotActWorldHost? _residentHost;
    private GodotNodeActivationLease _lease;
    private GodotIsometricTileMapSurface? _residentSurface;
    private Transform2D? _lastSurfaceTransform;
    private long _bindingRevision;
    public bool UsesResidentSurface { get; private set; }
    public GodotIsometricTileMapSurface? ResidentSurface => _residentSurface;

    /// <summary>Borrow terrain identity only. The caller owns activation and the world camera.</summary>
    public void BindResidentSurface(GodotActWorldHost host, GodotNodeActivationLease lease)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!GodotObject.IsInstanceValid(host) || host.IsQueuedForDeletion() || !host.IsLeaseCurrent(lease))
            throw new InvalidOperationException("battle.stale_surface_lease");
        GodotIsometricTileMapSurface surface = host.Nodes[lease.NodeId].Surface;
        if (!IsInsideTree() || !surface.IsInsideTree() || GetViewport() != surface.GetViewport())
            throw new InvalidOperationException("battle.surface_viewport_mismatch");
        _bindingRevision++;
        _residentHost = host;
        _residentSurface = surface;
        _lease = lease;
        UsesResidentSurface = true;
        _lastSurfaceTransform = null;
        _hovered = null;
        _colors.Clear();
        ZIndex = Math.Max(ZIndex, 10);
        SetProcess(true);
        QueueRedraw();
    }

    private bool HasCurrentSurface => _residentHost is not null && _residentSurface is not null &&
        GodotObject.IsInstanceValid(_residentHost) && !_residentHost.IsQueuedForDeletion() &&
        GodotObject.IsInstanceValid(_residentSurface) && !_residentSurface.IsQueuedForDeletion() &&
        _residentHost.IsLeaseCurrent(_lease);

    private Transform2D SurfaceToBoard => GetGlobalTransformWithCanvas().AffineInverse() * _residentSurface!.GetGlobalTransformWithCanvas();

    public Vector2 CellCenter(GridPoint cell)
    {
        if (!UsesResidentSurface) return IsometricBattleBoardLayout.GridToScreen(cell);
        if (!HasCurrentSurface) throw new InvalidOperationException("battle.stale_surface_lease");
        return SurfaceToBoard * _residentSurface!.CellCenter(cell);
    }

    public bool TryPointToCell(Vector2 point, out GridPoint cell)
    {
        if (!UsesResidentSurface) return IsometricBattleBoardLayout.TryScreenToGrid(point, out cell);
        if (!HasCurrentSurface) { cell = default; return false; }
        return _residentSurface!.TryPointToCell(SurfaceToBoard.AffineInverse() * point, out cell);
    }

    public override void _ExitTree()
    {
        _bindingRevision++;
        _residentHost = null;
        _residentSurface = null;
        _activeActor = null;
        SetProcess(false);
        // Never deactivate, reconfigure, reparent or free the borrowed map.
    }

    public event Action<GridPoint>? CellPressed;
    public event Action<Vector2>? PointerPressed;
    public event Action<GridPoint>? CellHovered;
    public event Action? HoverCleared;
    public event Action<Vector2>? PointerMoved;
    public event Action? PointerExited;
    public Vector2? ActiveMarkerPosition => _lastActivePosition;

    public static Color BaseTileColor(GridPoint cell, bool blocked)
    {
        if (blocked) return new Color(0.14f, 0.19f, 0.22f, 0.96f);
        return (cell.X + cell.Y) % 2 == 0
            ? new Color(0.39f, 0.36f, 0.32f, 0.96f)
            : new Color(0.32f, 0.38f, 0.41f, 0.96f);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        QueueRedraw();
    }

    public void SetVisuals(IReadOnlyDictionary<GridPoint, Color> colors, IEnumerable<GridPoint> blocked,
        IEnumerable<GridPoint>? shallowWater = null)
    {
        if (UsesResidentSurface && !HasCurrentSurface) return;
        HashSet<GridPoint> blockedCells = blocked.ToHashSet();
        HashSet<GridPoint> waterCells = (shallowWater ?? Array.Empty<GridPoint>()).ToHashSet();
        if (UsesResidentSurface)
        {
            var template = _residentSurface!.Template!.ToCoreDefinition();
            if (!blockedCells.SetEquals(template.Board.BlockedCells) ||
                !waterCells.SetEquals(template.ShallowWaterCells ?? Array.Empty<GridPoint>()))
                throw new InvalidOperationException("battle.surface_layout_mismatch");
        }
        _colors.Clear();
        foreach ((GridPoint cell, Color color) in colors) _colors[cell] = color;
        _blocked.Clear();
        foreach (GridPoint cell in blockedCells) _blocked.Add(cell);
        _shallowWater.Clear();
        foreach (GridPoint cell in waterCells) _shallowWater.Add(cell);
        QueueRedraw();
    }

    public void FollowActiveActor(GodotUnitActor? actor)
    {
        _activeActor = actor;
        _lastActivePosition = ActorPosition(actor);
        SetProcess(UsesResidentSurface || actor is not null);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (UsesResidentSurface)
        {
            if (!HasCurrentSurface)
            {
                _activeActor = null;
                _lastActivePosition = null;
                _hovered = null;
                SetProcess(false);
                QueueRedraw();
                return;
            }
            Transform2D transform = SurfaceToBoard;
            if (_lastSurfaceTransform != transform) { _lastSurfaceTransform = transform; QueueRedraw(); }
        }
        Vector2? next = ActorPosition(_activeActor);
        if (next == _lastActivePosition) return;
        _lastActivePosition = next;
        QueueRedraw();
    }

    private Vector2? ActorPosition(GodotUnitActor? actor) => actor is not null && GodotObject.IsInstanceValid(actor)
        ? (UsesResidentSurface ? GetGlobalTransformWithCanvas().AffineInverse() * actor.GetGlobalTransformWithCanvas().Origin : actor.Position) : null;

    private bool CanPublishInput(long revision) => revision == _bindingRevision && !IsQueuedForDeletion() &&
        (!UsesResidentSurface || HasCurrentSurface);

    public override void _GuiInput(InputEvent inputEvent)
    {
        long revision = _bindingRevision;
        if (!CanPublishInput(revision)) return;
        switch (inputEvent)
        {
            case InputEventMouseMotion motion:
                UpdateHover(motion.Position);
                if (CanPublishInput(revision)) PointerMoved?.Invoke(motion.Position);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } button:
                AcceptEvent();
                if (PointerPressed is not null) PointerPressed.Invoke(button.Position);
                else if (TryPointToCell(button.Position, out GridPoint cell)) CellPressed?.Invoke(cell);
                break;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit)
        {
            _hovered = null;
            long revision = _bindingRevision;
            if (!CanPublishInput(revision)) return;
            HoverCleared?.Invoke();
            if (CanPublishInput(revision)) PointerExited?.Invoke();
        }
    }

    public override void _Draw()
    {
        if (UsesResidentSurface && !HasCurrentSurface) return;
        Transform2D projection = UsesResidentSurface ? SurfaceToBoard : Transform2D.Identity;
        for (int sum = 18; sum >= 0; sum--)
        for (int x = 0; x < IsometricBattleBoardLayout.GridSize; x++)
        {
            int y = sum - x;
            if (y < 0 || y >= IsometricBattleBoardLayout.GridSize) continue;
            GridPoint cell = new(x, y);
            if (UsesResidentSurface)
            {
                if (_colors.TryGetValue(cell, out Color highlight))
                    DrawColoredPolygon(IsometricBattleBoardLayout.Diamond(cell).Select(point => projection * point).ToArray(), highlight);
                continue; // Terrain/decoration/water are the existing TileMap, never a replacement polygon board.
            }
            Color baseColor = BaseTileColor(cell, _blocked.Contains(cell));
            if (_shallowWater.Contains(cell)) baseColor = baseColor.Lerp(new Color(0.12f, 0.58f, 0.64f, 0.96f), 0.72f);
            Color fill = _colors.TryGetValue(cell, out Color overlay) ? baseColor.Lerp(overlay, Math.Clamp(overlay.A, 0f, 1f)) : baseColor;
            Vector2[] diamond = IsometricBattleBoardLayout.Diamond(cell);
            DrawColoredPolygon(diamond, fill);
            DrawPolyline([..diamond, diamond[0]], new Color(0.10f, 0.17f, 0.20f, 0.95f), 1.4f, true);
            if (_shallowWater.Contains(cell))
                DrawLine((diamond[3] + diamond[0]) / 2f + new Vector2(7, 3),
                    (diamond[0] + diamond[1]) / 2f + new Vector2(-7, 3), new Color(.65f, .94f, .91f, .42f), 1.2f, true);
        }
        if (_lastActivePosition is Vector2 center)
        {
            Vector2[] offsets = [new(0, -24), new(48, 0), new(0, 24), new(-48, 0)];
            Vector2[] marker = offsets.Select(offset => center + (UsesResidentSurface
                ? projection * offset - projection.Origin : offset)).ToArray();
            DrawColoredPolygon(marker, new Color(.95f, .66f, .24f, .55f));
            DrawPolyline([..marker, marker[0]], new Color(1f, .82f, .38f, .9f), 2f, true);
        }
    }

    private void UpdateHover(Vector2 position)
    {
        GridPoint? next = TryPointToCell(position, out GridPoint cell) ? cell : null;
        if (next == _hovered) return;
        _hovered = next;
        if (next is GridPoint value) CellHovered?.Invoke(value);
        else HoverCleared?.Invoke();
    }
}
