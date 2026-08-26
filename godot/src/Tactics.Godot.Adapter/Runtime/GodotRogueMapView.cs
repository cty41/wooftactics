using Godot;
using Tactics.Application.Runs;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Draws the engine-neutral Pure Run map snapshot without owning run mutations.</summary>
public partial class GodotRogueMapView : Control
{
    public static readonly Vector2 PreferredSize = new(980, 720);
    private const float LayerSpacing = 82f;
    private const float LaneSpacing = 145f;
    private const float NodeRadius = 28f;
    private PureRunMapSnapshot? _snapshot;
    private Vector2 _pan;
    private Vector2 _dragOrigin;
    private Vector2 _panOrigin;
    private float _zoom = 1f;
    private bool _overview;
    private bool _dragging;
    private bool _moved;
    private string? _hoveredNodeId;

    public event Action<string>? NodePressed;
    public event Action<PureRunMapNodeSnapshot?>? NodeHovered;

    public PureRunMapSnapshot? Snapshot => _snapshot;
    public float Zoom => _zoom;
    public Vector2 Pan => _pan;

    public override void _Ready()
    {
        CustomMinimumSize = PreferredSize;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        FocusMode = FocusModeEnum.All;
        SetProcess(true);
    }

    public void SetSnapshot(PureRunMapSnapshot snapshot, bool centerOnFocus)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        if (centerOnFocus) CenterOn(snapshot.FocusNodeId);
        QueueRedraw();
    }

    public Vector2 NodeCenter(string nodeId)
    {
        PureRunMapNodeSnapshot node = _snapshot?.Nodes.Single(value => value.NodeId == nodeId)
            ?? throw new InvalidOperationException($"Unknown map node: {nodeId}");
        float x = Size.X * .5f + node.Lane * LaneSpacing;
        float y = Size.Y - 62f - node.Layer * LayerSpacing;
        Vector2 basePoint = new(x, y);
        return Size * .5f + (basePoint - Size * .5f) * _zoom + _pan;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("263742"));
        if (_snapshot is null) return;
        foreach (PureRunMapConnectionSnapshot connection in _snapshot.Connections)
        {
            PureRunMapNodeSnapshot from = _snapshot.Nodes.Single(value => value.NodeId == connection.FromNodeId);
            PureRunMapNodeSnapshot to = _snapshot.Nodes.Single(value => value.NodeId == connection.ToNodeId);
            bool available = from.State is PureRunMapNodeState.Available or PureRunMapNodeState.Current or PureRunMapNodeState.Completed &&
                to.State is PureRunMapNodeState.Available or PureRunMapNodeState.Current or PureRunMapNodeState.Pending or PureRunMapNodeState.Completed;
            Color color = connection.Traversed ? new Color("d8c58a") : available ? new Color("7895a1") : new Color("40515a");
            Vector2 start = NodeCenter(connection.FromNodeId);
            Vector2 end = NodeCenter(connection.ToNodeId);
            Vector2 direction = start.DirectionTo(end);
            DrawLine(start + direction * NodeRadius, end - direction * NodeRadius, color,
                connection.Traversed ? 4f : available ? 2.5f : 1.25f, true);
        }
        foreach (PureRunMapNodeSnapshot node in _snapshot.Nodes)
            DrawNode(node);
        DrawHints();
    }

    public override void _Process(double delta)
    {
        if (!HasFocus()) return;
        Vector2 direction = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) direction.X += 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) direction.X -= 1;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) direction.Y += 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) direction.Y -= 1;
        if (direction != Vector2.Zero)
        {
            _pan = ClampPan(_pan + direction.Normalized() * (float)delta * 360f);
            QueueRedraw();
        }
    }

    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key || _snapshot is null) return;
        if (key.Keycode == Key.M)
        {
            _overview = !_overview;
            _zoom = _overview ? .72f : 1f;
            _pan = Vector2.Zero;
            QueueRedraw(); AcceptEvent();
        }
        else if (key.Keycode is Key.F or Key.Home)
        {
            _overview = false; _zoom = 1f; CenterOn(_snapshot.FocusNodeId); QueueRedraw(); AcceptEvent();
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } pressed:
                _dragging = true; _moved = false; _dragOrigin = pressed.Position; _panOrigin = _pan;
                GrabFocus();
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _dragging:
                Vector2 delta = motion.Position - _dragOrigin;
                if (delta.LengthSquared() > 16f) _moved = true;
                _pan = ClampPan(_panOrigin + delta); QueueRedraw(); AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false }:
                _dragging = false;
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } clicked:
                GrabFocus();
                if (FindNode(clicked.Position) is PureRunMapNodeSnapshot node) NodePressed?.Invoke(node.NodeId);
                AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheelUp:
                ZoomAt(wheelUp.Position, 1.12f); AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheelDown:
                ZoomAt(wheelDown.Position, 1f / 1.12f); AcceptEvent();
                break;
            case InputEventMouseMotion hover when !_dragging:
                PureRunMapNodeSnapshot? current = FindNode(hover.Position);
                if (current?.NodeId != _hoveredNodeId)
                {
                    _hoveredNodeId = current?.NodeId;
                    NodeHovered?.Invoke(current);
                    QueueRedraw();
                }
                break;
        }
    }

    private void ZoomAt(Vector2 pointer, float factor)
    {
        float next = Mathf.Clamp(_zoom * factor, .62f, 1.65f);
        Vector2 center = Size * .5f;
        Vector2 worldAtPointer = (pointer - center - _pan) / _zoom;
        _zoom = next;
        _pan = ClampPan(pointer - center - worldAtPointer * _zoom);
        _overview = false;
        QueueRedraw();
    }

    private void DrawHints()
    {
        Font font = ThemeDB.FallbackFont;
        const string hints = "[LMB] inspect   [RMB hold] drag   [Wheel] zoom   [M] overview   [F/Home] focus";
        Vector2 size = font.GetStringSize(hints, HorizontalAlignment.Left, -1, 14);
        Vector2 origin = new(Size.X - size.X - 22, Size.Y - 20);
        DrawRect(new Rect2(origin - new Vector2(10, 17), size + new Vector2(20, 23)), new Color(0, 0, 0, .42f));
        DrawString(font, origin, hints, HorizontalAlignment.Left, -1, 14, new Color("c4d0d3b8"));
    }

    private void DrawNode(PureRunMapNodeSnapshot node)
    {
        Vector2 center = NodeCenter(node.NodeId);
        Color fill = node.State switch
        {
            PureRunMapNodeState.Available => new Color("4fa9c6"),
            PureRunMapNodeState.Current => new Color("e4b85d"),
            PureRunMapNodeState.Selected => new Color("d99257"),
            PureRunMapNodeState.Pending => new Color("d56b5f"),
            PureRunMapNodeState.Completed => new Color("d9d6c7"),
            _ => new Color("52616a")
        };
        DrawCircle(center, NodeRadius, fill);
        Color outline = node.NodeId == _hoveredNodeId ? Colors.White : new Color("17242c");
        DrawArc(center, NodeRadius, 0, Mathf.Tau, 40, outline, node.NodeId == _hoveredNodeId ? 4f : 2f, true);
        string glyph = node.Kind switch
        {
            Tactics.Core.Runs.PureRunNodeKind.Battle => "B",
            Tactics.Core.Runs.PureRunNodeKind.Rest => "R",
            Tactics.Core.Runs.PureRunNodeKind.Store => "$",
            _ => "?"
        };
        Font font = ThemeDB.FallbackFont;
        DrawString(font, center + new Vector2(-8, 8), glyph, HorizontalAlignment.Left, -1, 22,
            node.State == PureRunMapNodeState.Locked ? new Color("9ba8ae") : new Color("17242c"));
        DrawString(font, center + new Vector2(-58, 49), node.Title, HorizontalAlignment.Center, 116, 16,
            node.State == PureRunMapNodeState.Locked ? new Color("87969d") : new Color("edf1ed"));
        DrawString(font, center + new Vector2(-48, -37), $"L{node.Layer}", HorizontalAlignment.Center, 96, 13,
            new Color("b7c6cc"));
    }

    private PureRunMapNodeSnapshot? FindNode(Vector2 point) => _snapshot?.Nodes
        .Where(node => point.DistanceSquaredTo(NodeCenter(node.NodeId)) <= NodeRadius * NodeRadius)
        .OrderBy(node => point.DistanceSquaredTo(NodeCenter(node.NodeId)))
        .ThenBy(node => node.NodeId, StringComparer.Ordinal).FirstOrDefault();

    private void CenterOn(string nodeId)
    {
        _pan = Vector2.Zero;
        Vector2 center = NodeCenter(nodeId);
        _pan = ClampPan(new Vector2(0, Size.Y * .5f - center.Y));
    }

    private Vector2 ClampPan(Vector2 value) => new(
        Mathf.Clamp(value.X, -Size.X * .65f, Size.X * .65f),
        Mathf.Clamp(value.Y, -Size.Y * .65f, Size.Y * .65f));
}
