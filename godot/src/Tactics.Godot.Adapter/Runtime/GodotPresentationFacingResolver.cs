using Tactics.Core.Board;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Presentation equivalent of the frozen Unity FacingResolver contract.</summary>
public static class GodotPresentationFacingResolver
{
    public static GodotUnitFacing Initial(int playerNumber) => playerNumber == 0 ? GodotUnitFacing.East : GodotUnitFacing.West;

    public static GodotUnitFacing Resolve(GridPoint from, GridPoint to, GodotUnitFacing current)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (dx == 0 && dy == 0) return current;
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        GodotUnitFacing xFacing = dx > 0 ? GodotUnitFacing.East : GodotUnitFacing.West;
        GodotUnitFacing yFacing = dy > 0 ? GodotUnitFacing.North : GodotUnitFacing.South;
        if (ax > ay) return xFacing;
        if (ay > ax) return yFacing;
        if (current == xFacing || current == yFacing) return current;
        return xFacing;
    }

    public static GodotUnitFacing PreviewMove(GridPoint origin, IReadOnlyList<GridPoint> path, GodotUnitFacing current) =>
        path.Count == 0 ? current : Resolve(origin, path[0], current);

    public static GodotUnitFacing PreviewTarget(GridPoint origin, GridPoint target, GodotUnitFacing current) =>
        Resolve(origin, target, current);
}
