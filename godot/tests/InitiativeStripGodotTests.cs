using GdUnit4;
using Godot;
using Tactics.Application.Battle;
using Tactics.Core.Content;
using Tactics.Core.Units;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public sealed class InitiativeStripGodotTests
{
    private static readonly IReadOnlyDictionary<ContentId, UnitDefinitionResource> EmptyDefinitions =
        new Dictionary<ContentId, UnitDefinitionResource>();

    [TestCase]
    [RequireGodotRuntime]
    public async Task TransitionLockCoversDeferredMovementAndCompletesOnce()
    {
        GodotInitiativeStrip strip = AddStrip();
        var lockChanges = new List<bool>();
        strip.TransitionLockChanged += lockChanges.Add;
        await NextFrame(strip);

        strip.Apply(1, Queue("a", "b"), EmptyDefinitions);

        AssertThat(strip.IsTransitionLocked).IsTrue();
        AssertThat(lockChanges.SequenceEqual(new[] { true })).IsTrue();
        await NextFrame(strip);
        AssertThat(strip.TransitionMovementCount).IsEqual(2);
        AssertThat(strip.IsTransitionLocked).IsTrue();
        AssertThat(lockChanges.SequenceEqual(new[] { true })).IsTrue();
        await Timer(strip, GodotInitiativeStrip.TransitionDurationSeconds + .08);

        AssertThat(strip.IsTransitionLocked).IsFalse();
        AssertThat(lockChanges.SequenceEqual(new[] { true, false })).IsTrue();
        strip.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ReplacementRefreshKeepsOneLockUntilReplacementCompletes()
    {
        GodotInitiativeStrip strip = AddStrip();
        var lockChanges = new List<bool>();
        strip.TransitionLockChanged += lockChanges.Add;
        await NextFrame(strip);

        strip.Apply(1, Queue("a", "b"), EmptyDefinitions);
        await NextFrame(strip);
        strip.Apply(1, Queue("b", "a"), EmptyDefinitions);

        AssertThat(strip.IsTransitionLocked).IsTrue();
        AssertThat(lockChanges.SequenceEqual(new[] { true })).IsTrue();
        await NextFrame(strip);
        AssertThat(strip.TransitionMovementCount).IsEqual(2);
        await Timer(strip, GodotInitiativeStrip.TransitionDurationSeconds + .08);

        AssertThat(strip.IsTransitionLocked).IsFalse();
        AssertThat(lockChanges.SequenceEqual(new[] { true, false })).IsTrue();
        strip.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ReorderedPortraitMovesFromItsPreviousSlotToItsNewSlot()
    {
        GodotInitiativeStrip strip = AddStrip();
        await NextFrame(strip);
        UnitInstanceId moved = new("b");

        strip.Apply(1, Queue("a", "b"), EmptyDefinitions);
        await NextFrame(strip);
        await Timer(strip, GodotInitiativeStrip.TransitionDurationSeconds + .08);
        AssertThat(strip.TryGetPortraitTransition(moved, out _, out Vector2 oldPosition, out Vector2 oldTarget)).IsTrue();
        AssertThat(oldPosition.IsEqualApprox(oldTarget)).IsTrue();

        strip.Apply(1, Queue("b", "a"), EmptyDefinitions);
        await NextFrame(strip);
        AssertThat(strip.TryGetPortraitTransition(moved, out Vector2 movementSource, out _, out Vector2 newTarget)).IsTrue();
        AssertThat(movementSource.IsEqualApprox(oldTarget)).IsTrue();
        AssertThat(movementSource.DistanceTo(newTarget)).IsGreater(1f);
        await Timer(strip, GodotInitiativeStrip.TransitionDurationSeconds + .08);

        AssertThat(strip.TryGetPortraitTransition(moved, out _, out Vector2 finalPosition, out Vector2 finalTarget)).IsTrue();
        AssertThat(finalPosition.IsEqualApprox(finalTarget)).IsTrue();
        strip.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PortraitMaterialsApplyMultiplyTintsAndGoatBodyMaskContract()
    {
        UnitDefinitionResource? poet = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/poet/PureRunPoet.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        UnitDefinitionResource? demonbound = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/demonbound/PureRunDemonbound.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        UnitDefinitionResource? decoy = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/poet/PoetAfterimage.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        UnitDefinitionResource? goat = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/units/PureRunGoatRanged.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(poet).IsNotNull();
        AssertThat(demonbound).IsNotNull();
        AssertThat(decoy).IsNotNull();
        AssertThat(goat).IsNotNull();
        if (poet is null || demonbound is null || decoy is null || goat is null) return;

        GodotInitiativeStrip strip = AddStrip();
        await NextFrame(strip);
        var definitions = new Dictionary<ContentId, UnitDefinitionResource>
        {
            [new ContentId("unit.pure-run.poet")] = poet,
            [new ContentId("unit.pure-run.demonbound")] = demonbound,
            [new ContentId("unit.pure-run.poet-decoy")] = decoy,
            [new ContentId("unit.pure-run.goat-ranged")] = goat
        };
        strip.Apply(1, Queue("pure-run.poet", "pure-run.demonbound", "pure-run.poet-decoy", "pure-run.goat-ranged"), definitions);

        TextureRect[] portraits = strip.FindChildren("Portrait", "TextureRect", true, false)
            .Cast<TextureRect>().ToArray();
        AssertThat(portraits.Length).IsEqual(4);
        AssertThat(portraits.Any(value => value.Visible)).IsFalse();
        ShaderMaterial[] materials = portraits.Select(value => value.Material as ShaderMaterial)
            .Where(value => value is not null).Cast<ShaderMaterial>().ToArray();
        AssertThat(materials.Length).IsEqual(4);
        if (materials.Length != 4)
        {
            strip.QueueFree();
            return;
        }

        AssertThat(materials[0].GetShaderParameter("tint_mode").AsInt32()).IsEqual(0);
        AssertThat(materials[0].GetShaderParameter("body_tint").AsColor().IsEqualApprox(poet.BodyTint)).IsTrue();
        AssertThat(materials[1].GetShaderParameter("body_tint").AsColor().IsEqualApprox(demonbound.BodyTint)).IsTrue();
        AssertThat(materials[2].GetShaderParameter("body_tint").AsColor().IsEqualApprox(decoy.BodyTint)).IsTrue();
        AssertThat(poet.BodyTint.IsEqualApprox(demonbound.BodyTint)).IsFalse();
        AssertThat(poet.BodyTint.IsEqualApprox(decoy.BodyTint)).IsFalse();
        AssertThat(demonbound.BodyTint.IsEqualApprox(decoy.BodyTint)).IsFalse();
        AssertThat(materials[3].GetShaderParameter("tint_mode").AsInt32()).IsEqual(1);
        AssertThat(materials[3].GetShaderParameter("body_tint").AsColor().IsEqualApprox(goat.BodyTint)).IsTrue();
        AssertThat(materials[3].GetShaderParameter("base_body_color").AsColor().IsEqualApprox(goat.BaseBodyColor)).IsTrue();
        string goatShaderCode = materials[3].Shader?.Code ?? string.Empty;
        string multiplyShaderCode = materials[0].Shader?.Code ?? string.Empty;
        AssertThat(goatShaderCode.Contains("discard", StringComparison.Ordinal)).IsTrue();
        AssertThat(goatShaderCode).IsEqual(multiplyShaderCode);
        strip.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task NextRoundFollowsASeparatorWithinTheSlotBudget()
    {
        GodotInitiativeStrip strip = AddStrip();
        await NextFrame(strip);
        BattleUiInitiativeEntry[] next = Queue("x", "y", "z").Select(item => item with { IsCurrent = false }).ToArray();

        strip.Apply(1, Queue("a", "b"), EmptyDefinitions, next);

        Node[] children = strip.FindChildren("*", "", true, false).ToArray();
        int separator = Array.FindIndex(children, node => node.Name == GodotInitiativeStrip.NextRoundSeparatorName);
        AssertThat(separator).IsGreaterEqual(0);
        AssertThat(Array.FindIndex(children, node => node.Name == "Slot_b")).IsLess(separator);
        AssertThat(Array.FindIndex(children, node => node.Name == "NextRound_x")).IsGreater(separator);
        AssertThat(GodotInitiativeStrip.VisibleNextRoundCount(2, 20)).IsEqual(GodotInitiativeStrip.TotalPortraitBudget - 2);
        AssertThat(GodotInitiativeStrip.VisibleNextRoundCount(10, 5)).IsEqual(0);
        await Timer(strip, GodotInitiativeStrip.TransitionDurationSeconds + .08);
        strip.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PortraitIsAStandaloneCropSoTheCircularMaskUsesItsOwnUv()
    {
        UnitDefinitionResource? goat = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/units/PureRunGoatRanged.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(goat).IsNotNull();
        if (goat is null) return;
        Texture2D? portrait = GodotInitiativeStrip.PortraitOf(goat);
        AssertThat(portrait).IsNotNull();
        AssertThat(portrait is AtlasTexture).IsFalse();
        Rect2 region = goat.HasPortraitRegion ? goat.PortraitRegion : GodotInitiativeStrip.DefaultFaceRegion(goat.DownRightTexture!);
        AssertThat(portrait!.GetWidth()).IsEqual((int)region.Size.X);
    }

    private static GodotInitiativeStrip AddStrip()
    {
        var strip = new GodotInitiativeStrip { Size = new Vector2(565, 52) };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(strip);
        return strip;
    }

    private static BattleUiInitiativeEntry[] Queue(params string[] unitIds) => unitIds
        .Select((unitId, index) => new BattleUiInitiativeEntry(
            new UnitInstanceId(unitId),
            new ContentId($"unit.{unitId}"),
            index % 2,
            index == 0))
        .ToArray();

    private static async Task NextFrame(Node node) =>
        await node.ToSignal(node.GetTree(), SceneTree.SignalName.ProcessFrame);

    private static async Task Timer(Node node, double seconds) =>
        await node.ToSignal(node.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
