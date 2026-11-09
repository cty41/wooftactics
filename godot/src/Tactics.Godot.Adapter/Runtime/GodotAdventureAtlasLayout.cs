using Godot;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record GodotAdventureAtlasNodeLayout(
    string NodeId,
    int Layer,
    float Lane,
    Rect2 WorldBounds);

/// <summary>
/// Projects the engine-neutral route onto the isometric atlas along the boards' own axes: each layer advances one
/// board along grid +X (screen up-right, beyond the x = 9 exit rim) and lanes line up along grid +Y (screen up-left,
/// parallel to that rim), so every map sits parallel to its neighbours.
/// </summary>
public static class GodotAdventureAtlasLayout
{
    public const string StartNodeId = "start";
    /// <summary>Gap between neighbouring boards as a fraction of one board step along each axis.</summary>
    public const float LayerGapFraction = .35f;
    public const float LaneGapFraction = .18f;

    /// <summary>Screen offset of one whole board along grid +X (up-right) for a board whose bounding box is <paramref name="mapSize"/>.</summary>
    public static Vector2 BoardAxisX(Vector2 mapSize) => new(mapSize.X * .5f, -mapSize.Y * .5f);
    /// <summary>Screen offset of one whole board along grid +Y (up-left).</summary>
    public static Vector2 BoardAxisY(Vector2 mapSize) => new(-mapSize.X * .5f, -mapSize.Y * .5f);

    public static IReadOnlyDictionary<string, GodotAdventureAtlasNodeLayout> Project(
        PureRunMapDefinition map,
        Vector2 mapSize)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (mapSize.X <= 0 || mapSize.Y <= 0) throw new ArgumentOutOfRangeException(nameof(mapSize));
        if (!map.Nodes.Any(value => value.NodeId == StartNodeId))
            throw new ArgumentException("Atlas topology requires the start node.", nameof(map));

        Vector2 layerStep = BoardAxisX(mapSize) * (1f + LayerGapFraction);
        Vector2 laneStep = BoardAxisY(mapSize) * (1f + LaneGapFraction);
        return map.Nodes.ToDictionary(
            node => node.NodeId,
            node =>
            {
                Vector2 position = layerStep * node.Layer + laneStep * node.Lane;
                return new GodotAdventureAtlasNodeLayout(
                    node.NodeId, node.Layer, node.Lane, new Rect2(position, mapSize));
            },
            StringComparer.Ordinal);
    }

    /// <summary>True when <paramref name="point"/> lies on the board diamond inscribed in <paramref name="bounds"/>.</summary>
    public static bool DiamondContains(Rect2 bounds, Vector2 point)
    {
        Vector2 half = bounds.Size * .5f;
        if (half.X <= 0 || half.Y <= 0) return false;
        Vector2 delta = point - bounds.GetCenter();
        return MathF.Abs(delta.X) / half.X + MathF.Abs(delta.Y) / half.Y <= 1f;
    }

    public static Rect2 Union(IEnumerable<Rect2> bounds)
    {
        Rect2[] values = bounds.ToArray();
        if (values.Length == 0) throw new ArgumentException("Atlas bounds cannot be empty.", nameof(bounds));
        float left = values.Min(value => value.Position.X);
        float top = values.Min(value => value.Position.Y);
        float right = values.Max(value => value.End.X);
        float bottom = values.Max(value => value.End.Y);
        return new Rect2(left, top, right - left, bottom - top);
    }
}
