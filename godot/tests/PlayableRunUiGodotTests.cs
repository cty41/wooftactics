using GdUnit4;
using Godot;
using System.Reflection;
using Tactics.Core.Content;
using Tactics.Core.AI;
using Tactics.Core.Board;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Skills;
using Tactics.Core.Units;
using Tactics.Godot.Adapter.Runtime;
using Tactics.Godot.Adapter.Editor;
using Tactics.Application.Battle;
using Tactics.Application.Presentation;
using Tactics.Application.Runs;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class PlayableRunUiGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void TacticsThemeDefinesProjectPaletteAndInteractiveStates()
    {
        using Theme theme = GodotTacticsTheme.Create();

        AssertThat(theme.GetColor("font_color", "Label")).IsEqual(GodotTacticsTheme.TextPrimary);
        AssertThat(theme.GetColor("font_disabled_color", GodotTacticsTheme.PrimaryButton))
            .IsEqual(GodotTacticsTheme.DisabledText);
        AssertThat(theme.GetStylebox("normal", GodotTacticsTheme.PrimaryButton) is StyleBoxFlat).IsTrue();
        AssertThat(theme.GetStylebox("hover", GodotTacticsTheme.PrimaryButton) is StyleBoxFlat).IsTrue();
        AssertThat(theme.GetStylebox("pressed", GodotTacticsTheme.PrimaryButton) is StyleBoxFlat).IsTrue();
        AssertThat(theme.GetStylebox("disabled", GodotTacticsTheme.PrimaryButton) is StyleBoxFlat).IsTrue();
        AssertThat(theme.GetStylebox("panel", GodotTacticsTheme.Panel) is StyleBoxFlat).IsTrue();
        AssertThat(theme.GetStylebox("panel", GodotTacticsTheme.Card) is StyleBoxFlat).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MainSceneLoadsNativePlayableUiContract()
    {
        AssertThat(GodotPlayableRunMain.CanvasWidth).IsEqual(1600);
        AssertThat(GodotPlayableRunMain.CanvasHeight).IsEqual(900);
        AssertThat(ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32()).IsEqual(1600);
        AssertThat(ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32()).IsEqual(900);
        AssertThat(ProjectSettings.GetSetting("display/window/stretch/mode").AsString()).IsEqual("canvas_items");
        AssertThat(ProjectSettings.GetSetting("display/window/stretch/aspect").AsString()).IsEqual("keep");
        // The battle shell intentionally keeps a fixed 1600x900 logical canvas.
        // Non-16:9 windows are letterboxed by Godot instead of dynamically
        // reflowing gameplay controls, which prevents stretching and cropping.
        AssertThat(ProjectSettings.GetSetting("display/window/size/window_width_override").AsInt32()).IsEqual(1600);
        AssertThat(ProjectSettings.GetSetting("display/window/size/window_height_override").AsInt32()).IsEqual(900);
        AssertThat(InputMap.HasAction("toggle_console")).IsTrue();
        PackedScene? scene = ResourceLoader.Load<PackedScene>("res://scenes/Main.tscn");
        AssertThat(scene).IsNotNull();
        Node? root = scene?.Instantiate();
        AssertThat(root).IsInstanceOf<TacticsMigrationRoot>();
        root?.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CheatConsoleStartsHiddenAndOccupiesTopQuarter()
    {
        var console = new GodotBattleCheatConsole();
        console._Ready();
        AssertThat(console.Visible).IsFalse();
        AssertThat(console.Size.Y).IsEqual(225f);
        AssertThat(console.MouseFilter).IsEqual(Control.MouseFilterEnum.Stop);
        AssertThat(Descendants<RichTextLabel>(console).Single().SelectionEnabled).IsTrue();
        console.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CheatConsoleCopiesVisibleFilterOrAllRetainedEntries()
    {
        var clipboard = new MemoryClipboard();
        var console = new GodotBattleCheatConsole(clipboard);
        console._Ready();
        console.SetEntries([
            new BattleUiLogEntry(BattleUiLogCategory.Gameplay, "damage", "DamageAppliedEvent"),
            new BattleUiLogEntry(BattleUiLogCategory.Ai, "decision", "AiDecisionEvent"),
            new BattleUiLogEntry(BattleUiLogCategory.Rejected, "blocked", "CommandRejectedEvent")
        ]);
        OptionButton filter = Descendants<OptionButton>(console).Single();
        filter.Select(1);
        filter.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
        Descendants<Button>(console).Single(value => value.Text == "Copy Visible").EmitSignal(Button.SignalName.Pressed);
        AssertThat(clipboard.Text).Contains("damage");
        AssertThat(clipboard.Text).NotContains("decision");

        Descendants<Button>(console).Single(value => value.Text == "Copy All").EmitSignal(Button.SignalName.Pressed);
        AssertThat(clipboard.Text).Contains("damage");
        AssertThat(clipboard.Text).Contains("decision");
        AssertThat(clipboard.Text).Contains("blocked");
        Label copyStatus = Descendants<Label>(console).Single(value => value.Name == "CopyStatus");
        AssertThat(copyStatus.Text).Contains("Copied");
        console.SetOpen(false);
        console.SetOpen(true);
        AssertThat(filter.Selected).IsEqual(1);
        AssertThat(console.RenderVisible()).Contains("damage");
        AssertThat(copyStatus.Text).IsEmpty();
        console.Free();
    }

    [TestCase]
    public void SettlementCoordinatorRejectsEveryDuplicateAfterTheFirstAttempt()
    {
        var coordinator = new GodotBattleSettlementCoordinator();
        var result = new PureRunBattleResult("run", 148, new ContentId("encounter.pure-run.special"),
            true, 11, 1, Array.Empty<BattlePartyResult>());
        AssertThat(coordinator.TryBegin(result, "terminal", out BattleSettlementDiagnostic first)).IsTrue();
        AssertThat((int)first.Stage).IsEqual((int)BattleSettlementStage.Submitting);
        AssertThat(coordinator.TryBegin(result, "duplicate", out BattleSettlementDiagnostic duplicate)).IsFalse();
        AssertThat(duplicate.Marker).StartsWith("duplicate:");

        AssertThat((int)coordinator.Reject("save.non_increasing_revision").Stage).IsEqual((int)BattleSettlementStage.Rejected);
        AssertThat(coordinator.TryBegin(result, "retry", out _)).IsFalse();
        coordinator.Reset();
        AssertThat(coordinator.TryBegin(result, "new_battle", out BattleSettlementDiagnostic retry)).IsTrue();
        AssertThat(retry.AttemptId).IsEqual(2L);
        AssertThat((int)coordinator.MarkSaved(149).Stage).IsEqual((int)BattleSettlementStage.Saved);
        AssertThat((int)coordinator.MarkNavigationCompleted().Stage).IsEqual((int)BattleSettlementStage.NavigationCompleted);
        AssertThat(coordinator.TryBegin(result, "late_callback", out _)).IsFalse();
    }

    [TestCase]
    public void FailedSaveResultWithTerminalSnapshotIsTreatedAsCommitted()
    {
        var summary = new PureRunSummary("run", 7, PureRunOutcome.BossVictory, 5, 10, 20,
            Array.Empty<ContentId>(), Array.Empty<string>(), Array.Empty<string>());
        var snapshot = new PureRunSaveSnapshot(149, null, summary);
        var failedReadback = new RunSessionResult(false, "save.write_failed", snapshot, null);
        var ordinaryFailure = new RunSessionResult(false, "save.write_failed", new PureRunSaveSnapshot(148, null, null), null);

        AssertThat(GodotPlayableRunMain.HasCommittedTerminalSnapshot(failedReadback)).IsTrue();
        AssertThat(GodotPlayableRunMain.HasCommittedTerminalSnapshot(ordinaryFailure)).IsFalse();
    }

    [TestCase]
    public void CheatConsoleBlocksEveryGameplayIntentWithoutPausingPlayback()
    {
        AssertThat(GodotPlayableRunMain.ShouldBlockBattleIntent(true, false)).IsTrue();
        AssertThat(GodotPlayableRunMain.ShouldBlockBattleIntent(false, true)).IsTrue();
        AssertThat(GodotPlayableRunMain.ShouldBlockBattleIntent(false, false, true)).IsTrue();
        AssertThat(GodotPlayableRunMain.ShouldBlockBattleIntent(false, false)).IsFalse();
    }

    [TestCase]
    public void BattleDiagnosticMetersUseCompactBounds()
    {
        AssertThat(GodotPlayableRunMain.UnitMeterSize).IsEqual(new Vector2(44, 18));
        AssertThat(GodotPlayableRunMain.UnitMeterBarHeight).IsEqual(7);
    }

    [TestCase]
    public void BattleActionLabelsUseDisplayNamesAndOnlyShowPositiveManaOnSecondLine()
    {
        AssertThat(GodotPlayableRunMain.FormatBattleActionLabel("Magic Attack", 0, false))
            .IsEqual("Magic Attack");
        AssertThat(GodotPlayableRunMain.FormatBattleActionLabel("Fireball", 5, false))
            .IsEqual("Fireball\nMP 5");
        AssertThat(GodotPlayableRunMain.FormatBattleActionLabel("Fireball", 5, true))
            .IsEqual("Fireball\nMP 5 · Used");
    }

    [TestCase]
    public void BattleHudPanelsStayInsideTheLogicalCanvas()
    {
        AssertThat(GodotPlayableRunMain.BattleHudPanelRects.Count).IsEqual(5);
        foreach (Rect2 rect in GodotPlayableRunMain.BattleHudPanelRects.Values)
        {
            AssertThat(rect.Position.X).IsGreaterEqual(0);
            AssertThat(rect.Position.Y).IsGreaterEqual(0);
            AssertThat(rect.End.X).IsLessEqual(GodotPlayableRunMain.CanvasWidth);
            AssertThat(rect.End.Y).IsLessEqual(GodotPlayableRunMain.CanvasHeight);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ActiveUnitPanelBindsIdentityResourcesAndHidesMissingSpecialProgress()
    {
        using Image image = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        using ImageTexture texture = ImageTexture.CreateFromImage(image);
        var definition = new UnitDefinitionResource
        {
            DisplayName = "Demonbound",
            DownRightTexture = texture
        };
        var panel = new GodotBattleActiveUnitPanel();
        panel._Ready();

        panel.Bind(definition, BattleUnit(corruption: 9));
        AssertThat(panel.Visible).IsTrue();
        AssertThat(Descendants<TextureRect>(panel).Single(value => value.Name == "ActiveUnitPortrait").Texture)
            .IsSame(texture);
        AssertThat(Descendants<Label>(panel).Single(value => value.Name == "ActiveUnitName").Text)
            .IsEqual("Demonbound");
        AssertThat(Descendants<Label>(panel).Single(value => value.Name == "HealthValue").Text)
            .IsEqual("16/20");
        AssertThat(Descendants<Label>(panel).Single(value => value.Name == "ManaValue").Text)
            .IsEqual("7/18");
        AssertThat(Descendants<Label>(panel).Single(value => value.Name == "SpecialValue").Text)
            .IsEqual("Corruption  9/10");
        AssertThat(Descendants<Control>(panel).Single(value => value.Name == "SpecialResourceRow").Visible)
            .IsTrue();

        panel.Bind(definition, BattleUnit());
        AssertThat(Descendants<Control>(panel).Single(value => value.Name == "SpecialResourceRow").Visible)
            .IsFalse();
        panel.Clear();
        AssertThat(panel.Visible).IsFalse();
        panel.Free();
    }

    [TestCase]
    public void CorruptionProgressUsesThreeRiskStagesAndPossessionPulse()
    {
        AssertThat((int)GodotBattleActiveUnitPanel.StageFor(0))
            .IsEqual((int)GodotBattleSpecialResourceStage.Low);
        AssertThat((int)GodotBattleActiveUnitPanel.StageFor(4))
            .IsEqual((int)GodotBattleSpecialResourceStage.Low);
        AssertThat((int)GodotBattleActiveUnitPanel.StageFor(5))
            .IsEqual((int)GodotBattleSpecialResourceStage.Elevated);
        AssertThat((int)GodotBattleActiveUnitPanel.StageFor(8))
            .IsEqual((int)GodotBattleSpecialResourceStage.Elevated);
        AssertThat((int)GodotBattleActiveUnitPanel.StageFor(9))
            .IsEqual((int)GodotBattleSpecialResourceStage.Critical);
        AssertThat((int)GodotBattleActiveUnitPanel.StageFor(10))
            .IsEqual((int)GodotBattleSpecialResourceStage.Critical);

        GodotBattleSpecialResourceView possessed = GodotBattleActiveUnitPanel.ProjectSpecialResource(
            BattleUnit(corruption: 10, possessed: true))!;
        AssertThat(possessed.Current).IsEqual(10);
        AssertThat(possessed.Maximum).IsEqual(10);
        AssertThat(possessed.Pulsing).IsTrue();
        AssertThat(GodotBattleActiveUnitPanel.ProjectSpecialResource(BattleUnit())).IsNull();
    }

    [TestCase]
    public void ActiveUnitPanelProjectionFollowsTurnOwnerInsteadOfSelectionOrFaction()
    {
        BattleUiUnitSnapshot player = BattleUnit();
        BattleUiUnitSnapshot enemy = player with
        {
            UnitId = new UnitInstanceId("enemy.0"),
            DefinitionId = new ContentId("unit.enemy"),
            PlayerNumber = 1
        };
        var snapshot = new BattleUiSnapshot(PlayableBattlePhase.AiTurn, 1, enemy.UnitId,
            BattleTargetingMode.None, null, [player, enemy], Array.Empty<SkillDefinition>(),
            Array.Empty<GridPoint>(), Array.Empty<BattleUiTarget>(), null, Array.Empty<GridPoint>(),
            new Dictionary<UnitInstanceId, GridPoint>(), Array.Empty<Tactics.Core.Battle.BattleEvent>(),
            [player.UnitId, enemy.UnitId], 1, null,
            MoveAvailability: new BattleUiMoveAvailability(false, "ai_turn"));

        AssertThat(GodotPlayableRunMain.ResolveActiveUnit(snapshot)).IsEqual(enemy);
        AssertThat(GodotPlayableRunMain.ResolveActiveUnit(snapshot with
        {
            ActiveUnitId = new UnitInstanceId("missing")
        })).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BattleHoverTooltipClampsToCanvasAndNeverInterceptsPointerInput()
    {
        var tooltip = new GodotBattleHoverTooltip();
        tooltip._Ready();
        tooltip.ShowDetail("Cell detail", new Vector2(1590, 890));

        AssertThat(tooltip.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        AssertThat(tooltip.Position).IsEqual(new Vector2(1180, 782));
        AssertThat(Descendants<Label>(tooltip).Single().MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        AssertThat(tooltip.Visible).IsTrue();
        tooltip.HideDetail();
        AssertThat(tooltip.Visible).IsFalse();
        tooltip.Free();
    }

    [TestCase]
    public void ActiveTileMarkerIsHiddenWhileCommittedActionPresentationRuns()
    {
        AssertThat(GodotPlayableRunMain.ShouldShowActiveMarker(false, false)).IsTrue();
        AssertThat(GodotPlayableRunMain.ShouldShowActiveMarker(true, false)).IsFalse();
        AssertThat(GodotPlayableRunMain.ShouldShowActiveMarker(false, true)).IsFalse();
    }

    private static BattleUiUnitSnapshot BattleUnit(int? corruption = null, bool possessed = false) => new(
        new UnitInstanceId("unit.0"),
        new ContentId("unit.pure-run.demonbound"),
        new GridPoint(1, 1),
        0,
        true,
        16,
        20,
        7,
        18,
        false,
        Array.Empty<ContentId>(),
        new Dictionary<ContentId, int>(),
        Corruption: corruption,
        IsPossessed: possessed);

    [TestCase]
    [RequireGodotRuntime]
    public async Task PartialStartingSkillsAreDiscardedWhenMainIsRecreated()
    {
        var store = new ProbeRunStore();
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var first = new GodotPlayableRunMain();
        first.ConfigureTestContext(new GodotPlayableRunTestContext(store, 37, "setup-draft-first"));
        tree.Root.AddChild(first);
        await first.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        PureRunSessionService service = (PureRunSessionService)typeof(GodotPlayableRunMain)
            .GetField("_run", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(first)!;
        AssertThat(service.SelectPartyMember("pure_run_amazon").Succeeded).IsTrue();
        AssertThat(service.SelectPartyMember("pure_run_demonbound").Succeeded).IsTrue();
        AssertThat(service.SelectPartyMember("pure_run_mage").Succeeded).IsTrue();
        AssertThat(service.ChooseParty(["pure_run_amazon", "pure_run_demonbound", "pure_run_mage"]).Succeeded).IsTrue();
        AssertThat(service.ChooseStartingSkill("pure_run_amazon", new ContentId("skill.amazon.thrust.lv1")).Succeeded).IsTrue();
        AssertThat(store.SaveCount).IsEqual(0);
        first.QueueFree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        var replay = new GodotPlayableRunMain();
        replay.ConfigureTestContext(new GodotPlayableRunTestContext(store, 37, "setup-draft-replay"));
        tree.Root.AddChild(replay);
        await replay.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        GodotPlayableRunProbe probe = replay.CaptureTestProbe();
        AssertThat(probe.PageTitle).Contains("START CAMP");
        AssertThat(probe.SetupDraftActive).IsTrue();
        AssertThat(probe.SetupSelectedCount).IsEqual(0);
        AssertThat(store.SaveCount).IsEqual(0);

        PureRunSessionService replayService = (PureRunSessionService)typeof(GodotPlayableRunMain)
            .GetField("_run", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(replay)!;
        AssertThat(replayService.SelectPartyMember("pure_run_amazon").Succeeded).IsTrue();
        typeof(GodotPlayableRunMain).GetMethod("DiscardSetupFromPause", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(replay, null);
        await replay.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(replay.CaptureTestProbe().SetupDraftActive).IsTrue();
        AssertThat(replay.CaptureTestProbe().SetupSelectedCount).IsEqual(0);
        AssertThat(store.SaveCount).IsEqual(0);

        AssertThat(replayService.SelectPartyMember("pure_run_amazon").Succeeded).IsTrue();
        typeof(GodotPlayableRunMain).GetMethod("SaveAndQuit", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(replay, null);
        AssertThat(replay.CaptureTestProbe().QuitRequested).IsTrue();
        AssertThat(replayService.HasSetupDraft).IsFalse();
        AssertThat(store.SaveCount).IsEqual(0);
        replay.QueueFree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task EscapeUsesTheSameGlobalPauseOverlayAcrossStartSkillAndInitialAdventure()
    {
        var store = new ProbeRunStore();
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, 37, "pause-shell"));
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(ui);
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        GodotPlayableRunProbe start = ui.CaptureTestProbe();
        AssertThat(start.PageTitle).Contains("START CAMP");
        AssertThat(start.InputContext).IsEqual("Setup");
        GodotObject overlay = ui.GetNode("GlobalPauseOverlay");

        // Real GUI focus may consume ui_cancel before _UnhandledInput. Exercise the
        // root _Input phase directly so this regression cannot pass on unhandled-only routing.
        ui._Input(EscapeKeyEvent());
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(ui.CaptureTestProbe().PauseMenuVisible).IsTrue();
        Button continueButton = Descendants<Button>((Node)overlay).Single(value => value.Text == "CONTINUE");
        await ClickControl(continueButton, tree);
        AssertThat(ui.CaptureTestProbe().PauseMenuVisible).IsFalse();

        PureRunSessionService service = (PureRunSessionService)typeof(GodotPlayableRunMain)
            .GetField("_run", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ui)!;
        AssertThat(service.SelectPartyMember("pure_run_amazon").Succeeded).IsTrue();
        AssertThat(service.SelectPartyMember("pure_run_demonbound").Succeeded).IsTrue();
        AssertThat(service.SelectPartyMember("pure_run_mage").Succeeded).IsTrue();
        RunSessionResult skillSetup = service.ChooseParty(["pure_run_amazon", "pure_run_demonbound", "pure_run_mage"]);
        typeof(GodotPlayableRunMain).GetMethod("ShowNewRunSetup", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(ui, new object[] { skillSetup.Snapshot! });
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(ui.CaptureTestProbe().PageTitle).Contains("STARTING SKILL");
        await ToggleEscape(ui, tree);
        AssertThat(ui.CaptureTestProbe().PauseMenuVisible).IsTrue();
        AssertThat(ReferenceEquals(overlay, ui.GetNode("GlobalPauseOverlay"))).IsTrue();
        await ToggleEscape(ui, tree);

        AssertThat(service.ChooseStartingSkill("pure_run_amazon", new ContentId("skill.amazon.thrust.lv1")).Succeeded).IsTrue();
        RunSessionResult completed = service.ChooseStartingSkill("pure_run_mage", new ContentId("skill.mage.fireball.lv1"));
        AssertThat(completed.Succeeded).IsTrue();
        AssertThat(store.SaveCount).IsEqual(1);
        typeof(GodotPlayableRunMain).GetMethod("RouteRunState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(ui, new object[] { completed.Snapshot! });
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(ui.CaptureTestProbe().PageTitle).Contains("TILE ADVENTURE");
        AssertThat(ui.CaptureTestProbe().InputContext).IsEqual("Adventure");
        Control page = (Control)typeof(GodotPlayableRunMain)
            .GetField("_page", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ui)!;
        AssertThat(new Rect2(page.GetNode<PanelContainer>("PageHeader").Position,
            page.GetNode<PanelContainer>("PageHeader").Size)).IsEqual(GodotPlayableRunMain.AdventureHeaderRect);
        AssertThat(new Rect2(page.GetNode<ColorRect>("RunShellPanel").Position,
            page.GetNode<ColorRect>("RunShellPanel").Size)).IsEqual(GodotPlayableRunMain.AdventureRunShellRect);
        AssertThat(new Rect2(page.GetNode<PanelContainer>("AdventureLeaderPanel").Position,
            page.GetNode<PanelContainer>("AdventureLeaderPanel").Size)).IsEqual(GodotPlayableRunMain.AdventureLeaderRect);
        AssertThat(new Rect2(page.GetNode<Button>("AdventureInventoryButton").Position,
            page.GetNode<Button>("AdventureInventoryButton").Size)).IsEqual(GodotPlayableRunMain.AdventureInventoryRect);
        await ToggleEscape(ui, tree);
        AssertThat(ui.CaptureTestProbe().PauseMenuVisible).IsTrue();
        AssertThat(ReferenceEquals(overlay, ui.GetNode("GlobalPauseOverlay"))).IsTrue();
        Button optionsButton = Descendants<Button>((Node)overlay).Single(value => value.Text == "OPTIONS");
        await ClickControl(optionsButton, tree);
        AssertThat(ui.CaptureTestProbe().PauseMenuView).IsEqual("Options");
        await ToggleEscape(ui, tree);
        AssertThat(ui.CaptureTestProbe().PauseMenuVisible).IsFalse();
        await ToggleEscape(ui, tree);
        AssertThat(ui.CaptureTestProbe().PauseMenuView).IsEqual("Root");
        AssertThat(Descendants<Button>((Node)overlay).Select(value => value.Text).ToArray()).NotContains("BACK");
        optionsButton = Descendants<Button>((Node)overlay).Single(value => value.Text == "OPTIONS");
        await ClickControl(optionsButton, tree);
        Button backButton = Descendants<Button>((Node)overlay).Single(value => value.Text == "BACK");
        await ClickControl(backButton, tree);
        AssertThat(ui.CaptureTestProbe().PauseMenuView).IsEqual("Root");
        AssertThat(Descendants<Button>((Node)overlay).Select(value => value.Text).ToArray()).Contains("CONTINUE");
        continueButton = Descendants<Button>((Node)overlay).Single(value => value.Text == "CONTINUE");
        await ClickControl(continueButton, tree);
        AssertThat(ui.CaptureTestProbe().PauseMenuVisible).IsFalse();

        GodotAdventureBoardView adventureBoard = (GodotAdventureBoardView)typeof(GodotPlayableRunMain)
            .GetField("_adventureBoard", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ui)!;
        GridPoint origin = adventureBoard.ActorCells.Values.First(cell => cell == new GridPoint(2, 5));
        await ClickPoint(adventureBoard, adventureBoard.CellCenter(new GridPoint(8, 5)), tree);
        for (int frame = 0; frame < 180 && adventureBoard.LastMovementDiagnostic?.CurrentCell == origin; frame++)
            await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        adventureBoard._Notification((int)Control.NotificationResized);
        await ClickPoint(adventureBoard, adventureBoard.CellCenter(new GridPoint(4, 6)), tree);
        for (int frame = 0; frame < 240 && adventureBoard.IsMovementLocked; frame++)
            await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(ui.CaptureTestProbe().BattleActive).IsFalse();
        adventureBoard = (GodotAdventureBoardView)typeof(GodotPlayableRunMain)
            .GetField("_adventureBoard", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ui)!;

        await ClickPoint(adventureBoard, adventureBoard.CellCenter(new GridPoint(8, 5)), tree);
        for (int frame = 0; frame < 360 && !ui.CaptureTestProbe().BattleActive; frame++)
            await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(ui.CaptureTestProbe().BattleActive).IsTrue();
        AssertThat(ui.FindChild("AdventureBattleEntryBoard", true, false)).IsNull();
        ui._Input(EscapeKeyEvent());
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(ui.CaptureTestProbe().PlaybackPaused).IsTrue();
        optionsButton = Descendants<Button>((Node)overlay).Single(value => value.Text == "OPTIONS");
        await ClickControl(optionsButton, tree);
        backButton = Descendants<Button>((Node)overlay).Single(value => value.Text == "BACK");
        await ClickControl(backButton, tree);
        continueButton = Descendants<Button>((Node)overlay).Single(value => value.Text == "CONTINUE");
        await ClickControl(continueButton, tree);
        AssertThat(ui.CaptureTestProbe().PlaybackPaused).IsFalse();

        MethodInfo togglePause = typeof(GodotPlayableRunMain).GetMethod("TogglePause",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        togglePause.Invoke(ui, null);
        ui._Input(EscapeKeyEvent());
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        continueButton = Descendants<Button>((Node)overlay).Single(value => value.Text == "CONTINUE");
        await ClickControl(continueButton, tree);
        AssertThat(ui.CaptureTestProbe().PlaybackPaused).IsTrue();
        togglePause.Invoke(ui, null);

        ui._Input(EscapeKeyEvent());
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Button abandon = Descendants<Button>((Node)overlay).Single(value => value.Text == "ABANDON RUN");
        await ClickControl(abandon, tree);
        ConfirmationDialog confirmation = (ConfirmationDialog)typeof(GodotPlayableRunMain)
            .GetField("_pauseConfirmation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ui)!;
        AssertThat(ReferenceEquals(confirmation.GetParent(), overlay)).IsTrue();
        AssertThat(ui.CaptureTestProbe().PauseMenuVisible).IsTrue();
        AssertThat(ui.CaptureTestProbe().PlaybackPaused).IsTrue();
        confirmation.EmitSignal(ConfirmationDialog.SignalName.Canceled);
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(ui.CaptureTestProbe().PauseMenuView).IsEqual("Root");
        continueButton = Descendants<Button>((Node)overlay).Single(value => value.Text == "CONTINUE");
        await ClickControl(continueButton, tree);
        AssertThat(ui.CaptureTestProbe().PlaybackPaused).IsFalse();

        ui._Input(EscapeKeyEvent());
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        abandon = Descendants<Button>((Node)overlay).Single(value => value.Text == "ABANDON RUN");
        await ClickControl(abandon, tree);
        confirmation = (ConfirmationDialog)typeof(GodotPlayableRunMain)
            .GetField("_pauseConfirmation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ui)!;
        confirmation.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(ui.CaptureTestProbe().PauseMenuVisible).IsFalse();
        AssertThat(ui.CaptureTestProbe().PlaybackPaused).IsFalse();
        AssertThat(typeof(GodotPlayableRunMain).GetField("_pauseConfirmation",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ui)).IsNull();

        ui.QueueFree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DeterministicBattleRoutingFailsClosedForUnknownNodeIds()
    {
        MethodInfo supported = typeof(GodotPlayableRunMain).GetMethod("IsSupportedDeterministicBattleNode",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        AssertThat((bool)supported.Invoke(null, new object[] { "layer_05_battle" })!).IsTrue();
        AssertThat((bool)supported.Invoke(null, new object[] { "layer_08_elite" })!).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PauseMenuIsOnePersistentMainOverlayAndRebuildsActionsFromInputContext()
    {
        var ui = new GodotPlayableRunMain();
        var root = new Control();
        ui.AddChild(root);
        MethodInfo build = typeof(GodotPlayableRunMain).GetMethod("BuildPauseMenu",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo rebuild = typeof(GodotPlayableRunMain).GetMethod("RebuildPauseMenuContent",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        build.Invoke(ui, new object[] { root, true });
        rebuild.Invoke(ui, null);
        ColorRect overlay = Descendants<ColorRect>(ui).Single(control => control.Name == "GlobalPauseOverlay");
        string[] runButtons = Descendants<Button>(overlay).Select(button => button.Text).ToArray();

        build.Invoke(ui, new object[] { root, false });
        ColorRect sameOverlay = Descendants<ColorRect>(ui).Single(control => control.Name == "GlobalPauseOverlay");
        typeof(GodotPlayableRunMain).GetMethod("SetInputContext", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(ui, new object[] { GodotPlayableRunMain.RunInputContext.Setup });
        rebuild.Invoke(ui, null);
        string[] setupButtons = Descendants<Button>(overlay).Select(button => button.Text).ToArray();

        AssertThat(ReferenceEquals(overlay, sameOverlay)).IsTrue();
        AssertThat(runButtons).Contains("CONTINUE");
        AssertThat(runButtons).Contains("ABANDON RUN");
        AssertThat(runButtons).Contains("SAVE AND QUIT");
        AssertThat(setupButtons).Contains("DISCARD SETUP");
        AssertThat(setupButtons).Contains("QUIT — SETUP NOT SAVED");
        AssertThat(overlay.ZIndex).IsGreater(1000);
        ui.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RunDefinitionUsesCanonicalPoisonSpearStartingChoice()
    {
        var resource = new PureRunDefinitionResource
        {
            ContentIdValue = "run.pure-run.three-encounter-v1",
            EncounterContentIds = ["encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3"],
            CharacterIds = ["pure_run_mage", "pure_run_necromancer", "pure_run_amazon"],
            UnitContentIds = ["unit.pure-run.mage", "unit.pure-run.necromancer", "unit.pure-run.amazon"],
            StartingSkillContentIds = ["skill.mage.fireball.lv1", "skill.necromancer.summon-skeleton.lv1", "skill.amazon.thrust.lv1"]
        };
        PureRunDefinition definition = resource.ToCoreDefinition();
        PureRunPartyTemplate amazon = definition.Party.Single(value =>
            value.CharacterId == "pure_run_amazon");

        AssertThat(amazon.EffectiveStartingSkillChoices)
            .Contains(new ContentId("skill.poison-spear.lv1"));
        AssertThat(amazon.EffectiveStartingSkillChoices)
            .NotContains(new ContentId("skill.amazon.poison-spear.lv1"));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RunDefinitionV2SerializesAllFourCandidateAttributes()
    {
        var resource = ResourceLoader.Load<PureRunDefinitionResource>(
            "res://content/runs/PureRunThreeEncounterV1.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(resource).IsNotNull();
        if (resource is null) return;

        PureRunDefinition definition = resource.ToCoreDefinition();
        PureRunPartyTemplate demonbound = definition.Party.Single(value =>
            value.CharacterId == "pure_run_demonbound");

        AssertThat(resource.SchemaVersion).IsEqual(2);
        AssertThat(resource.Charismas.Length).IsEqual(4);
        AssertThat(demonbound.Attributes).IsEqual(new UnitAttributes(5, 5, 5, 5, 6, 5));
        AssertThat(demonbound.EffectiveInherentSkills)
            .Contains(new ContentId("skill.demonbound.meditation"));
    }

    [TestCase]
    public void CanonicalPoisonSpearResourceAdvancesThroughTheAmazonBranch()
    {
        SkillDefinition first = PoisonSpearSkillResource.CreateCoreDefinition(
            new ContentId("skill.poison-spear.lv1"), 5, 5, 9, 2);
        var second = new SkillDefinition(new ContentId("skill.amazon.poison-spear.lv2"),
            "amazon_poison_spear_lv2", SkillRole.Amazon, SkillKind.Active, 2, 6, 1, 6,
            SkillExecutionKind.PoisonSpear, 12, SkillDamageKind.Physical,
            new ContentId("buff.poison"), 3, branchId: "amazon.poison-spear");
        var amazon = new RunCharacterState("pure_run_amazon", new ContentId("unit.pure-run.amazon"), 1,
            new UnitAttributes(5, 5, 5, 5, 5, 5), 20, 20, 10, 10, false,
            [first.ContentId], learnedSkillStates: [new RunLearnedSkillState(first.BranchId, 1, first.ContentId)]);
        var skills = new Dictionary<ContentId, SkillDefinition>
        {
            [first.ContentId] = first,
            [second.ContentId] = second
        };

        IReadOnlyList<SkillDefinition> candidates = new RunInventoryProgressionService()
            .GrowthCandidates(amazon, skills);

        AssertThat(first.BranchId).IsEqual("amazon.poison-spear");
        AssertThat(candidates.Select(value => value.ContentId).ToArray()).Contains(second.ContentId);
        AssertThat(candidates.Select(value => value.ContentId).ToArray()).NotContains(first.ContentId);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RogueMapPlacesStartBelowBossAndKeepsLockedNodesReadOnly()
    {
        var nodes = new[]
        {
            new PureRunMapNodeSnapshot("start", 0, PureRunNodeKind.Battle, "Start", null, 0,
                PureRunMapNodeState.Completed),
            new PureRunMapNodeSnapshot("layer_01_battle", 1, PureRunNodeKind.Battle, "N1", null, 0,
                PureRunMapNodeState.Current),
            new PureRunMapNodeSnapshot("layer_07_battle", 7, PureRunNodeKind.Battle, "Boss", null, 0,
                PureRunMapNodeState.Locked, "map.node_locked")
        };
        var view = new GodotRogueMapView { Size = GodotRogueMapView.PreferredSize };
        view.SetSnapshot(new PureRunMapSnapshot(nodes,
            [new PureRunMapConnectionSnapshot("start", "layer_01_battle", true, true)],
            "layer_01_battle"), true);

        AssertThat(view.NodeCenter("start").Y).IsGreater(view.NodeCenter("layer_07_battle").Y);
        AssertThat(view.Snapshot!.FocusNodeId).IsEqual("layer_01_battle");
        AssertThat(view.Snapshot.Nodes.Single(value => value.NodeId == "layer_07_battle").UnavailableReason)
            .IsEqual("map.node_locked");
        view.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CompactMeterUsesActorWidthRangeWithoutThemeExpansion()
    {
        var actor = new GodotUnitActor { Position = new Vector2(200, 300) };
        var meter = new GodotCompactUnitMeter();
        meter.Bind(actor, 10, 20, 4, 8);

        AssertThat(meter.Size.X).IsBetween(38f, 48f);
        AssertThat(meter.Size.Y).IsEqual(GodotPlayableRunMain.UnitMeterSize.Y);
        AssertThat(meter.Visible).IsFalse();
        AssertThat(meter.GetChildCount()).IsEqual(0);
        meter.Free(); actor.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HealNumberUsesCommittedGreenPresentationAndHonorsPlaybackControls()
    {
        UnitInstanceId target = new("party-mage");
        var actor = new GodotUnitActor { Position = new Vector2(200, 300) };
        var layer = new GodotDamageNumberLayer();
        layer.Configure(new Dictionary<UnitInstanceId, GodotUnitActor> { [target] = actor });
        layer.SetSpeed(4f);
        layer.SetPaused(true);
        layer.Spawn(new BattlePresentationNumber(BattlePresentationNumberKind.Heal, target, "+3",
            PresentationMarkerKind.Impact, 0));

        Label label = layer.ActiveLabels.Single();
        AssertThat(label.Text).IsEqual("+3");
        AssertThat(label.Modulate.R).IsEqualApprox(.31f, .001f);
        AssertThat(label.Modulate.G).IsEqualApprox(1f, .001f);
        AssertThat(layer.PlaybackSpeed).IsEqual(4f);
        AssertThat(layer.IsPaused).IsTrue();
        layer.Clear();
        AssertThat(layer.ActiveCount).IsEqual(0);
        layer.Free(); actor.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartupLoadsCanonicalCatalogBeforeStoreRouting()
    {
        var ui = ReadyForStaticUiTest();
        string summary = ui.StartupContractSummary;
        AssertThat(summary).Contains("units=14");
        AssertThat(summary).Contains("skills=");
        AssertThat(summary).Contains("ai=9");
        AssertThat(summary).Contains("layouts=5");
        AssertThat(summary).Contains("encounters=9");
        AssertThat(summary).Contains("map=True");
        AssertThat(summary).Contains("treasure=True");
        AssertThat(summary).Contains("catalog=166");
        ui.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ProgressionFirstRendersIndependentAttributeChoices()
    {
        var ui = ReadyForStaticUiTest();
        var attributes = new UnitAttributes(5, 5, 5, 6, 5, 5);
        var mage = new RunCharacterState("pure_run_mage", new ContentId("unit.pure-run.mage"), 1, attributes,
            20, 20, 12, 12, false, new[] { new ContentId("skill.mage.fireball.lv1") },
            learnedSkillStates: new[] { new RunLearnedSkillState("mage.fireball", 1, new ContentId("skill.mage.fireball.lv1")) });
        var necromancer = new RunCharacterState("pure_run_necromancer", new ContentId("unit.pure-run.necromancer"), 1, attributes, 20, 20, 12, 12, false, Array.Empty<ContentId>());
        var amazon = new RunCharacterState("pure_run_amazon", new ContentId("unit.pure-run.amazon"), 1, attributes, 20, 20, 12, 12, false, Array.Empty<ContentId>());
        var pending = new PendingProgression("battle:n1:progression", "n1", mage.CharacterId);
        var run = new PureRunState("run-test", 7, 2, PureRunPhase.Ready, 1, new ContentId("encounter.pure-run.n2"),
            new[] { mage, necromancer, amazon }, pendingProgression: new[] { pending });
        MethodInfo? show = typeof(GodotPlayableRunMain).GetMethod("ShowProgression", BindingFlags.Instance | BindingFlags.NonPublic);
        show?.Invoke(ui, new object[] { run, pending });
        string[] buttonTexts = Descendants<Button>(ui).Select(button => button.Text).ToArray();
        AssertThat(buttonTexts.Count(text => text.StartsWith("+1 ", StringComparison.Ordinal))).IsEqual(6);
        AssertThat(buttonTexts.Any(text => text.Contains("mage.fireball", StringComparison.Ordinal))).IsFalse();
        AssertThat(buttonTexts.Any(text => text == "Back to Map")).IsFalse();
        PanelContainer progressionPanel = Descendants<PanelContainer>(ui).Single(panel => panel.Name == "ProgressionFlowPanel");
        AssertFormalPanel(progressionPanel);
        ui.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RunMapPageConsumesTheApplicationSnapshotWithoutStartingBattle()
    {
        var ui = ReadyForStaticUiTest();
        UnitAttributes attributes = new(5, 5, 5, 6, 5, 5);
        RunCharacterState Character(string id, string unit, string skill) => new(id, new ContentId(unit), 1,
            attributes, 20, 20, 10, 10, false, [new ContentId(skill)]);
        var run = new PureRunState("run-map-test", 7, 1, PureRunPhase.Ready, 0,
            new ContentId("encounter.pure-run.n1"),
        [
            Character("pure_run_mage", "unit.pure-run.mage", "skill.mage.fireball.lv1"),
            Character("pure_run_necromancer", "unit.pure-run.necromancer", "skill.necromancer.summon-skeleton.lv1"),
            Character("pure_run_amazon", "unit.pure-run.amazon", "skill.amazon.thrust.lv1")
        ]);

        typeof(GodotPlayableRunMain).GetMethod("ShowRunMap", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(ui, new object[] { run });
        GodotRogueMapView? map = Descendants<GodotRogueMapView>(ui).SingleOrDefault();

        AssertThat(map).IsNotNull();
        AssertThat(map!.Snapshot!.Nodes.Count).IsEqual(16);
        AssertThat(map.Snapshot.FocusNodeId).IsEqual("layer_01_battle");
        AssertFormalPanel(Descendants<PanelContainer>(ui).Single(panel => panel.Name == "MapDetailPanel"));
        ui.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ProgressionAfterAttributeAllocationRendersSkillChoices()
    {
        var ui = ReadyForStaticUiTest();
        var attributes = new UnitAttributes(5, 5, 5, 6, 5, 5);
        var proposed = new UnitAttributes(5, 5, 5, 7, 5, 5);
        var mage = new RunCharacterState("pure_run_mage", new ContentId("unit.pure-run.mage"), 1, attributes,
            20, 20, 12, 12, false, new[] { new ContentId("skill.mage.fireball.lv1") },
            learnedSkillStates: new[] { new RunLearnedSkillState("mage.fireball", 1, new ContentId("skill.mage.fireball.lv1")) });
        var others = new[]
        {
            new RunCharacterState("pure_run_necromancer", new ContentId("unit.pure-run.necromancer"), 1, attributes, 20, 20, 12, 12, false, Array.Empty<ContentId>()),
            new RunCharacterState("pure_run_amazon", new ContentId("unit.pure-run.amazon"), 1, attributes, 20, 20, 12, 12, false, Array.Empty<ContentId>())
        };
        var pending = new PendingProgression("battle:n1:progression", "n1", mage.CharacterId);
        var run = new PureRunState("run-test", 7, 2, PureRunPhase.Ready, 1, new ContentId("encounter.pure-run.n2"),
            new[] { mage }.Concat(others).ToArray(), pendingProgression: new[] { pending });
        typeof(GodotPlayableRunMain).GetMethod("BindRunUiScope", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(ui, new object[] { run.RunId });
        FieldInfo draftsField = typeof(GodotPlayableRunMain).GetField("_progressionDrafts",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var drafts = (Dictionary<(string RunId, string TransactionKey), UnitAttributes>)draftsField.GetValue(ui)!;
        drafts[(run.RunId, pending.TransactionKey)] = proposed;
        typeof(GodotPlayableRunMain).GetMethod("ShowProgression", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(ui, new object[] { run, pending });
        string[] buttonTexts = Descendants<Button>(ui).Select(button => button.Text).ToArray();
        string[] growthCards = buttonTexts.Where(text => text.StartsWith("Learn ", StringComparison.Ordinal) || text.StartsWith("Upgrade ", StringComparison.Ordinal)).ToArray();
        AssertThat(growthCards.Length).IsEqual(3);
        AssertThat(growthCards.Any(text => text.Contains("Upgrade 火球术 Lv1 → Lv2", StringComparison.Ordinal))).IsTrue();
        AssertThat(growthCards.Any(text => text.Contains("Learn Lv1 Lv1", StringComparison.Ordinal))).IsFalse();
        AssertThat(growthCards.Distinct(StringComparer.Ordinal).Count()).IsEqual(3);
        string labels = string.Join('\n', Descendants<Label>(ui).Select(label => label.Text));
        AssertThat(labels).Contains("Current skills:");
        AssertThat(labels).Contains("火球术");
        AssertThat(labels).Contains("MP 7");
        AssertThat(labels).Contains("施加2层点燃");
        AssertThat(buttonTexts.Any(text => text == "Back to Map")).IsFalse();
        ui.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RunUiScopeClearsProgressionAndInventoryDraftsAcrossRunIds()
    {
        var ui = ReadyForStaticUiTest();
        MethodInfo bind = typeof(GodotPlayableRunMain).GetMethod("BindRunUiScope",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo draftsField = typeof(GodotPlayableRunMain).GetField("_progressionDrafts",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var drafts = (Dictionary<(string RunId, string TransactionKey), UnitAttributes>)draftsField.GetValue(ui)!;
        bind.Invoke(ui, new object[] { "old-run" });
        drafts[("old-run", "shared-progression")] = new UnitAttributes(6, 5, 5, 5, 5, 5);
        typeof(GodotPlayableRunMain).GetField("_inventorySelectedInstanceId",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ui, "drop-1-0");

        bind.Invoke(ui, new object[] { "new-run" });

        AssertThat(drafts.Count).IsEqual(0);
        AssertThat((string?)typeof(GodotPlayableRunMain).GetField("_inventorySelectedInstanceId",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ui)).IsNull();
        ui.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartingSkillResourcesExposeCanonicalBranchAndUiMetadata()
    {
        SkillDefinitionResource fireball = ResourceLoader.Load<SkillDefinitionResource>("res://content/skills/MageFireballLv1.tres")!;
        SkillUiMetadata metadata = SkillUiMetadata.From(fireball);

        AssertThat(fireball.BranchId).IsEqual("mage.fireball");
        AssertThat(metadata.DisplayName).IsEqual("火球术");
        AssertThat(metadata.Level).IsEqual(1);
        AssertThat(metadata.Description).Contains("点燃");
        AssertThat(metadata.ManaCost).IsEqual(7);
        PoisonSpearSkillResource poison = ResourceLoader.Load<PoisonSpearSkillResource>("res://content/poison_spear/PoisonSpearSkillLv1.tres")!;
        SkillUiMetadata poisonMetadata = SkillUiMetadata.From(poison);
        AssertThat(poisonMetadata.RequiredAttribute).IsEqual("Agility");
        AssertThat(poisonMetadata.MinimumAttribute).IsEqual(5);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PointerResolverUsesOpaquePixelsAndCanonicalDrawOrder()
    {
        static GodotUnitActor Actor(Color topLeft, Color topRight, int z)
        {
            Image image = Image.CreateEmpty(2, 1, false, Image.Format.Rgba8);
            image.SetPixel(0, 0, topLeft);
            image.SetPixel(1, 0, topRight);
            return new GodotUnitActor
            {
                Body = new Sprite2D { Texture = ImageTexture.CreateFromImage(image), Centered = false },
                Shadow = new Sprite2D(),
                ZIndex = z
            };
        }

        var rearId = new UnitInstanceId("rear");
        var frontId = new UnitInstanceId("front");
        GodotUnitActor rear = Actor(Colors.White, Colors.White, 10);
        GodotUnitActor front = Actor(new Color(1, 1, 1, 0), Colors.White, 20);
        var actors = new Dictionary<UnitInstanceId, GodotUnitActor> { [rearId] = rear, [frontId] = front };

        AssertThat(GodotUnitPointerResolver.Resolve(actors, new Vector2(.25f, .5f))).IsEqual(rearId);
        AssertThat(GodotUnitPointerResolver.Resolve(actors, new Vector2(1.25f, .5f))).IsEqual(frontId);
        rear.Free(); front.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AmazonActorSwitchesHeldAndUnarmedTexturesWithoutChangingFacing()
    {
        UnitDefinitionResource definition = ResourceLoader.Load<UnitDefinitionResource>("res://content/units/PureRunAmazon.tres")!;
        GodotUnitActor actor = GodotUnitFactory.InstantiateActor(definition);
        actor.SetFacing(GodotUnitFacing.East);
        actor.SetSpearHeld(false);
        AssertThat(actor.IsSpearHeld).IsFalse();
        AssertThat(actor.Body!.Texture!.ResourcePath).Contains("doge_hunter_idle_unarmed_ul.png");
        AssertThat(actor.PresentationFacing).IsEqual(GodotUnitFacing.East);
        actor.SetSpearHeld(true);
        AssertThat(actor.Body.Texture!.ResourcePath).Contains("doge_hunter_ul.png");
        actor.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PlayerActionPosesUseFacingPairsAndMissingProfilesFallBackToIdle()
    {
        UnitDefinitionResource mage = ResourceLoader.Load<UnitDefinitionResource>("res://content/units/PureRunMage.tres")!;
        UnitDefinitionResource amazon = ResourceLoader.Load<UnitDefinitionResource>("res://content/units/PureRunAmazon.tres")!;
        UnitDefinitionResource skeleton = ResourceLoader.Load<UnitDefinitionResource>("res://content/units/PureRunSkeletonWarrior.tres")!;
        GodotUnitActor mageActor = GodotUnitFactory.InstantiateActor(mage);
        GodotUnitActor amazonActor = GodotUnitFactory.InstantiateActor(amazon);
        GodotUnitActor skeletonActor = GodotUnitFactory.InstantiateActor(skeleton);

        mageActor.SetFacing(GodotUnitFacing.East);
        mageActor.SetActionPose(GodotUnitActionPose.Cast);
        AssertThat(mageActor.Body!.Texture!.ResourcePath).Contains("doge_mage_cast_ul.png");
        AssertThat(mageActor.Body.FlipH).IsTrue();
        mageActor.SetActionPose(GodotUnitActionPose.Hit);
        AssertThat(mageActor.Body.Texture!.ResourcePath).Contains("doge_mage_hit_ul.png");

        amazonActor.SetFacing(GodotUnitFacing.South);
        amazonActor.SetActionPose(GodotUnitActionPose.Ranged);
        AssertThat(amazonActor.Body!.Texture!.ResourcePath).Contains("doge_hunter_melee_attack_dr.png");
        amazonActor.SetActionPose(null);
        AssertThat(amazonActor.Body.Texture).IsEqual(amazon.DownRightTexture);
        amazonActor.SetSpearHeld(false);
        amazonActor.SetActionPose(GodotUnitActionPose.Melee);
        AssertThat(amazonActor.Body.Texture).IsEqual(amazon.UnarmedDownRightTexture);
        AssertThat(amazonActor.IsSpearHeld).IsFalse();

        skeletonActor.SetActionPose(GodotUnitActionPose.Hit);
        AssertThat(skeletonActor.Body!.Texture).IsEqual(skeleton.DownRightTexture);
        mageActor.Free(); amazonActor.Free(); skeletonActor.Free();
    }

    [TestCase]
    public void LightningStartsAboveBoardAndEndsAtTargetHead()
    {
        Vector2 head = new(720, 340);
        Vector2 start = GodotBattlePresentationPlayer.VerticalLightningStart(head, 110);
        AssertThat(start.X).IsEqual(head.X);
        AssertThat(start.Y).IsEqual(78f);
        AssertThat(start).IsNotEqual(new Vector2(400, 500));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DroppedSpearLayerPersistsCommittedCellsAndClearsAfterRecovery()
    {
        var layer = new GodotDroppedSpearLayer();
        UnitInstanceId owner = new("party.amazon.0");
        GridPoint cell = new(4, 3);

        layer.Sync(new Dictionary<UnitInstanceId, GridPoint> { [owner] = cell });
        AssertThat(layer.MarkerCount).IsEqual(1);
        AssertThat(layer.MarkerPositions[owner]).IsEqual(IsometricBattleBoardLayout.GridToScreen(cell));

        layer.Sync(new Dictionary<UnitInstanceId, GridPoint>());
        AssertThat(layer.MarkerCount).IsEqual(0);
        layer.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void InventoryRendersSingleBackpackAndActionableLoadoutColumns()
    {
        var ui = ReadyForStaticUiTest();
        UnitAttributes attributes = new(5, 5, 5, 6, 5, 5);
        RunCharacterState Character(string id, string unit) => new(id, new ContentId(unit), 1, attributes,
            20, 20, 5, 15, false, Array.Empty<ContentId>());
        var equipment = new RunEquipmentState(new ItemInstanceId("qa-sword"),
            new ContentId("item.equipment.sword-01"), EquipmentSlot.Weapon);
        var run = new PureRunState("run-inventory", 7, 1, PureRunPhase.Ready, 0,
            new ContentId("encounter.pure-run.n1"),
        [
            Character("pure_run_mage", "unit.pure-run.mage"),
            Character("pure_run_necromancer", "unit.pure-run.necromancer"),
            Character("pure_run_amazon", "unit.pure-run.amazon")
        ], backpackEquipment: [equipment]);

        typeof(GodotPlayableRunMain).GetMethod("ShowInventory", BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(PureRunState)], null)?.Invoke(ui, [run]);
        string[] buttons = Descendants<Button>(ui).Select(value => value.Text).ToArray();

        AssertThat(buttons.Count(value => value.Contains("qa-sword", StringComparison.Ordinal))).IsEqual(1);
        AssertThat(buttons).Contains("[ Equipment ]");
        AssertThat(buttons).Contains("Consumables");
        AssertThat(buttons).Contains("Back");
        PanelContainer[] inventoryCards = Descendants<PanelContainer>(ui)
            .Where(panel => panel.ThemeTypeVariation == GodotTacticsTheme.Card && panel.Size.Y == 640)
            .ToArray();
        AssertThat(inventoryCards.Length).IsEqual(3);
        AssertThat(inventoryCards.All(panel => panel.MouseFilter == Control.MouseFilterEnum.Ignore)).IsTrue();
        AssertThat(inventoryCards.Select(panel => panel.Name.ToString()).OrderBy(value => value).ToArray())
            .ContainsExactly("InventoryBackpackPanel", "InventoryCharacterPanel", "InventoryDetailPanel");
        foreach (PanelContainer panel in inventoryCards) AssertFormalPanel(panel);
        ui.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void InventoryEntryExistsOnlyOnRogueMap()
    {
        var ui = ReadyForStaticUiTest();
        string[] homeButtons = Descendants<Button>(ui).Select(value => value.Text).ToArray();
        AssertThat(homeButtons).NotContains("Inventory");

        MethodInfo routeMap = typeof(GodotPlayableRunMain).GetMethod("ShowRunMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        routeMap.Invoke(ui, [InventoryRun()]);
        string[] mapButtons = Descendants<Button>(ui).Select(value => value.Text).ToArray();
        AssertThat(mapButtons.Count(value => value == "Inventory")).IsEqual(1);
        ui.Free();
    }

    private static PureRunState InventoryRun()
    {
        RunCharacterState Character(string id, string unit) => new(id, new ContentId(unit), 1,
            new UnitAttributes(5, 5, 5, 5, 5, 5), 20, 20, 5, 10, false, Array.Empty<ContentId>());
        return new PureRunState("run-inventory-map", 7, 1, PureRunPhase.Ready, 0,
            new ContentId("encounter.pure-run.n1"),
            [Character("pure_run_mage", "unit.pure-run.mage"), Character("pure_run_necromancer", "unit.pure-run.necromancer"), Character("pure_run_amazon", "unit.pure-run.amazon")]);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SummonAiResourcesBindInternalSkillsAndStayOutOfGrowthUi()
    {
        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/ContentCatalog.tres", string.Empty, ResourceLoader.CacheMode.Ignore)!;
        Dictionary<string, GodotResourceEntry> entries = catalog.Entries.ToDictionary(value => value.ContentIdValue);

        AssertThat(entries.Count).IsEqual(166);
        foreach (string id in new[]
        {
            "skill.summon.skeleton-attack.lv1", "skill.summon.skeleton-attack.lv2",
            "skill.summon.skeleton-mage-fireball.lv1", "skill.summon.skeleton-mage-fireball.lv2"
        })
        {
            SkillDefinitionResource skill = ResourceLoader.Load<SkillDefinitionResource>(entries[id].DiagnosticPathValue,
                string.Empty, ResourceLoader.CacheMode.Ignore)!;
            AssertThat(skill.GrowthVisible).IsFalse();
            AssertThat(skill.IsBasicAbility).IsTrue();
        }

        AiDefinition skeleton = ResourceLoader.Load<AiDefinitionResource>(entries["ai.summon.basic-melee"].DiagnosticPathValue,
            string.Empty, ResourceLoader.CacheMode.Ignore)!.ToCoreDefinition();
        AiDefinition caster = ResourceLoader.Load<AiDefinitionResource>(entries["ai.summon.fire-demon"].DiagnosticPathValue,
            string.Empty, ResourceLoader.CacheMode.Ignore)!.ToCoreDefinition();
        AssertThat(skeleton.SkillIds).Contains(new ContentId("skill.summon.skeleton-attack.lv1"));
        AssertThat(caster.PreferredMinimumRange).IsEqual(2);
        AssertThat(caster.PreferredMaximumRange).IsEqual(3);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void OwnershipClosureCatalogContainsTenValidatedLevelThreeSkills()
    {
        GodotResourceCatalog batch = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/skills/OwnershipClosureCatalog.tres", string.Empty, ResourceLoader.CacheMode.Ignore)!;
        AssertThat(batch.Entries.Length).IsEqual(10);
        foreach (GodotResourceEntry entry in batch.Entries)
        {
            SkillDefinitionResource resource = ResourceLoader.Load<SkillDefinitionResource>(entry.DiagnosticPathValue,
                string.Empty, ResourceLoader.CacheMode.Ignore)!;
            SkillDefinition definition = resource.ToCoreDefinition();
            AssertThat(definition.Level).IsEqual(3);
            AssertThat(definition.ContentId.Value).IsEqual(entry.ContentIdValue);
        }

        SkillDefinition fireball = ResourceLoader.Load<SkillDefinitionResource>(
            "res://content/skills/MageFireballLv3.tres", string.Empty, ResourceLoader.CacheMode.Ignore)!.ToCoreDefinition();
        SkillDefinition skeleton = ResourceLoader.Load<SkillDefinitionResource>(
            "res://content/skills/NecromancerSummonSkeletonLv3.tres", string.Empty, ResourceLoader.CacheMode.Ignore)!.ToCoreDefinition();
        AssertThat(fireball.ExecutionProfile.DetonateStatusContentId!.Value.Value).IsEqual("buff.ignite");
        AssertThat(skeleton.ExecutionProfile.SummonAttackContentId!.Value.Value).IsEqual("skill.summon.skeleton-attack.lv3");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AuthoritativeMapAndTreasureResourcesCompile()
    {
        var map = ResourceLoader.Load<PureRunMapResource>("res://content/map/PureRunDefaultMap.tres");
        var treasure = ResourceLoader.Load<PureRunTreasureResource>("res://content/map/PureRunStandardTreasure.tres");
        AssertThat(map).IsNotNull();
        AssertThat(treasure).IsNotNull();
        if (map is null || treasure is null) return;
        PureRunMapDefinition definition = map.ToCoreDefinition();
        PureRunTreasureDefinition reward = treasure.ToCoreDefinition();
        PureRunMapWorkbench.ValidateResource(map);
        EncounterFixtureWorkbench.ValidateCanonicalAssets();
        AssertThat(definition.Nodes.Count).IsEqual(16);
        AssertThat(definition.Connections.Count).IsEqual(23);
        AssertThat(definition.Nodes.Count(value => value.Kind == PureRunNodeKind.Treasure)).IsEqual(2);
        AssertThat(reward.GoldMinimum).IsEqual(2);
        AssertThat(reward.GoldMaximum).IsEqual(5);
        AssertThat(reward.Equipment.Count).IsEqual(1);
        AssertThat(reward.Buffs.Count).IsEqual(1);
    }

    [TestCase]
    public void AudioFrameworkValidatesIndependentSettings()
    {
        GodotAudioSettingsV1 settings = GodotAudioSettingsStore.Validate(new GodotAudioSettingsV1(1f, 0.6f, 0.7f, 0.8f, false));
        AssertThat(settings.Music).IsEqual(0.6f);
        AssertThat(settings.Sfx).IsEqual(0.7f);
        AssertThat(settings.Ui).IsEqual(0.8f);
    }

    [TestCase]
    public void SettlementReportsOnlyTheLatestBattleDrop()
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        RunCharacterState Character(string id) => new(id, new ContentId($"unit.{id}"), 1, attributes,
            20, 20, 5, 10, false, Array.Empty<ContentId>());
        var prior = new BattleConsumableState(new ItemInstanceId("drop-1-item.consumable.life-potion"),
            new ContentId("item.consumable.life-potion"), 1, 1);
        var run = new PureRunState("run", 7, 2, PureRunPhase.Ready, 1, new ContentId("encounter.pure-run.n2"),
            [Character("a"), Character("b"), Character("c")], backpackConsumables: [prior], battlesCompleted: 2);

        AssertThat(GodotPlayableRunMain.SettlementDropLabel(run)).IsEqual("No item drop");
    }

    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (T nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void AssertFormalPanel(PanelContainer panel)
    {
        AssertThat((int)panel.MouseFilter).IsEqual((int)Control.MouseFilterEnum.Ignore);
        AssertThat(panel.Position.X).IsGreaterEqual(0);
        AssertThat(panel.Position.Y).IsGreaterEqual(0);
        AssertThat(panel.Position.X + panel.Size.X).IsLessEqual(1600);
        AssertThat(panel.Position.Y + panel.Size.Y).IsLessEqual(900);
    }

    private static GodotPlayableRunMain ReadyForStaticUiTest()
    {
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(new FailingRunStore(), 37, "static-ui"));
        ui._Ready();
        return ui;
    }

    private static InputEventKey EscapeKeyEvent() => new()
    {
        Keycode = Key.Escape,
        PhysicalKeycode = Key.Escape,
        Pressed = true,
        Echo = false
    };

    private static async Task ClickPoint(Control control, Vector2 localPoint, SceneTree tree)
    {
        Vector2 point = control.GetGlobalTransformWithCanvas() * localPoint;
        Viewport viewport = control.GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, false);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point,
            ButtonIndex = MouseButton.Left, Pressed = true }, false);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point,
            ButtonIndex = MouseButton.Left, Pressed = false }, false);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static async Task ClickControl(Control control, SceneTree tree)
    {
        Vector2 point = control.GetGlobalTransformWithCanvas() * (control.Size * .5f);
        Viewport viewport = control.GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, false);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            Pressed = true
        }, false);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            Pressed = false
        }, false);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static async Task ToggleEscape(GodotPlayableRunMain ui, SceneTree tree)
    {
        ui.GetViewport().PushInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = true
        }, false);
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        ui.GetViewport().PushInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = false
        }, false);
        await ui.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private sealed class FailingRunStore : IRunSaveStore
    {
        public RunStoreResult Load() => new(false, "qa.static_store", null);
        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision) =>
            new(false, "qa.static_store", null);
    }

    private sealed class ProbeRunStore : IRunSaveStore
    {
        private PureRunSaveSnapshot? _snapshot;
        public int SaveCount { get; private set; }
        public RunStoreResult Load() => new(true, null, _snapshot);
        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
        {
            long actual = _snapshot?.Revision ?? 0;
            if (actual != expectedRevision) return new(false, "save.revision_conflict", _snapshot);
            _snapshot = snapshot;
            SaveCount++;
            return new(true, null, _snapshot);
        }
    }

    private sealed class MemoryClipboard : ITextClipboard
    {
        public string Text { get; private set; } = string.Empty;
        public void SetText(string text) => Text = text;
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ReplacingAPageDoesNotRetainDisposedUnitMeters()
    {
        var ui = ReadyForStaticUiTest();
        FieldInfo? metersField = typeof(GodotPlayableRunMain).GetField("_unitMeters", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? newPage = typeof(GodotPlayableRunMain).GetMethod("NewPage", BindingFlags.Instance | BindingFlags.NonPublic);
        var meters = (Dictionary<UnitInstanceId, Control>?)metersField?.GetValue(ui);
        var disposedMeter = new Control();
        ui.AddChild(disposedMeter);
        meters?.Add(new UnitInstanceId("test.disposed-meter"), disposedMeter);
        disposedMeter.Free();

        newPage?.Invoke(ui, new object[] { "PAGE REPLACEMENT TEST", "Disposed references must be forgotten", false });

        AssertThat(meters).IsNotNull();
        AssertThat(meters?.Count ?? -1).IsEqual(0);
        typeof(GodotPlayableRunMain).GetMethod("SetStatus", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(ui, new object[] { "current-page-error" });
        Control currentPage = (Control)typeof(GodotPlayableRunMain).GetField("_page",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ui)!;
        AssertThat(currentPage.GetNode<Label>("PageFallbackStatus").Text).IsEqual("current-page-error");

        ui.Free();
    }
}
