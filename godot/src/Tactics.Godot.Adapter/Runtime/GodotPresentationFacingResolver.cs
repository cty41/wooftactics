using Tactics.Core.Board;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Projects authoritative Core facing and provides non-committing preview helpers.</summary>
public static class GodotPresentationFacingResolver
{
    public static GodotUnitFacing Initial(int playerNumber) => ToGodot(UnitFacingResolver.Initial(playerNumber));

    public static GodotUnitFacing Resolve(GridPoint from, GridPoint to, GodotUnitFacing current) =>
        ToGodot(UnitFacingResolver.Resolve(from, to, ToCore(current)));

    public static GodotUnitFacing PreviewMove(GridPoint origin, IReadOnlyList<GridPoint> path, GodotUnitFacing current) =>
        path.Count == 0
            ? current
            : Resolve(path.Count > 1 ? path[^2] : origin, path[^1], current);

    public static GodotUnitFacing PreviewTarget(GridPoint origin, GridPoint target, GodotUnitFacing current) =>
        Resolve(origin, target, current);

    public static GodotUnitFacing ToGodot(UnitFacing facing) => facing switch
    {
        UnitFacing.North => GodotUnitFacing.North,
        UnitFacing.East => GodotUnitFacing.East,
        UnitFacing.South => GodotUnitFacing.South,
        UnitFacing.West => GodotUnitFacing.West,
        _ => throw new ArgumentOutOfRangeException(nameof(facing))
    };

    public static UnitFacing ToCore(GodotUnitFacing facing) => facing switch
    {
        GodotUnitFacing.North => UnitFacing.North,
        GodotUnitFacing.East => UnitFacing.East,
        GodotUnitFacing.South => UnitFacing.South,
        GodotUnitFacing.West => UnitFacing.West,
        _ => throw new ArgumentOutOfRangeException(nameof(facing))
    };
}
