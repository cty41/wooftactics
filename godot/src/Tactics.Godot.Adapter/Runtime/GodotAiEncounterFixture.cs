using Godot;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record AiFixtureTurnResult(
    int GlobalStep,
    int RoundBefore,
    int RoundAfter,
    string ActorId,
    string NextActorId,
    int CandidateCount,
    string SelectedIntent,
    string SelectedSkill,
    bool UsedPattern,
    int PatternBefore,
    int PatternAfter,
    string Events,
    string StateFingerprint);

public sealed record AiFixtureRoundResult(
    int RoundBefore,
    int RoundAfter,
    IReadOnlyList<AiFixtureTurnResult> Turns,
    bool HitCommandLimit,
    string StateFingerprint);

/// <summary>1600x900 gameplay fixture exposing deterministic AI scenario selection and logs.</summary>
public partial class GodotAiEncounterFixture : Control
{
    public const int CanvasWidth = 1600;
    public const int CanvasHeight = 900;
    private const string BatchCatalogPath = "res://content/ai_encounters/ContentCatalog.tres";
    private const string GlobalCatalogPath = "res://content/ContentCatalog.tres";
    private static readonly string[] Scenarios = { "N1", "N2", "N3", "Elite Charger", "Elite Poison Caster" };
    private readonly AiDecisionService _decisions = new();
    private readonly AiTurnService _turns = new();
    private Dictionary<ContentId, SkillDefinition> _skills = new();
    private Dictionary<ContentId, AiDefinition> _ai = new();
    private Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private Dictionary<UnitInstanceId, AiDefinition> _unitAi = new();
    private Dictionary<UnitInstanceId, int> _patternCursors = new();
    private BattleState? _state;
    private Label? _title;
    private Label? _log;
    private readonly List<string> _history = new();
    private int _index;
    private int _step;
    private string _last = "Ready.";
    private EncounterDefinition? _draftEncounter;
    private BattleLayoutDefinition? _draftLayout;

    public override void _Ready()
    {
        LoadDefinitions();
        BuildUi();
        ResetScenario();
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Left) { _index = (_index + Scenarios.Length - 1) % Scenarios.Length; ResetScenario(); }
        else if (key.Keycode == Key.Right) { _index = (_index + 1) % Scenarios.Length; ResetScenario(); }
        else if (key.Keycode == Key.Space) ExecuteSingleTurn();
        else if (key.Keycode is Key.Enter or Key.KpEnter) ExecuteCurrentRound();
        else if (key.Keycode == Key.R) ResetCurrentScenario();
    }

    public AiFixtureTurnResult ExecuteSingleTurn() => ExecuteSingleTurnCore(refresh: true);

    public AiFixtureRoundResult ExecuteCurrentRound()
    {
        if (_state is null) throw new InvalidOperationException("Scenario is not initialized.");
        int startRound = _state.Round;
        var turns = new List<AiFixtureTurnResult>();
        while (_state.Round == startRound && turns.Count < 64)
            turns.Add(ExecuteSingleTurnCore(refresh: false));
        bool hitLimit = turns.Count >= 64;
        _last = hitLimit
            ? "LAST ACTION: AUTO ROUND FAILED — structured command limit reached (64)."
            : $"LAST ACTION: AUTO ROUND — {turns.Count} AI actors completed round {startRound}.";
        _history.Add($"=== ROUND {startRound} COMPLETE: {turns.Count} turns; next round {_state.Round} ===");
        Refresh();
        return new AiFixtureRoundResult(startRound, _state.Round, turns, hitLimit, CreateStateFingerprint());
    }

    public void SelectScenario(int index)
    {
        if (index < 0 || index >= Scenarios.Length) throw new ArgumentOutOfRangeException(nameof(index));
        _draftEncounter = null; _draftLayout = null; _index = index;
        ResetCurrentScenario();
    }

    public void ResetCurrentScenario()
    {
        ResetScenario();
    }

    public void LoadDraft(EncounterDefinition encounter, BattleLayoutDefinition layout)
    {
        _draftEncounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        _draftLayout = layout ?? throw new ArgumentNullException(nameof(layout));
        if (encounter.LayoutId != layout.ContentId) throw new InvalidOperationException("Draft Encounter/Layout identity mismatch.");
        if (encounter.Monsters.Count > layout.EnemySpawns.Count) throw new InvalidOperationException("Draft layout has fewer enemy spawns than Encounter monsters.");
        ResetScenario();
    }

    private AiFixtureTurnResult ExecuteSingleTurnCore(bool refresh)
    {
        if (_state is null) throw new InvalidOperationException("Scenario is not initialized.");
        SkipNonAiActors();
        int roundBefore = _state.Round;
        UnitInstanceId actorId = _state.ActiveUnitId;
        AiDefinition definition = _unitAi[actorId];
        int patternIndex = _patternCursors.GetValueOrDefault(actorId);
        AiTurnPlan plan = _decisions.Decide(_state, definition, _skills, patternIndex);
        AiPlanExecutionResult result = _turns.Execute(_state, plan, _skills);
        _state = result.State;
        _patternCursors[actorId] = result.NextPatternIndex;
        SkipNonAiActors();
        _step++;
        string scores = string.Join("; ", plan.Candidates.Where(value => value.IsLegal).Take(6).Select(value => $"{value.Intent}/{value.SkillId?.Value ?? "-"}={value.TotalScore:0.##}"));
        string events = string.Join(" -> ", result.Events.Select(value => value.GetType().Name));
        string skill = plan.Selected.SkillId?.Value ?? "-";
        string line = $"#{_step} R{roundBefore} {actorId} -> {plan.Selected.Intent}/{skill}; candidates={plan.Candidates.Count}; pattern={plan.UsesPattern} {patternIndex}->{result.NextPatternIndex}; next={_state.ActiveUnitId}";
        _history.Add(line);
        _last = $"LAST ACTION: SINGLE TURN — exactly 1 AI actor.\n{line}\nScores: {scores}\nEvents: {events}";
        if (refresh) Refresh();
        return new AiFixtureTurnResult(_step, roundBefore, _state.Round, actorId.Value, _state.ActiveUnitId.Value, plan.Candidates.Count, plan.Selected.Intent.ToString(), skill, plan.UsesPattern, patternIndex, result.NextPatternIndex, events, CreateStateFingerprint());
    }

    private void SkipNonAiActors()
    {
        if (_state is null) return;
        int remainingGuard = _state.Units.Count * 2 + 1;
        while (!_unitAi.ContainsKey(_state.ActiveUnitId) && remainingGuard-- > 0)
        {
            BattleTransition skipped = new BattleTransitionService().Apply(_state,
                new EndTurnCommand(_state.ActiveUnitId));
            if (!skipped.Succeeded)
                throw new InvalidOperationException("AI fixture could not skip a non-AI target turn.");
            _state = skipped.State;
        }
        if (!_unitAi.ContainsKey(_state.ActiveUnitId))
            throw new InvalidOperationException("AI fixture could not reach an AI-controlled actor.");
    }

    private void ResetScenario()
    {
        (_state, _unitAi) = CreateScenario(_index);
        _patternCursors = _unitAi.Keys.ToDictionary(value => value, _ => 0);
        _history.Clear();
        _step = 0;
        _last = $"RESET — fixed seed=6; {_unitAi.Count} AI actors{(_draftEncounter is null ? string.Empty : "; AUTHORING DRAFT")}. Space advances 1 actor; Enter advances all {_unitAi.Count} actors in the round.";
        Refresh();
    }

    private (BattleState State, Dictionary<UnitInstanceId, AiDefinition> UnitAi) CreateScenario(int index)
    {
        BattleLayoutDefinition layout = _draftLayout ?? LoadLayout(index == 2 ? "battle-layout.pure-run.center-blocker" : "battle-layout.pure-run.open");
        (string[] unitIds, string[] aiIds) = _draftEncounter is not null
            ? (_draftEncounter.Monsters.Select(value => value.UnitId.Value).ToArray(), _draftEncounter.Monsters.Select(value => value.AiId.Value).ToArray())
            : index < 3 ? LoadEncounter($"encounter.pure-run.n{index + 1}") :
            index == 3 ? (new[] { "unit.pure-run.goat-elite-charger" }, new[] { "ai.pure-run.elite-charger" }) :
            (new[] { "unit.pure-run.goat-elite-poison-caster" }, new[] { "ai.pure-run.elite-poison-caster" });
        var cells = Enumerable.Range(0, BoardSpec.Width).SelectMany(x => Enumerable.Range(0, BoardSpec.Height).Select(y =>
            new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState(
                blocksMovement: layout.BlockedCells.Contains(new GridPoint(x, y)),
                terrain: layout.ShallowWater.Contains(new GridPoint(x, y)) ? TerrainKind.ShallowWater : TerrainKind.Ground)))).ToDictionary();
        var units = new List<BattleUnitState>();
        for (int i = 0; i < Math.Min(3, layout.PartySpawns.Count); i++)
        {
            UnitInstanceId id = new($"fixture.party.{i}");
            units.Add(new BattleUnitState(new UnitState(id, new ContentId("unit.pure-run.amazon"), layout.PartySpawns[i], 3, 10, 0, i), 40, 40, maxMana: 30, currentMana: 30));
        }
        var order = new List<UnitInstanceId>();
        var bindings = new Dictionary<UnitInstanceId, AiDefinition>();
        for (int i = 0; i < aiIds.Length; i++)
        {
            UnitInstanceId id = new($"fixture.enemy.{i}");
            units.Add(new BattleUnitState(new UnitState(id, new ContentId(unitIds[i]), layout.EnemySpawns[i], 3, 10, 1, i), 35, 35, maxMana: 30, currentMana: 30, physicalAttack: 6, magicalAttack: 6));
            order.Add(id);
            bindings.Add(id, _ai[new ContentId(aiIds[i])]);
        }
        return (new BattleState(new BoardSnapshot(cells), units, order, randomState: 6), bindings);
    }

    private void LoadDefinitions()
    {
        GodotResourceCatalog global = RequiredResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath, "Global Catalog load failed");
        global.Validate();
        _paths = global.Entries.ToDictionary(entry => entry.ContentIdValue, entry => entry.ResourceLocator, StringComparer.Ordinal);
        foreach (GodotResourceEntry entry in global.Entries.Where(value => value.ResourceTypeIdValue == "skill"))
        {
            Resource resource = ResourceLoader.Load(entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException($"Missing skill: {entry.ContentIdValue} at {entry.DiagnosticPathValue}");
            if (resource is SkillDefinitionResource skill) _skills.Add(new ContentId(entry.ContentIdValue), skill.ToCoreDefinition());
            else if (resource is PoisonSpearSkillResource poison) _skills.Add(new ContentId(entry.ContentIdValue), poison.ToCoreDefinition());
        }
        foreach (GodotResourceEntry entry in global.Entries.Where(value => value.ResourceTypeIdValue == "ai"))
            _ai.Add(new ContentId(entry.ContentIdValue), (ResourceLoader.Load<AiDefinitionResource>(entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException($"Missing AI: {entry.ContentIdValue} at {entry.DiagnosticPathValue}")).ToCoreDefinition());
        _ = RequiredResourceLoader.Load<GodotResourceCatalog>(BatchCatalogPath, "AI/Encounter Catalog load failed");
    }

    private BattleLayoutDefinition LoadLayout(string id) =>
        (ResourceLoader.Load<BattleLayoutResource>(_paths[id], string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Missing layout: {id}")).ToCoreDefinition();

    private (string[] Units, string[] Ai) LoadEncounter(string id)
    {
        EncounterDefinitionResource resource = ResourceLoader.Load<EncounterDefinitionResource>(_paths[id], string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Missing encounter: {id}");
        return (resource.MonsterUnitContentIds, resource.MonsterAiContentIds);
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("738491") }; background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        var panel = new VBoxContainer { Position = new Vector2(80, 60), Size = new Vector2(1440, 780) }; panel.AddThemeConstantOverride("separation", 18); background.AddChild(panel);
        var heading = new Label { Text = "Pure Run AI / Encounter Fixture" }; heading.AddThemeFontSizeOverride("font_size", 34); panel.AddChild(heading);
        var help = new Label { Text = "Left/Right: scenario   Space: single real AI turn   Enter: auto round   R: deterministic reset" }; help.AddThemeFontSizeOverride("font_size", 22); panel.AddChild(help);
        _title = new Label(); _title.AddThemeFontSizeOverride("font_size", 30); panel.AddChild(_title);
        _log = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(1400, 600) }; _log.AddThemeFontSizeOverride("font_size", 21); panel.AddChild(_log);
    }

    private void Refresh()
    {
        if (_title is null || _log is null || _state is null) return;
        _title.Text = _draftEncounter is null ? $"{_index + 1}/5  {Scenarios[_index]} — round {_state.Round}, active {_state.ActiveUnitId}" : $"DRAFT  {_draftEncounter.ContentId} — round {_state.Round}, active {_state.ActiveUnitId}";
        string units = string.Join(" | ", _state.Units.Values.OrderBy(value => value.Unit.InstanceId.Value, StringComparer.Ordinal).Select(value => $"{value.Unit.InstanceId}: HP {value.CurrentHealth}, MP {value.CurrentMana}, cell {value.Unit.Position}"));
        string history = _history.Count == 0 ? "(no turns executed)" : string.Join('\n', _history.TakeLast(12));
        string eliteNote = _unitAi.Count == 1 ? "\nNOTE: Single-enemy Elite scenario — Space and Enter both execute one actor, so their state result is intentionally identical." : string.Empty;
        _log.Text = $"Seed/state: {_state.RandomState}  Global step: {_step}\n{_last}{eliteNote}\n\nTURN HISTORY\n{history}\n\nState: {units}\n\nPresentation: semantic gameplay diagnostics only; formal enemy VFX not copied.";
    }

    private string CreateStateFingerprint()
    {
        if (_state is null) return string.Empty;
        string units = string.Join(';', _state.Units.Values.OrderBy(value => value.Unit.InstanceId.Value, StringComparer.Ordinal).Select(value => $"{value.Unit.InstanceId.Value}:{value.CurrentHealth}:{value.CurrentMana}:{value.Unit.Position.X},{value.Unit.Position.Y}"));
        string cursors = string.Join(';', _patternCursors.OrderBy(value => value.Key.Value, StringComparer.Ordinal).Select(value => $"{value.Key.Value}:{value.Value}"));
        return $"r={_state.Round}|a={_state.ActiveUnitId.Value}|rng={_state.RandomState}|u={units}|p={cursors}";
    }
}
