using Godot;
using Tactics.Application.Battle;
using Tactics.Application.Runs;
using Tactics.Application.Presentation;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Skills;
using Tactics.Core.Units;
using System.Text.Json;
using System.Security.Cryptography;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Native 1600x900 auto-continue Pure Run flow.</summary>
public partial class GodotPlayableRunMain : Control
{
    public const int CanvasWidth = 1600;
    public const int CanvasHeight = 900;
    public const int PauseOverlayZIndex = 4000;
    public const int StartPageUiZIndex = 1200;
    internal static readonly Rect2 AdventureHeaderRect = new(24, 20, 550, 122);
    internal static readonly Rect2 AdventureRunShellRect = new(1180, 24, 380, 104);
    internal static readonly Rect2 AdventureLeaderRect = new(32, 820, 440, 52);
    internal static readonly Rect2 AdventureInventoryRect = new(1210, 816, 350, 56);
    public const string StartCurrentControlsHint = "[LMB] select/move   [M] route overview   [F/Home] leader";
    public const string StartOverviewControlsHint = "[LMB] inspect node   [RMB/WASD] browse route   [M] return to camp";
    // Only panels that own their content. Empty framing cards around turn/actions/end-turn were removed.
    internal static IReadOnlyDictionary<string, Rect2> BattleHudPanelRects { get; } =
        new Dictionary<string, Rect2>(StringComparer.Ordinal)
        {
            ["BattleUnitPanel"] = new(new Vector2(18, 18), new Vector2(430, 190)),
        };
    public static readonly Vector2 UnitMeterSize = new(44, 18);
    public const int UnitMeterBarHeight = 7;
    private readonly Dictionary<ContentId, UnitDefinition> _units = new();
    private readonly Dictionary<ContentId, UnitDefinitionResource> _unitResources = new();
    private readonly Dictionary<ContentId, SkillDefinition> _skills = new();
    private readonly Dictionary<ContentId, SkillUiMetadata> _skillUi = new();
    private readonly Dictionary<ContentId, AiDefinition> _ai = new();
    private readonly Dictionary<ContentId, BattleLayoutDefinition> _layouts = new();
    private readonly Dictionary<ContentId, EncounterDefinition> _encounters = new();
    private readonly Dictionary<ContentId, EquipmentDefinition> _equipment = new();
    private readonly Dictionary<ContentId, ConsumableDefinition> _consumables = new();
    private readonly Dictionary<string, string> _layerFourEventPayloads = new(StringComparer.Ordinal);
    private PlayableBattleBalanceProfile? _balance;
    private PlayableEnemySpeedProfile? _enemySpeed;
    private PureRunDefinition? _runDefinition;
    private PureRunMapDefinition? _mapDefinition;
    private PureRunTreasureDefinition? _treasureDefinition;
    private readonly Dictionary<UnitInstanceId, GodotUnitActor> _actors = new();
    private readonly Dictionary<UnitInstanceId, Control> _unitMeters = new();
    private readonly List<BattleUiLogEntry> _logs = new();
    private PureRunSessionService? _run;
    private PlayableBattleSessionService? _battle;
    private Control? _page;
    private Label? _status;
    private Container? _skillPanel;
    private GodotIsometricBattleBoard? _board;
    private RichTextLabel? _eventLog;
    private GodotBattleCheatConsole? _cheatConsole;
    private GodotBattleActiveUnitPanel? _activeUnitPanel;
    private Label? _cellDebug;
    private Label? _turnOrder;
    private Button? _speedButton;
    private Button? _stepButton;
    private Button? _endTurnButton;
    private bool _playbackPaused;
    private float _playbackSpeed = 1f;
    private int _logFilter;
    private BattleUiSnapshot? _visibleSnapshot;
    private GridPoint? _hoveredCell;
    private (UnitInstanceId UnitId, GodotUnitFacing Facing)? _targetingFacingPreview;
    private ContentId? _currentEncounterId;
    private readonly GodotBattleSettlementCoordinator _settlement = new();
    private Label? _settlementStatus;
    private PureRunBattleResult? _terminalSettlementResult;
    private int _settlementRetryCount;
    private int _settlementNavigationRetryCount;
    private GodotBattlePresentationPlayer? _presentationPlayer;
    private BattleUiSnapshot? _presentationAfter;
    private bool _continueAutomaticAfterPresentation;
    private bool _pauseAfterCurrentFrame;
    private bool _presentationInputLocked;
    private StandardUnitPresentationResource? _presentationProfile;
    private StatusPresentationResource? _statusPresentationProfile;
    private readonly List<SkillPresentationResource> _skillPresentationProfiles=new();
    private readonly PureRunFlowProjector _flowProjector = new();
    private GodotRogueMapView? _mapView;
    private GodotAdventureBoardView? _adventureBoard;
    private AdventureExplorationSession? _adventureSession;
    private PendingAdventureExit? _pendingAdventureExit;
    private GodotStartCampView? _startCampView;
    private readonly List<string> _partySelectionOrder = new();
    private string? _adventureLastBoardContentId;
    private Label? _mapDetail;
    private int _catalogCount;
    private string? _inventoryCharacterId;
    private bool _inventoryEquipmentTab = true;
    private string? _inventorySelectedInstanceId;
    private string? _uiRunId;
    private readonly Dictionary<(string RunId, string TransactionKey), UnitAttributes> _progressionDrafts = new();
    private GodotDamageNumberLayer? _damageNumbers;
    private readonly List<BattlePresentationNumber> _presentationNumberHistory = new();
    private GodotDroppedSpearLayer? _droppedSpears;
    private Control? _pauseMenu;
    private VBoxContainer? _pauseMenuContent;
    private Button? _pauseMenuDefaultFocus;
    private ConfirmationDialog? _pauseConfirmation;
    private PauseMenuView _pauseMenuView = PauseMenuView.Root;
    private bool _pauseMenuPausedPlayback;
    private readonly List<(string Tag, Action Dismiss)> _escapeLayers = new();
    private RunInputContext _inputContext = RunInputContext.Disabled;
    private GodotPlayableRunTestContext? _testContext;
    private IRunSaveStore? _saveStore;
    private bool _readyEntered;
    private bool _quitRequested;
    private string _currentPageTitle = string.Empty;

    private sealed record PendingAdventureExit(string RunId, string TargetNodeId, GridPoint ExitCell, Control Page);
    private enum InventoryReturnTarget { RunRoute }
    private enum PauseMenuView { Root, Options }
    internal enum PresentationDrainAction { DequeueFrame, CompleteBattle, Pause, Refresh }
    internal sealed record RunInputContext(bool CanPause, bool ControlsBattlePlayback,
        bool PausesAdventureMovement, bool SetupDraftActive)
    {
        public static readonly RunInputContext Disabled = new(false, false, false, false);
        public static readonly RunInputContext Setup = new(true, false, true, true);
        public static readonly RunInputContext Adventure = new(true, false, true, false);
        public static readonly RunInputContext Battle = new(true, true, false, false);
    }

    public bool IsReadyForInput => !_actPreparationPending && _run is not null && _page is not null && _units.Count == 14 &&
        _skills.Count >= 22 && _ai.Count == 9 && _layouts.Count >= 2 && _encounters.Count >= 3 &&
        _mapDefinition is not null && _treasureDefinition is not null && _catalogCount == 166;
    public string StartupContractSummary =>
        $"run={_run is not null}, page={_page is not null}, units={_units.Count}, skills={_skills.Count}, " +
        $"ai={_ai.Count}, layouts={_layouts.Count}, encounters={_encounters.Count}, " +
        $"map={_mapDefinition is not null}, treasure={_treasureDefinition is not null}, catalog={_catalogCount}";

    public override void _Ready()
    {
        _readyEntered = true;
        Theme = GodotTacticsTheme.Create();
        _saveStore = _testContext?.SaveStore ?? new GodotRunSaveStore();
        _playbackSpeed = _testContext?.InitialPlaybackSpeed ?? 1f;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        LoadCatalogs();
        RouteStartup();
    }

    public void ConfigureTestContext(GodotPlayableRunTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_readyEntered || IsInsideTree()) throw new InvalidOperationException("Test context must be configured before the Main scene enters the tree.");
        if (!GodotBattlePresentationPlayer.IsSupportedSpeed(context.InitialPlaybackSpeed))
            throw new ArgumentOutOfRangeException(nameof(context), "Unsupported initial playback speed.");
        _testContext = context;
    }

    public GodotPlayableRunProbe CaptureTestProbe() => new(
        _currentPageTitle,
        SaveStore.Load().Snapshot,
        _battle?.CaptureSnapshot(),
        _battle is not null,
        _presentationInputLocked,
        _presentationPlayer?.IsPlaying == true,
        _battle?.HasPendingAutomaticFrames == true,
        _damageNumbers?.ActiveCount ?? 0,
        _presentationNumberHistory.ToArray(),
        _playbackPaused,
        _playbackSpeed,
        _pauseMenu?.Visible == true,
        _pauseMenuView.ToString(),
        _cheatConsole?.Visible == true,
        _logs.Count(value => value.Category == BattleUiLogCategory.Rejected) + (_settlement.Current?.Stage == BattleSettlementStage.Rejected ? 1 : 0),
        _quitRequested,
        _status is not null && GodotObject.IsInstanceValid(_status) ? _status.Text : null,
        CaptureAdventureProbe(),
        _inputContext == RunInputContext.Setup ? "Setup" : _inputContext == RunInputContext.Battle ? "Battle" :
            _inputContext == RunInputContext.Adventure ? "Adventure" : "Disabled",
        _run?.HasSetupDraft == true,
        _run?.CurrentSetupProjection?.PendingRunSetup?.SelectedCharacterIds.Count ?? 0,
        _run?.CurrentSetupProjection?.PendingRunSetup?.SelectedCharacterIds.ToArray() ?? []);

    private GodotAdventureRuntimeProbe? CaptureAdventureProbe()
    {
        PureRunSaveSnapshot? snapshot = SaveStore.Load().Snapshot;
        PureRunSaveSnapshot? setupProjection = _run?.CurrentSetupProjection;
        PureRunState? run = snapshot?.ActiveRun;
        RunAdventureState? adventure = run?.AdventureState;
        if (_run?.HasSetupDraft == true)
        {
            if (setupProjection?.PendingRunSetup is null || _startCampView is null || !GodotObject.IsInstanceValid(_startCampView)) return null;
            return new GodotAdventureRuntimeProbe(_startCampView.MapInstance.Surface.Template!.BoardContentIdValue, null,
                _startCampView.CandidateCells.ToDictionary(value => value.Key, value => $"{value.Value.X},{value.Value.Y}", StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["start-exit"] = _partySelectionOrder.Count == _runDefinition?.ActivePartySize ? "Ready" : "Locked"
                }, [], "PendingSetup", null, null, null, null,
                0, 0, 0, _partySelectionOrder.Count, 0, 0, _startCampView.LastInputDiagnostic,
                _startCampView.IsMovementLocked, _startCampView.IsMovementLocked, _startCampView.LastMovementDiagnostic);
        }
        if (adventure is null || run is null) return null;
        string[] candidates = _mapDefinition is null ? [] : RunAdventureTransitionService
            .ImmediateSuccessors(run!, _mapDefinition).Select(value => value.NodeId).ToArray();
        return new GodotAdventureRuntimeProbe(adventure.BoardContentId.Value, adventure.LeaderId,
            EnsureAdventureSession(run!).ActorCells.ToDictionary(value => value.ActorId, value => $"{value.Cell.X},{value.Cell.Y}", StringComparer.Ordinal),
            DeriveAdventureObjectStates(run!), candidates, adventure.Lifecycle.ToString(),
            DeriveAdventureEventResolution(run!), adventure.PendingEventContext.ToString(),
            run?.EscortState?.Lifecycle.ToString(), run?.EscortState?.ProtectedNpcAlive,
            run?.MapState?.StoreOffers?.Count ?? 0, run?.MapState?.StoreOffers?.Count(value => value.Purchased) ?? 0,
            checked((int)adventure.LeaderRevision),
            checked((int)adventure.InteractionRevision), checked((int)adventure.ExitRevision), checked((int)adventure.SceneRevision), null,
            _adventureBoard?.IsMovementLocked == true, _adventureBoard?.IsMovementLocked == true,
            _adventureBoard?.LastMovementDiagnostic,
            _adventureBoard?.LastPointerCell is GridPoint pointerCell ? $"{pointerCell.X},{pointerCell.Y}" : null);
    }

    private IReadOnlyDictionary<string, string> DeriveAdventureObjectStates(PureRunState run)
    {
        var states = new Dictionary<string, string>(StringComparer.Ordinal);
        RunAdventureState adventure = run.AdventureState!;
        if (adventure.Lifecycle == RunAdventureLifecycle.InitialExploration) states["exit:layer_01_battle"] = "Ready";
        foreach (PureRunMapNodeDefinition successor in RunAdventureTransitionService.ImmediateSuccessors(run, MapDefinition))
            states[$"exit:{successor.NodeId}"] = RunAdventureTransitionService.IsExitUnlocked(run) ? "Ready" : "Locked";
        string board = adventure.BoardContentId.Value;
        bool committed = run.NodeTransaction?.Committed == true;
        if (board.Contains(".rest", StringComparison.Ordinal)) states["rest-campfire"] = committed ? "Interacted" : "Ready";
        if (board.Contains(".store", StringComparison.Ordinal)) states["store-merchant"] = committed ? "Interacted" : "Ready";
        if (board.Contains(".treasure", StringComparison.Ordinal)) states["treasure-chest"] = committed ? "Interacted" : "Ready";
        if (adventure.PendingEventObjectId is { } pending) states[pending] = "Awakened";
        if (run.NodeTransaction is { } transaction) states[transaction.NodeId] = transaction.Committed ? "Committed" : "Pending";
        if (adventure.PendingEventContext == RunAdventureEventContextKind.None && run.NodeTransaction?.Committed == true)
        {
            if (board.Contains("cursedchestmimic", StringComparison.OrdinalIgnoreCase)) states["cursed-chest"] = "Defeated";
            if (board.Contains("fallenaltarguardian", StringComparison.OrdinalIgnoreCase)) states["fallen-altar"] = "Purified";
            if (board.Contains("lostvillagerescort", StringComparison.OrdinalIgnoreCase))
                states["lost-villager"] = run.EscortState?.ProtectedNpcAlive == true ? "Safe" : "Down";
        }
        return states;
    }

    private static string? DeriveAdventureEventResolution(PureRunState run)
    {
        string board = run.AdventureState?.BoardContentId.Value ?? string.Empty;
        if (!board.EndsWith(".resolved", StringComparison.Ordinal)) return null;
        if (board.Contains("cursedchestmimic", StringComparison.OrdinalIgnoreCase)) return "CursedChestMimicDefeated";
        if (board.Contains("fallenaltarguardian", StringComparison.OrdinalIgnoreCase)) return "FallenAltarGuardianDefeated";
        if (board.Contains("lostvillagerescort", StringComparison.OrdinalIgnoreCase)) return "EscortCompleted";
        return null;
    }

    public IReadOnlyList<GodotBattleUnitProjection> CaptureBattleUnitProjections() => _battle is null
        ? Array.Empty<GodotBattleUnitProjection>()
        : _battle.State.Units.Values.Where(unit => unit.Unit.PlayerNumber == 0)
            .Select(unit => new GodotBattleUnitProjection(unit.Unit.InstanceId, unit.MaxHealth, unit.MaxMana,
                unit.BaseSpeed, unit.PhysicalAttack, unit.MagicalAttack, unit.Unit.MoveRange, unit.Unit.Initiative,
                unit.ManaRecoveryPerTurn)).ToArray();

    public bool ValidateInventoryProjectionEnteredBattle()
    {
        IReadOnlyList<GodotInventoryBattleProjectionEvidence> evidence = CaptureInventoryBattleProjectionEvidence();
        return evidence.Count > 0 && evidence.All(value => value.Matches) && evidence.Any(value =>
            value.EquipmentCount > 0 && (value.ProjectedMaxHealth != value.BaseMaxHealth ||
                                         value.ProjectedMaxMana != value.BaseMaxMana));
    }

    public IReadOnlyList<BattleUiLogEntry> CaptureRejectedBattleLogEntries() =>
        _logs.Where(value => value.Category == BattleUiLogCategory.Rejected).ToArray();

    public BattleUiSnapshot? CaptureVisibleBattleSnapshot() => _visibleSnapshot;

    public IReadOnlyList<GodotInventoryBattleProjectionEvidence> CaptureInventoryBattleProjectionEvidence()
    {
        PureRunState? run = SaveStore.Load().Snapshot?.ActiveRun;
        if (run is null || _battle is null) return Array.Empty<GodotInventoryBattleProjectionEvidence>();
        var evidence = new List<GodotInventoryBattleProjectionEvidence>();
        Dictionary<string, GodotBattleUnitProjection> actual = CaptureBattleUnitProjections()
            .ToDictionary(value => value.UnitId.Value, StringComparer.Ordinal);
        foreach (RunCharacterState character in run.Party)
        {
            UnitDefinition unit = _units[character.UnitContentId];
            EquipmentStatProjection projection = EquipmentStatProjector.Project(character.Attributes, unit.Speed,
                character.Equipment.Select(item => _equipment[item.DefinitionId]));
            UnitDerivedStats baseline = UnitDerivedStatRules.Calculate(character.Attributes, unit.Speed);
            (int physical, int magical) = _balance?.Attacks(character.UnitContentId) ?? (2, 2);
            bool found = actual.TryGetValue("party-" + character.CharacterId, out GodotBattleUnitProjection? battle);
            bool matches = found && battle!.MaxHealth == projection.DerivedStats.MaxHealth &&
                battle.MaxMana == projection.DerivedStats.MaxMana &&
                battle.MoveRange == projection.DerivedStats.MoveRange &&
                Math.Abs(battle.Initiative - projection.DerivedStats.Initiative) <= .001f &&
                battle.ManaRecoveryPerTurn == projection.Attributes.Intelligence &&
                battle.PhysicalAttack == physical && battle.MagicalAttack == magical;
            evidence.Add(new GodotInventoryBattleProjectionEvidence(character.CharacterId, character.Equipment.Count,
                baseline.MaxHealth, projection.DerivedStats.MaxHealth, battle?.MaxHealth ?? -1,
                baseline.MaxMana, projection.DerivedStats.MaxMana, battle?.MaxMana ?? -1,
                projection.DerivedStats.MoveRange, battle?.MoveRange ?? -1,
                projection.DerivedStats.Initiative, battle?.Initiative ?? -1,
                projection.Attributes.Intelligence, battle?.ManaRecoveryPerTurn ?? -1, matches));
        }
        return evidence;
    }

    public bool TryResolveTestBattlePointerTarget(string targetKind, string locator,
        out Control? surface, out Vector2 globalPoint)
    {
        surface = _board;
        globalPoint = Vector2.Zero;
        if (_board is null || _visibleSnapshot is null) return false;
        if (string.Equals(targetKind, "BattleCell", StringComparison.Ordinal))
        {
            string[] parts = locator.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y) ||
                x is < 0 or >= IsometricBattleBoardLayout.GridSize || y is < 0 or >= IsometricBattleBoardLayout.GridSize)
                return false;
            globalPoint = _board.GetGlobalTransform() * IsometricBattleBoardLayout.GridToScreen(new GridPoint(x, y));
            return true;
        }
        if (!string.Equals(targetKind, "BattleUnit", StringComparison.Ordinal)) return false;
        UnitInstanceId? unitId = string.Equals(locator, "CurrentPlayer", StringComparison.OrdinalIgnoreCase)
            ? _visibleSnapshot.ActiveUnitId
            : _visibleSnapshot.Units.FirstOrDefault(unit =>
                string.Equals(unit.UnitId.Value, locator, StringComparison.Ordinal) ||
                string.Equals(unit.DefinitionId.Value, locator, StringComparison.Ordinal))?.UnitId;
        if (unitId is null || !_actors.TryGetValue(unitId.Value, out GodotUnitActor? actor) ||
            !GodotObject.IsInstanceValid(actor)) return false;
        globalPoint = _board.GetGlobalTransform() * actor.Position;
        return true;
    }

    public bool TryResolveTestAdventurePointerTarget(string targetKind, string locator,
        out Control? surface, out Vector2 globalPoint)
    {
        if (_startCampView is not null && GodotObject.IsInstanceValid(_startCampView) &&
            _startCampView.TryResolveTarget(targetKind, locator, out globalPoint))
        {
            surface = _startCampView.InputSurface;
            return true;
        }
        surface = _adventureBoard;
        globalPoint = Vector2.Zero;
        return _adventureBoard is not null && GodotObject.IsInstanceValid(_adventureBoard) &&
            _adventureBoard.TryResolveTarget(targetKind, locator, out globalPoint);
    }

    private IRunSaveStore SaveStore => _saveStore ?? throw new InvalidOperationException("Run save store is not initialized.");

    private void RequestQuit()
    {
        _quitRequested = true;
        if (_testContext?.InterceptQuit != true) GetTree().Quit();
    }

    public override void _ExitTree()
    {
        ReleaseActWorld();
        DisposePresentationPlayer();
    }
    public override void _Process(double delta)
    {
        if (_presentationPlayer is null) return;
        if (ShouldRecoverPresentationFrame(_presentationInputLocked, _presentationPlayer.IsPlaying, _playbackPaused))
            _presentationPlayer.TryRecoverStalledFrame("presentation_tween_stopped_without_completion");
    }

    internal static bool ShouldRecoverPresentationFrame(bool inputLocked, bool presentationPlaying, bool paused) =>
        inputLocked && !presentationPlaying && !paused;

    internal static PresentationDrainAction ResolvePresentationDrainAction(bool hasPendingFrames,
        bool hasTerminalResult, bool paused, bool pauseAfterCurrentFrame)
    {
        if (hasPendingFrames) return paused || pauseAfterCurrentFrame
            ? PresentationDrainAction.Pause
            : PresentationDrainAction.DequeueFrame;
        if (hasTerminalResult) return PresentationDrainAction.CompleteBattle;
        return paused || pauseAfterCurrentFrame ? PresentationDrainAction.Pause : PresentationDrainAction.Refresh;
    }

    public override void _Input(InputEvent inputEvent)
    {
        // Escape must be handled in the global input phase. Focused Buttons and other
        // GUI controls may consume ui_cancel before _UnhandledInput runs.
        if (TryHandleGlobalEscape(inputEvent))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_startCampView is null || _pauseMenu?.Visible == true || _cheatConsole?.Visible == true) return;
        if (!_startCampView.HandleAtlasKey(inputEvent)) return;
        GetViewport().SetInputAsHandled();
    }

    private bool TryHandleGlobalEscape(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key ||
            key.Keycode != Key.Escape && key.PhysicalKeycode != Key.Escape) return false;
        if (_cheatConsole?.Visible == true)
        {
            _cheatConsole.SetOpen(false);
            return true;
        }
        if (_battle is not null && _visibleSnapshot?.TargetingMode != BattleTargetingMode.None)
        {
            ApplyIntent(new CancelTargetingIntent());
            return true;
        }
        // One ESC dismisses only the top registered layer (Options, confirm, inventory, …).
        if (TryDismissTopEscapeLayer()) return true;
        if (!_inputContext.CanPause) return false;
        OpenPauseMenu();
        return true;
    }

    private void PushEscapeLayer(string tag, Action dismiss)
    {
        RemoveEscapeLayer(tag);
        _escapeLayers.Add((tag, dismiss));
    }

    private void EnsureEscapeLayer(string tag, Action dismiss)
    {
        if (_escapeLayers.Any(layer => layer.Tag == tag)) return;
        if (tag == "pause.close") _escapeLayers.Insert(0, (tag, dismiss));
        else _escapeLayers.Add((tag, dismiss));
    }

    private void RemoveEscapeLayer(string tag)
    {
        for (int index = _escapeLayers.Count - 1; index >= 0; index--)
        {
            if (_escapeLayers[index].Tag != tag) continue;
            _escapeLayers.RemoveAt(index);
            return;
        }
    }

    private void ClearEscapeLayers() => _escapeLayers.Clear();

    private bool TryDismissTopEscapeLayer()
    {
        if (_escapeLayers.Count == 0) return false;
        Action dismiss = _escapeLayers[^1].Dismiss;
        _escapeLayers.RemoveAt(_escapeLayers.Count - 1);
        dismiss();
        return true;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_battle is not null && inputEvent.IsActionPressed("toggle_console"))
        {
            if (_cheatConsole is not null) _cheatConsole.SetOpen(!_cheatConsole.Visible);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_cheatConsole?.Visible == true || _pauseMenu?.Visible == true) return;
        if (inputEvent is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (_battle is not null && key.Keycode is (Key.Enter or Key.KpEnter))
                ApplyIntent(new EndTurnIntent());
        }
        else if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
            ApplyIntent(new CancelTargetingIntent());
    }

    private void LoadCatalogs()
    {
        GodotResourceCatalog catalog = RequiredResourceLoader.Load<GodotResourceCatalog>(
            "res://content/ContentCatalog.tres", "Canonical Catalog load failed");
        _catalogCount = catalog.Entries.Length;
        _balance = RequiredResourceLoader.Load<PlayableLv1BalanceProfileResource>(
            "res://content/ui/PlayableLv1BalanceProfile.tres", "Playable Lv1 balance profile load failed").ToCoreProfile();
        _enemySpeed = RequiredResourceLoader.Load<PlayableEnemySpeedProfileResource>(
            "res://content/ui/PlayableEnemySpeedProfile.tres", "Playable enemy speed profile load failed").ToCoreProfile();
        _presentationProfile = ResourceLoader.Load<StandardUnitPresentationResource>(
            "res://content/presentation/StandardUnitPresentationV1.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        PureRunDefinitionResource? runResource = null;
        foreach (GodotResourceEntry entry in catalog.Entries)
        {
            Resource resource = ResourceLoader.Load(entry.ResourceLocator, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException($"Missing canonical resource: {entry.ContentIdValue}");
            var id = new ContentId(entry.ContentIdValue);
            switch (resource)
            {
                case UnitDefinitionResource unit:
                    _unitResources[id] = unit; _units[id] = unit.ToCoreDefinition(); break;
                case SkillDefinitionResource skill:
                    _skills[id] = skill.ToCoreDefinition(); _skillUi[id] = SkillUiMetadata.From(skill); break;
                case PoisonSpearSkillResource poison:
                    _skills[id] = poison.ToCoreDefinition(); _skillUi[id] = SkillUiMetadata.From(poison); break;
                case AiDefinitionResource ai: _ai[id] = ai.ToCoreDefinition(); break;
                case BattleLayoutResource layout: _layouts[id] = layout.ToCoreDefinition(); break;
                case EncounterDefinitionResource encounter:
                    _encounters[id] = new EncounterDefinition(id, new ContentId(encounter.LayoutContentId),
                        Enumerable.Range(0, encounter.MonsterUnitContentIds.Length).Select(index =>
                            new EncounterMonsterDefinition(new ContentId(encounter.MonsterUnitContentIds[index]),
                                new ContentId(encounter.MonsterAiContentIds[index]),
                                _ai.TryGetValue(new ContentId(encounter.MonsterAiContentIds[index]), out AiDefinition? definition)
                                    ? definition.SkillIds : Array.Empty<ContentId>())).ToArray(), encounter.HealthMultiplier,
                        encounter.OutputMultiplier, encounter.MinimumStartingMana,
                        Enum.Parse<EncounterClass>(encounter.EncounterClassValue)); break;
                case PureRunDefinitionResource run: runResource = run; break;
                case SkillPresentationResource presentation: _skillPresentationProfiles.Add(presentation); break;
                case StatusPresentationResource statusPresentation: _statusPresentationProfile = statusPresentation; break;
                case EquipmentDefinitionResource equipment: _equipment[id] = equipment.ToCoreDefinition(); break;
                case ConsumableDefinitionResource consumable: _consumables[id] = consumable.ToCoreDefinition(); break;
                case PureRunMapResource map: _mapDefinition = map.ToCoreDefinition(); break;
                case PureRunTreasureResource treasure: _treasureDefinition = treasure.ToCoreDefinition(); break;
                case PureRunLayerFourResource layerFour when layerFour.KindValue == "encounter":
                    using (JsonDocument payload = JsonDocument.Parse(layerFour.PayloadJson))
                    {
                        string[] units=payload.RootElement.GetProperty("monsters").EnumerateArray().Select(value=>value.GetString()!).ToArray();
                        string[] aiIds=units.Select(value=>"ai.pure-run."+value["unit.pure-run.".Length..].Replace("goat-",string.Empty)).ToArray();
                        var layoutId=new ContentId("battle-layout.pure-run.split-flank");
                        if (!_layouts.ContainsKey(layoutId))
                            throw new InvalidOperationException("Catalog is missing the authored split-flank battle layout.");
                        _encounters[id]=new EncounterDefinition(id,layoutId,units.Select((unit,index)=>new EncounterMonsterDefinition(new ContentId(unit),new ContentId(aiIds[index]),_ai[new ContentId(aiIds[index])].SkillIds)).ToArray());
                    }
                    break;
                case PureRunLayerFourResource layerFour when layerFour.KindValue == "event":
                    using (JsonDocument payload = JsonDocument.Parse(layerFour.PayloadJson))
                        _layerFourEventPayloads[payload.RootElement.GetProperty("sourceId").GetString()!] = layerFour.PayloadJson;
                    break;
            }
        }
        // Encounter resources can sort before AI entries in the canonical catalog; rebuild their skill bindings now.
        foreach (GodotResourceEntry entry in catalog.Entries.Where(value =>
                     value.ContentIdValue.StartsWith("encounter.pure-run.", StringComparison.Ordinal) &&
                     value.ResourceTypeIdValue == "encounter" &&
                     !value.ContentIdValue.EndsWith(".n4", StringComparison.Ordinal)))
        {
            var resource = ResourceLoader.Load<EncounterDefinitionResource>(entry.ResourceLocator)!;
            var id = new ContentId(entry.ContentIdValue);
            _encounters[id] = new EncounterDefinition(id, new ContentId(resource.LayoutContentId),
                Enumerable.Range(0, resource.MonsterUnitContentIds.Length).Select(index =>
                {
                    var aiId = new ContentId(resource.MonsterAiContentIds[index]);
                    return new EncounterMonsterDefinition(new ContentId(resource.MonsterUnitContentIds[index]), aiId, _ai[aiId].SkillIds);
                }).ToArray(),resource.HealthMultiplier,resource.OutputMultiplier,resource.MinimumStartingMana,
                Enum.Parse<EncounterClass>(resource.EncounterClassValue));
        }
        _runDefinition=(runResource ?? throw new InvalidOperationException("Run definition is missing.")).ToCoreDefinition();
        if (_mapDefinition is null) throw new InvalidOperationException("Authoritative run map is missing.");
        if (_treasureDefinition is null) throw new InvalidOperationException("Treasure definition is missing.");
        PureRunContentValidator.ValidateSkillReferences(_runDefinition, _skills.Keys);
        _run = new PureRunSessionService(_runDefinition, SaveStore, mapDefinition: _mapDefinition);
    }

    internal const string StaleSaveResetMessage = "Save is from an older build and was cleared. Start a new run.";

    private void ShowHome()
    {
        _logs.Clear();_visibleSnapshot=null;
        _battle = null;
        Control root = CreatePage("PURE RUN", "Seven-layer deterministic run", false, false);
        PanelContainer panel = PanelAt(root, new Vector2(570, 165), new Vector2(460, 570));
        VBoxContainer menu = new();
        panel.AddChild(menu);
        Label title = Label("TACTICS", 42); title.HorizontalAlignment = HorizontalAlignment.Center; menu.AddChild(title);
        Label subtitle = Label("PURE RUN", 16); subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.AddThemeColorOverride("font_color", GodotTacticsTheme.TextSecondary); menu.AddChild(subtitle);
        menu.AddChild(new ColorRect { Color = GodotTacticsTheme.Accent, CustomMinimumSize = new Vector2(0, 2), MouseFilter = MouseFilterEnum.Ignore });
        Button newRun = Button("New Run", () => StartNewRun()); menu.AddChild(newRun);
        RunStoreResult loaded = SaveStore.Load();
        Button continueRun = Button("Continue", ContinueRun);
        continueRun.Disabled = !loaded.Succeeded || loaded.Snapshot is null ||
            (loaded.Snapshot.ActiveRun is null && loaded.Snapshot.TerminalSummary is null);
        menu.AddChild(continueRun);
        menu.AddChild(Button("Options", ShowHomeOptions));
        menu.AddChild(Button("Quit", RequestQuit));
        string status = loaded.ErrorCode?.Contains("run_reset_for_v7", StringComparison.Ordinal) == true
            ? "Save upgraded to V7. The previous unfinished run was cleared; start a new run."
            : loaded.Snapshot?.ActiveRun is null ? "No active run" : $"Active run: {loaded.Snapshot.ActiveRun.EncounterContentId.Value}";
        _status = Label(status, 16); _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.AddThemeColorOverride("font_color", GodotTacticsTheme.TextSecondary); menu.AddChild(_status);
    }

    private void RouteStartup()
    {
        _logs.Clear(); _visibleSnapshot = null; _battle = null;
        RunStoreResult loaded = SaveStore.Load();
        // GodotRunSaveStore quarantines unrecoverable documents during Load. A second
        // read observes the clean slot while preserving the corrupt evidence files.
        if (!loaded.Succeeded) loaded = SaveStore.Load();
        if (loaded.Succeeded && loaded.Snapshot is { } snapshot &&
            (snapshot.ActiveRun is not null || snapshot.TerminalSummary is not null))
        {
            if (snapshot.TerminalSummary is not null) ShowSummary(snapshot.TerminalSummary);
            else ContinueRun();
            return;
        }
        StartNewRunConfirmed();
    }

    private void ShowHomeOptions()
    {
        Control root = NewPage("OPTIONS", "Display and presentation settings");
        PanelContainer panel = PanelAt(root, new Vector2(500, 205), new Vector2(600, 500));
        var menu = new VBoxContainer(); panel.AddChild(menu);
        menu.AddChild(Label("DISPLAY", 24));
        menu.AddChild(Label("Logical canvas  1600 × 900\nScaling  Canvas Items / Keep\nRenderer  Compatibility or Forward+", 18));
        Button fullscreen = Button(FullscreenButtonText(), () =>
        {
            Window window = GetWindow();
            window.Mode = window.Mode == Window.ModeEnum.Fullscreen ? Window.ModeEnum.Windowed : Window.ModeEnum.Fullscreen;
            ShowHomeOptions();
        });
        menu.AddChild(fullscreen);
        menu.AddChild(Label("Battle playback speed is controlled from the battle HUD.", 16));
        menu.AddChild(Button("Back", ShowHome));
        PushEscapeLayer("home.options", ShowHome);
    }

    private string FullscreenButtonText() => GetWindow().Mode == Window.ModeEnum.Fullscreen
        ? "Fullscreen: On"
        : "Fullscreen: Off";

    private void StartNewRun()
    {
        RunStoreResult loaded = SaveStore.Load();
        if (loaded.Snapshot?.ActiveRun is not null)
        {
            var confirm = new ConfirmationDialog { DialogText = "Overwrite the active Pure Run?", Title = "New Run" };
            AddChild(confirm); confirm.Confirmed += () => { confirm.QueueFree(); StartNewRunConfirmed(); }; confirm.Canceled += confirm.QueueFree; confirm.PopupCentered();
            return;
        }
        StartNewRunConfirmed();
    }

    private void StartNewRunConfirmed()
    {
        int seed = _testContext?.FixedSeed ?? System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        RunSessionResult started = _run!.BeginNewRunSetup(seed);
        if (!started.Succeeded) { SetStatus(started.ErrorCode); return; }
        ReleaseActWorld();
        _battle = null;
        ShowNewRunSetup(started.Snapshot!);
    }

    private void ContinueRun()
    {
        RunSessionResult resumed = _run!.ResumeRun();
        if (resumed.ErrorCode == PureRunSessionService.StaleSaveReset)
        {
            ReleaseActWorld();
            ShowHome();
            SetStatus(StaleSaveResetMessage);
            return;
        }
        if (!resumed.Succeeded) { SetStatus(resumed.ErrorCode); return; }
        if (resumed.EncounterRequest is EncounterRequest request) StartBattle(request);
        else if (resumed.Snapshot is not null) RouteRunState(resumed.Snapshot);
    }

    private void ShowNewRunSetup(PureRunSaveSnapshot snapshot)
    {
        PendingRunSetup setup = snapshot.PendingRunSetup ?? throw new InvalidOperationException("Pending setup is missing.");
        if (setup.SelectedCharacterIds.Count < _runDefinition!.ActivePartySize &&
            _runDefinition.Party.Count > _runDefinition.ActivePartySize)
        {
            ShowPartySelection();
            return;
        }
        PureRunPartyTemplate template = _runDefinition!.Party.Single(value =>
            string.Equals(value.CharacterId, setup.CurrentCharacterId, StringComparison.Ordinal));
        Control root = NewPage("NEW RUN — STARTING SKILL", $"Choose 1 of 3 for {template.CharacterId} ({setup.CurrentCharacterIndex + 1}/3)");
        SetInputContext(RunInputContext.Setup);
        var choices = new VBoxContainer { Position = new Vector2(470, 230), Size = new Vector2(660, 430) };
        root.AddChild(choices);
        foreach (ContentId skill in template.EffectiveStartingSkillChoices)
        {
            ContentId captured = skill;
            SkillUiMetadata metadata = _skillUi[captured];
            Button skillButton = Button($"{metadata.DisplayName} Lv{metadata.Level}\n{metadata.Description}\nMP {metadata.ManaCost}  Range {RangeLabel(metadata)}", () =>
            {
                RunSessionResult result = _run!.ChooseStartingSkill(template.CharacterId, captured);
                if (!result.Succeeded) { SetStatus(result.ErrorCode); return; }
                if (result.Snapshot!.PendingRunSetup is not null) ShowNewRunSetup(result.Snapshot);
                else RouteCommittedSetup(result);
            });
            skillButton.Name = $"starting_skill__{captured.Value.Replace('.', '_')}";
            choices.AddChild(skillButton);
        }
        _status = LabelAt(root, "Setup commits only after all starting skills are confirmed. Press Esc for run controls.", new Vector2(470, 680), 18);
    }

    private void ShowPartySelection()
    {
        PendingRunSetup setup = _run?.CurrentSetupProjection?.PendingRunSetup ??
            throw new InvalidOperationException("Transient setup draft is missing.");
        if (!EnsureSetupActPrepared(setup)) return;
        Control root = NewPage("NEW RUN — START CAMP", "Select exactly 3. Your first choice becomes the leader and can move on camp tiles.");
        SetInputContext(RunInputContext.Setup);
        _partySelectionOrder.Clear();
        _partySelectionOrder.AddRange(setup.SelectedCharacterIds);
        GodotStartCampCandidate[] candidates = _runDefinition!.Party.Select(value =>
            new GodotStartCampCandidate(value.CharacterId, _unitResources[value.UnitContentId])).ToArray();
        _startCampView = new GodotStartCampView { Name = "StartCampView" };
        root.AddChild(_startCampView);
        _actWorld!.ActivateNode("start");
        _actWorld.Visible = true;
        _startCampView.ConfigureResidentWorld(_actWorld, candidates, MapDefinition);
        _startCampView.ConfigureMovement(_presentationProfile ?? new StandardUnitPresentationResource(), _playbackSpeed);
        _startCampView.MovementRejected += SetStatus;
        _startCampView.PreviewPressed += nodeId =>
        {
            PureRunMapNodeDefinition node = MapDefinition.Nodes.Single(value => value.NodeId == nodeId);
            SetStatus($"Planning preview — {node.Title ?? node.NodeId} | {node.Kind} | Layer {node.Layer}. No encounter details revealed yet.");
        };
        _startCampView.SetSelection(_partySelectionOrder, _partySelectionOrder.Count == _runDefinition.ActivePartySize);
        _startCampView.CandidatePressed += characterId =>
        {
            if (_partySelectionOrder.Contains(characterId, StringComparer.Ordinal))
            {
                const string error = "party.already_selected";
                _startCampView.RecordSelectionOutcome(false, error, _partySelectionOrder.Count);
                SetStatus($"{characterId} is already selected. Party order is locked for this run.");
                return;
            }
            RunSessionResult selected = _run!.SelectPartyMember(characterId);
            if (!selected.Succeeded)
            {
                _startCampView.RecordSelectionOutcome(false, selected.ErrorCode, _partySelectionOrder.Count);
                SetStatus(selected.ErrorCode);
                return;
            }
            _partySelectionOrder.Add(characterId);
            _startCampView.SetSelection(_partySelectionOrder, _partySelectionOrder.Count == _runDefinition.ActivePartySize);
            _startCampView.RecordSelectionOutcome(true, null, _partySelectionOrder.Count);
            SetStatus(PartySelectionStatus());
        };
        _startCampView.ExitPressed += () =>
        {
            if (_partySelectionOrder.Count != _runDefinition.ActivePartySize) { SetStatus("Choose exactly three candidates first."); return; }
            RunSessionResult result = _run!.ChooseParty(_partySelectionOrder);
            if (!result.Succeeded) { SetStatus(result.ErrorCode); return; }
            if (result.Snapshot!.PendingRunSetup is not null) ShowNewRunSetup(result.Snapshot);
            else RouteCommittedSetup(result);
        };
        PanelContainer partyPanel = PanelAt(root, new Vector2(1080, 150), new Vector2(460, 220), GodotTacticsTheme.Card);
        partyPanel.Name = "StartCampPartyPanel";
        partyPanel.ZIndex = StartPageUiZIndex;
        Label partyInstructions = LabelAt(root, "PARTY SETUP\nChoose exactly 3 candidates\nFirst choice = Leader\nSelected members cannot be removed or reordered\nExit unlocks at 3/3", new Vector2(1105, 172), 18);
        partyInstructions.Name = "StartCampPartyInstructions";
        partyInstructions.Size = new Vector2(410, 170);
        partyInstructions.ZIndex = StartPageUiZIndex + 1;
        _status = LabelAt(root, PartySelectionStatus(), new Vector2(470, 810), 18);
        _status.Name = "StartCampStatus";
        _status.ZIndex = StartPageUiZIndex;
        Label controlsHint = LabelAt(root,
            StartCurrentControlsHint,
            new Vector2(710, 742), 14);
        controlsHint.Name = "StartCampControlsHint";
        controlsHint.Size = new Vector2(830, 28);
        controlsHint.HorizontalAlignment = HorizontalAlignment.Right;
        controlsHint.AddThemeColorOverride("font_color", new Color("c4d0d3b8"));
        controlsHint.ZIndex = StartPageUiZIndex;
        _startCampView.CameraModeChanged += mode => controlsHint.Text = mode == StartAtlasCameraMode.Current
            ? StartCurrentControlsHint
            : StartOverviewControlsHint;
    }

    private string PartySelectionStatus()
    {
        string leader = _partySelectionOrder.FirstOrDefault() ?? "not selected";
        string slots = string.Join(" | ", Enumerable.Range(0, _runDefinition!.ActivePartySize)
            .Select(index => index < _partySelectionOrder.Count ? _partySelectionOrder[index] : "empty"));
        return $"Party {_partySelectionOrder.Count}/{_runDefinition.ActivePartySize}  Leader: {leader}  Slots: {slots}  " +
            (_partySelectionOrder.Count == _runDefinition.ActivePartySize ? "Exit ready." : "Exit locked: select more candidates.");
    }

    private void ShowInitialAdventure(PureRunState run)
    {
        RunAdventureState adventure = run.AdventureState ?? throw new InvalidOperationException("adventure.state_missing");
        Control root = NewPage("PURE RUN — TILE ADVENTURE", "Select leader • Click reachable tile • Idle companions hold position");
        AddRunShell(root, run, "Adventure");
        AdventureActorPlacement[] actors = AdventurePlacements(run);
        AdventureBoardDefinition definition = CreateInitialAdventureBoard(actors);
        _adventureBoard = new GodotAdventureBoardView { Name = "InitialAdventureBoard" };
        root.AddChild(_adventureBoard);
        ConfigureAdventureBoard(_adventureBoard, run, definition);
        WireAdventureMovement(run, definition, ShowInitialAdventure);
        _adventureBoard.ObjectPressed += objectId => HandleExitPointer(run, definition, objectId);
        Button inventory = Button("Inventory", AdventureNavigation(() => ShowInventory(run)));
        inventory.Name = "AdventureInventoryButton";
        inventory.ZIndex = StartPageUiZIndex;
        root.AddChild(PlaceControl(inventory, AdventureInventoryRect.Position, AdventureInventoryRect.Size));
        PanelContainer leaderPanel = PanelAt(root, AdventureLeaderRect.Position, AdventureLeaderRect.Size, GodotTacticsTheme.Card);
        leaderPanel.Name = "AdventureLeaderPanel";
        leaderPanel.ZIndex = StartPageUiZIndex;
        Label leaderLabel = Label($"Leader: {adventure.LeaderId}", 16);
        leaderLabel.HorizontalAlignment = HorizontalAlignment.Center;
        leaderLabel.VerticalAlignment = VerticalAlignment.Center;
        leaderPanel.AddChild(leaderLabel);
    }

    private static AdventureBoardDefinition CreateInitialAdventureBoard(IReadOnlyList<AdventureActorPlacement> actors)
    {
        GridPoint[] perimeter = Enumerable.Range(0, 10).SelectMany(value =>
                new[] { new GridPoint(value, 0), new GridPoint(value, 9) })
            .Concat(Enumerable.Range(1, 8).SelectMany(value => new[] { new GridPoint(0, value), new GridPoint(9, value) }))
            .Distinct().ToArray();
        return new AdventureBoardDefinition(new ContentId("adventure-board.pure-run.initial"), 10, 10, perimeter,
            [new AdventureBoardObject("exit:layer_01_battle", AdventureObjectKind.Exit, new GridPoint(8, 5), false, false,
                "layer_01_battle")],
            actors, new GridPoint(1, 5), new GridPoint(8, 5));
    }

    private void BeginReadyEncounter()
    {
        AddLog(new BattleUiLogEntry(BattleUiLogCategory.Gameplay,"Settlement Continue requested the next encounter","EncounterNavigationEvent"));
        RunSessionResult resumed = _run!.ResumeRun();
        if (!resumed.Succeeded)
        {
            SetStatus(resumed.ErrorCode);
            return;
        }
        if (resumed.EncounterRequest is EncounterRequest pendingRequest)
        {
            StartBattle(pendingRequest);
            return;
        }
        if (resumed.Snapshot?.ActiveRun is not { Phase: PureRunPhase.Ready } ||
            resumed.Snapshot.ActiveRun.PendingProgression.Count > 0)
        {
            RouteRunState(resumed.Snapshot!);
            return;
        }
        RunSessionResult begun = _run!.BeginEncounter();
        if (!begun.Succeeded || begun.EncounterRequest is null) { SetStatus(begun.ErrorCode); return; }
        StartBattle(begun.EncounterRequest);
    }

    private void StartBattle(EncounterRequest request)
    {
        if (_run is null || _run.HasSetupDraft) { SetStatus("battle.stale_request"); return; }
        // Resume applies the same zero-write legacy party repair used when the request was issued.
        RunSessionResult loaded = _run.ResumeRun();
        if (!loaded.Succeeded || loaded.Snapshot is not { } snapshot)
        { SetStatus("battle.stale_request"); return; }
        var validation = PureRunEncounterRequestValidator.Validate(snapshot, request, MapDefinition);
        if (!validation.Succeeded)
        { SetStatus($"battle.stale_request:{validation.ErrorCode}"); return; }
        PureRunState persisted = snapshot.ActiveRun!;
        // Rendering consumes a proven node projection, not an Encounter-to-node guess. This is
        // in-memory recovery only; production persistence stays V11 until all transitions migrate.
        var scene = PureRunPendingSceneRecovery.Recover(snapshot, MapDefinition);
        if (!scene.Succeeded) { SetStatus(scene.ErrorCode); return; }
        RunNodeSceneIdentity identity = scene.Snapshot.ActiveRun!.Checkpoint!.SceneIdentity!;
        EncounterRequest boundRequest = request with { SceneIdentity = identity };
        var sceneValidation = PureRunEncounterRequestValidator.Validate(scene.Snapshot, boundRequest, MapDefinition);
        if (!sceneValidation.Succeeded) { SetStatus(sceneValidation.ErrorCode); return; }
        bool mysteryBattle = MapDefinition.Nodes.Single(node => node.NodeId == identity.NodeId).Kind == PureRunNodeKind.Mystery;
        if (!EnsureRunActPrepared(persisted, () => StartBattle(request))) return;
        bool residentTemplate = HasResidentBattleTemplate(identity);
        bool residentBattle = !mysteryBattle || residentTemplate;
        if (!mysteryBattle && !residentTemplate)
        { SetStatus("battle.resident_template_mismatch"); return; }
        if (mysteryBattle && residentTemplate &&
            !HasResidentEventTemplate(identity, scene.Snapshot.ActiveRun!.AdventureState!.PendingEventContext))
        { SetStatus("battle.resident_event_template_mismatch"); return; }
        EncounterDefinition encounter = _encounters[request.EncounterContentId];
        if (residentBattle && !HasResidentBattleLayout(identity, _layouts[encounter.LayoutId]))
        { SetStatus("battle.surface_layout_mismatch"); return; }
        _settlement.Reset();
        _terminalSettlementResult=null;
        _settlementRetryCount=0;
        _settlementNavigationRetryCount=0;
        _presentationNumberHistory.Clear();
        _currentEncounterId=request.EncounterContentId;
        // Use the same validated snapshot for encounter context; a second load could mix two checkpoints.
        bool escortBattle = persisted.EscortState?.Lifecycle == RunEscortLifecycle.BattlePending;
        _battle = new PlayableBattleSessionFactory().Create(boundRequest, encounter, _layouts[encounter.LayoutId], _units,
            _skills, _ai, _balance, _enemySpeed, _equipment,
            escortBattle ? new PlayableBattleSessionFactory.ProtectedNpcBattleConfig(
                new ContentId("unit.pure-run.mage"), new GridPoint(2, 5)) : null);
        BuildBattlePage(residentBattle ? identity : null);
        AddLog(new BattleUiLogEntry(BattleUiLogCategory.Gameplay,$"Entered {EncounterLabel(request.EncounterContentId)} ({request.EncounterContentId.Value})","EncounterNavigationEvent"));
        RefreshLog();
    }

    private void BuildBattlePage(RunNodeSceneIdentity? residentIdentity)
    {
        ContentId encounterId=_currentEncounterId??throw new InvalidOperationException("Battle encounter identity is missing.");
        Control root = NewPage($"PURE RUN BATTLE — {EncounterLabel(encounterId)}", $"{encounterId.Value}   |   Left click: select/confirm   Right click or Esc: cancel   Enter: end turn", true);
        _logs.Clear();_playbackPaused=false;_playbackSpeed=_testContext?.InitialPlaybackSpeed ?? 1f;
        Transform2D boardFit = GodotBattleBoardFitter.Fit(GodotBattleBoardFitter.BoardBounds(), new Rect2(30, 90, 1540, 650));
        float boardScale = boardFit.X.Length();
        _board = new GodotIsometricBattleBoard { Position = boardFit.Origin, Scale = Vector2.One * boardScale, Size = new Vector2(1200, 650) };
        _board.PointerPressed += OnBoardPointerPressed;
        _board.CellHovered += HoverCell;
        _board.HoverCleared += ClearHover;
        _board.PointerMoved += UpdateHoveredMeter;
        _board.PointerExited += HideUnitMeters;
        root.AddChild(_board);
        if (residentIdentity is not null) BindResidentBattleSurface(residentIdentity);
        _presentationPlayer = new GodotBattlePresentationPlayer();
        _presentationPlayer.Configure(_presentationProfile ?? new StandardUnitPresentationResource());
        _presentationPlayer.ConfigureSkills(_skillPresentationProfiles);
        _presentationPlayer.SetSpeed(_playbackSpeed);
        _presentationPlayer.FrameCompleted+=OnPresentationFrameCompleted;
        _damageNumbers = new GodotDamageNumberLayer();
        _damageNumbers.Configure(_actors);
        _presentationPlayer.NumberRequested += SpawnPresentationNumber;
        _board.AddChild(_presentationPlayer);
        _board.AddChild(_damageNumbers);
        _droppedSpears = new GodotDroppedSpearLayer { ZIndex = 88 };
        _board.AddChild(_droppedSpears);
        foreach ((string name, Rect2 rect) in BattleHudPanelRects)
            HudPanelAt(root, name, rect.Position, rect.Size);
        var actionScroll = new ScrollContainer { Position = new Vector2(30, 765), Size = new Vector2(1145, 110), ZIndex = 1201 };
        root.AddChild(actionScroll);
        _skillPanel = new HBoxContainer { CustomMinimumSize = new Vector2(1120, 90) };
        actionScroll.AddChild(_skillPanel);
        // Clip before the playback controls at x=1160 so long turn lists do not collide with Pause/Speed.
        _turnOrder=LabelAt(root,string.Empty,new Vector2(475,32),18);
        _turnOrder.Size=new Vector2(660,50);
        _turnOrder.HorizontalAlignment=HorizontalAlignment.Center;
        _turnOrder.ClipText=true;
        _turnOrder.ZIndex=1201;
        _activeUnitPanel = new GodotBattleActiveUnitPanel
        {
            Name = "BattleActiveUnitPanel",
            Position = new Vector2(26, 26),
            Size = new Vector2(414, 174),
            ZIndex = 1201
        };
        root.AddChild(_activeUnitPanel);
        _cellDebug=LabelAt(root,string.Empty,new Vector2(475,78),15);
        _cellDebug.Name="BattleCellDebug";
        _cellDebug.Size=new Vector2(660,24);
        _cellDebug.HorizontalAlignment=HorizontalAlignment.Center;
        _cellDebug.ZIndex=1201;
        _settlementStatus=LabelAt(root,string.Empty,new Vector2(475,100),16);_settlementStatus.Size=new Vector2(660,34);_settlementStatus.HorizontalAlignment=HorizontalAlignment.Center;_settlementStatus.ZIndex=1201;
        var controls=new HBoxContainer{Position=new Vector2(1160,32),Size=new Vector2(410,55),ZIndex=1201};root.AddChild(controls);
        controls.AddChild(SmallButton("Pause/Resume",TogglePause));_stepButton=SmallButton("Step",()=>{if(_playbackPaused)PlaybackStep(true);});_stepButton.Disabled=true;controls.AddChild(_stepButton);_speedButton=SmallButton($"Speed {_playbackSpeed:0.#}x",ToggleSpeed);controls.AddChild(_speedButton);
        _cheatConsole=new GodotBattleCheatConsole();_cheatConsole.ClearRequested+=()=>{_logs.Clear();RefreshLog();};root.AddChild(_cheatConsole);
        _endTurnButton=Button("End Turn\nEnter",()=>ApplyIntent(new EndTurnIntent()));_endTurnButton.ZIndex=1201;root.AddChild(PlaceControl(_endTurnButton,new Vector2(1325,801),new Vector2(242,72)));
        BuildPauseMenu(root);
        _eventLog=null;
        _status = _settlementStatus;
        RefreshBattle();
        if(_battle!.HasPendingAutomaticFrames)PlaybackStep(false);
    }

    private void SpawnPresentationNumber(BattlePresentationNumber number)
    {
        _presentationNumberHistory.Add(number);
        _damageNumbers?.Spawn(number);
    }

    private void RefreshBattle(BattleUiSnapshot? presented=null)
    {
        if (_battle is null || _board is null || _skillPanel is null) return;
        BattleUiSnapshot snapshot = presented??_battle.CaptureSnapshot();_visibleSnapshot=snapshot;
        BattleUiUnitSnapshot[] visible=snapshot.Units.Where(unit=>unit.IsAlive||snapshot.Corpses.Contains(unit.Cell)).ToArray();
        var visibleIds=visible.Select(unit=>unit.UnitId).ToHashSet();
        foreach(UnitInstanceId removed in _actors.Keys.Where(id=>!visibleIds.Contains(id)).ToArray())
        {if(GodotObject.IsInstanceValid(_actors[removed]))_actors[removed].QueueFree();_actors.Remove(removed);if(_unitMeters.Remove(removed,out Control? meter)&&GodotObject.IsInstanceValid(meter))meter.QueueFree();}
        foreach (BattleUiUnitSnapshot unit in visible)
        {
            if(!_actors.TryGetValue(unit.UnitId,out GodotUnitActor? actor)||!GodotObject.IsInstanceValid(actor))
            {actor=GodotUnitFactory.InstantiateActor(_unitResources[unit.DefinitionId]);actor.ConfigureInstanceIdentity(unit.UnitId.Value);actor.Scale=Vector2.One*.34f;actor.SetFacing(GodotPresentationFacingResolver.Initial(unit.PlayerNumber));actor.ConfigurePresentation(_presentationProfile??new StandardUnitPresentationResource());_board.AddChild(actor);_actors[unit.UnitId]=actor;}
            if(!(_presentationPlayer?.IsPlaying??false))actor.Position = IsometricBattleBoardLayout.GridToScreen(unit.Cell);
            actor.SetDeathVisual(!unit.IsAlive);
            actor.Modulate = unit.IsPossessed ? new Color(1.25f, .55f, 1.35f) : Colors.White;
            actor.SetSpearHeld(unit.DefinitionId.Value != "unit.pure-run.amazon" || !snapshot.DroppedSpears.ContainsKey(unit.UnitId));
            StatusPresentationResource statusProfile = _statusPresentationProfile ?? new StatusPresentationResource();
            actor.SetStatuses(unit.IsAlive ? unit.Statuses : Array.Empty<BattleUiStatusSnapshot>(),
                statusProfile.MaximumVisibleStatuses, statusProfile.PulseDuration);
            actor.ZIndex = 100 + (18-unit.Cell.X-unit.Cell.Y) * 12 + unit.Cell.X;
            if(!_unitMeters.TryGetValue(unit.UnitId,out Control? meter)||meter is not GodotCompactUnitMeter compact||!GodotObject.IsInstanceValid(meter))
            {
                if(meter is not null&&GodotObject.IsInstanceValid(meter))meter.QueueFree();
                compact=new GodotCompactUnitMeter();_board.AddChild(compact);_unitMeters[unit.UnitId]=compact;
            }
            compact.ZIndex=400+(18-unit.Cell.X-unit.Cell.Y)*12+unit.Cell.X;
            compact.Bind(actor,unit.CurrentHealth,unit.MaxHealth,unit.CurrentMana,unit.MaxMana);
        }
        _droppedSpears?.Sync(snapshot.DroppedSpears);
        foreach (Node child in _skillPanel.GetChildren())
        {
            _skillPanel.RemoveChild(child);
            child.QueueFree();
        }
        BattleUiUnitSnapshot? activeSnapshot=ResolveActiveUnit(snapshot);
        if (activeSnapshot is not null && _unitResources.TryGetValue(activeSnapshot.DefinitionId,
                out UnitDefinitionResource? activeDefinition))
            _activeUnitPanel?.Bind(activeDefinition, activeSnapshot);
        else
            _activeUnitPanel?.Clear();
        bool aiPlayback=_battle.HasPendingAutomaticFrames||_presentationInputLocked;
        BattleUiMoveAvailability moveAvailability=snapshot.MoveAvailability??new BattleUiMoveAvailability(true,null);
        Button moveButton=ActionButton("Move", () => ApplyIntent(new BeginMoveIntent()));moveButton.Name="MoveAction";moveButton.Disabled=aiPlayback||!moveAvailability.IsAvailable;moveButton.TooltipText=moveAvailability.FailureCode??"Move to a legal tile";_skillPanel.AddChild(moveButton);
        if(snapshot.MeditationAvailability is BattleUiSkillAvailability meditation)
        {
            Button meditationButton=ActionButton("Meditate\n-5 Corruption",()=>ApplyIntent(new MeditateIntent()));meditationButton.Name="MeditateAction";meditationButton.Disabled=aiPlayback||!meditation.IsAvailable;meditationButton.TooltipText=meditation.FailureCode??"Reduce Corruption by 5 and end the turn";_skillPanel.AddChild(meditationButton);
        }
        bool spearDropped=snapshot.DroppedSpears.ContainsKey(snapshot.ActiveUnitId);
        foreach (SkillDefinition skill in snapshot.ActiveSkills.Where(skill => !skill.IsPassive&&skill.ExecutionKind!=SkillExecutionKind.Meditation&&(!skill.Hidden||skill.ExecutionKind==SkillExecutionKind.PickupSpear&&spearDropped)))
        {
            BattleUiSkillAvailability availability = snapshot.SkillAvailability?.Single(value => value.SkillId == skill.ContentId)
                ?? new BattleUiSkillAvailability(skill.ContentId, true, null);
            string? usageFailure=availability.FailureCode;
            string displayName = _skillUi.TryGetValue(skill.ContentId, out SkillUiMetadata? metadata)
                ? metadata.DisplayName
                : skill.ContentId.Value.Split('.').Reverse().Skip(skill.Level > 0 ? 1 : 0).FirstOrDefault() ?? skill.ContentId.Value;
            Button skillButton=ActionButton(FormatBattleActionLabel(displayName, skill.ManaCost, usageFailure is not null), () => ApplyIntent(new SelectSkillIntent(skill.ContentId)));skillButton.Name="SkillAction_"+skill.ContentId.Value.Replace('.','_');skillButton.Disabled=aiPlayback||usageFailure is not null;skillButton.TooltipText=usageFailure??SkillTooltip(skill);_skillPanel.AddChild(skillButton);
        }
        foreach(SkillDefinition passive in snapshot.ActiveSkills.Where(skill=>skill.IsPassive))_skillPanel.AddChild(Label($"Passive: {passive.ContentId.Value}",16));
        if(_endTurnButton is not null)_endTurnButton.Disabled=aiPlayback||snapshot.Phase!=PlayableBattlePhase.PlayerTurn;
        ApplyHighlights(snapshot);
        bool presentationPlaying = _presentationPlayer?.IsPlaying ?? false;
        _board.FollowActiveActor(ShouldShowActiveMarker(_presentationInputLocked, presentationPlaying)
            ? _actors.GetValueOrDefault(snapshot.ActiveUnitId)
            : null);
        if(_turnOrder is not null)_turnOrder.Text=$"Round {snapshot.Round} | Turn: "+string.Join(" → ",snapshot.TurnOrder.Select((id,index)=>$"{(index==snapshot.ActiveTurnIndex?"▶":"")}{id.Value}{(snapshot.Units.First(unit=>unit.UnitId==id).IsAlive?string.Empty:"✝")}"));
        RefreshLog();
    }

    private void ApplyHighlights(BattleUiSnapshot snapshot)
    {
        var colors=new Dictionary<GridPoint,Color>();
        for(int y=0;y<IsometricBattleBoardLayout.GridSize;y++)for(int x=0;x<IsometricBattleBoardLayout.GridSize;x++)colors[new GridPoint(x,y)]=new Color(.32f,.42f,.47f,.18f);
        foreach(BattleUiUnitSnapshot unit in snapshot.Units.Where(unit=>unit.IsAlive))
            colors[unit.Cell]=unit.UnitId==snapshot.ActiveUnitId?colors[unit.Cell]:unit.PlayerNumber==0?new Color(.34f,.52f,.62f,.6f):colors[unit.Cell];
        foreach(BattleUiUnitSnapshot unit in snapshot.Units.Where(unit=>unit.IsAlive&&unit.HasMovedThisTurn&&unit.UnitId!=snapshot.ActiveUnitId))colors[unit.Cell]=new Color(.36f,.42f,.48f,.55f);
        foreach(GridPoint corpse in snapshot.Corpses)colors[corpse]=new Color(.38f,.18f,.48f,.8f);
        foreach(GridPoint spear in snapshot.DroppedSpears.Values)colors[spear]=new Color(1f,.55f,.15f,.85f);
        if(snapshot.TargetingMode==BattleTargetingMode.Move)foreach(GridPoint cell in snapshot.LegalMoveCells)colors[cell]=new Color(.2f,.8f,1f,.75f);
        if(snapshot.TargetingMode==BattleTargetingMode.Skill&&snapshot.SelectedSkillId is ContentId skillId)
        {
            if(snapshot.SkillPreview is BattleUiSkillPreview skillPreview)
                foreach(GridPoint cell in skillPreview.RangeCells)colors[cell]=new Color(.58f,.22f,.2f,.48f);
            foreach(BattleUiTarget target in snapshot.LegalTargets.Where(target=>target.SkillId==skillId))
                colors[target.Cell]=_skills[skillId].ExecutionKind==SkillExecutionKind.PickupSpear?new Color(.25f,.9f,.35f,.82f):new Color(1f,.3f,.2f,.78f);
        }
        if(_hoveredCell is GridPoint hovered)
        {
            if(snapshot.TargetingMode==BattleTargetingMode.Move&&snapshot.LegalMoveCells.Contains(hovered))
            {IReadOnlyList<GridPoint> path=_battle?.PreviewMovePath(hovered)??Array.Empty<GridPoint>();foreach(GridPoint cell in path)colors[cell]=new Color(1f,.85f,.2f,.85f);colors[hovered]=new Color(1f,.5f,0,.9f);}
            if(snapshot.TargetingMode==BattleTargetingMode.Skill&&_battle?.PreviewSkillTarget(hovered) is BattleUiImpactPreview impact)
            {
                if(impact.IsLegal)
                {
                    foreach(GridPoint cell in impact.PathCells)colors[cell]=new Color(1f,.85f,.2f,.85f);
                    foreach(GridPoint cell in impact.ImpactCells)colors[cell]=new Color(1f,.5f,0,.72f);
                    if(impact.PrimaryImpactCell is GridPoint primary)colors[primary]=new Color(1f,.5f,0,.9f);
                }
            }
            colors[hovered]=colors[hovered].Lightened(.22f);
        }
        _board?.SetVisuals(colors,snapshot.BlockedCells??Array.Empty<GridPoint>(),snapshot.ShallowWaterCells);
    }

    internal static BattleUiUnitSnapshot? ResolveActiveUnit(BattleUiSnapshot snapshot) =>
        snapshot.Units.FirstOrDefault(unit => unit.UnitId == snapshot.ActiveUnitId);

    private void OnBoardCellPressed(GridPoint cell)=>ApplyIntent(new ConfirmCellIntent(cell));

    private void OnBoardPointerPressed(Vector2 pointer)
    {
        UnitInstanceId? unitId = GodotUnitPointerResolver.Resolve(_actors, pointer);
        if (unitId is UnitInstanceId resolved && _visibleSnapshot?.Units.FirstOrDefault(unit => unit.UnitId == resolved) is BattleUiUnitSnapshot unit)
        {
            OnBoardCellPressed(unit.Cell);
            return;
        }
        if (IsometricBattleBoardLayout.TryScreenToGrid(pointer, out GridPoint cell)) OnBoardCellPressed(cell);
    }

    private void HoverCell(GridPoint cell)
    {
        RestoreTargetingFacing();
        _hoveredCell = cell;
        if (_cellDebug is not null) _cellDebug.Text = $"Cell ({cell.X},{cell.Y})";
        if (_visibleSnapshot is not BattleUiSnapshot snapshot) return;
        PreviewTargetingFacing(snapshot, cell);
        ApplyHighlights(snapshot);
    }

    private void ClearHover()
    {
        RestoreTargetingFacing();
        _hoveredCell = null;
        if (_cellDebug is not null) _cellDebug.Text = string.Empty;
        if (_visibleSnapshot is not null) ApplyHighlights(_visibleSnapshot);
    }

    private void UpdateHoveredMeter(Vector2 pointer)
    {
        UnitInstanceId? hovered = GodotUnitPointerResolver.Resolve(_actors, pointer);
        if (hovered is UnitInstanceId unitId && _visibleSnapshot?.Units.FirstOrDefault(unit => unit.UnitId == unitId) is BattleUiUnitSnapshot unit)
            HoverCell(unit.Cell);
        foreach ((UnitInstanceId id, Control meter) in _unitMeters)
            if (GodotObject.IsInstanceValid(meter)) meter.Visible = hovered is UnitInstanceId value && value == id;
    }

    private void HideUnitMeters()
    {
        foreach (Control meter in _unitMeters.Values)
            if (GodotObject.IsInstanceValid(meter)) meter.Visible = false;
    }

    private void PreviewTargetingFacing(BattleUiSnapshot snapshot, GridPoint cell)
    {
        if (!_actors.TryGetValue(snapshot.ActiveUnitId, out GodotUnitActor? actor) || !GodotObject.IsInstanceValid(actor)) return;
        GodotUnitFacing current = actor.PresentationFacing;
        GodotUnitFacing preview = current;
        if (snapshot.TargetingMode == BattleTargetingMode.Move && snapshot.LegalMoveCells.Contains(cell))
            preview = GodotPresentationFacingResolver.PreviewMove(snapshot.Units.Single(unit => unit.UnitId == snapshot.ActiveUnitId).Cell,
                _battle?.PreviewMovePath(cell) ?? Array.Empty<GridPoint>(), current);
        else if (snapshot.TargetingMode == BattleTargetingMode.Skill && snapshot.SkillPreview?.RangeCells.Contains(cell) == true)
            preview = GodotPresentationFacingResolver.PreviewTarget(snapshot.Units.Single(unit => unit.UnitId == snapshot.ActiveUnitId).Cell, cell, current);
        else return;
        _targetingFacingPreview = (snapshot.ActiveUnitId, current);
        actor.SetFacing(preview);
    }

    private void RestoreTargetingFacing()
    {
        if (_targetingFacingPreview is not { } preview) return;
        if (_actors.TryGetValue(preview.UnitId, out GodotUnitActor? actor) && GodotObject.IsInstanceValid(actor)) actor.SetFacing(preview.Facing);
        _targetingFacingPreview = null;
    }

    private void ApplyIntent(BattleUiIntent intent)
    {
        if (_battle is null) return;
        if (ShouldBlockBattleIntent(_cheatConsole?.Visible == true, _presentationInputLocked, _pauseMenu?.Visible == true))
        {
            string reason = _cheatConsole?.Visible == true ? "cheat_console_open" : "presentation_in_progress";
            AddLog(new BattleUiLogEntry(BattleUiLogCategory.Rejected,reason,"CommandRejectedEvent"));
            RefreshLog();
            return;
        }
        RestoreTargetingFacing();
        BattleUiIntentResult result = _battle.Submit(intent);
        AddEvents(result.Events);
        if(!result.Succeeded&&result.Events.Count==0&&result.FailureCode is not null)AddLog(new BattleUiLogEntry(
            BattleUiLogCategory.Rejected,
            $"{result.FailureCode}:intent={intent.GetType().Name}:visible={_visibleSnapshot?.TargetingMode.ToString() ?? "none"}:selected={_visibleSnapshot?.SelectedSkillId?.Value ?? "none"}",
            "CommandRejectedEvent"));
        if(_battle.HasPendingAutomaticFrames){if(result.Presentation is BattlePresentationFrame pendingPresentation)BeginPresentation(pendingPresentation,true);else PlaybackStep(true);return;}
        if(result.Presentation is BattlePresentationFrame presentation)
        {
            // A terminal player action still owns its release, hit and defeat
            // presentation. Settlement starts only after that committed frame
            // reaches After; otherwise the page change hides the final action.
            BeginPresentation(presentation,false);
            return;
        }
        if (result.BattleResult is PureRunBattleResult battleResult){CompleteBattle(battleResult);return;}
        RefreshBattle();
        if (!result.Succeeded) SetStatus(result.FailureCode);
    }

    internal static bool ShouldBlockBattleIntent(bool cheatConsoleVisible, bool presentationInputLocked,
        bool pauseMenuVisible = false) => cheatConsoleVisible || presentationInputLocked || pauseMenuVisible;
    internal static bool HasCommittedTerminalSnapshot(RunSessionResult result) =>
        result.Snapshot?.TerminalSummary is not null;

    private BattleSettlementDiagnostic CompleteBattle(PureRunBattleResult battleResult)
    {
        _terminalSettlementResult=battleResult;
        if(!_settlement.TryBegin(battleResult,"terminal_queue_drained",out BattleSettlementDiagnostic begun))
        {
            LogSettlement(begun, BattleUiLogCategory.Rejected);
            return begun;
        }
        LogSettlement(begun, BattleUiLogCategory.Gameplay);
        try
        {
            RunAdventureEventContextKind eventContext = SaveStore.Load().Snapshot?.ActiveRun?.AdventureState?.PendingEventContext
                ?? RunAdventureEventContextKind.None;
            if (eventContext != RunAdventureEventContextKind.None)
            {
                bool protectedNpcAlive = _battle?.State.Units.Values.Any(unit =>
                    unit.Unit.InstanceId.Value == "escort-lost-villager" && unit.IsAlive) == true;
                RunSessionResult eventBattle = _run!.ApplyEventBattleResult(battleResult, protectedNpcAlive);
                if (!eventBattle.Succeeded) return HandleSettlementFailure(eventBattle);
                if (eventBattle.Snapshot?.TerminalSummary is PureRunSummary eventSummary)
                    return CompleteSettlementNavigation(eventBattle, () => ShowSummary(eventSummary));
                return CompleteSettlementNavigation(eventBattle, () =>
                {
                    ShowPostEventScene(eventBattle.Snapshot!.ActiveRun!, eventContext.ToString());
                });
            }
            if(battleResult.EncounterContentId.Value=="encounter.pure-run.n4")
            {
                RunSessionResult layerFour=_run!.ApplyLayerFourBattleResult(battleResult);
                if(!layerFour.Succeeded)return HandleSettlementFailure(layerFour);
                layerFour=ResolveAdventureBoard(layerFour);
                if(!layerFour.Succeeded)return HandleSettlementFailure(layerFour);
                return CompleteSettlementNavigation(layerFour, () =>
                {
                    if(layerFour.Snapshot?.TerminalSummary is PureRunSummary summary)ShowSummary(summary);
                    else RouteRunState(layerFour.Snapshot!);
                });
            }
            if(battleResult.EncounterContentId.Value is "encounter.pure-run.e1" or "encounter.pure-run.e2" or "encounter.pure-run.special")
            {
                PureRunState? active=SaveStore.Load().Snapshot?.ActiveRun;
                bool boss=battleResult.EncounterContentId.Value.EndsWith(".special",StringComparison.Ordinal);
                if(!boss&&active?.NodeTransaction?.NodeId.StartsWith("layer_06_",StringComparison.Ordinal)==true)
                {
                    RunSessionResult layerSix=_run!.ApplyLayerFourBattleResult(battleResult);
                    if(!layerSix.Succeeded)return HandleSettlementFailure(layerSix);
                    layerSix=ResolveAdventureBoard(layerSix);
                    if(!layerSix.Succeeded)return HandleSettlementFailure(layerSix);
                    return CompleteSettlementNavigation(layerSix, () => RouteRunState(layerSix.Snapshot!));
                }
                PureRunFullRunService full=new(_consumables.Keys);
                RunSessionResult late=_run!.ApplyFullRunTransition(state=>boss?full.CompleteBoss(state,battleResult):full.CompleteLayerFive(state,battleResult));
                if(!late.Succeeded)return HandleSettlementFailure(late);
                if (!boss)
                {
                    late=ResolveAdventureBoard(late);
                    if(!late.Succeeded)return HandleSettlementFailure(late);
                }
                return CompleteSettlementNavigation(late, () =>
                {
                    if(late.Snapshot?.TerminalSummary is PureRunSummary terminal)ShowSummary(terminal);
                    else ShowSettlement(late.Snapshot!);
                });
            }
            RunSessionResult settled=_run!.ApplyBattleResult(battleResult);
            if(!settled.Succeeded)return HandleSettlementFailure(settled);
            settled=ResolveAdventureBoard(settled);
            if(!settled.Succeeded)return HandleSettlementFailure(settled);
            return CompleteSettlementNavigation(settled, () => ShowSettlement(settled.Snapshot!));
        }
        catch(Exception exception)
        {
            string code=$"settlement.exception.{exception.GetType().Name}";
            GD.PushError($"Boss settlement failed: {code}: {exception.Message}");
            if(_settlement.Current?.Stage==BattleSettlementStage.Saved)
            {
                BattleSettlementDiagnostic failedNavigation=_settlement.MarkNavigationFailure(code);
                LogSettlement(failedNavigation,BattleUiLogCategory.Rejected);
                RecoverSavedSettlementNavigation();
                return failedNavigation;
            }
            return RejectSettlement(code);
        }
    }

    private RunSessionResult ResolveAdventureBoard(RunSessionResult result)
    {
        if (!result.Succeeded || result.Snapshot?.ActiveRun is null || result.Snapshot.TerminalSummary is not null)
            return result;
        return _run!.ApplyMutation(state => new RunMutationResult(true, null,
            new RunAdventureTransitionService().ResolveBoard(state)));
    }

    private BattleSettlementDiagnostic CompleteSettlementNavigation(RunSessionResult result,Action navigate)
    {
        long revision=result.Snapshot?.Revision??throw new InvalidOperationException("Saved settlement snapshot is missing.");
        BattleSettlementDiagnostic saved=_settlement.MarkSaved(revision);LogSettlement(saved,BattleUiLogCategory.Gameplay);
        navigate();
        BattleSettlementDiagnostic completed=_settlement.MarkNavigationCompleted();LogSettlement(completed,BattleUiLogCategory.Gameplay);
        return completed;
    }

    private BattleSettlementDiagnostic RejectSettlement(string? errorCode)
    {
        string code=errorCode??"settlement.rejected";
        BattleSettlementDiagnostic rejected=_settlement.Reject(code);
        LogSettlement(rejected,BattleUiLogCategory.Rejected);
        GD.PushError($"Battle settlement rejected: {code}");
        if(code=="save.write_failed"&&_settlementRetryCount==0)
        {
            _settlementRetryCount++;
            Callable.From(RetryRejectedSettlement).CallDeferred();
        }
        return rejected;
    }

    private BattleSettlementDiagnostic HandleSettlementFailure(RunSessionResult result)
    {
        if(HasCommittedTerminalSnapshot(result))
        {
            PureRunSaveSnapshot snapshot=result.Snapshot!;
            BattleSettlementDiagnostic saved=_settlement.MarkSaved(snapshot.Revision);
            LogSettlement(saved,BattleUiLogCategory.Gameplay);
            RecoverSavedSettlementNavigation(snapshot);
            return _settlement.Current!;
        }
        return RejectSettlement(result.ErrorCode);
    }

    private void RetryRejectedSettlement()
    {
        if(_terminalSettlementResult is null||_settlement.Current?.Stage!=BattleSettlementStage.Rejected)return;
        _settlement.Reset();
        CompleteBattle(_terminalSettlementResult);
    }

    private void RecoverSavedSettlementNavigation() => RecoverSavedSettlementNavigation(null);

    private void RecoverSavedSettlementNavigation(PureRunSaveSnapshot? knownSnapshot)
    {
        try
        {
            PureRunSaveSnapshot? snapshot=knownSnapshot;
            if(snapshot?.TerminalSummary is null)
            {
                RunStoreResult loaded=SaveStore.Load();
                if(loaded.Succeeded)snapshot=loaded.Snapshot;
            }
            if(snapshot?.TerminalSummary is null)throw new InvalidOperationException("Saved terminal summary is unavailable.");
            RouteRunState(snapshot);
            BattleSettlementDiagnostic completed=_settlement.MarkNavigationCompleted();
            LogSettlement(completed,BattleUiLogCategory.Gameplay);
        }
        catch(Exception exception)
        {
            string code=$"settlement.navigation.{exception.GetType().Name}";
            BattleSettlementDiagnostic failed=_settlement.MarkNavigationFailure(code);
            LogSettlement(failed,BattleUiLogCategory.Rejected);
            GD.PushError($"Settlement navigation recovery failed: {code}: {exception.Message}");
            if(_settlementNavigationRetryCount++==0)Callable.From(RecoverSavedSettlementNavigation).CallDeferred();
        }
    }

    private void LogSettlement(BattleSettlementDiagnostic diagnostic,BattleUiLogCategory category)
    {
        string message=$"Settlement #{diagnostic.AttemptId} {diagnostic.Stage}; encounter={diagnostic.EncounterContentId.Value}; checkpoint={diagnostic.CheckpointRevision}; saved={diagnostic.SavedRevision?.ToString()??"none"}; marker={diagnostic.Marker}; error={diagnostic.ErrorCode??"none"}";
        AddLog(new BattleUiLogEntry(category,message,"BattleSettlementDiagnostic"));
        if(_settlementStatus is not null)_settlementStatus.Text=diagnostic.ErrorCode is null?message:$"SETTLEMENT ERROR: {diagnostic.ErrorCode}";
        RefreshLog();
        GD.Print(message);
    }

    private void PlaybackStep(bool forced)
    {
        if(_battle is null||(_playbackPaused&&!forced)||(_presentationPlayer?.IsPlaying??false))return;
        BattleUiFrame? frame=_battle.DequeueAutomaticFrame();
        if(frame is not null){if(frame.Decision is { } decision)AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,$"{decision.ActorId.Value} selected {decision.Intent}{(decision.SkillId is null?string.Empty:" + "+decision.SkillId.Value)} to {decision.Destination}; target {decision.TargetId?.Value??"none"} ({decision.TargetDefinitionId?.Value??"none"}); score {decision.Score:0.##} [distance {decision.DistanceScore:0.##}, damage {decision.DamageScore:0.##}, target {decision.TargetScore:0.##}, status {decision.StatusScore:0.##}]; candidates {decision.CandidateCount}",nameof(AiDecisionEvent)));AddEvents(frame.Events);BeginPresentation(frame.Presentation,true,forced&&_playbackPaused);return;}
        RefreshBattle();TryCompleteBattleAfterDrain("automatic_queue_drained");
    }
    private void TogglePause(){_playbackPaused=!_playbackPaused;if(_stepButton is not null)_stepButton.Disabled=!_playbackPaused;_presentationPlayer?.SetPaused(_playbackPaused);_damageNumbers?.SetPaused(_playbackPaused);AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,_playbackPaused?"AI playback paused":"AI playback resumed","Playback"));if(!_playbackPaused&&_battle?.HasPendingAutomaticFrames==true&&!(_presentationPlayer?.IsPlaying??false))PlaybackStep(false);RefreshLog();}
    private void ToggleSpeed()
    {
        _playbackSpeed = _playbackSpeed switch { 1f => 2f, 2f => 4f, 4f => .5f, _ => 1f };
        _presentationPlayer?.SetSpeed(_playbackSpeed);
        _damageNumbers?.SetSpeed(_playbackSpeed);
        if (_speedButton is not null) _speedButton.Text = $"Speed {_playbackSpeed:0.#}x";
        AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,$"Playback {_playbackSpeed:0.#}x","Playback"));
        RefreshLog();
    }

    private void BeginPresentation(BattlePresentationFrame frame,bool continueAutomatic,bool pauseAfter=false)
    {
        _presentationInputLocked=true;
        _presentationAfter=frame.After;_continueAutomaticAfterPresentation=continueAutomatic;_pauseAfterCurrentFrame=pauseAfter;
        RefreshBattle(frame.Before);_presentationPlayer?.Play(frame,_actors);
        if(_playbackPaused&&!pauseAfter)_presentationPlayer?.SetPaused(true);
    }
    private void OnPresentationFrameCompleted(PresentationFrameCompletion completion)
    {
        if (completion.Recovered)
            AddLog(new BattleUiLogEntry(BattleUiLogCategory.Rejected,
                $"Recovered presentation frame {completion.Stage}: {completion.Reason}","PresentationRecoveryEvent"));
        _presentationInputLocked=false;
        BattleUiSnapshot? after=_presentationAfter;_presentationAfter=null;if(after is not null)RefreshBattle(after);
        bool shouldContinue=_continueAutomaticAfterPresentation;_continueAutomaticAfterPresentation=false;
        bool pauseAfter=_pauseAfterCurrentFrame;_pauseAfterCurrentFrame=false;
        PresentationDrainAction action=ResolvePresentationDrainAction(
            shouldContinue&&_battle?.HasPendingAutomaticFrames==true,
            _battle?.BattleResult is not null,
            _playbackPaused,
            pauseAfter);
        switch(action)
        {
            case PresentationDrainAction.DequeueFrame:
                PlaybackStep(false);
                break;
            case PresentationDrainAction.CompleteBattle:
                TryCompleteBattleAfterDrain(completion.Recovered?"presentation_recovered":"presentation_completed");
                break;
            case PresentationDrainAction.Pause:
                _playbackPaused=true;
                RefreshLog();
                break;
            default:
                RefreshBattle();
                break;
        }
    }

    private void TryCompleteBattleAfterDrain(string marker)
    {
        if(_battle?.BattleResult is not PureRunBattleResult result)return;
        LogTerminalDiagnostics(marker);
        CompleteBattle(result);
    }

    private void LogTerminalDiagnostics(string marker)
    {
        if(_battle is null)return;
        BattleTerminalDiagnostics diagnostics=_battle.TerminalDiagnostics;
        string players=string.Join(",",diagnostics.LivingPlayerUnits.Select(value=>$"{value.UnitId.Value}:{value.CurrentHealth}:{value.ControlKind}"));
        string enemies=string.Join(",",diagnostics.LivingEnemyUnits.Select(value=>$"{value.UnitId.Value}:{value.CurrentHealth}:{value.ControlKind}"));
        AddLog(new BattleUiLogEntry(BattleUiLogCategory.Gameplay,
            $"Terminal {marker}; result={diagnostics.TerminalResultGenerated}; queued={diagnostics.PendingAutomaticFrameCount}; next={diagnostics.NextAutomaticStage??"none"}; players=[{players}]; enemies=[{enemies}]",
            "BattleTerminalDiagnostics"));
        RefreshLog();
    }

    private void AddEvents(IEnumerable<BattleEvent> events){foreach(BattleEvent item in events)AddLog(FormatEvent(item));}
    private void AddLog(BattleUiLogEntry entry){if(_logs.Count>=100)_logs.RemoveAt(0);_logs.Add(entry);}
    private static BattleUiLogEntry FormatEvent(BattleEvent item)=>item switch
    {
        UnitMovedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.UnitId.Value} moved {e.Origin} → {e.Destination}",nameof(UnitMovedEvent)),
        DamageAppliedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.TargetId.Value} took {e.Amount} damage; HP {e.RemainingHealth}",nameof(DamageAppliedEvent)),
        UnitDefeatedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.UnitId.Value} was defeated",nameof(UnitDefeatedEvent)),
        CorpseCreatedEvent e=>new(BattleUiLogCategory.Gameplay,$"Corpse created at {e.Cell} from {e.UnitId.Value}",nameof(CorpseCreatedEvent)),
        CorpseConsumedEvent e=>new(BattleUiLogCategory.Gameplay,$"Corpse consumed at {e.Cell}",nameof(CorpseConsumedEvent)),
        UnitSummonedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.OwnerId.Value} summoned {e.SummonId.Value} at {e.Cell}",nameof(UnitSummonedEvent)),
        ManaRestoredEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.TargetId.Value} restored {e.Amount} MP; MP {e.CurrentMana}",nameof(ManaRestoredEvent)),
        SkillUsedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.ActorId.Value} used {e.SkillId.Value} on {e.TargetId.Value}",nameof(SkillUsedEvent)),
        SpearDroppedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.OwnerId.Value} dropped spear at {e.Cell}",nameof(SpearDroppedEvent)),
        SpearRecoveredEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.OwnerId.Value} recovered spear at {e.Cell}",nameof(SpearRecoveredEvent)),
        CommandRejectedEvent e=>new(BattleUiLogCategory.Rejected,$"{e.ActorId.Value}: {e.Reason}",nameof(CommandRejectedEvent)),
        _=>new(BattleUiLogCategory.Gameplay,item.ToString()??item.GetType().Name,item.GetType().Name)
    };
    private void RefreshLog()
    {
        _cheatConsole?.SetEntries(_logs);
        if(_eventLog is null)return;
        IEnumerable<BattleUiLogEntry> shown=_logs;if(_logFilter>0)shown=shown.Where(item=>(int)item.Category==_logFilter-1);
        _eventLog.Text=string.Join('\n',shown.Select(item=>$"[{item.EventType}] {item.Message}"));_eventLog.ScrollToLine(Math.Max(0,_eventLog.GetLineCount()-1));
    }

    private void ShowSettlement(PureRunSaveSnapshot snapshot)
    {
        _battle = null;
        if (snapshot.TerminalSummary is PureRunSummary summary) { ShowSummary(summary); return; }
        PureRunState run = snapshot.ActiveRun!;
        if ((run.Phase is PureRunPhase.AwaitingLayerFourChoice or PureRunPhase.ResolvingLayerFourNode or PureRunPhase.ReadyForLayerFive or
            PureRunPhase.AwaitingLayerSixChoice or PureRunPhase.ResolvingLayerSixNode or PureRunPhase.ReadyForBoss) &&
            run.PendingProgression.Count == 0)
        { RouteMap(run); return; }
        if(run.Phase==PureRunPhase.ReadyForLayerSix&&run.PendingProgression.Count==0){RouteMap(run);return;}
        string completed=_currentEncounterId is ContentId completedId?EncounterLabel(completedId):$"Battle {run.BattlesCompleted}";
        string next=run.Phase switch
        {
            PureRunPhase.AwaitingLayerFourChoice => "Layer 4 Map",
            PureRunPhase.ReadyForLayerSix => "Layer 6 Map",
            _ => EncounterLabel(run.EncounterContentId)
        };
        Control root = NewPage("BATTLE SETTLEMENT", $"{completed} completed → Next: {next}");
        AddRunShell(root, run, "Settlement");
        PanelAt(root, new Vector2(430, 210), new Vector2(740, 430)).Name = "SettlementResultPanel";
        string itemResult = SettlementDropLabel(run);
        LabelAt(root, $"Gold: {run.Gold}\nItems: {itemResult}\nPending Progression: {run.PendingProgression.LastOrDefault()?.CharacterId ?? "none"}\nDead: {string.Join(", ", run.Party.Where(value => value.IsDead).Select(value => value.CharacterId))}", new Vector2(480, 260), 28);
        PendingProgression? pending=run.PendingProgression.FirstOrDefault();
        bool continueRequested=false;
        Button nextButton = Button(pending is null ? $"Continue — {next}" : "Continue — Progression",()=>
        {
            if(continueRequested)return;continueRequested=true;
            if (pending is not null) ShowProgression(run, pending); else RouteRunState(snapshot);
        }); nextButton.Position = new Vector2(650, 610); nextButton.Size = new Vector2(300, 70); root.AddChild(nextButton);
    }

    private void ShowRunMap(PureRunState run)
    {
        _adventureLastBoardContentId = null;
        Control root = NewPage("PURE RUN MAP", "Read-only route preview. Drag or use the wheel to inspect; choose exits in the current Tile scene.");
        PureRunFlowSnapshot flow = _flowProjector.Project(run, _runDefinition!, MapDefinition);
        AddRunShell(root, run, "Map");
        PanelAt(root, new Vector2(1120, 175), new Vector2(430, 590), GodotTacticsTheme.Card).Name = "MapDetailPanel";
        _mapView = new GodotRogueMapView { Position = new Vector2(60, 135), Size = new Vector2(1040, 700) };
        _mapView.NodePressed += nodeId => SetStatus($"Map preview only: {nodeId}. Choose through the current Tile scene exit.");
        _mapView.NodeHovered += node =>
        {
            if (_mapDetail is null) return;
            _mapDetail.Text = node is null
                ? "Hover a node to inspect it."
                : $"{node.Title}  |  {node.State}\n{node.ContentId?.Value ?? node.NodeId}\n{node.UnavailableReason ?? "Ready"}";
        };
        root.AddChild(_mapView);
        _mapView.SetSnapshot(flow.Map!, true);
        _mapDetail = LabelAt(root, "Hover a node to inspect it.", new Vector2(1140, 220), 19);
        _mapDetail.Size = new Vector2(390, 180); _mapDetail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (run.PendingProgression.FirstOrDefault() is PendingProgression pending)
        {
            LabelAt(root, "Progression must be completed before the next node.", new Vector2(1140, 430), 18);
            root.AddChild(PlaceControl(Button("Complete Progression", () => ShowProgression(run, pending)),
                new Vector2(1140, 475), new Vector2(360, 58)));
        }
        root.AddChild(PlaceControl(Button("Inventory", AdventureNavigation(() => ShowInventory(run))), new Vector2(1140, 560), new Vector2(360, 58)));
        root.AddChild(PlaceControl(Button("Return to Current Scene", () => RouteRunState(new PureRunSaveSnapshot(run.Revision, run, null))),
            new Vector2(1140, 630), new Vector2(360, 58)));
        _status = LabelAt(root, string.Empty, new Vector2(1140, 790), 16);
    }

    private void AddRunShell(Control root, PureRunState run, string page)
    {
        SetInputContext(RunInputContext.Adventure);
        var panel = new ColorRect
        {
            Name = "RunShellPanel",
            Color = new Color("1c2a33e6"),
            Position = new Vector2(1115, 125),
            Size = new Vector2(420, 82),
            ZIndex = StartPageUiZIndex
        };
        root.AddChild(panel);
        int currentHealth = run.Party.Sum(value => value.CurrentHealth);
        int maxHealth = run.Party.Sum(value => value.MaxHealth);
        int currentMana = run.Party.Sum(value => value.CurrentMana);
        int maxMana = run.Party.Sum(value => value.MaxMana);
        Label summary = LabelAt(root,
            $"{page}  •  Gold {run.Gold}  •  Bag {run.BackpackConsumables.Count + run.BackpackEquipment.Count}\n" +
            $"Party {run.Party.Count}  •  HP {currentHealth}/{maxHealth}  •  MP {currentMana}/{maxMana}",
            new Vector2(1130, 138), 14);
        summary.Name = "RunShellSummary";
        summary.Size = new Vector2(390, 62);
        summary.ZIndex = StartPageUiZIndex + 1;
    }

    private PureRunMapDefinition MapDefinition => _mapDefinition ??
        throw new InvalidOperationException("Authoritative run map is not loaded.");

    private void RouteLayerFour(PureRunState run)
    {
        switch(run.NodeTransaction?.Kind)
        {
            case PureRunNodeKind.Battle: BeginLayerFourBattle(); break;
            case PureRunNodeKind.Rest: ShowAdventureNodeEntry(run, PureRunNodeKind.Rest); break;
            case PureRunNodeKind.Store: ShowAdventureNodeEntry(run, PureRunNodeKind.Store); break;
            case PureRunNodeKind.Mystery: ShowAdventureEventEntry(run, "CursedChestMimic"); break;
            case PureRunNodeKind.Treasure: ShowAdventureNodeEntry(run, PureRunNodeKind.Treasure); break;
            default: SetStatus("layer4.route_missing"); break;
        }
    }

    private void ShowAdventureEventEntry(PureRunState run, string contextKind)
    {
        bool escort = run.EscortState is { Lifecycle: RunEscortLifecycle.Traveling } escortState &&
            run.NodeTransaction?.NodeId == escortState.DestinationNodeId;
        if (escort) contextKind = "LostVillagerEscort";
        bool altar = contextKind == "FallenAltarGuardian";
        string objectId = escort ? "lost-villager" : altar ? "fallen-altar" : "cursed-chest";
        ContentId boardId = new($"adventure-board.pure-run.event.{contextKind.ToLowerInvariant()}");
        if (run.AdventureState?.BoardContentId != boardId)
        {
            RunSessionResult entered = _run!.ApplyMutation(state => new RunMutationResult(true, null,
                new RunAdventureTransitionService().EnterBoard(state, boardId)));
            if (!entered.Succeeded) { SetStatus(entered.ErrorCode); return; }
            ShowAdventureEventEntry(entered.Snapshot!.ActiveRun!, contextKind);
            return;
        }
        AdventureObjectKind kind = escort ? AdventureObjectKind.Npc : altar ? AdventureObjectKind.Altar : AdventureObjectKind.Chest;
        string description = escort ? "The lost villager must survive the ambush." : altar ? "The altar is guarded." : "The chest moves when approached.";
        Control root = NewPage($"{LayerLabel(run)} — TILE EVENT", description);
        AddRunShell(root, run, "Event");
        string nodeId = run.NodeTransaction?.NodeId ?? RunAdventureTransitionService.CurrentNodeId(run);
        AdventureMapTemplateDefinition? residentTemplate = null;
        GodotAdventureMapInstance? residentMap = null;
        if (_actContent?.Blueprint.Bindings.TryGetValue(nodeId, out PureRunNodeContentBinding? binding) == true &&
            binding.EventContext.ToString() == contextKind && _actWorld is { IsPrepared: true } host &&
            host.Nodes.TryGetValue(nodeId, out GodotAdventureMapInstance? map) &&
            map.Surface.Template?.ToCoreDefinition().ContentId == binding.TemplateId)
        {
            residentTemplate = map.Surface.Template.ToCoreDefinition();
            residentMap = map;
        }
        AdventureActorPlacement[] actors = residentTemplate is null
            ? AdventurePlacements(run)
            : EnsureAdventureSession(run, residentTemplate).Placements();
        AdventureBoardDefinition definition;
        if (residentTemplate is not null)
        {
            definition = residentTemplate.Board with { ContentId = boardId, Actors = actors };
        }
        else
        {
            GridPoint[] perimeter = Enumerable.Range(0, 10).SelectMany(value =>
                    new[] { new GridPoint(value, 0), new GridPoint(value, 9) })
                .Concat(Enumerable.Range(1, 8).SelectMany(value => new[] { new GridPoint(0, value), new GridPoint(9, value) }))
                .Distinct().ToArray();
            AdventureBoardObject[] exits = CreateExitObjects(run, nodeId, false);
            definition = new AdventureBoardDefinition(boardId, 10, 10, perimeter,
                new[] { new AdventureBoardObject(objectId, kind, new GridPoint(7, 5), false, false) }.Concat(exits).ToArray(), actors,
                new GridPoint(1, 5), new GridPoint(8, 5));
        }
        _adventureBoard = new GodotAdventureBoardView { Name = "AdventureEventBoard" };
        _adventureLastBoardContentId = definition.ContentId.Value;
        root.AddChild(_adventureBoard);
        if (residentMap is not null)
        {
            GodotNodeActivationLease lease = _actWorld!.ActivateNode(nodeId);
            foreach (var node in _actWorld.Nodes) node.Value.Visible = node.Key == nodeId;
            _actWorld.Visible = true;
            _adventureBoard.BindResidentSurface(_actWorld, lease, residentMap.Surface);
        }
        ConfigureAdventureBoard(_adventureBoard, run, definition);
        WireAdventureMovement(run, definition, changed => ShowAdventureEventEntry(changed, contextKind));
        _adventureBoard.ObjectPressed += pressed =>
        {
            if (pressed.StartsWith("exit:", StringComparison.Ordinal)) { SetStatus("Complete the event before leaving."); return; }
            if (pressed != objectId) return;
            GridPoint leaderCell = AdventureLeaderCell(run);
            if (!RunAdventureTransitionService.IsAdjacent(leaderCell, definition.Objects.Single(value => value.ObjectId == objectId).Cell))
            { SetStatus("Move the leader next to the event object first."); return; }
            try
            {
                var encounterId = new ContentId("encounter.pure-run.n1");
                RunSessionResult begun = _run!.ApplyMutation(state =>
                {
                    PureRunState source = state;
                    if (contextKind == "LostVillagerEscort")
                    {
                        RunEscortTransition escortPending = new PureRunEscortService().BeginBattle(source);
                        if (!escortPending.Succeeded)
                            return new RunMutationResult(false, escortPending.RejectionCode, source);
                        source = escortPending.State;
                    }
                    RunAdventureEventContextKind eventContext = Enum.Parse<RunAdventureEventContextKind>(contextKind);
                    source = new RunAdventureTransitionService().BeginEventBattle(source, eventContext,
                        source.NodeTransaction?.NodeId ?? "event-node", objectId);
                    LayerFourNodeResolution result = new PureRunLayerFourNodeService().BeginEventBattle(source, encounterId);
                    return new RunMutationResult(result.Succeeded, result.RejectionCode, result.State);
                });
                if (!begun.Succeeded || begun.Snapshot?.ActiveRun?.Checkpoint is null) { SetStatus(begun.ErrorCode); return; }
                PureRunState pending = begun.Snapshot.ActiveRun;
                StartBattle(AdventureEncounterRequest(pending, encounterId));
            }
            catch (Exception exception)
            {
                SetStatus($"event_battle.{exception.GetType().Name}:{exception.Message}");
                GD.PushError($"Event battle failed: {exception}");
            }
        };
        _status = LabelAt(root, $"Interact: {objectId}", new Vector2(470, 810), 18);
    }

    private void ShowPostEventScene(PureRunState run, string contextKind)
    {
        _battle = null;
        bool escort = contextKind == "LostVillagerEscort";
        bool altar = contextKind == "FallenAltarGuardian";
        string objectId = escort ? "lost-villager" : altar ? "fallen-altar" : "cursed-chest";
        string eventResolution = escort
            ? run.EscortState?.Lifecycle == RunEscortLifecycle.Completed ? "EscortCompleted" : "EscortFailed"
            : altar ? "FallenAltarGuardianDefeated" : "CursedChestMimicDefeated";
        string resolvedDescription = escort
            ? run.EscortState?.ProtectedNpcAlive == true ? "The villager survived the ambush." : "The villager was lost."
            : altar ? "The altar is quiet." : "The mimic has collapsed into an opened chest.";
        Control root = NewPage($"{LayerLabel(run)} — EVENT RESOLVED", resolvedDescription);
        AddRunShell(root, run, "Event Result");
        string nodeId = run.NodeTransaction?.NodeId ?? RunAdventureTransitionService.CurrentNodeId(run);
        AdventureMapTemplateDefinition? residentTemplate = null;
        GodotAdventureMapInstance? residentMap = null;
        if (_actContent?.Blueprint.Bindings.TryGetValue(nodeId, out PureRunNodeContentBinding? binding) == true &&
            binding.EventContext.ToString() == contextKind && _actWorld is { IsPrepared: true } host &&
            host.Nodes.TryGetValue(nodeId, out GodotAdventureMapInstance? map) &&
            map.Surface.Template?.ToCoreDefinition().ContentId == binding.TemplateId)
        {
            residentTemplate = map.Surface.Template.ToCoreDefinition();
            residentMap = map;
        }
        AdventureActorPlacement[] actors = residentTemplate is null
            ? AdventurePlacements(run)
            : EnsureAdventureSession(run, residentTemplate).Placements();
        AdventureBoardDefinition definition;
        if (residentTemplate is not null)
        {
            AdventureBoardObject[] resolvedObjects = escort && run.EscortState?.ProtectedNpcAlive != true
                ? residentTemplate.Board.Objects.Where(value => value.ObjectId != objectId).ToArray()
                : residentTemplate.Board.Objects.ToArray();
            definition = residentTemplate.Board with
            {
                ContentId = run.AdventureState!.BoardContentId,
                Actors = actors,
                Objects = resolvedObjects
            };
        }
        else
        {
            AdventureBoardObject[] exits = CreateExitObjects(run, nodeId, true);
            definition = new AdventureBoardDefinition(run.AdventureState!.BoardContentId, 10, 10, [],
                new[] { new AdventureBoardObject(objectId, escort ? AdventureObjectKind.Npc : altar ? AdventureObjectKind.Altar : AdventureObjectKind.Chest, new GridPoint(7, 5), false, false) }.Concat(exits).ToArray(), actors,
                new GridPoint(1, 5), new GridPoint(8, 5));
        }
        _adventureBoard = new GodotAdventureBoardView { Name = "ResolvedAdventureEventBoard" };
        _adventureLastBoardContentId = definition.ContentId.Value;
        root.AddChild(_adventureBoard);
        if (residentMap is not null)
        {
            GodotNodeActivationLease lease = _actWorld!.ActivateNode(nodeId);
            foreach (var node in _actWorld.Nodes) node.Value.Visible = node.Key == nodeId;
            _actWorld.Visible = true;
            _adventureBoard.BindResidentSurface(_actWorld, lease, residentMap.Surface);
        }
        ConfigureAdventureBoard(_adventureBoard, run, definition);
        WireAdventureMovement(run, definition, changed => ShowPostEventScene(changed, contextKind));
        _adventureBoard.ObjectPressed += pressed => HandleExitPointer(run, definition, pressed);
        root.AddChild(PlaceControl(Button("Continue", AdventureNavigation(() => RouteMap(run))), new Vector2(650, 740), new Vector2(300, 50)));
        _status = LabelAt(root, eventResolution, new Vector2(470, 810), 18);
    }

    private void ShowAdventureNodeEntry(PureRunState run, PureRunNodeKind kind)
    {
        string objectId = kind switch
        {
            PureRunNodeKind.Rest => "rest-campfire",
            PureRunNodeKind.Store => "store-merchant",
            PureRunNodeKind.Treasure => "treasure-chest",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        AdventureObjectKind objectKind = kind switch
        {
            PureRunNodeKind.Rest => AdventureObjectKind.Campfire,
            PureRunNodeKind.Store => AdventureObjectKind.Merchant,
            PureRunNodeKind.Treasure => AdventureObjectKind.Chest,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        ContentId boardId = new($"adventure-board.pure-run.{kind.ToString().ToLowerInvariant()}");
        if (run.AdventureState?.BoardContentId != boardId)
        {
            RunSessionResult entered = _run!.ApplyMutation(state => new RunMutationResult(true, null,
                new RunAdventureTransitionService().EnterBoard(state, boardId)));
            if (!entered.Succeeded) { SetStatus(entered.ErrorCode); return; }
            ShowAdventureNodeEntry(entered.Snapshot!.ActiveRun!, kind);
            return;
        }
        Control root = NewPage($"{LayerLabel(run)} — TILE {kind.ToString().ToUpperInvariant()}", "Move through the scene and interact with the highlighted object.");
        AddRunShell(root, run, kind.ToString());
        AdventureActorPlacement[] actors = AdventurePlacements(run);
        GridPoint[] perimeter = Enumerable.Range(0, 10).SelectMany(value =>
                new[] { new GridPoint(value, 0), new GridPoint(value, 9) })
            .Concat(Enumerable.Range(1, 8).SelectMany(value => new[] { new GridPoint(0, value), new GridPoint(9, value) }))
            .Distinct().ToArray();
        bool exitsOpen = kind == PureRunNodeKind.Store;
        AdventureBoardObject[] exits = CreateExitObjects(run, run.NodeTransaction?.NodeId ?? RunAdventureTransitionService.CurrentNodeId(run), exitsOpen);
        AdventureBoardDefinition definition = new(boardId, 10, 10, perimeter,
            new[] { new AdventureBoardObject(objectId, objectKind, new GridPoint(7, 5), false, false) }.Concat(exits).ToArray(), actors,
            new GridPoint(1, 5), new GridPoint(8, 5));
        _adventureBoard = new GodotAdventureBoardView { Name = $"{kind}AdventureBoard" };
        _adventureLastBoardContentId = definition.ContentId.Value;
        root.AddChild(_adventureBoard);
        ConfigureAdventureBoard(_adventureBoard, run, definition);
        WireAdventureMovement(run, definition, changed => ShowAdventureNodeEntry(changed, kind));
        _adventureBoard.ObjectPressed += pressed =>
        {
            if (pressed.StartsWith("exit:", StringComparison.Ordinal))
            {
                if (kind != PureRunNodeKind.Store) { SetStatus($"Complete {kind} before leaving."); return; }
                HandleExitPointer(run, definition, pressed);
                return;
            }
            if (pressed != objectId) return;
            GridPoint leaderCell = AdventureLeaderCell(run);
            if (!RunAdventureTransitionService.IsAdjacent(leaderCell, definition.Objects.Single(value => value.ObjectId == objectId).Cell))
            { SetStatus("Move the leader next to the interaction object first."); return; }
            switch (kind)
            {
                case PureRunNodeKind.Rest: ShowRest(run); break;
                case PureRunNodeKind.Store: ShowStore(run); break;
                case PureRunNodeKind.Treasure: ShowTreasure(run); break;
            }
        };
        _status = LabelAt(root, $"Interact: {objectId}", new Vector2(470, 810), 18);
    }

    private AdventureExplorationSession EnsureAdventureSession(PureRunState run)
    {
        if (_adventureSession is null || !_adventureSession.Matches(run))
            _adventureSession = AdventureExplorationSession.Restore(run,
                [new GridPoint(2, 5), new GridPoint(1, 4), new GridPoint(1, 6)]);
        return _adventureSession;
    }

    private AdventureExplorationSession EnsureAdventureSession(PureRunState run,
        AdventureMapTemplateDefinition template)
    {
        if (_adventureSession is null || !_adventureSession.Matches(run))
            _adventureSession = AdventureExplorationSession.Restore(run, template);
        return _adventureSession;
    }

    private AdventureActorPlacement[] AdventurePlacements(PureRunState run) => EnsureAdventureSession(run).Placements();

    private GridPoint AdventureLeaderCell(PureRunState run) =>
        EnsureAdventureSession(run).CellOf(run.AdventureState?.LeaderId ?? throw new InvalidOperationException("adventure.state_missing"));

    private static void ConfigureAdventureCornerOverlays(Control root)
    {
        if (root.GetNodeOrNull<PanelContainer>("PageHeader") is { } header)
        {
            header.Position = AdventureHeaderRect.Position;
            header.Size = AdventureHeaderRect.Size;
            if (header.FindChild("PageTitle", true, false) is Label title)
                title.AddThemeFontSizeOverride("font_size", 26);
            if (header.FindChild("PageSubtitle", true, false) is Label subtitle)
            {
                subtitle.AddThemeFontSizeOverride("font_size", 13);
                subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            }
        }
        if (root.GetNodeOrNull<ColorRect>("RunShellPanel") is { } shell)
        {
            shell.Position = AdventureRunShellRect.Position;
            shell.Size = AdventureRunShellRect.Size;
        }
        if (root.GetNodeOrNull<Label>("RunShellSummary") is { } summary)
        {
            summary.Position = AdventureRunShellRect.Position + new Vector2(16, 14);
            summary.Size = AdventureRunShellRect.Size - new Vector2(32, 24);
        }
    }

    private void ConfigureAdventureBoard(GodotAdventureBoardView view, PureRunState run,
        AdventureBoardDefinition definition)
    {
        if (view.GetParent() is Control root) ConfigureAdventureCornerOverlays(root);
        AdventureExplorationSession session = EnsureAdventureSession(run);
        session.SetLeader(run.AdventureState?.LeaderId ?? throw new InvalidOperationException("adventure.state_missing"));
        var definitions = new Dictionary<string, UnitDefinitionResource>(StringComparer.Ordinal);
        foreach (RunCharacterState member in run.Party)
        {
            if (!_unitResources.TryGetValue(member.UnitContentId, out UnitDefinitionResource? resource))
                throw new InvalidOperationException($"Adventure actor '{member.CharacterId}' is missing Unit resource '{member.UnitContentId.Value}'.");
            definitions[member.CharacterId] = resource;
        }
        string leaderId = run.AdventureState?.LeaderId ?? throw new InvalidOperationException("adventure.state_missing");
        view.ConfigureActors(definitions, leaderId, _presentationProfile ?? new StandardUnitPresentationResource(), _playbackSpeed);
        view.SetBoard(definition);
        view.MovementRejected += SetStatus;
    }

    private Action AdventureNavigation(Action action) => () =>
    {
        if (_adventureBoard?.IsMovementLocked == true || _startCampView?.IsMovementLocked == true)
        {
            SetStatus("adventure.movement_in_progress");
            return;
        }
        action();
    };

    private void WireAdventureMovement(PureRunState run, AdventureBoardDefinition definition, Action<PureRunState> rerender)
    {
        GodotAdventureBoardView board = _adventureBoard!;
        board.ManualMovementRequested += () => _pendingAdventureExit = null;
        board.ActorPressed += actorId =>
        {
            AdventureExplorationSession session = EnsureAdventureSession(run);
            RunSessionResult changed = _run!.ApplyMutation(state => new RunMutationResult(true, null,
                new RunAdventureTransitionService().SelectLeader(session.Hydrate(state), actorId)));
            if (!changed.Succeeded) { SetStatus(changed.ErrorCode); return; }
            session.SetLeader(actorId);
            rerender(changed.Snapshot!.ActiveRun!);
        };
        board.MovementInterruptedAtCell += cell =>
        {
            _pendingAdventureExit = null;
            AdventureExplorationSession session = EnsureAdventureSession(run);
            string leaderId = run.AdventureState?.LeaderId ?? throw new InvalidOperationException("adventure.state_missing");
            session.SetCell(leaderId, cell);
        };
        board.MovementCompleted += completion =>
        {
            if (completion.Origin == completion.Destination)
            {
                SetStatus("Leader returned to the starting cell; no movement was committed.");
                return;
            }
            AdventureExplorationSession session = EnsureAdventureSession(run);
            RunSessionResult changed = _run!.ApplyMutation(state =>
            {
                try
                {
                    PureRunState hydrated = session.Hydrate(state);
                    return new RunMutationResult(true, null,
                        new RunAdventureTransitionService().MoveLeader(hydrated, definition, completion.Destination));
                }
                catch (InvalidOperationException error) { return new RunMutationResult(false, error.Message, state); }
            });
            if (!changed.Succeeded)
            {
                PureRunState fallback = changed.Snapshot?.ActiveRun ?? run;
                rerender(fallback);
                SetStatus(changed.ErrorCode ?? "adventure.movement_commit_failed");
                GD.PushError("[AdventureMovement] outcome=commit-failed error=" +
                    (changed.ErrorCode ?? "adventure.movement_commit_failed"));
                return;
            }
            session.SetCell(completion.ActorId, completion.Destination);
            PureRunState changedRun = changed.Snapshot!.ActiveRun!;
            PendingAdventureExit? pending = _pendingAdventureExit;
            _pendingAdventureExit = null;
            if (pending is not null && pending.RunId == changedRun.RunId && ReferenceEquals(pending.Page, _page) &&
                RunAdventureTransitionService.IsAdjacent(completion.Destination, pending.ExitCell))
            {
                HandleAdventureExit(changedRun, pending.TargetNodeId);
                return;
            }
            rerender(changedRun);
        };
    }

    private AdventureBoardObject[] CreateExitObjects(PureRunState run, string currentNodeId, bool unlocked)
    {
        PureRunMapNodeDefinition[] successors = MapDefinition.Connections
            .Where(edge => edge.FromNodeId == currentNodeId)
            .Select(edge => MapDefinition.Nodes.Single(node => node.NodeId == edge.ToNodeId))
            .Where(node => run.MapState?.ReachableNodeIds.Count is not > 0 ||
                run.MapState.ReachableNodeIds.Contains(node.NodeId, StringComparer.Ordinal) ||
                node.Layer is 1 or 2 or 3 or 5 or 7)
            .OrderBy(node => node.Lane).ThenBy(node => node.NodeId, StringComparer.Ordinal).ToArray();
        int[] xs = successors.Length switch
        {
            0 => [],
            1 => [8],
            2 => [3, 7],
            3 => [2, 5, 8],
            4 => [2, 4, 6, 8],
            _ => Enumerable.Range(0, successors.Length).Select(index => 1 + index * 8 / Math.Max(1, successors.Length - 1)).ToArray()
        };
        return successors.Select((node, index) => new AdventureBoardObject($"exit:{node.NodeId}", AdventureObjectKind.Exit,
            new GridPoint(xs[index], 8), false, false, node.NodeId, !unlocked)).ToArray();
    }

    private void HandleExitPointer(PureRunState run, AdventureBoardDefinition definition, string objectId)
    {
        AdventureBoardObject? exit = definition.Objects.FirstOrDefault(value => value.ObjectId == objectId && value.Kind == AdventureObjectKind.Exit);
        if (exit?.TargetNodeId is null) return;
        if (exit.IsLocked) { SetStatus("Complete the current node before leaving."); return; }
        GridPoint leaderCell = AdventureLeaderCell(run);
        if (RunAdventureTransitionService.IsAdjacent(leaderCell, exit.Cell))
        {
            HandleAdventureExit(run, exit.TargetNodeId);
            return;
        }
        if (_adventureBoard is null || _page is null) return;
        GridPoint[] occupied = _adventureBoard.ActorCells
            .Where(value => value.Key != run.AdventureState?.LeaderId)
            .Select(value => value.Value).ToArray();
        AdventureApproachRoute? route = AdventureBoardPathfinder.FindApproach(definition, leaderCell, exit.Cell, occupied);
        if (route is null) { SetStatus("adventure.exit_unreachable"); return; }
        _pendingAdventureExit = new PendingAdventureExit(run.RunId, exit.TargetNodeId, exit.Cell, _page);
        if (!_adventureBoard.TryRequestLeaderMovement(route.Destination, out string? rejection))
        {
            _pendingAdventureExit = null;
            SetStatus(rejection ?? "adventure.exit_unreachable");
            return;
        }
        SetStatus("Moving to exit…");
    }

    private void HandleAdventureExit(PureRunState run, string targetNodeId)
    {
        PureRunMapNodeDefinition targetNode = MapDefinition.Nodes.Single(node => node.NodeId == targetNodeId);
        if (targetNode.Kind is (PureRunNodeKind.Battle or PureRunNodeKind.Elite or PureRunNodeKind.Boss) &&
            !IsSupportedDeterministicBattleNode(targetNodeId))
        {
            SetStatus("battle_node.route_missing:" + targetNodeId);
            return;
        }
        if (run.NodeTransaction is { Kind: PureRunNodeKind.Store, Committed: false })
        {
            RunSessionResult left = _run!.ApplyLayerFourMutation(state =>
            {
                LayerFourNodeResolution result = new PureRunLayerFourNodeService().LeaveStore(state);
                return new RunMutationResult(result.Succeeded, result.RejectionCode, result.State);
            });
            if (!left.Succeeded) { SetStatus(left.ErrorCode); return; }
            run = left.Snapshot!.ActiveRun!;
        }
        if (targetNodeId.StartsWith("layer_06_", StringComparison.Ordinal) && run.Phase == PureRunPhase.ReadyForLayerSix)
        {
            RunSessionResult unlocked = _run!.ApplyFullRunTransition(state =>
                new PureRunFullRunService(_consumables.Keys).UnlockLayerSix(state, MapDefinition));
            if (!unlocked.Succeeded) { SetStatus(unlocked.ErrorCode); return; }
            run = unlocked.Snapshot!.ActiveRun!;
        }
        RunSessionResult moved = _run!.ApplyMutation(state =>
        {
            try
            {
                var service = new RunAdventureTransitionService();
                PureRunState exited = service.CommitExit(state, MapDefinition, targetNodeId);
                PureRunState entered = service.EnterBoard(exited, AdventureBoardIdForNode(targetNodeId));
                if (targetNodeId.StartsWith("layer_04_", StringComparison.Ordinal) ||
                    targetNodeId.StartsWith("layer_06_", StringComparison.Ordinal))
                {
                    LayerFourNodeResolution selected = new PureRunLayerFourNodeService().SelectNode(entered, MapDefinition, targetNodeId);
                    return new RunMutationResult(selected.Succeeded, selected.RejectionCode, selected.State);
                }
                return new RunMutationResult(true, null, entered);
            }
            catch (InvalidOperationException error) { return new RunMutationResult(false, error.Message, state); }
        });
        if (!moved.Succeeded) { SetStatus(moved.ErrorCode); return; }
        PureRunState entered = moved.Snapshot!.ActiveRun!;
        if (targetNode.Kind is PureRunNodeKind.Battle or PureRunNodeKind.Elite or PureRunNodeKind.Boss)
        {
            BeginDeterministicBattleNode(targetNodeId);
            return;
        }
        if (targetNodeId.StartsWith("layer_04_", StringComparison.Ordinal)) { RouteLayerFour(entered); return; }
        if (targetNodeId.StartsWith("layer_06_", StringComparison.Ordinal)) { RouteLayerSixNode(entered); return; }
        ShowResolvedAdventureNode(entered);
    }

    private static bool IsSupportedDeterministicBattleNode(string nodeId) => nodeId is
        "layer_01_battle" or "layer_02_battle" or "layer_03_battle" or "layer_04_battle" or
        "layer_05_battle" or "layer_06_battle" or "layer_07_battle";

    private void BeginDeterministicBattleNode(string nodeId)
    {
        Action? begin = nodeId switch
        {
            "layer_01_battle" or "layer_02_battle" or "layer_03_battle" => BeginReadyEncounter,
            "layer_04_battle" => BeginLayerFourBattle,
            "layer_05_battle" => BeginLayerFive,
            "layer_06_battle" => BeginLayerSixBattle,
            "layer_07_battle" => BeginBoss,
            _ => null
        };
        if (begin is null) { SetStatus("battle_node.route_missing:" + nodeId); return; }
        begin();
    }

    private void ShowResolvedAdventureNode(PureRunState run)
    {
        string nodeId = RunAdventureTransitionService.CurrentNodeId(run);
        ContentId boardId = run.AdventureState!.BoardContentId;
        Control root = NewPage($"{nodeId} — RESOLVED", "Choose one directly reachable exit.");
        AddRunShell(root, run, "Resolved Node");
        AdventureActorPlacement[] actors = AdventurePlacements(run);
        AdventureBoardDefinition definition = CreateNodeBoard(boardId, actors, CreateExitObjects(run, nodeId, true));
        _adventureBoard = new GodotAdventureBoardView { Name = "ResolvedAdventureNodeBoard" };
        root.AddChild(_adventureBoard);
        ConfigureAdventureBoard(_adventureBoard, run, definition);
        WireAdventureMovement(run, definition, ShowResolvedAdventureNode);
        _adventureBoard.ObjectPressed += pressed => HandleExitPointer(run, definition, pressed);
        root.AddChild(PlaceControl(Button("Inventory", AdventureNavigation(() => ShowInventory(run))), new Vector2(1130, 650), new Vector2(360, 58)));
        root.AddChild(PlaceControl(Button("Map Preview", AdventureNavigation(() => ShowRunMap(run))), new Vector2(1130, 720), new Vector2(360, 58)));
        _status = LabelAt(root, "Node resolved", new Vector2(470, 810), 18);
    }

    private static AdventureBoardDefinition CreateNodeBoard(ContentId boardId, IReadOnlyList<AdventureActorPlacement> actors,
        IReadOnlyList<AdventureBoardObject> objects)
    {
        GridPoint[] perimeter = Enumerable.Range(0, 10).SelectMany(value =>
                new[] { new GridPoint(value, 0), new GridPoint(value, 9) })
            .Concat(Enumerable.Range(1, 8).SelectMany(value => new[] { new GridPoint(0, value), new GridPoint(9, value) }))
            .Distinct().ToArray();
        return new AdventureBoardDefinition(boardId, 10, 10, perimeter, objects, actors,
            new GridPoint(1, 5), new GridPoint(8, 4));
    }

    private void RouteMap(PureRunState run)
    {
        RouteRunState(new PureRunSaveSnapshot(run.Revision, run, null));
    }

    private void BindRunUiScope(string runId)
    {
        if (string.Equals(_uiRunId, runId, StringComparison.Ordinal)) return;
        _uiRunId = runId;
        _progressionDrafts.Clear();
        _inventoryCharacterId = null;
        _inventoryEquipmentTab = true;
        _inventorySelectedInstanceId = null;
    }

    private void ResetRunUiScope()
    {
        _uiRunId = null;
        _progressionDrafts.Clear();
        _inventoryCharacterId = null;
        _inventoryEquipmentTab = true;
        _inventorySelectedInstanceId = null;
    }

    private void RouteRunState(PureRunSaveSnapshot snapshot)
    {
        if (snapshot.TerminalSummary is PureRunSummary summary) { ResetRunUiScope(); ShowSummary(summary); return; }
        if (snapshot.PendingRunSetup is not null) { ResetRunUiScope(); ShowNewRunSetup(snapshot); return; }
        PureRunState run = snapshot.ActiveRun ?? throw new InvalidOperationException("Active run is missing.");
        if (!EnsureRunActPrepared(run, () => RouteRunState(snapshot))) return;
        BindRunUiScope(run.RunId);
        if(run.Phase==PureRunPhase.ResolvingLayerFourNode){RouteLayerFour(run);return;}
        if(run.Phase==PureRunPhase.ResolvingLayerSixNode){RouteLayerSixNode(run);return;}
        if(run.Phase==PureRunPhase.PendingBattle&&run.Checkpoint is not null)
        {
            RunSessionResult resumed = _run!.ResumeRun();
            if (!resumed.Succeeded || resumed.EncounterRequest is null || resumed.EncounterRequest.RunId != run.RunId)
            { SetStatus(resumed.ErrorCode ?? "battle.stale_request"); return; }
            StartBattle(resumed.EncounterRequest);
            return;
        }
        if (run.PendingProgression.FirstOrDefault() is PendingProgression pending)
        {
            ShowProgression(run, pending);
            return;
        }
        if (run.AdventureState is { Lifecycle: RunAdventureLifecycle.InitialExploration }) ShowInitialAdventure(run);
        else if (run.AdventureState is { Lifecycle: RunAdventureLifecycle.MapActive } adventure &&
                 !adventure.BoardContentId.Value.EndsWith(".resolved", StringComparison.Ordinal) &&
                 TryGetAdventureBoardNodeId(adventure.BoardContentId, out string nodeId) &&
                 MapDefinition.Nodes.Single(node => node.NodeId == nodeId).Kind is PureRunNodeKind.Battle or PureRunNodeKind.Elite or PureRunNodeKind.Boss)
        {
            BeginDeterministicBattleNode(nodeId);
        }
        else ShowResolvedAdventureNode(run);
    }

    private static bool TryGetAdventureBoardNodeId(ContentId boardId, out string nodeId)
    {
        const string marker = ".node.";
        int index = boardId.Value.IndexOf(marker, StringComparison.Ordinal);
        nodeId = index < 0 ? string.Empty : boardId.Value[(index + marker.Length)..];
        if (nodeId.EndsWith(".resolved", StringComparison.Ordinal)) nodeId = nodeId[..^".resolved".Length];
        nodeId = nodeId.Replace('-', '_');
        return nodeId.Length > 0;
    }

    private static ContentId AdventureBoardIdForNode(string nodeId) =>
        new($"adventure-board.pure-run.node.{nodeId.Replace('_', '-')}");

    private void BeginLayerFourBattle()
    {
        var encounterId=new ContentId("encounter.pure-run.n4");
        RunSessionResult result=_run!.ApplyMutation(state=>
        {
            LayerFourNodeResolution begun=new PureRunLayerFourNodeService().BeginN4(state,encounterId);
            return new RunMutationResult(begun.Succeeded,begun.RejectionCode,begun.State);
        });
        if(!result.Succeeded||result.Snapshot?.ActiveRun?.Checkpoint is null){SetStatus(result.ErrorCode);return;}
        PureRunState pending=result.Snapshot.ActiveRun;StartBattle(AdventureEncounterRequest(pending, encounterId));
    }

    private void ShowRest(PureRunState run)
    {
        Control root=NewPage($"{LayerLabel(run)} — REST","Preview: living party members recover ceil(30% max HP/MP); dead characters remain dead.");
        AddRunShell(root,run,"Rest");
        LabelAt(root,string.Join('\n',run.Party.Select(c=>$"{c.CharacterId}: HP {c.CurrentHealth} → {(c.IsDead?c.CurrentHealth:Math.Min(c.MaxHealth,c.CurrentHealth+(int)Math.Ceiling(c.MaxHealth*.3)))} / {c.MaxHealth}, MP {c.CurrentMana} → {(c.IsDead?c.CurrentMana:Math.Min(c.MaxMana,c.CurrentMana+(int)Math.Ceiling(c.MaxMana*.3)))} / {c.MaxMana}")),new Vector2(360,250),26);
        root.AddChild(PlaceControl(Button("Confirm Rest",()=>CommitLayerFour(state=>new PureRunLayerFourNodeService().ConfirmRest(state))),new Vector2(650,650),new Vector2(300,65)));
    }

    private void ShowStore(PureRunState run)
    {
        if(run.MapState?.StoreOffers is not {Count:>0})
        {
            RunStoreOffer[] gear=_equipment.Values.Select(v=>new RunStoreOffer(v.ContentId,v.Price,false)).ToArray();
            RunStoreOffer[] items=_consumables.Values.Select(v=>new RunStoreOffer(v.ContentId,v.Price,true)).ToArray();
            RunSessionResult opened=_run!.ApplyMutation(state=>{LayerFourNodeResolution r=new PureRunLayerFourNodeService().OpenStore(state,gear,items);return new RunMutationResult(r.Succeeded,r.RejectionCode,r.State);});
            if(!opened.Succeeded){SetStatus(opened.ErrorCode);return;}run=opened.Snapshot!.ActiveRun!;
        }
        Control root=NewPage($"{LayerLabel(run)} — STORE",$"Gold {run.Gold}. Stock is persisted and will not reroll after Reload.");
        AddRunShell(root,run,"Store");
        var menu=new VBoxContainer{Position=new Vector2(430,210),Size=new Vector2(740,520)};root.AddChild(menu);
        foreach(RunStoreOfferState offer in run.MapState!.StoreOffers!)
        {Button buy=Button($"{offer.ContentId.Value} — {offer.Price} gold{(offer.Purchased?" [SOLD]":"")}",()=>PurchaseStore(offer.InstanceId));buy.Disabled=offer.Purchased;menu.AddChild(buy);}
        menu.AddChild(Button("Leave Store",()=>CommitLayerFour(state=>new PureRunLayerFourNodeService().LeaveStore(state))));
    }

    private void PurchaseStore(ItemInstanceId id)
    {
        RunSessionResult result=_run!.ApplyMutation(state=>{LayerFourNodeResolution r=new PureRunLayerFourNodeService().Purchase(state,id,_consumables,_equipment);return new RunMutationResult(r.Succeeded,r.RejectionCode,r.State);});
        if(!result.Succeeded){SetStatus(result.ErrorCode);return;}ShowStore(result.Snapshot!.ActiveRun!);
    }

    private void ShowMystery(PureRunState run)
    {
        string sourceId=run.MapState!.MysteryEventAssignments[run.NodeTransaction!.NodeId];
        if(run.MapState.MysteryAdjudicatorAssignments?.ContainsKey(run.NodeTransaction.NodeId)!=true)
        {
            RunSessionResult assignment=_run!.ApplyMutation(state=>{LayerFourNodeResolution r=new PureRunLayerFourNodeService().AssignMysteryAdjudicator(state,sourceId);return new RunMutationResult(r.Succeeded,r.RejectionCode,r.State);});
            if(!assignment.Succeeded){SetStatus(assignment.ErrorCode);return;}ShowMystery(assignment.Snapshot!.ActiveRun!);return;
        }
        string adjudicatorId=run.MapState.MysteryAdjudicatorAssignments[run.NodeTransaction.NodeId];
        RunCharacterState adjudicator=run.Party.Single(character=>character.CharacterId==adjudicatorId&&!character.IsDead);
        using JsonDocument document=JsonDocument.Parse(_layerFourEventPayloads[sourceId]);JsonElement rootElement=document.RootElement;
        Control root=NewPage($"{LayerLabel(run)} — {rootElement.GetProperty("title").GetString()}",rootElement.GetProperty("description").GetString()!);
        AddRunShell(root,run,"Mystery");
        var menu=new VBoxContainer{Position=new Vector2(330,180),Size=new Vector2(940,620)};root.AddChild(menu);
        menu.AddChild(Label($"Adjudicator: {adjudicator.CharacterId}",22));
        if(run.MapState.MysteryResolution is RunMysteryResolutionState resolved)
        {
            menu.AddChild(Label($"{resolved.OptionId}: {(resolved.Succeeded?"Success":"Failure")} — roll {resolved.Roll}, chance {resolved.SuccessRate}%\nEffect: {resolved.Effect} {resolved.Amount}",24));
            menu.AddChild(Button("Confirm Result",()=>CommitLayerFour(state=>new PureRunLayerFourNodeService().ConfirmMystery(state,_consumables))));return;
        }
        foreach(JsonElement option in rootElement.GetProperty("options").EnumerateArray())
        {
            string optionId=option.GetProperty("id").GetString()!;string attribute=option.GetProperty("attribute").GetString()!;
            int value=AttributeValue(adjudicator.Attributes,attribute);int rate=attribute=="None"?100:Math.Clamp(option.GetProperty("baseSuccessRate").GetInt32()+(value-5)*5,5,95);
            menu.AddChild(Button($"{option.GetProperty("text").GetString()} — {attribute} {value}: {rate}%",()=>ResolveMystery(sourceId,optionId)));
        }
    }

    private void ResolveMystery(string sourceId,string optionId)
    {
        using JsonDocument document=JsonDocument.Parse(_layerFourEventPayloads[sourceId]);JsonElement option=document.RootElement.GetProperty("options").EnumerateArray().Single(v=>v.GetProperty("id").GetString()==optionId);JsonElement success=option.GetProperty("success");JsonElement failure=option.TryGetProperty("failure",out JsonElement f)&&f.ValueKind!=JsonValueKind.Null?f:success;
        RunSessionResult result=_run!.ApplyMutation(state=>{LayerFourNodeResolution r=new PureRunLayerFourNodeService().ResolveMystery(state,sourceId,optionId,EventAttribute(option.GetProperty("attribute").GetString()!),option.GetProperty("baseSuccessRate").GetInt32(),success.GetProperty("type").GetString()!,success.GetProperty("amount").GetInt32(),EffectContentId(success),failure.GetProperty("type").GetString()!,failure.GetProperty("amount").GetInt32(),EffectContentId(failure));return new RunMutationResult(r.Succeeded,r.RejectionCode,r.State);});
        if(!result.Succeeded){SetStatus(result.ErrorCode);return;}ShowMystery(result.Snapshot!.ActiveRun!);
    }

    private void ShowTreasure(PureRunState run)
    {
        if (run.MapState?.TreasureResolution is null)
        {
            RunSessionResult resolved = _run!.ApplyMutation(state =>
            {
                LayerFourNodeResolution value = new PureRunLayerFourNodeService()
                    .ResolveTreasure(state, _treasureDefinition!);
                return new RunMutationResult(value.Succeeded, value.RejectionCode, value.State);
            });
            if (!resolved.Succeeded) { SetStatus(resolved.ErrorCode); return; }
            run = resolved.Snapshot!.ActiveRun!;
        }
        RunTreasureResolutionState outcome = run.MapState!.TreasureResolution!;
        Control root = NewPage($"{LayerLabel(run)} — TREASURE", "The committed reward is persisted and will not reroll after Reload.");
        AddRunShell(root, run, "Treasure");
        var menu = new VBoxContainer { Position = new Vector2(460, 220), Size = new Vector2(680, 480) };
        root.AddChild(menu);
        menu.AddChild(Label($"Gold: +{outcome.Gold}\nEquipment: {outcome.EquipmentContentId?.Value ?? "None"}\n" +
            $"Consumable: {outcome.ConsumableContentId?.Value ?? "None"}\nBuff: {outcome.BuffContentId?.Value ?? "None"}\n" +
            $"Buff target: {outcome.TargetCharacterId}", 24));
        menu.AddChild(Button("Confirm Treasure", () => CommitLayerFour(state =>
            new PureRunLayerFourNodeService().ConfirmTreasure(state, _treasureDefinition!, _equipment, _consumables))));
    }

    private static RunEventAttribute EventAttribute(string value)=>value switch{"None"=>RunEventAttribute.None,"Strength"=>RunEventAttribute.Strength,"Dexterity" or "Agility"=>RunEventAttribute.Agility,"Constitution"=>RunEventAttribute.Constitution,"Intelligence"=>RunEventAttribute.Intelligence,"Charisma"=>RunEventAttribute.Charisma,"Luck"=>RunEventAttribute.Luck,_=>throw new InvalidOperationException($"Unknown event attribute: {value}.")};

    private static ContentId? EffectContentId(JsonElement effect){if(!effect.TryGetProperty("itemId",out JsonElement item))return null;string value=item.GetString()!;return value switch{"cleansing_potion"=>new ContentId("item.consumable.cleansing-potion"),"Assets/Tactics/ScriptableObjects/Buffs/EventDamageReduction.asset"=>new ContentId("buff.event-damage-reduction"),"Assets/Tactics/ScriptableObjects/Buffs/EventDamageTakenUp.asset"=>new ContentId("buff.event-damage-taken-up"),_=>new ContentId(value)};}
    private static int AttributeValue(UnitAttributes a,string name)=>name switch{"Strength"=>a.Strength,"Agility"=>a.Agility,"Constitution"=>a.Constitution,"Intelligence"=>a.Intelligence,"Charisma"=>a.Charisma,"Luck"=>a.Luck,"None"=>5,_=>5};
    private void CommitLayerFour(Func<PureRunState,LayerFourNodeResolution> command){RunSessionResult result=_run!.ApplyLayerFourMutation(state=>{LayerFourNodeResolution r=command(state);return new RunMutationResult(r.Succeeded,r.RejectionCode,r.State);});if(!result.Succeeded){SetStatus(result.ErrorCode);return;}if(result.Snapshot?.TerminalSummary is PureRunSummary summary){ShowSummary(summary);return;}result=ResolveAdventureBoard(result);if(!result.Succeeded){SetStatus(result.ErrorCode);return;}RouteRunState(result.Snapshot!);}

    private void BeginLayerFive(){RunSessionResult result=_run!.ApplyFullRunTransition(state=>new PureRunFullRunService(_consumables.Keys).BeginLayerFive(state,MapDefinition));if(!result.Succeeded||result.EncounterRequest is null){SetStatus(result.ErrorCode);return;}StartBattle(result.EncounterRequest);}
    private void RouteLayerSixNode(PureRunState run){switch(run.NodeTransaction?.Kind){case PureRunNodeKind.Battle:BeginLayerSixBattle();break;case PureRunNodeKind.Rest:ShowAdventureNodeEntry(run,PureRunNodeKind.Rest);break;case PureRunNodeKind.Store:ShowAdventureNodeEntry(run,PureRunNodeKind.Store);break;case PureRunNodeKind.Mystery:ShowAdventureEventEntry(run,"FallenAltarGuardian");break;case PureRunNodeKind.Treasure:ShowAdventureNodeEntry(run,PureRunNodeKind.Treasure);break;default:SetStatus("layer6.route_missing");break;}}
    private void BeginLayerSixBattle(){RunSessionResult result=_run!.ApplyMutation(state=>{ContentId id=new PureRunMapService(MapDefinition).SelectLateEncounter(state.Seed,"layer_06_battle");LayerFourNodeResolution begun=new PureRunLayerFourNodeService().BeginN4(state,id);return new RunMutationResult(begun.Succeeded,begun.RejectionCode,begun.State);});if(!result.Succeeded||result.Snapshot?.ActiveRun?.Checkpoint is null){SetStatus(result.ErrorCode);return;}PureRunState pending=result.Snapshot.ActiveRun;StartBattle(AdventureEncounterRequest(pending, pending.EncounterContentId));}

    private static EncounterRequest AdventureEncounterRequest(PureRunState run, ContentId encounterId) => new(
        run.RunId, run.Checkpoint!.Revision, encounterId, run.Checkpoint.Party, run.AdventureState?.Revision ?? 0,
        run.Checkpoint.SceneIdentity);
    private void BeginBoss(){RunSessionResult result=_run!.ApplyFullRunTransition(state=>new PureRunFullRunService(_consumables.Keys).BeginBoss(state,MapDefinition));if(!result.Succeeded||result.EncounterRequest is null){SetStatus(result.ErrorCode);return;}StartBattle(result.EncounterRequest);}

    private void ShowInventory(PureRunState run) => ShowInventory(run, InventoryReturnTarget.RunRoute);

    private void ShowInventory(PureRunState run, InventoryReturnTarget returnTarget)
    {
        BindRunUiScope(run.RunId);
        if (_inventoryCharacterId is null || run.Party.All(value => value.CharacterId != _inventoryCharacterId))
            _inventoryCharacterId = run.Party[0].CharacterId;
        ItemInstanceId? selectedItemId = string.IsNullOrEmpty(_inventorySelectedInstanceId) ? null : new ItemInstanceId(_inventorySelectedInstanceId);
        InventoryUiSnapshot inventory = new InventoryUiProjector().Project(run, _inventoryCharacterId, selectedItemId,
            _equipment, _consumables, _units.ToDictionary(value => value.Key, value => value.Value.Speed));
        RunCharacterState selectedCharacter = inventory.SelectedCharacter.Character;
        InventoryAttributeProjection attributes = inventory.SelectedCharacter.Attributes;
        Control root=NewPage("INVENTORY","Backpack, equipment loadout, carried consumable and derived character state");
        AddRunShell(root,run,"Inventory");
        PanelAt(root,new Vector2(50,140),new Vector2(390,640),GodotTacticsTheme.Card).Name="InventoryCharacterPanel";
        PanelAt(root,new Vector2(450,140),new Vector2(490,640),GodotTacticsTheme.Card).Name="InventoryBackpackPanel";
        PanelAt(root,new Vector2(950,140),new Vector2(600,640),GodotTacticsTheme.Card).Name="InventoryDetailPanel";
        var columns=new HBoxContainer{Position=new Vector2(65,150),Size=new Vector2(1470,610)};root.AddChild(columns);
        var characters=new VBoxContainer{CustomMinimumSize=new Vector2(390,580)};columns.AddChild(characters);
        foreach(RunCharacterState character in run.Party)
        {
            RunCharacterState captured = character;
            characters.AddChild(Button($"{(captured.CharacterId == selectedCharacter.CharacterId ? "▶ " : string.Empty)}{captured.CharacterId}  Lv{captured.Level}", () =>
            {
                _inventoryCharacterId = captured.CharacterId; _inventorySelectedInstanceId = null;
                ShowInventory(run, returnTarget);
            }));
        }
        characters.AddChild(AttributeProjectionLabel(selectedCharacter, attributes));
        characters.AddChild(Label("Skills:\n" + string.Join('\n', selectedCharacter.LearnedSkillStates
            .Select(value => $"{value.BranchId} Lv{value.Level}")), 18));

        var backpack=new VBoxContainer{CustomMinimumSize=new Vector2(500,580)};columns.AddChild(backpack);
        var tabs=new HBoxContainer();backpack.AddChild(tabs);
        tabs.AddChild(Button(_inventoryEquipmentTab?"[ Equipment ]":"Equipment",()=>{_inventoryEquipmentTab=true;_inventorySelectedInstanceId=null;ShowInventory(run,returnTarget);}));
        tabs.AddChild(Button(!_inventoryEquipmentTab?"[ Consumables ]":"Consumables",()=>{_inventoryEquipmentTab=false;_inventorySelectedInstanceId=null;ShowInventory(run,returnTarget);}));
        if (_inventoryEquipmentTab)
        {
            if (run.BackpackEquipment.Count == 0) backpack.AddChild(Label("Equipment backpack is empty.\nEquipment is obtained from Store or Mystery routes.",18));
            foreach (RunEquipmentState item in run.BackpackEquipment)
            {
                RunEquipmentState captured=item;
                backpack.AddChild(Button($"{captured.InstanceId.Value}  |  {_equipment[captured.DefinitionId].DisplayName}",()=>
                { _inventorySelectedInstanceId=captured.InstanceId.Value; ShowInventory(run,returnTarget); }));
            }
        }
        else
        {
            if (run.BackpackConsumables.Count == 0) backpack.AddChild(Label("Consumable backpack is empty.\nNormal battles use the frozen deterministic drop chance.",18));
            foreach (BattleConsumableState item in run.BackpackConsumables)
            {
                BattleConsumableState captured=item;
                backpack.AddChild(Button($"{captured.InstanceId.Value}  |  {_consumables[captured.DefinitionId].DisplayName}  {captured.RemainingCharges}/{captured.MaxCharges}",()=>
                { _inventorySelectedInstanceId=captured.InstanceId.Value; ShowInventory(run,returnTarget); }));
            }
        }

        var detail=new VBoxContainer{CustomMinimumSize=new Vector2(520,580)};columns.AddChild(detail);
        detail.AddChild(Label($"Loadout — {selectedCharacter.CharacterId}",22));
        foreach(EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
        {
            RunEquipmentState? equipped=selectedCharacter.Equipment.FirstOrDefault(value=>value.Slot==slot);
            detail.AddChild(Label($"{slot}: {(equipped is null?"empty":_equipment[equipped.DefinitionId].DisplayName)}",17));
            if(equipped is not null)
            {
                EquipmentSlot capturedSlot=slot;
                detail.AddChild(Button($"Unequip {slot}",()=>CommitInventoryMutation(run.RunId,state=>new RunInventoryProgressionService().Unequip(state,state.Revision,selectedCharacter.CharacterId,capturedSlot,_equipment,_units[selectedCharacter.UnitContentId].Speed),returnTarget)));
            }
        }
        detail.AddChild(Label($"Carried: {(selectedCharacter.CarriedConsumables.FirstOrDefault() is BattleConsumableState carried?_consumables[carried.DefinitionId].DisplayName:"empty")}",17));
        if(selectedCharacter.CarriedConsumables.Count>0)
            detail.AddChild(Button("Unload carried consumable",()=>CommitInventoryMutation(run.RunId,state=>new RunInventoryProgressionService().Unload(state,state.Revision,selectedCharacter.CharacterId),returnTarget)));

        RunEquipmentState? selectedEquipment=run.BackpackEquipment.FirstOrDefault(value=>value.InstanceId.Value==_inventorySelectedInstanceId);
        BattleConsumableState? selectedConsumable=run.BackpackConsumables.FirstOrDefault(value=>value.InstanceId.Value==_inventorySelectedInstanceId);
        if(selectedEquipment is not null)
        {
            EquipmentDefinition definition=_equipment[selectedEquipment.DefinitionId];
            detail.AddChild(Label($"\n{definition.DisplayName}\n{definition.Slot} | {definition.Rarity} | Price {definition.Price}\nSource: Store / Mystery\nBonuses: STR {definition.AttributeBonuses.Strength} AGI {definition.AttributeBonuses.Agility} CON {definition.AttributeBonuses.Constitution} INT {definition.AttributeBonuses.Intelligence} CHA {definition.AttributeBonuses.Charisma} LUCK {definition.AttributeBonuses.Luck}",17));
            detail.AddChild(Button(selectedCharacter.Equipment.Any(value=>value.Slot==definition.Slot)?"Replace equipped item":"Equip",()=>CommitInventoryMutation(run.RunId,state=>new RunInventoryProgressionService().Equip(state,state.Revision,selectedCharacter.CharacterId,selectedEquipment.InstanceId,_equipment,_units[selectedCharacter.UnitContentId].Speed),returnTarget)));
        }
        else if(selectedConsumable is not null)
        {
            ConsumableDefinition definition=_consumables[selectedConsumable.DefinitionId];
            detail.AddChild(Label($"\n{definition.DisplayName}\nPrice {definition.Price}\n{definition.Description}\nSource: deterministic Battle drop / Store / Mystery",17));
            detail.AddChild(Button(selectedCharacter.CarriedConsumables.Count>0?"Replace carried consumable":"Carry",()=>CommitInventoryMutation(run.RunId,state=>new RunInventoryProgressionService().Carry(state,state.Revision,selectedCharacter.CharacterId,selectedConsumable.InstanceId),returnTarget)));
        }
        root.AddChild(PlaceControl(Button("Back",()=>ReturnFromInventory(run,returnTarget)),new Vector2(650,815),new Vector2(300,55)));
        PushEscapeLayer("inventory", () => ReturnFromInventory(run, returnTarget));
    }

    private void CommitInventoryMutation(string runId, Func<PureRunState,RunMutationResult> mutation, InventoryReturnTarget returnTarget)
    {
        RunSessionResult result=_run!.ApplyMutation(state => string.Equals(state.RunId, runId, StringComparison.Ordinal)
            ? mutation(state)
            : new RunMutationResult(false, "ui.run_scope_changed", state));
        if(!result.Succeeded||result.Snapshot?.ActiveRun is not PureRunState run){SetStatus(result.ErrorCode);return;}
        _inventorySelectedInstanceId=null;
        ShowInventory(run,returnTarget);
    }

    private void ReturnFromInventory(PureRunState run, InventoryReturnTarget returnTarget)
    {
        _inventorySelectedInstanceId=null;
        RouteRunState(new PureRunSaveSnapshot(run.Revision,run,null));
    }

    private static RichTextLabel AttributeProjectionLabel(RunCharacterState character, InventoryAttributeProjection value)
    {
        string Line(string name, int basis, int bonus, int total)
        {
            string color = bonus > 0 ? "6fd08c" : bonus < 0 ? "ff6b6b" : "d8e0e6";
            return $"{name} {basis} → [color=#{color}]{total} ({(bonus >= 0 ? "+" : string.Empty)}{bonus})[/color]";
        }
        return new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            CustomMinimumSize = new Vector2(380, 215),
            Text = $"\nHP {character.CurrentHealth}/{value.DerivedStats.MaxHealth}  MP {character.CurrentMana}/{value.DerivedStats.MaxMana}\n" +
                Line("STR", value.Base.Strength, value.Bonus.Strength, value.Total.Strength) + "  " + Line("AGI", value.Base.Agility, value.Bonus.Agility, value.Total.Agility) + "\n" +
                Line("CON", value.Base.Constitution, value.Bonus.Constitution, value.Total.Constitution) + "  " + Line("INT", value.Base.Intelligence, value.Bonus.Intelligence, value.Total.Intelligence) + "\n" +
                Line("CHA", value.Base.Charisma, value.Bonus.Charisma, value.Total.Charisma) + "  " + Line("LUCK", value.Base.Luck, value.Bonus.Luck, value.Total.Luck) + "\n" +
                $"Move {value.DerivedStats.MoveRange}  Initiative {value.DerivedStats.Initiative}\n" +
                $"Turn MP +{value.Total.Intelligence} (INT)  Max MP {value.DerivedStats.MaxMana} (CHA × 3)"
        };
    }

    private void ShowProgression(PureRunState run, PendingProgression pending)
    {
        BindRunUiScope(run.RunId);
        RunCharacterState character=run.Party.Single(value=>value.CharacterId==pending.CharacterId);
        Control root=NewPage("PROGRESSION",$"{character.CharacterId}: attribute allocation → skill selection");
        AddRunShell(root,run,"Progression");
        PanelAt(root,new Vector2(405,140),new Vector2(790,690)).Name="ProgressionFlowPanel";
        var menu=new VBoxContainer{Position=new Vector2(430,160),Size=new Vector2(740,650)};root.AddChild(menu);
        var progressionService=new RunInventoryProgressionService();
        if (!_progressionDrafts.TryGetValue((run.RunId, pending.TransactionKey), out UnitAttributes proposed))
        {
            menu.AddChild(Label($"Step 1/2 — choose one attribute\nSTR {character.Attributes.Strength}  AGI {character.Attributes.Agility}  CON {character.Attributes.Constitution}\nINT {character.Attributes.Intelligence}  CHA {character.Attributes.Charisma}  LUCK {character.Attributes.Luck}",22));
            foreach (string attribute in new[] { "Strength", "Agility", "Constitution", "Intelligence", "Charisma", "Luck" })
            {
                string selectedAttribute = attribute;
                Button attributeButton = Button($"+1 {selectedAttribute}", () => PreviewProgressionAttribute(run, pending,
                    Raise(character.Attributes, selectedAttribute)));
                attributeButton.Name = $"ProgressionAttribute_{selectedAttribute}";
                menu.AddChild(attributeButton);
            }
        }
        else
        {
            RunCharacterState preview = new(character.CharacterId, character.UnitContentId, character.Level, proposed,
                character.CurrentHealth, character.MaxHealth, character.CurrentMana, character.MaxMana, character.IsDead,
                character.LearnedSkills, character.Equipment, character.CarriedConsumables, character.LearnedSkillStates,
                character.StartingSkillContentId);
            menu.AddChild(Label($"Step 2/2 — choose a skill\nSTR {proposed.Strength}  AGI {proposed.Agility}  CON {proposed.Constitution}\nINT {proposed.Intelligence}  CHA {proposed.Charisma}  LUCK {proposed.Luck}",22));
            menu.AddChild(Label("Current skills:\n" + string.Join('\n', character.LearnedSkillStates.Select(value =>
            {
                SkillUiMetadata known = _skillUi[value.DefinitionId];
                return $"{known.DisplayName} Lv{value.Level} — {(known.IsPassive ? "Passive" : "Active")} — MP {known.ManaCost}\n{known.Description}";
            })), 17));
            SkillDefinition[] candidates=progressionService.PreviewGrowthOffer(run,pending.TransactionKey,proposed,_skills,_runDefinition!).ToArray();
            foreach((SkillDefinition skill, int index) in candidates.Select((value, index) => (value, index)))
            {
                Button choice=Button(GrowthChoiceLabel(preview,skill,_skillUi[skill.ContentId]),()=>
                    CompleteProgression(run.RunId,pending.TransactionKey,proposed,skill.ContentId));
                choice.Name = $"ProgressionSkillChoice_{index}";
                choice.ThemeTypeVariation=GodotTacticsTheme.ActionButton;
                choice.CustomMinimumSize=new Vector2(720,112);
                menu.AddChild(choice);
            }
            if(candidates.Length==0)
            {
                menu.AddChild(Label("No skill candidate is legal after this allocation. Attribute-only confirmation is allowed.",18));
                menu.AddChild(Button("Confirm Attribute",()=>CompleteProgression(run.RunId,pending.TransactionKey,proposed,null)));
            }
            PushEscapeLayer("progression.step2", () =>
            {
                _progressionDrafts.Remove((run.RunId, pending.TransactionKey));
                ShowProgression(run, pending);
            });
        }
    }

    private static string GrowthChoiceLabel(RunCharacterState character,SkillDefinition skill,SkillUiMetadata metadata)
    {
        RunLearnedSkillState? learned=character.LearnedSkillStates.FirstOrDefault(value=>value.BranchId==skill.BranchId);
        string title = learned is null
            ? $"Learn {metadata.DisplayName} Lv{skill.Level}"
            : $"Upgrade {metadata.DisplayName} Lv{learned.Level} → Lv{skill.Level}";
        string requirement=string.IsNullOrEmpty(metadata.RequiredAttribute)?"None":$"{metadata.RequiredAttribute} {metadata.MinimumAttribute}";
        string prerequisite=string.IsNullOrEmpty(metadata.PrerequisiteBranchId)?"None":metadata.PrerequisiteBranchId;
        return $"{title}\n{metadata.Description}\n{(metadata.IsPassive ? "Passive" : "Active")}  MP {metadata.ManaCost}  Range {RangeLabel(metadata)}\nRequires: {requirement}  Prerequisite: {prerequisite}";
    }

    private static string RangeLabel(SkillUiMetadata metadata) => metadata.MinRange == metadata.MaxRange
        ? metadata.MaxRange.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : $"{metadata.MinRange}-{metadata.MaxRange}";

    internal static string SettlementDropLabel(PureRunState run)
    {
        string prefix = $"drop-{run.BattlesCompleted}-";
        ContentId[] drops = run.BackpackConsumables
            .Where(value => value.InstanceId.Value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(value => value.DefinitionId).ToArray();
        return drops.Length == 0 ? "No item drop" : string.Join(", ", drops.Select(value => value.Value));
    }

    private void PreviewProgressionAttribute(PureRunState run, PendingProgression pending, UnitAttributes attributes)
    {
        _progressionDrafts[(run.RunId, pending.TransactionKey)] = attributes;
        ShowProgression(run, pending);
    }

    private void CompleteProgression(string runId, string transactionKey, UnitAttributes attributes, ContentId? skillId)
    {
        RunSessionResult result = _run!.ApplyMutation(state =>
        {
            if (!string.Equals(state.RunId, runId, StringComparison.Ordinal))
                return new RunMutationResult(false, "ui.run_scope_changed", state);
            return new RunInventoryProgressionService()
                .CompleteProgression(state,state.Revision,transactionKey,attributes,skillId,_skills,_runDefinition!);
        });
        if(!result.Succeeded){SetStatus(result.ErrorCode);return;}
        _progressionDrafts.Remove((runId, transactionKey));
        ShowSettlement(result.Snapshot!);
    }

    private static UnitAttributes Raise(UnitAttributes a,string name)=>name switch{"Strength"=>new(a.Strength+1,a.Agility,a.Constitution,a.Intelligence,a.Charisma,a.Luck),"Agility"=>new(a.Strength,a.Agility+1,a.Constitution,a.Intelligence,a.Charisma,a.Luck),"Constitution"=>new(a.Strength,a.Agility,a.Constitution+1,a.Intelligence,a.Charisma,a.Luck),"Intelligence"=>new(a.Strength,a.Agility,a.Constitution,a.Intelligence+1,a.Charisma,a.Luck),"Charisma"=>new(a.Strength,a.Agility,a.Constitution,a.Intelligence,a.Charisma+1,a.Luck),"Luck"=>new(a.Strength,a.Agility,a.Constitution,a.Intelligence,a.Charisma,a.Luck+1),_=>a};
    private void CommitMutation(Func<PureRunState,RunMutationResult> mutation){RunSessionResult result=_run!.ApplyMutation(mutation);if(!result.Succeeded){SetStatus(result.ErrorCode);return;}ShowSettlement(result.Snapshot!);}
    private static Control PlaceControl(Control control,Vector2 position,Vector2 size){control.Position=position;control.Size=size;return control;}

    private void ShowSummary(PureRunSummary summary)
    {
        ReleaseActWorld();
        _battle = null;
        ResetRunUiScope();
        Control root = NewPage(summary.Outcome.ToString(), "Three-encounter slice complete");
        PanelAt(root,new Vector2(450,205),new Vector2(700,470)).Name="TerminalSummaryPanel";
        LabelAt(root, $"Battles: {summary.BattlesCompleted}\nKills: {summary.EnemiesDefeated}\nGold: {summary.TotalGoldEarned}\nItems: {string.Join(", ", summary.AcquiredItems.Select(id => id.Value))}\nDead: {string.Join(", ", summary.DeadCharacters)}", new Vector2(520, 260), 30);
        Button home = Button("Continue to Fresh Camp", () => { _run!.ConsumeCompletedSummary(); StartNewRunConfirmed(); }); home.Position = new Vector2(620, 620); home.Size = new Vector2(360, 70); root.AddChild(home);
    }

    private void AbandonRun()
    {
        RunSessionResult result = _run!.AbandonRun();
        if (result.Snapshot?.TerminalSummary is PureRunSummary summary) ShowSummary(summary); else SetStatus(result.ErrorCode);
    }

    private void SetInputContext(RunInputContext context)
    {
        if (_pauseMenu?.Visible == true) ClosePauseMenu();
        _inputContext = context;
        EnsurePauseMenu();
        ClearPauseMenuContent();
        _pauseMenuView = PauseMenuView.Root;
    }

    private void BuildPauseMenu(Control root, bool controlsBattlePlayback = true)
    {
        _ = root;
        SetInputContext(controlsBattlePlayback ? RunInputContext.Battle : RunInputContext.Adventure);
    }

    private void EnsurePauseMenu()
    {
        if (_pauseMenu is not null && GodotObject.IsInstanceValid(_pauseMenu)) return;
        var overlay = new ColorRect
        {
            Name = "GlobalPauseOverlay",
            Color = new Color(0, 0, 0, .72f),
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = PauseOverlayZIndex
        };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);
        PanelContainer panel = PanelAt(overlay, new Vector2(570, 165), new Vector2(460, 520));
        _pauseMenuContent = new VBoxContainer { Name = "GlobalPauseMenuContent" };
        panel.AddChild(_pauseMenuContent);
        _pauseMenu = overlay;
    }

    private void ClearPauseMenuContent()
    {
        if (_pauseMenuContent is null || !GodotObject.IsInstanceValid(_pauseMenuContent)) return;
        if (_pauseMenuDefaultFocus is not null && GodotObject.IsInstanceValid(_pauseMenuDefaultFocus) &&
            _pauseMenuDefaultFocus.HasFocus()) _pauseMenuDefaultFocus.ReleaseFocus();
        _pauseMenuDefaultFocus = null;
        foreach (Node child in _pauseMenuContent.GetChildren())
        {
            bool wasInsideTree = child.IsInsideTree();
            _pauseMenuContent.RemoveChild(child);
            if (wasInsideTree) child.QueueFree(); else child.Free();
        }
    }

    private void RebuildPauseMenuContent()
    {
        if (_pauseMenuContent is null || !GodotObject.IsInstanceValid(_pauseMenuContent)) return;
        RemoveEscapeLayer("pause.options");
        RemoveEscapeLayer("pause.abandon");
        ClearPauseMenuContent();
        _pauseMenuView = PauseMenuView.Root;
        Label title = Label("PAUSED", 36);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _pauseMenuContent.AddChild(title);
        Label subtitle = Label(_inputContext.SetupDraftActive
            ? "New Run setup is not saved until all starting skills are confirmed."
            : "Game is paused", 16);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.AddThemeColorOverride("font_color", GodotTacticsTheme.TextSecondary);
        _pauseMenuContent.AddChild(subtitle);
        _pauseMenuContent.AddChild(new ColorRect
        {
            Color = GodotTacticsTheme.Accent,
            CustomMinimumSize = new Vector2(0, 2),
            MouseFilter = MouseFilterEnum.Ignore
        });
        _pauseMenuDefaultFocus = Button("CONTINUE", ClosePauseMenu);
        _pauseMenuContent.AddChild(_pauseMenuDefaultFocus);
        _pauseMenuContent.AddChild(Button("OPTIONS", ShowPauseOptions));
        _pauseMenuContent.AddChild(_inputContext.SetupDraftActive
            ? Button("DISCARD SETUP", DiscardSetupFromPause)
            : Button("ABANDON RUN", ConfirmAbandonRun));
        _pauseMenuContent.AddChild(Button(_inputContext.SetupDraftActive ? "QUIT — SETUP NOT SAVED" : "SAVE AND QUIT", SaveAndQuit));
        EnsureEscapeLayer("pause.close", ClosePauseMenu);
    }

    private void ConfirmAbandonRun()
    {
        if (_pauseMenu is null || _pauseMenu.Visible == false ||
            _pauseConfirmation is not null && GodotObject.IsInstanceValid(_pauseConfirmation)) return;
        var confirm = new ConfirmationDialog
        {
            Title = "Abandon Run",
            DialogText = "Abandon this run? This records an Abandoned summary before a fresh camp is created."
        };
        _pauseConfirmation = confirm;
        _pauseMenu.AddChild(confirm);
        confirm.Confirmed += () =>
        {
            RemoveEscapeLayer("pause.abandon");
            _pauseConfirmation = null;
            confirm.QueueFree();
            ClosePauseMenu();
            AbandonRun();
        };
        confirm.Canceled += () =>
        {
            RemoveEscapeLayer("pause.abandon");
            _pauseConfirmation = null;
            confirm.QueueFree();
            RebuildPauseMenuContent();
            _pauseMenuDefaultFocus?.GrabFocus();
        };
        PushEscapeLayer("pause.abandon", () =>
        {
            if (_pauseConfirmation is not null && GodotObject.IsInstanceValid(_pauseConfirmation))
                _pauseConfirmation.QueueFree();
            _pauseConfirmation = null;
            RebuildPauseMenuContent();
            _pauseMenuDefaultFocus?.GrabFocus();
        });
        confirm.PopupCentered();
    }

    private void DiscardSetupFromPause()
    {
        ClosePauseMenu();
        RunSessionResult canceled = _run!.CancelNewRunSetup();
        if (!canceled.Succeeded) { SetStatus(canceled.ErrorCode); return; }
        ReleaseActWorld();
        if (canceled.Snapshot?.ActiveRun is not null) RouteRunState(canceled.Snapshot);
        else StartNewRunConfirmed();
    }

    private void SaveAndQuit()
    {
        ClosePauseMenu();
        if (_inputContext.SetupDraftActive && _run?.HasSetupDraft == true)
            _ = _run.CancelNewRunSetup();
        ReleaseActWorld();
        RequestQuit();
    }

    private void ShowPauseOptions()
    {
        if (_pauseMenuContent is null) return;
        ClearPauseMenuContent();
        _pauseMenuView = PauseMenuView.Options;
        _pauseMenuContent.AddChild(Label("OPTIONS\nPresentation speed is controlled from the battle HUD.", 24));
        _pauseMenuDefaultFocus = Button("BACK", () =>
        {
            if (!TryDismissTopEscapeLayer()) RebuildPauseMenuContent();
        });
        _pauseMenuContent.AddChild(_pauseMenuDefaultFocus);
        PushEscapeLayer("pause.options", RebuildPauseMenuContent);
        _pauseMenuDefaultFocus.GrabFocus();
    }

    private void OpenPauseMenu()
    {
        if (!_inputContext.CanPause || _pauseMenu?.Visible == true) return;
        EnsurePauseMenu();
        RebuildPauseMenuContent();
        // Godot GUI hit testing gives later siblings priority independently of CanvasItem ZIndex.
        // Pages are replaced after this persistent overlay, so restore the overlay to the end of
        // Main's child order every time it opens or the visible menu can render above a page while
        // that page's full-screen input surface still receives the click.
        Control pauseMenu = _pauseMenu!;
        MoveChild(pauseMenu, GetChildCount() - 1);
        pauseMenu.Visible = true;
        _pauseMenuDefaultFocus?.GrabFocus();
        if (_inputContext.PausesAdventureMovement)
        {
            _adventureBoard?.SetMovementPaused(true);
            _startCampView?.SetMovementPaused(true);
        }
        _pauseMenuPausedPlayback = _inputContext.ControlsBattlePlayback && !_playbackPaused;
        if (_pauseMenuPausedPlayback) TogglePause();
    }

    private void ClosePauseMenu()
    {
        if (_pauseMenu is null) return;
        ClearEscapeLayers();
        if (_pauseConfirmation is not null && GodotObject.IsInstanceValid(_pauseConfirmation))
            _pauseConfirmation.QueueFree();
        _pauseConfirmation = null;
        _pauseMenu.Visible = false;
        if (_inputContext.PausesAdventureMovement)
        {
            _adventureBoard?.SetMovementPaused(false);
            _startCampView?.SetMovementPaused(false);
        }
        if (_pauseMenuPausedPlayback && _playbackPaused) TogglePause();
        _pauseMenuPausedPlayback = false;
        ClearPauseMenuContent();
        _pauseMenuView = PauseMenuView.Root;
    }

    private Control NewPage(string title, string subtitle, bool battleBackdrop = false) =>
        CreatePage(title, subtitle, battleBackdrop, true);

    private Control CreatePage(string title, string subtitle, bool battleBackdrop, bool showHeader)
    {
        HideActWorldForPage();
        ClosePauseMenu();
        ClearEscapeLayers();
        _inputContext = RunInputContext.Disabled;
        _currentPageTitle = title;
        // The old page owns every actor and meter. Queueing the page frees those
        // children, so page navigation must forget their managed references rather
        // than attempting to QueueFree the disposed children during the next refresh.
        _page?.QueueFree();
        _status = null;
        _actors.Clear();
        _unitMeters.Clear();
        _visibleSnapshot=null;
        _hoveredCell=null;
        _targetingFacingPreview=null;
        DisposePresentationPlayer();_board=null;
        _skillPanel=null;
        _turnOrder=null;
        _cellDebug=null;
        _speedButton=null;
        _activeUnitPanel=null;
        _settlementStatus=null;
        _cheatConsole=null;
        _eventLog=null;
        _mapView=null;
        _adventureBoard=null;
        _pendingAdventureExit=null;
        _startCampView=null;
        _mapDetail=null;
        _damageNumbers=null;
        var root = new Control(); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(root); _page = root;
        Control background = battleBackdrop ? new GodotBattleBackdrop() : new ColorRect { Color = GodotTacticsTheme.Background };
        background.ZIndex = -100; // Persistent Act maps render above the page-owned backdrop.
        background.MouseFilter = MouseFilterEnum.Ignore;
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); root.AddChild(background);
        if (!battleBackdrop && showHeader)
        {
            PanelContainer header = PanelAt(root, new Vector2(48, 24), new Vector2(1504, 104));
            header.Name = "PageHeader";
            header.ZIndex = StartPageUiZIndex;
            var labels = new VBoxContainer(); header.AddChild(labels);
            Label pageTitle = Label(title, 34); pageTitle.Name = "PageTitle"; labels.AddChild(pageTitle);
            Label detail = Label(subtitle, 16); detail.Name = "PageSubtitle";
            detail.AddThemeColorOverride("font_color", GodotTacticsTheme.TextSecondary); labels.AddChild(detail);
        }
        return root;
    }

    private static PanelContainer PanelAt(Control parent, Vector2 position, Vector2 size,
        string variation = GodotTacticsTheme.Panel)
    {
        var panel = new PanelContainer
        {
            Position = position,
            Size = size,
            ThemeTypeVariation = variation,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        parent.AddChild(panel);
        return panel;
    }

    private static PanelContainer HudPanelAt(Control parent, string name, Vector2 position, Vector2 size)
    {
        PanelContainer panel = PanelAt(parent, position, size, GodotTacticsTheme.Card);
        panel.Name = name;
        panel.MouseFilter = MouseFilterEnum.Ignore;
        panel.ZIndex = 1200;
        return panel;
    }

    private void DisposePresentationPlayer()
    {
        if(_presentationPlayer is null)return;
        _presentationPlayer.FrameCompleted-=OnPresentationFrameCompleted;
        _presentationPlayer.NumberRequested-=SpawnPresentationNumber;
        _presentationPlayer.Clear();
        _presentationPlayer=null;
        _presentationAfter=null;
        _continueAutomaticAfterPresentation=false;
        _pauseAfterCurrentFrame=false;
        _presentationInputLocked=false;
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(300, 56), ThemeTypeVariation = GodotTacticsTheme.PrimaryButton }; button.Pressed += action; return button;
    }
    private static Button SmallButton(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(118, 44), ThemeTypeVariation = GodotTacticsTheme.CompactButton }; button.Pressed += action; return button;
    }
    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(168, 54), ThemeTypeVariation = GodotTacticsTheme.ActionButton,
            FocusMode = FocusModeEnum.None };
        button.Pressed += action;
        return button;
    }
    internal static string FormatBattleActionLabel(string displayName, int manaCost, bool used)
    {
        string detail = manaCost > 0 ? $"MP {manaCost}" : string.Empty;
        if (used) detail = string.IsNullOrEmpty(detail) ? "Used" : $"{detail} · Used";
        return string.IsNullOrEmpty(detail) ? displayName : $"{displayName}\n{detail}";
    }
    internal static bool ShouldShowActiveMarker(bool presentationInputLocked, bool presentationPlaying) =>
        !presentationInputLocked && !presentationPlaying;
    private static Label Label(string text, int size) { var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", size); return label; }
    private static Label LabelAt(Control parent, string text, Vector2 position, int size) { Label label = Label(text, size); label.Position = position; parent.AddChild(label); return label; }
    private void SetStatus(string? text)
    {
        if ((_status is null || !GodotObject.IsInstanceValid(_status)) &&
            _page is not null && GodotObject.IsInstanceValid(_page))
        {
            _status = LabelAt(_page, string.Empty, new Vector2(400, 852), 15);
            _status.Name = "PageFallbackStatus";
            _status.Size = new Vector2(800, 32);
            _status.HorizontalAlignment = HorizontalAlignment.Center;
            _status.ZIndex = PauseOverlayZIndex - 1;
            _status.MouseFilter = MouseFilterEnum.Ignore;
        }
        if (_status is not null && GodotObject.IsInstanceValid(_status)) _status.Text = text ?? string.Empty;
    }
    private static string SkillTooltip(SkillDefinition skill)=>skill.ExecutionKind==SkillExecutionKind.Fireball
        ? "Lv1: single target; hits the first enemy on the selected ray. Splash begins at Lv2."
        : $"Range {skill.MinRange}-{skill.MaxRange}; damage {skill.Damage}.";
    private static string EncounterLabel(ContentId id)=>id.Value.EndsWith(".n1",StringComparison.Ordinal)?"N1":id.Value.EndsWith(".n2",StringComparison.Ordinal)?"N2":id.Value.EndsWith(".n3",StringComparison.Ordinal)?"N3":id.Value;
    private static string LayerLabel(PureRunState run)=>run.NodeTransaction?.NodeId.StartsWith("layer_06_",StringComparison.Ordinal)==true?"LAYER 6":"LAYER 4";
}
