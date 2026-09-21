using Godot;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Appends the shared one-cell Unit movement pose to a caller-owned Tween.</summary>
public static class GodotUnitMoveTweenBuilder
{
    public static void AppendSegment(Tween tween, GodotUnitActor actor, Vector2 origin, Vector2 destination,
        StandardUnitPresentationResource profile)
    {
        ArgumentNullException.ThrowIfNull(tween);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(profile);
        if (actor.Body is null) return;

        Vector2 direction = origin.DirectionTo(destination);
        Vector2 perpendicular = new(-direction.Y, direction.X);
        float half = profile.MoveCycleDuration * .5f;
        float tilt = Mathf.DegToRad(direction.X >= 0f ? -profile.MoveTiltDegrees : profile.MoveTiltDegrees);
        Vector2 midpoint = origin.Lerp(destination, .5f);

        tween.TweenProperty(actor, "position", midpoint, half).SetTrans(Tween.TransitionType.Linear);
        tween.Parallel().TweenProperty(actor.Body, "position",
                perpendicular * profile.MoveSwayPixels + Vector2.Up * profile.MoveLiftPixels, half)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(actor.Body, "rotation", tilt, half)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(actor.Body, "scale", new Vector2(.98f, 1.02f), half)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

        tween.TweenProperty(actor, "position", destination, half).SetTrans(Tween.TransitionType.Linear);
        tween.Parallel().TweenProperty(actor.Body, "position", -perpendicular * profile.MoveSwayPixels, half)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.Parallel().TweenProperty(actor.Body, "rotation", -tilt * .65f, half)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.Parallel().TweenProperty(actor.Body, "scale", Vector2.One, half)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    public static void AppendSettle(Tween tween, GodotUnitActor actor, StandardUnitPresentationResource profile)
    {
        ArgumentNullException.ThrowIfNull(tween);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(profile);
        if (actor.Body is null) return;

        tween.TweenProperty(actor.Body, "position", Vector2.Zero, profile.MoveSettleDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(actor.Body, "rotation", 0f, profile.MoveSettleDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(actor.Body, "scale", Vector2.One, profile.MoveSettleDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }
}
