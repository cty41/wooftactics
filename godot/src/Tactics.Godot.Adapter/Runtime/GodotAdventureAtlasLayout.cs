using Godot;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record GodotAdventureAtlasNodeLayout(
    string NodeId,
    int Layer,
    float Lane,
    Rect2 WorldBounds);

/// <summary>Projects the engine-neutral route onto an isometric, right-up atlas world.</summary>
public static class GodotAdventureAtlasLayout
{
    public const string StartNodeId = "start";
    public const float LayerGap = 160f;
    public const float DiagonalGap = 100f;
    public const float BranchGap = 80f;

    public static IReadOnlyDictionary<string, GodotAdventureAtlasNodeLayout> Project(
        PureRunMapDefinition map,
        Vector2 mapSize)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (mapSize.X <= 0 || mapSize.Y <= 0) throw new ArgumentOutOfRangeException(nameof(mapSize));
        if (!map.Nodes.Any(value => value.NodeId == StartNodeId))
            throw new ArgumentException("Atlas topology requires the start node.", nameof(map));

        Vector2 layerStep = new(mapSize.X + LayerGap, -(mapSize.Y + DiagonalGap));
        float branchStep = mapSize.X + BranchGap;
        return map.Nodes.ToDictionary(
            node => node.NodeId,
            node =>
            {
                Vector2 position = layerStep * node.Layer + new Vector2(node.Lane * branchStep, 0);
                return new GodotAdventureAtlasNodeLayout(
                    node.NodeId, node.Layer, node.Lane, new Rect2(position, mapSize));
            },
            StringComparer.Ordinal);
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
