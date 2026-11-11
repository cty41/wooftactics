using System.Text.Json;
using GdUnit4;
using Godot;
using Tactics.Application.Battle;
using Tactics.Application.Units;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Statuses;
using Tactics.Core.Skills;
using Tactics.Core.Units;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class UnitBatchGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void AllGlobalCatalogUnitsSatisfyVisualResourceContracts()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/ContentCatalog.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(catalog).IsNotNull();
        if (catalog is null)
            return;

        GodotResourceEntry[] unitEntries = catalog.Entries
            .Where(entry => entry.ResourceTypeIdValue == "unit")
            .OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal)
            .ToArray();
        AssertThat(unitEntries.Length).IsGreater(0);

        foreach (GodotResourceEntry entry in unitEntries)
        {
            var definition = ResourceLoader.Load<UnitDefinitionResource>(
                entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.Ignore);
            AssertThat(definition).IsNotNull();
            if (definition is null)
                continue;

            definition.ValidateVisualContract();
            AssertBodyOffsetWithinTexture(definition.DownRightTexture!, definition.DownRightBodyOffset);
            AssertBodyOffsetWithinTexture(definition.UpLeftTexture!, definition.UpLeftBodyOffset);
            if (definition.DeathTexture is not null)
                AssertBodyOffsetWithinTexture(definition.DeathTexture, definition.DeathBodyOffset);
            AssertThat(definition.ShadowTexture).IsNotNull();
            AssertThat(definition.ShadowScale.X).IsGreater(0f);
            AssertThat(definition.ShadowScale.Y).IsEqual(definition.ShadowScale.X);
            AssertThat(definition.ShadowOpacity).IsGreaterEqual(0f);
            AssertThat(definition.ShadowOpacity).IsLessEqual(1f);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MawBatCorpseCentersOnGroundContactAndSettlesWithoutExtraDescent()
    {
        var bat = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/units/PureRunMawBat.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(bat).IsNotNull();
        if (bat is null) return;

        Texture2D death = bat.DeathTexture!;
        Rect2I used = death.GetImage().GetUsedRect();
        var corpseCenter = new Vector2(used.Position.X + used.Size.X / 2f, used.Position.Y + used.Size.Y / 2f);
        Vector2 canvasCenter = new(death.GetWidth() / 2f, death.GetHeight() / 2f);
        // Corpse AABB centre lands on the actor origin (the shadow's ground contact).
        AssertThat((corpseCenter - canvasCenter + bat.DeathBodyOffset).Length() < 2f).IsTrue();
        AssertThat(bat.DeathBodyOffset.IsEqualApprox(Vector2.Zero)).IsFalse();

        GodotUnitActor actor = GodotUnitFactory.InstantiateActor(bat);
        actor.SetDeathVisual(true);
        actor._Process(1.0);
        AssertThat(actor.Body!.GetParent<Node2D>().Position.IsEqualApprox(Vector2.Zero)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DemonboundUsesAuthoredMoveFourContractWithoutChangingFrozenUnits()
    {
        var resource = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/demonbound/PureRunDemonbound.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(resource).IsNotNull();
        if (resource is null) return;

        UnitDefinition definition = resource.ToCoreDefinition();
        AssertThat(resource.DerivedStatModeValue).IsEqual("explicit");
        AssertThat(definition.DerivedStatMode).IsEqual(UnitDerivedStatMode.Explicit);
        AssertThat(definition.Speed).IsEqual(4f);
        AssertThat(definition.DerivedStats.MoveRange).IsEqual(4);
        AssertThat(definition.DerivedStats.Initiative).IsEqual(8f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DemonboundUsesItsApprovedIdleActionAndDeathTextures()
    {
        var resource = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/demonbound/PureRunDemonbound.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(resource).IsNotNull();
        if (resource is null) return;

        (Texture2D? Texture, string Path)[] expected =
        {
            (resource.DownRightTexture, "res://assets/units/doge_demonbound.png"),
            (resource.UpLeftTexture, "res://assets/units/doge_demonbound_ul.png"),
            (resource.DeathTexture, "res://assets/units/doge_demonbound_death.png"),
            (resource.MeleeDownRightTexture, "res://assets/units/actions/doge_demonbound_melee_attack_dr.png"),
            (resource.MeleeUpLeftTexture, "res://assets/units/actions/doge_demonbound_melee_attack_ul.png"),
            (resource.CastDownRightTexture, "res://assets/units/actions/doge_demonbound_cast_dr.png"),
            (resource.CastUpLeftTexture, "res://assets/units/actions/doge_demonbound_cast_ul.png"),
            (resource.HitDownRightTexture, "res://assets/units/actions/doge_demonbound_hit_dr.png"),
            (resource.HitUpLeftTexture, "res://assets/units/actions/doge_demonbound_hit_ul.png")
        };
        AssertThat(expected.Length).IsEqual(9);
        foreach ((Texture2D? texture, string path) in expected)
        {
            AssertThat(texture).IsNotNull();
            AssertThat(texture?.ResourcePath).IsEqual(path);
        }
        AssertThat(resource.RangedDownRightTexture).IsNull();
        AssertThat(resource.RangedUpLeftTexture).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DemonboundPlayableBalanceUsesFourPointBareMeleeDamage()
    {
        var resource = ResourceLoader.Load<PlayableLv1BalanceProfileResource>(
            "res://content/ui/PlayableLv1BalanceProfile.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(resource).IsNotNull();
        if (resource is null) return;

        (int physical, int magical) = resource.ToCoreProfile().Attacks(
            new ContentId("unit.pure-run.demonbound"));
        AssertThat(physical).IsEqual(4);
        AssertThat(magical).IsEqual(2);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DemonboundSkillResourcesMatchTheFormalManaDamageAndCorruptionTables()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/ContentCatalog.tres", string.Empty, ResourceLoader.CacheMode.Ignore)!;
        var expected = new Dictionary<string, (int Mana, int Damage, int Corruption)>
        {
            ["skill.demonbound.meditation"] = (0, 0, 0),
            ["skill.demonbound.bane.lv1"] = (3, 5, 3), ["skill.demonbound.bane.lv2"] = (3, 6, 3),
            ["skill.demonbound.bane.lv3"] = (3, 7, 3), ["skill.demonbound.cleave.lv1"] = (4, 6, 2),
            ["skill.demonbound.cleave.lv2"] = (4, 6, 2),
            ["skill.demonbound.infernal-blast.lv1"] = (5, 4, 3),
            ["skill.demonbound.infernal-blast.lv2"] = (3, 4, 3),
            ["skill.demonbound.infernal-blast.lv3"] = (3, 4, 4),
            ["skill.demonbound.hellfire.lv1"] = (5, 5, 4),
            ["skill.demonbound.hellfire.lv2"] = (5, 5, 4),
            ["skill.demonbound.mindfulness.lv1"] = (0, 0, 0),
            ["skill.demonbound.mindfulness.lv2"] = (0, 0, 0),
            ["skill.demonbound.mindfulness.lv3"] = (0, 0, 0),
            ["skill.demonbound.regeneration.lv1"] = (5, 0, 5),
            ["skill.demonbound.regeneration.lv2"] = (5, 0, 6)
        };

        SkillDefinition[] skills = catalog.Entries.Where(entry => expected.ContainsKey(entry.ContentIdValue))
            .Select(entry => ResourceLoader.Load<SkillDefinitionResource>(entry.DiagnosticPathValue,
                string.Empty, ResourceLoader.CacheMode.Ignore)!.ToCoreDefinition()).ToArray();

        AssertThat(skills.Length).IsEqual(16);
        foreach (SkillDefinition skill in skills)
        {
            (int mana, int damage, int corruption) = expected[skill.ContentId.Value];
            AssertThat(skill.ManaCost).IsEqual(mana);
            AssertThat(skill.Damage).IsEqual(damage);
            AssertThat(skill.ExecutionProfile.CorruptionCost).IsEqual(corruption);
            AssertThat(skill.Role).IsEqual(SkillRole.Demonbound);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BaneUsesAProgrammaticPurpleTravellingCrescentProfile()
    {
        var resource = ResourceLoader.Load<SkillPresentationResource>(
            "res://content/demonbound/BanePresentation.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(resource).IsNotNull();
        if (resource is null) return;

        AssertThat(resource.SkillBranch).IsEqual("demonbound.bane");
        AssertThat(resource.ProgrammaticKind).IsEqual("bane-crescent");
        AssertThat(resource.PrimaryColor.B).IsGreater(resource.PrimaryColor.R);
        AssertThat(resource.TravelDuration).IsEqual(.28f);
        AssertThat(resource.ImpactDuration).IsEqual(.16f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CatalogResourcesPreserveFrozenMembershipAndUseCurrentDerivedFormula()
    {
        using JsonDocument golden = LoadGolden();
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/units/ContentCatalog.tres");
        AssertThat(catalog).IsNotNull();
        if (catalog is null)
            return;

        UnitBatchValidation validation = UnitBatchValidator.Validate(catalog);
        IReadOnlyDictionary<string, JsonElement> vectors = golden.RootElement.GetProperty("units")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("contentId").GetString()!, StringComparer.Ordinal);
        foreach ((string contentId, JsonElement vector) in vectors)
        {
            bool loaded = catalog.TryGet(contentId, out Resource? resource);
            AssertThat(loaded).IsTrue();
            AssertThat(resource).IsInstanceOf<UnitDefinitionResource>();
            if (resource is not UnitDefinitionResource definition)
                continue;
            var state = GodotUnitFactory.CreateBattleState(
                definition,
                new UnitInstanceId($"test.{definition.SourceId}.0"),
                new GridPoint(2, 3),
                1,
                7);
            AssertThat(definition.ContentIdValue).IsEqual(contentId);
            UnitDerivedStats derived = UnitDerivedStatRules.Calculate(
                new UnitAttributes(definition.Strength, definition.Agility, definition.Constitution,
                    definition.Intelligence, definition.Charisma, definition.Luck),
                definition.MovementTraitModifier);
            AssertThat(definition.DerivedStatModeValue).IsEqual("frozen-formula");
            AssertThat(definition.MaxHealth).IsEqual(derived.MaxHealth);
            AssertThat(definition.MaxMana).IsEqual(derived.MaxMana);
            AssertThat(definition.StartingMana).IsEqual(derived.StartingMana);
            AssertThat(definition.MoveRange).IsEqual(derived.MoveRange);
            AssertThat(definition.Initiative).IsEqual(derived.Initiative);
            AssertThat(state.Unit.DefinitionId.Value).IsEqual(contentId);
            AssertThat(state.Unit.InstanceId.Value).IsNotEqual(contentId);
            AssertThat(state.CurrentHealth).IsEqual(definition.MaxHealth);
            AssertThat(state.CurrentMana).IsEqual(definition.StartingMana);
        }

        AssertThat(validation.CatalogEntryCount).IsEqual(14);
        AssertThat(validation.UnitCount).IsEqual(13);
        AssertThat(validation.SpawnedStates.Count).IsEqual(13);

        UnitDefinitionResource LoadUnit(string contentId) => ResourceLoader.Load<UnitDefinitionResource>(
            catalog.Entries.Single(entry => entry.ContentIdValue == contentId).DiagnosticPathValue,
            string.Empty,
            ResourceLoader.CacheMode.Ignore)!;
        UnitDefinitionResource mage = LoadUnit("unit.pure-run.mage");
        UnitDefinitionResource necromancer = LoadUnit("unit.pure-run.necromancer");
        UnitDefinitionResource amazon = LoadUnit("unit.pure-run.amazon");
        AssertThat(new[] { mage.Strength, mage.Agility, mage.Constitution, mage.Intelligence, mage.Charisma, mage.Luck })
            .ContainsExactly(4, 5, 3, 6, 6, 6);
        AssertThat(new[] { necromancer.Strength, necromancer.Agility, necromancer.Constitution,
            necromancer.Intelligence, necromancer.Charisma, necromancer.Luck })
            .ContainsExactly(5, 3, 6, 6, 6, 4);
        AssertThat(new[] { amazon.Strength, amazon.Agility, amazon.Constitution, amazon.Intelligence,
            amazon.Charisma, amazon.Luck }).ContainsExactly(5, 5, 5, 5, 5, 5);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SharedActorUsesDirectionalTexturesBodyOnlyMirroringAndSafeDeathFallback()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/units/ContentCatalog.tres");
        AssertThat(catalog).IsNotNull();
        if (catalog is null || !catalog.TryGet("unit.pure-run.mage", out Resource? mageResource) ||
            mageResource is not UnitDefinitionResource mage)
        {
            return;
        }
        catalog.TryGet("unit.pure-run.skeleton-warrior", out Resource? skeletonResource);
        var skeleton = skeletonResource as UnitDefinitionResource;
        AssertThat(skeleton).IsNotNull();
        if (skeleton is null)
            return;

        GodotUnitActor mageActor = GodotUnitFactory.InstantiateActor(mage);
        Texture2D shadow = mageActor.Shadow!.Texture!;
        mageActor.SetFacing(GodotUnitFacing.South);
        bool southUsesDr = mageActor.Body!.Texture == mage.DownRightTexture && !mageActor.Body.FlipH &&
            mageActor.Body.Offset.IsEqualApprox(mage.DownRightBodyOffset);
        mageActor.SetFacing(GodotUnitFacing.North);
        bool northUsesUl = mageActor.Body.Texture == mage.UpLeftTexture && !mageActor.Body.FlipH &&
            mageActor.Body.Offset.IsEqualApprox(mage.UpLeftBodyOffset);
        mageActor.SetFacing(GodotUnitFacing.East);
        bool eastUsesMirroredUl = mageActor.Body.Texture == mage.UpLeftTexture && mageActor.Body.FlipH;
        mageActor.SetFacing(GodotUnitFacing.West);
        bool westUsesMirroredDrBodyOnly = mageActor.Body.Texture == mage.DownRightTexture &&
            mageActor.Body.FlipH && !mageActor.Shadow.FlipH &&
            mageActor.Shadow.Texture == shadow;
        mageActor.SetDeathVisual(true);
        bool deathUsesUnmirroredDeathTexture = mageActor.Body.Texture == mage.DeathTexture &&
            !mageActor.Body.FlipH && mageActor.Body.Offset.IsEqualApprox(mage.DeathBodyOffset);
        mageActor.SetStatuses([new BattleUiStatusSnapshot(new ContentId("buff.poison"),
            StatusEffectKind.Poison, StatusPolarity.Harmful, 2, 1)]);
        mageActor.SetDeathVisual(true);
        bool deathClearsStatusPresentation = mageActor.StatusOverlay!.StatusCount == 0;
        bool spriteGeometryMatchesFrozenUnityContract =
            mage.DownRightBodyOffset.IsEqualApprox(new Vector2(0f, -108f)) &&
            mage.UpLeftBodyOffset.IsEqualApprox(new Vector2(0f, -108f)) &&
            mage.DeathBodyOffset.IsEqualApprox(Vector2.Zero) &&
            mageActor.Shadow.Position.IsEqualApprox(new Vector2(0f, 3.84f)) &&
            mageActor.Shadow.Scale.IsEqualApprox(new Vector2(1.6f, 1.6f)) &&
            Mathf.IsEqualApprox(mageActor.Shadow.Modulate.A, 0.9f);

        GodotUnitActor skeletonActor = GodotUnitFactory.InstantiateActor(skeleton);
        skeletonActor.SetDeathVisual(true);
        bool summonUsesSafeLivingFallback = !skeletonActor.IsShowingDeath &&
            skeletonActor.Body!.Texture == skeleton.DownRightTexture;
        mageActor.Free();
        skeletonActor.Free();

        AssertThat(southUsesDr).IsTrue();
        AssertThat(northUsesUl).IsTrue();
        AssertThat(eastUsesMirroredUl).IsTrue();
        AssertThat(westUsesMirroredDrBodyOnly).IsTrue();
        AssertThat(deathUsesUnmirroredDeathTexture).IsTrue();
        AssertThat(deathClearsStatusPresentation).IsTrue();
        AssertThat(spriteGeometryMatchesFrozenUnityContract).IsTrue();
        AssertThat(summonUsesSafeLivingFallback).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GoatBodyMaskMaterialAndCpuReferencePreserveTheTintBoundary()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/units/ContentCatalog.tres");
        AssertThat(catalog).IsNotNull();
        if (catalog is null ||
            !catalog.TryGet("unit.pure-run.goat-ranged", out Resource? goatResource) ||
            goatResource is not UnitDefinitionResource goat ||
            !catalog.TryGet("unit.pure-run.fire-demon", out Resource? fireResource) ||
            fireResource is not UnitDefinitionResource fireDemon)
        {
            return;
        }

        GodotUnitActor goatActor = GodotUnitFactory.InstantiateActor(goat);
        GodotUnitActor fireActor = GodotUnitFactory.InstantiateActor(fireDemon);
        bool goatUsesMaterial = goat.BodyTintModeValue == UnitBodyTintModes.GoatBodyMaskV1 &&
            goat.BodyTintMaterial is not null && goatActor.Body!.Material == goat.BodyTintMaterial &&
            goatActor.Body.Modulate.IsEqualApprox(Colors.White) &&
            goat.BodyTintMaterial.Shader?.ResourcePath ==
                "res://src/Tactics.Godot.Adapter/Runtime/Shaders/GoatBodyTint.gdshader" &&
            goat.BodyTintMaterial.GetShaderParameter("body_tint").AsColor().IsEqualApprox(goat.BodyTint) &&
            goat.BodyTintMaterial.GetShaderParameter("base_body_color").AsColor()
                .IsEqualApprox(goat.BaseBodyColor);
        bool fireUsesMultiply = fireDemon.BodyTintModeValue == UnitBodyTintModes.Multiply &&
            fireActor.Body!.Material is null && fireActor.Body.Modulate.IsEqualApprox(fireDemon.BodyTint);

        Color bodyResult = GodotUnitTintReference.Apply(
            goat.BaseBodyColor,
            goat.BodyTintModeValue,
            goat.BodyTint,
            goat.BaseBodyColor);
        var farSource = new Color(0.92f, 0.86f, 0.68f, 0.75f);
        Color farResult = GodotUnitTintReference.Apply(
            farSource,
            goat.BodyTintModeValue,
            goat.BodyTint,
            goat.BaseBodyColor);
        bool baseMapsToTint = bodyResult.IsEqualApprox(goat.BodyTint);
        bool farColorAndAlphaRemain = farResult.IsEqualApprox(farSource);
        Image firstCopy = GodotUnitTintReference.CopyTextureImage(goat.DownRightTexture!);
        firstCopy.Resize(64, 64, Image.Interpolation.Lanczos);
        Image secondCopy = GodotUnitTintReference.CopyTextureImage(goat.DownRightTexture!);
        bool captureCopyDoesNotMutateSharedTexture = secondCopy.GetSize() == new Vector2I(256, 256);

        goatActor.SetDeathVisual(true);
        bool deathKeepsMaterial = goatActor.Body!.Material == goat.BodyTintMaterial;
        goatActor.SetBodyTintEnabled(false);
        bool comparisonDisablesOnlyMaterial = goatActor.Body.Material is null &&
            goatActor.Body.Modulate.IsEqualApprox(Colors.White);
        goatActor.Free();
        fireActor.Free();

        AssertThat(goatUsesMaterial).IsTrue();
        AssertThat(fireUsesMultiply).IsTrue();
        AssertThat(baseMapsToTint).IsTrue();
        AssertThat(farColorAndAlphaRemain).IsTrue();
        AssertThat(captureCopyDoesNotMutateSharedTexture).IsTrue();
        AssertThat(deathKeepsMaterial).IsTrue();
        AssertThat(comparisonDisablesOnlyMaterial).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedGalleryAndSpawnFixtureLoadAsTypedPackedScenes()
    {
        var galleryScene = ResourceLoader.Load<PackedScene>("res://content/units/UnitGallery.tscn");
        var fixtureScene = ResourceLoader.Load<PackedScene>("res://content/units/UnitSpawnFixture.tscn");
        AssertThat(galleryScene).IsNotNull();
        AssertThat(fixtureScene).IsNotNull();
        if (galleryScene is null || fixtureScene is null)
            return;

        Node gallery = galleryScene.Instantiate();
        Node fixture = fixtureScene.Instantiate();
        AssertThat(gallery).IsInstanceOf<GodotUnitGallery>();
        AssertThat(fixture).IsInstanceOf<GodotUnitSpawnFixture>();
        if (gallery is GodotUnitGallery typedGallery)
        {
            typedGallery.BuildGallery();
            AssertThat(typedGallery.Actors.Count).IsEqual(13);
            AssertThat(typedGallery.Actors[0].Position).IsEqual(new Vector2(160f, 190f));
            AssertThat(typedGallery.Actors[12].Position).IsEqual(new Vector2(800f, 750f));
            AssertThat(typedGallery.Actors.All(actor =>
                actor.Scale.IsEqualApprox(new Vector2(0.65f, 0.65f)))).IsTrue();
            Label amazonLabel = typedGallery.GetChildren()
                .OfType<Label>()
                .Single(label => label.Text == "Amazon");
            AssertThat(amazonLabel.Position).IsEqual(new Vector2(50f, 242.5f));
            AssertThat(amazonLabel.Size).IsEqual(new Vector2(220f, 42.5f));
            AssertThat(amazonLabel.GetThemeFontSize("font_size")).IsEqual(20);
            Label instructions = typedGallery.GetChildren()
                .OfType<Label>()
                .Single(label => label.Text.StartsWith("1 South", StringComparison.Ordinal));
            AssertThat(instructions.Position).IsEqual(new Vector2(25f, 5f));
            AssertThat(instructions.Size).IsEqual(new Vector2(1550f, 35f));
            ColorRect background = typedGallery.GetChildren().OfType<ColorRect>().Single();
            AssertThat(background.Size).IsEqual(new Vector2(1600f, 900f));
            AssertThat(typedGallery.Actors.All(actor =>
                actor.Facing == GodotUnitFacing.South && !actor.IsShowingDeath)).IsTrue();
            AssertThat(typedGallery.CurrentFacing).IsEqual(GodotUnitFacing.South);
            AssertThat(typedGallery.IsShowingDeathMode).IsFalse();
            AssertThat(typedGallery.IsGoatTintEnabled).IsTrue();
            typedGallery.SetAllFacing(GodotUnitFacing.West);
            AssertThat(typedGallery.Actors.All(actor => actor.Body!.FlipH)).IsTrue();
            AssertThat(typedGallery.Actors.All(actor => !actor.Shadow!.FlipH)).IsTrue();
            typedGallery.SetAllDeath(true);
            AssertThat(typedGallery.Actors.Count(actor => actor.IsShowingDeath)).IsEqual(10);
            typedGallery.SetGoatTintEnabled(false);
            AssertThat(typedGallery.Actors.Where(actor => actor.UsesGoatBodyMaskTint)
                .All(actor => actor.Body!.Material is null)).IsTrue();
            typedGallery.ResetPreview();
            AssertThat(typedGallery.Actors.All(actor =>
                actor.Facing == GodotUnitFacing.South && !actor.IsShowingDeath)).IsTrue();
            AssertThat(typedGallery.Actors.Where(actor => actor.UsesGoatBodyMaskTint)
                .All(actor => actor.Body!.Material is not null && actor.IsBodyTintEnabled)).IsTrue();
            AssertThat(typedGallery.CurrentFacing).IsEqual(GodotUnitFacing.South);
            AssertThat(typedGallery.IsShowingDeathMode).IsFalse();
            AssertThat(typedGallery.IsGoatTintEnabled).IsTrue();
        }
        if (fixture is GodotUnitSpawnFixture typedFixture)
        {
            IReadOnlyList<Tactics.Core.Battle.BattleUnitState> states = typedFixture.CreateStates();
            AssertThat(states.Count).IsEqual(13);
            AssertThat(states.Select(state => state.Unit.Position).Distinct().Count()).IsEqual(13);
            AssertThat(states.All(state =>
                state.Unit.Position.X >= 0 && state.Unit.Position.X < 10 &&
                state.Unit.Position.Y >= 0 && state.Unit.Position.Y < 10)).IsTrue();
            AssertThat(states.All(state =>
                state.Unit.Position.X >= 1 && state.Unit.Position.X <= 8 &&
                state.Unit.Position.Y >= 1 && state.Unit.Position.Y <= 8)).IsTrue();
            typedFixture.BuildPreview();
            GodotUnitActor[] actors = typedFixture.GetChildren().OfType<GodotUnitActor>().ToArray();
            AssertThat(actors.Length).IsEqual(13);
            AssertThat(actors.All(actor =>
                actor.Scale.IsEqualApprox(new Vector2(0.375f, 0.375f)))).IsTrue();
            for (int index = 0; index < actors.Length; index++)
            {
                Vector2 expectedCenter = GodotUnitSpawnFixture.GetCellCenter(states[index].Unit.Position);
                AssertThat(actors[index].Position).IsEqual(expectedCenter);
                Rect2 bounds = GodotUnitSpawnFixture.ComputeActorVisualBounds(actors[index]);
                AssertThat(IsWithin(bounds, GodotUnitSpawnFixture.BoardVisualSafeRect)).IsTrue();
                AssertThat(IsWithin(bounds, GodotUnitSpawnFixture.ViewportVisualSafeRect)).IsTrue();
            }
        }
        AssertThat(UnitPreviewLayout.CanvasSize).IsEqual(new Vector2I(1600, 900));
        AssertThat(GodotUnitSpawnFixture.GridOrigin).IsEqual(new Vector2(440f, 90f));
        AssertThat(GodotUnitSpawnFixture.CellSize).IsEqual(72f);
        AssertThat(GodotUnitSpawnFixture.ViewportSafeInset).IsEqual(24f);
        AssertThat(GodotUnitSpawnFixture.BoardSafeInset).IsEqual(8f);
        AssertThat(GodotUnitSpawnFixture.OverflowPolicy)
            .IsEqual(
                "internal-grid-overflow-allowed; board-frame-and-viewport-clipping-forbidden");
        AssertThat(GodotUnitSpawnFixture.PreviewBackgroundColor)
            .IsEqual(new Color("82909b"));
        AssertThat(GodotUnitSpawnFixture.PreviewGridColor)
            .IsEqual(new Color("455865"));
        gallery.Free();
        fixture.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ProjectWindowUsesSixteenByNineCanvasItemScaling()
    {
        AssertThat(ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32())
            .IsEqual(1600);
        AssertThat(ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32())
            .IsEqual(900);
        AssertThat(ProjectSettings.GetSetting("display/window/size/window_width_override").AsInt32())
            .IsEqual(1600);
        AssertThat(ProjectSettings.GetSetting("display/window/size/window_height_override").AsInt32())
            .IsEqual(900);
        AssertThat(ProjectSettings.GetSetting("display/window/stretch/mode").AsString())
            .IsEqual("canvas_items");
        AssertThat(ProjectSettings.GetSetting("display/window/stretch/aspect").AsString())
            .IsEqual("keep");
    }

    private static JsonDocument LoadGolden()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "unit-batch-v1.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static bool IsWithin(Rect2 bounds, Rect2 safeRect)
    {
        Vector2 boundsEnd = bounds.Position + bounds.Size;
        Vector2 safeEnd = safeRect.Position + safeRect.Size;
        return bounds.Position.X >= safeRect.Position.X &&
            bounds.Position.Y >= safeRect.Position.Y &&
            boundsEnd.X <= safeEnd.X &&
            boundsEnd.Y <= safeEnd.Y;
    }

    private static void AssertBodyOffsetWithinTexture(Texture2D texture, Vector2 offset)
    {
        AssertThat(texture.GetWidth()).IsGreater(0);
        AssertThat(texture.GetHeight()).IsGreater(0);
        AssertThat(Mathf.Abs(offset.X)).IsLessEqual(texture.GetWidth());
        AssertThat(Mathf.Abs(offset.Y)).IsLessEqual(texture.GetHeight());
    }
}
