using GdUnit4;
using Godot;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public sealed class GodotUnitActorGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task HoverOutlineInheritsTransientBodyTweenTransformIncludingScale()
    {
        UnitDefinitionResource? definition = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/demonbound/PureRunDemonbound.tres",
            string.Empty,
            ResourceLoader.CacheMode.Ignore);
        AssertThat(definition).IsNotNull();
        if (definition is null) return;

        GodotUnitActor actor = GodotUnitFactory.InstantiateActor(definition);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(actor);
        await actor.ToSignal(actor.GetTree(), SceneTree.SignalName.ProcessFrame);
        actor.SetHoverOutline(new Color("72f18c"));
        Sprite2D? outline = actor.HoverOutline;
        AssertThat(outline).IsNotNull();
        AssertThat(outline?.GetParent()).IsEqual(actor.Body);
        if (outline is null || actor.Body is null)
        {
            actor.QueueFree();
            return;
        }

        Vector2 targetPosition = new(36f, -18f);
        Vector2 targetScale = new(1.7f, .65f);
        const float targetRotation = .6f;
        Tween tween = actor.CreateTween().SetParallel(true);
        tween.TweenProperty(actor.Body, "position", targetPosition, .24);
        tween.TweenProperty(actor.Body, "rotation", targetRotation, .24);
        tween.TweenProperty(actor.Body, "scale", targetScale, .24);
        tween.Pause();
        tween.CustomStep(.1);

        AssertThat(actor.Body.Position.DistanceTo(Vector2.Zero)).IsGreater(1f);
        AssertThat(actor.Body.Position.DistanceTo(targetPosition)).IsGreater(1f);
        AssertThat(Mathf.Abs(actor.Body.Rotation)).IsGreater(.01f);
        AssertThat(actor.Body.Scale.DistanceTo(Vector2.One)).IsGreater(.01f);
        AssertThat(outline.GlobalPosition.IsEqualApprox(actor.Body.GlobalPosition)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(outline.GlobalRotation, actor.Body.GlobalRotation)).IsTrue();
        AssertThat(outline.GlobalScale.IsEqualApprox(actor.Body.GlobalScale * 1.08f)).IsTrue();

        tween.Kill();
        actor.QueueFree();
    }
}
