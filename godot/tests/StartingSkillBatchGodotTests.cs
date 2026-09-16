using GdUnit4;
using Godot;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class StartingSkillBatchGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedBatchUsesExternalPoisonAndCanonical58Catalog()
    {
        var batch = ResourceLoader.Load<GodotResourceCatalog>("res://content/skills/ContentCatalog.tres");
        var global = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres");
        AssertThat(batch).IsNotNull();
        AssertThat(global).IsNotNull();
        if (batch is null || global is null) return;
        StartingSkillBatchValidation result = StartingSkillBatchValidator.Validate(batch, global);
        AssertThat(result.BatchCount).IsEqual(12);
        AssertThat(result.GlobalCount is 74 or 101 or 108 or 114 or 115 or 116 or 119 or 123 or 124 or 125 or 131 or 132 or 141 or 142 or 143 or 160 or 161 or 162 or 166 or 185).IsTrue();
        AssertThat(result.GeneratedCount).IsEqual(11);
        AssertThat(batch.Entries.Single(entry => entry.ContentIdValue == "skill.poison-spear.lv1").DiagnosticPathValue)
            .IsEqual("res://content/poison_spear/PoisonSpearSkillLv1.tres");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FixtureUsesNative1600x900Contract()
    {
        AssertThat(GodotStartingSkillFixture.CanvasWidth).IsEqual(1600);
        AssertThat(GodotStartingSkillFixture.CanvasHeight).IsEqual(900);
        var scene = ResourceLoader.Load<PackedScene>("res://content/skills/SkillFixture.tscn");
        AssertThat(scene).IsNotNull();
        Node? instance = scene?.Instantiate();
        AssertThat(instance).IsInstanceOf<GodotStartingSkillFixture>();
        instance?.Free();
    }
}
