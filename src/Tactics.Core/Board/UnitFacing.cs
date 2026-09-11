namespace Tactics.Core.Board;

/// <summary>Engine-neutral cardinal facing used by deterministic battle mechanics.</summary>
public enum UnitFacing
{
    North,
    East,
    South,
    West
}

/// <summary>Resolves cardinal facing from logical grid displacement.</summary>
public static class UnitFacingResolver
{
    public static UnitFacing Initial(int playerNumber) => playerNumber == 0 ? UnitFacing.East : UnitFacing.West;

    public static UnitFacing Resolve(GridPoint from, GridPoint to, UnitFacing current)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        int ax = Math.Abs(dx);
        int ay = Math.Abs(dy);
        if (ax == 0 && ay == 0) return current;
        if (ax > ay) return dx > 0 ? UnitFacing.East : UnitFacing.West;
        if (ay > ax) return dy > 0 ? UnitFacing.North : UnitFacing.South;

        bool horizontal = current is UnitFacing.East or UnitFacing.West;
        bool vertical = current is UnitFacing.North or UnitFacing.South;
        if (horizontal && ((current == UnitFacing.East && dx > 0) ||
                           (current == UnitFacing.West && dx < 0)))
            return current;
        if (vertical && ((current == UnitFacing.North && dy > 0) ||
                         (current == UnitFacing.South && dy < 0)))
            return current;
        return dx > 0 ? UnitFacing.East : UnitFacing.West;
    }

    public static GridPoint BackwardStep(GridPoint origin, UnitFacing facing) => facing switch
    {
        UnitFacing.North => new GridPoint(origin.X, origin.Y - 1),
        UnitFacing.East => new GridPoint(origin.X - 1, origin.Y),
        UnitFacing.South => new GridPoint(origin.X, origin.Y + 1),
        UnitFacing.West => new GridPoint(origin.X + 1, origin.Y),
        _ => throw new ArgumentOutOfRangeException(nameof(facing))
    };
}
