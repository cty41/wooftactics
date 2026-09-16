using GdUnit4;
using Godot;
using Tactics.Core.Board;
using Tactics.Godot.Adapter.Runtime;
using Tactics.Application.Presentation;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public sealed class IsometricBattleBoardGodotTests
{
    [TestCase]
    public void GridProjectionRoundTripsAllCells()
    {
        for (int y = 0; y < IsometricBattleBoardLayout.GridSize; y++)
        for (int x = 0; x < IsometricBattleBoardLayout.GridSize; x++)
        {
            GridPoint expected = new(x, y);
            bool found = IsometricBattleBoardLayout.TryScreenToGrid(IsometricBattleBoardLayout.GridToScreen(expected), out GridPoint actual);
            AssertThat(found).IsTrue();
            AssertThat(actual).IsEqual(expected);
        }
    }

    [TestCase]
    public void LegacyIsometricProjectionMatchesBattleProjection()
    {
        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
            AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(x, y)))
                .IsEqual(IsometricGridProjection.GridToScreen(new GridPoint(x, y)));
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AdventureViewBuildsARealTenByTenTileMapLayerAndSemanticTargets()
    {
        var view = new GodotAdventureBoardView();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(view);
        await view.ToSignal(view.GetTree(), SceneTree.SignalName.ProcessFrame);
        AdventureBoardDefinition board = AdventureBoard();

        view.SetBoard(board);

        AssertThat(view.TileLayer.GetUsedCells().Count).IsEqual(100);
        AssertThat(view.TryResolveTarget("AdventureCell", "3,4", out Vector2 cell)).IsTrue();
        AssertThat(cell).IsEqual(view.CellCenter(new GridPoint(3, 4)));
        AssertThat(view.TryPointToCell(cell, out GridPoint roundTrip)).IsTrue();
        AssertThat(roundTrip).IsEqual(new GridPoint(3, 4));
        AssertThat(view.TryResolveTarget("AdventureActor", "party-mage", out _)).IsTrue();
        AssertThat(view.TryResolveTarget("AdventureObject", "campfire", out _)).IsTrue();
        view.QueueFree();
    }

    [TestCase]
    public void ProjectionMatchesNativeCanvasContract()
    {
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(0, 0))).IsEqual(new Vector2(550f, 601f));
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(9, 0))).IsEqual(new Vector2(982f, 385f));
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(0, 9))).IsEqual(new Vector2(118f, 385f));
        AssertThat(IsometricBattleBoardLayout.GridToScreen(new GridPoint(9, 9))).IsEqual(new Vector2(550f, 169f));
    }

    [TestCase]
    public void BoardFitterCentersCompleteDiamondBoundsInFullGameplaySafeArea()
    {
        Rect2 bounds = GodotBattleBoardFitter.BoardBounds();
        Rect2 safe = new(30, 90, 1540, 650);
        Transform2D fit = GodotBattleBoardFitter.Fit(bounds, safe);
        Rect2 fitted = GodotBattleBoardFitter.TransformBounds(bounds, fit);

        AssertThat(fitted.GetCenter().DistanceTo(safe.GetCenter())).IsLess(0.01f);
        AssertThat(fitted.Position.X).IsGreaterEqual(safe.Position.X - .01f);
        AssertThat(fitted.End.X).IsLessEqual(safe.End.X + .01f);
        AssertThat(fitted.Position.Y).IsGreaterEqual(safe.Position.Y - .01f);
        AssertThat(fitted.End.Y).IsLessEqual(safe.End.Y + .01f);
    }

    [TestCase]
    public void BoardFitterInversePreservesAllGridCenters()
    {
        Transform2D fit = GodotBattleBoardFitter.Fit(GodotBattleBoardFitter.BoardBounds(),
            new Rect2(30, 90, 1540, 650));
        Transform2D inverse = fit.AffineInverse();
        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
        {
            Vector2 local = IsometricBattleBoardLayout.GridToScreen(new GridPoint(x, y));
            AssertThat(inverse * (fit * local)).IsEqualApprox(local, Vector2.One * .001f);
        }
    }

    [TestCase]
    public void OutsideAndSharedEdgePickingIsDeterministic()
    {
        AssertThat(IsometricBattleBoardLayout.TryScreenToGrid(new Vector2(20, 20), out _)).IsFalse();
        Vector2 sharedEdge = IsometricBattleBoardLayout.GridToScreen(new GridPoint(2, 2)) + new Vector2(48, 0);
        AssertThat(IsometricBattleBoardLayout.TryScreenToGrid(sharedEdge, out GridPoint selected)).IsTrue();
        AssertThat(selected).IsEqual(new GridPoint(2, 1));
    }

    [TestCase]
    public void PartyProjectsBelowEnemiesWithoutChangingLogicalSpawns()
    {
        Vector2 party=IsometricBattleBoardLayout.GridToScreen(new GridPoint(1,4));
        Vector2 enemy=IsometricBattleBoardLayout.GridToScreen(new GridPoint(7,4));
        AssertThat(party.Y).IsGreater(enemy.Y);
        AssertThat(party.X).IsLess(enemy.X);
    }

    [TestCase]
    public void PresentationFacingMatchesFrozenUnityRules()
    {
        AssertThat(GodotPresentationFacingResolver.Initial(0)).IsEqual(GodotUnitFacing.East);
        AssertThat(GodotPresentationFacingResolver.Initial(1)).IsEqual(GodotUnitFacing.West);
        AssertThat(GodotPresentationFacingResolver.Resolve(new GridPoint(0,0),new GridPoint(1,3),GodotUnitFacing.East)).IsEqual(GodotUnitFacing.North);
        AssertThat(GodotPresentationFacingResolver.Resolve(new GridPoint(2,2),new GridPoint(1,1),GodotUnitFacing.West)).IsEqual(GodotUnitFacing.West);
    }

    [TestCase]
    public void TargetingFacingUsesFinalMoveSegmentAndSkillTarget()
    {
        GridPoint origin = new(1, 4);
        AssertThat(GodotPresentationFacingResolver.PreviewMove(origin,
            new[] { new GridPoint(2, 4), new GridPoint(2, 5) }, GodotUnitFacing.East)).IsEqual(GodotUnitFacing.North);
        AssertThat(GodotPresentationFacingResolver.PreviewTarget(origin,
            new GridPoint(1, 2), GodotUnitFacing.East)).IsEqual(GodotUnitFacing.South);
    }

    [TestCase]
    public void InitiativeStripCapsPortraitsAtNineAndUsesTenthSlotForEllipsis()
    {
        AssertThat(GodotInitiativeStrip.VisiblePortraitCount(9)).IsEqual(9);
        AssertThat(GodotInitiativeStrip.ShowsEllipsis(9)).IsFalse();
        AssertThat(GodotInitiativeStrip.VisiblePortraitCount(10)).IsEqual(9);
        AssertThat(GodotInitiativeStrip.ShowsEllipsis(10)).IsTrue();
        AssertThat(GodotInitiativeStrip.TransitionDurationSeconds).IsBetween(.2, .25);
    }

    [TestCase]
    public void MultiCellMoveDurationExceedsLegacyTimerAndMustSerializeAttack()
    {
        AssertThat(GodotBattlePresentationPlayer.EstimateMoveDuration(3, .22d, .06d)).IsGreater(.45d);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StandardUnitProfileCarriesFrozenUnityMotionAndContactContract()
    {
        var profile = new StandardUnitPresentationResource();
        AssertThat(profile.MoveCycleDuration).IsEqualApprox(.22f, .0001f);
        AssertThat(profile.MoveTiltDegrees).IsEqualApprox(5f, .0001f);
        AssertThat(profile.MoveLiftPixels).IsEqualApprox(3f, .0001f);
        AssertThat(profile.MoveSwayPixels).IsEqualApprox(3f, .0001f);
        AssertThat(profile.HitShakeDuration).IsEqualApprox(.07f, .0001f);
        AssertThat(profile.HitRecoilPixels).IsEqualApprox(10f, .0001f);
        AssertThat(profile.LethalCollapseScale).IsEqual(new Vector2(1.02f, .58f));
        AssertThat(profile.CorpseStartHeightPixels).IsEqualApprox(8f, .0001f);
        AssertThat(profile.ShadowContactOffsetY).IsLess(0f);
    }

    [TestCase]
    public void PresentationPlayerIncludesMoveSwayHitRecoilAndCorpseLanding()
    {
        string source = File.ReadAllText(Path.Combine("src", "Tactics.Godot.Adapter", "Runtime",
            "GodotBattlePresentationPlayer.cs"));
        AssertThat(source.Contains("PlayMoveSegment", StringComparison.Ordinal)).IsTrue();
        AssertThat(source.Contains("PlayHitReaction", StringComparison.Ordinal)).IsTrue();
        AssertThat(source.Contains("PlayCorpseLanding", StringComparison.Ordinal)).IsTrue();
        AssertThat(source.Contains("LethalCollapseScale", StringComparison.Ordinal)).IsTrue();
        AssertThat(source.Contains("actor.Body, \"scale\"", StringComparison.Ordinal)).IsTrue();
        AssertThat(source.Contains("_rootBaselines", StringComparison.Ordinal)).IsTrue();
    }

    [TestCase]
    public void PlaybackSpeedSupportsUnityCycleValues()
    {
        AssertThat(GodotBattlePresentationPlayer.IsSupportedSpeed(.5f)).IsTrue();
        AssertThat(GodotBattlePresentationPlayer.IsSupportedSpeed(1f)).IsTrue();
        AssertThat(GodotBattlePresentationPlayer.IsSupportedSpeed(2f)).IsTrue();
        AssertThat(GodotBattlePresentationPlayer.IsSupportedSpeed(4f)).IsTrue();
        AssertThat(GodotBattlePresentationPlayer.IsSupportedSpeed(.75f)).IsFalse();
    }

    [TestCase]
    public void PresentationRecoveryOnlyRunsForAnUnlockedStalledFrame()
    {
        AssertThat(GodotPlayableRunMain.ShouldRecoverPresentationFrame(true, false, false)).IsTrue();
        AssertThat(GodotPlayableRunMain.ShouldRecoverPresentationFrame(false, false, false)).IsFalse();
        AssertThat(GodotPlayableRunMain.ShouldRecoverPresentationFrame(true, true, false)).IsFalse();
        AssertThat(GodotPlayableRunMain.ShouldRecoverPresentationFrame(true, false, true)).IsFalse();
    }

    [TestCase]
    public void TerminalSettlementWinsAfterTheCommittedPresentationQueueDrains()
    {
        AssertThat((int)GodotPlayableRunMain.ResolvePresentationDrainAction(true, true, false, false))
            .IsEqual((int)GodotPlayableRunMain.PresentationDrainAction.DequeueFrame);
        AssertThat((int)GodotPlayableRunMain.ResolvePresentationDrainAction(false, true, false, false))
            .IsEqual((int)GodotPlayableRunMain.PresentationDrainAction.CompleteBattle);
        AssertThat((int)GodotPlayableRunMain.ResolvePresentationDrainAction(false, false, false, false))
            .IsEqual((int)GodotPlayableRunMain.PresentationDrainAction.Refresh);
        AssertThat((int)GodotPlayableRunMain.ResolvePresentationDrainAction(false, true, true, false))
            .IsEqual((int)GodotPlayableRunMain.PresentationDrainAction.CompleteBattle);
        AssertThat((int)GodotPlayableRunMain.ResolvePresentationDrainAction(true, true, true, false))
            .IsEqual((int)GodotPlayableRunMain.PresentationDrainAction.Pause);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task EmptyPresentationFrameCompletesExactlyOnce()
    {
        var player = new GodotBattlePresentationPlayer();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        var completions = new List<PresentationFrameCompletion>();
        player.FrameCompleted += completions.Add;

        player.Play(new BattlePresentationFrame("Decision", null!, null!, [], []),
            new Dictionary<Tactics.Core.Units.UnitInstanceId, GodotUnitActor>());
        await player.ToSignal(player.GetTree(), SceneTree.SignalName.ProcessFrame);
        await player.ToSignal(player.GetTree(), SceneTree.SignalName.ProcessFrame);

        AssertThat(completions.Count).IsEqual(1);
        AssertThat(completions[0].Stage).IsEqual("Decision");
        AssertThat(completions[0].Recovered).IsFalse();
        AssertThat(player.HasPendingFrame).IsFalse();
        player.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task BaneCrescentUsesTwoTravelSegmentsAndReleasesItsTransientNode()
    {
        var host = new Node();
        var player = new GodotBattlePresentationPlayer();
        host.AddChild(player);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        player.ConfigureSkills([new SkillPresentationResource
        {
            SkillBranch = "demonbound.bane", ProgrammaticKind = "bane-crescent",
            PrimaryColor = new Color(.72f, .2f, 1f), SecondaryColor = new Color(1f, .45f, .95f),
            TravelDuration = .28f, ImpactDuration = .16f
        }]);
        player.SetSpeed(4f);
        UnitInstanceId actor = new("demonbound"), near = new("near"), far = new("far");
        ContentId skill = new("skill.demonbound.bane.lv1");
        BattlePresentationMarker[] markers = [new(PresentationMarkerKind.Begin, 0)];
        BattlePresentationCue action = new(PresentationCueKind.Melee, actor, near, skill,
            new GridPoint(1, 1), new GridPoint(3, 1), [new GridPoint(2, 1), new GridPoint(3, 1)],
            [near, far], markers);
        BattlePresentationCue firstHit = new(PresentationCueKind.Hit, near, near, skill,
            new GridPoint(2, 1), new GridPoint(2, 1), [], [near], markers, InstigatorId: actor);
        BattlePresentationCue secondHit = new(PresentationCueKind.Hit, far, far, skill,
            new GridPoint(3, 1), new GridPoint(3, 1), [], [far], markers, InstigatorId: actor);
        var numbers = new List<UnitInstanceId>();
        player.NumberRequested += value => numbers.Add(value.TargetId);

        player.Play(new BattlePresentationFrame("Bane", null!, null!, [action, firstHit, secondHit],
            [new(BattlePresentationNumberKind.Normal, near, "-6", PresentationMarkerKind.Impact, 0),
             new(BattlePresentationNumberKind.Normal, far, "-6", PresentationMarkerKind.Impact, 1)]),
            new Dictionary<UnitInstanceId, GodotUnitActor>());
        await player.ToSignal(player.GetTree().CreateTimer(.03), SceneTreeTimer.SignalName.Timeout);
        AssertThat(player.ActiveTransientCount).IsEqual(1);
        await player.ToSignal(player.GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);

        AssertThat(numbers.SequenceEqual(new[] { near, far })).IsTrue();
        AssertThat(player.ActiveTransientCount).IsEqual(0);
        AssertThat(player.HasPendingFrame).IsFalse();
        host.QueueFree();
    }

    [TestCase]
    public void ProgrammaticFxIsHiddenUntilItsReleaseCallback()
    {
        string source=File.ReadAllText(Path.Combine("src","Tactics.Godot.Adapter","Runtime",
            "GodotBattlePresentationPlayer.cs"));
        int hidden=source.IndexOf("Visible=false",StringComparison.Ordinal);
        int release=source.IndexOf("fx.Visible=true",StringComparison.Ordinal);
        int travel=source.IndexOf("\"Progress\",1f",StringComparison.Ordinal);
        AssertThat(hidden).IsGreaterEqual(0);
        AssertThat(release).IsGreater(hidden);
        AssertThat(travel).IsGreater(release);
    }

    [TestCase]
    public void BaseTilesAlternateWarmAndCoolProjectPalette()
    {
        Color first = GodotIsometricBattleBoard.BaseTileColor(new GridPoint(0, 0), false);
        Color neighbor = GodotIsometricBattleBoard.BaseTileColor(new GridPoint(1, 0), false);
        AssertThat(first).IsNotEqual(neighbor);
        AssertThat(GodotIsometricBattleBoard.BaseTileColor(new GridPoint(2, 0), false)).IsEqual(first);
    }

    [TestCase]
    public void BattleBackdropUsesProjectOwnedGradientContract()
    {
        AssertThat(GodotBattleBackdrop.ShaderCode.Contains("vignette_strength", StringComparison.Ordinal)).IsTrue();
        AssertThat(GodotBattleBackdrop.ShaderCode.Contains("noise_strength", StringComparison.Ordinal)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedBoardResourceAndCatalogAreValid()
    {
        var board = ResourceLoader.Load<IsometricBattleBoardResource>("res://content/presentation/BattleBoardPureRunIsometricV1.tres");
        var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres");
        AssertThat(board).IsNotNull();
        AssertThat(catalog).IsNotNull();
        if (board is null || catalog is null) return;
        AssertThat(board.TileSize).IsEqual(new Vector2(96, 48));
        AssertThat(catalog.Entries.Length).IsEqual(185);
        AssertThat(catalog.Entries.Count(entry => entry.ContentIdValue == "battle-board.pure-run.isometric-v1")).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ProgrammaticSkillProfilesExcludeThirdPartyPayload()
    {
        string[] paths=["FireballPresentation.tres","BoneSpearPresentation.tres","ThrustPresentation.tres","IceBoltPresentation.tres","LightningPresentation.tres","PoisonSpearPresentation.tres","AmplifyDamagePresentation.tres"];
        SkillPresentationResource[] profiles=paths.Select(name=>ResourceLoader.Load<SkillPresentationResource>($"res://content/presentation/{name}")!).ToArray();
        AssertThat(profiles.All(value=>value is not null)).IsTrue();
        AssertThat(profiles.All(value=>value.PayloadBoundary=="programmatic-only-no-piloto-payload")).IsTrue();
        AssertThat(profiles.Single(value=>value.ProgrammaticKind=="fireball").LevelOneHasAreaEffect).IsFalse();
        AssertThat(profiles.Single(value=>value.ProgrammaticKind=="bone-spear").MaximumGhosts).IsEqual(2);
        var catalog=ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres")!;
        AssertThat(catalog.Entries.Length).IsEqual(185);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StatusProfileRemainsAndNonUnityBoardCameraProfileIsRemoved()
    {
        var status=ResourceLoader.Load<StatusPresentationResource>("res://content/presentation/StandardStatusPresentationV1.tres");
        var catalog=ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres");
        AssertThat(status).IsNotNull();AssertThat(catalog).IsNotNull();if(status is null||catalog is null)return;
        AssertThat(status.MaximumVisibleStatuses).IsEqual(4);
        AssertThat(catalog.Entries.Any(entry=>entry.ContentIdValue=="presentation.camera.battle-focus-v1")).IsFalse();
    }

    private static AdventureBoardDefinition AdventureBoard() => new(
        new ContentId("adventure-board.test.camp"), 10, 10,
        Enumerable.Range(0, 10).SelectMany(value => new[] { new GridPoint(value, 0), new GridPoint(value, 9) })
            .Concat(Enumerable.Range(1, 8).SelectMany(value => new[] { new GridPoint(0, value), new GridPoint(9, value) }))
            .Distinct().ToArray(),
        [new AdventureBoardObject("campfire", AdventureObjectKind.Campfire, new GridPoint(4, 5), true, false)],
        [new AdventureActorPlacement("party-mage", new GridPoint(2, 5))],
        new GridPoint(1, 5), new GridPoint(8, 5));
}
