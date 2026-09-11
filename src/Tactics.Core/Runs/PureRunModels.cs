using Tactics.Core.Content;
using Tactics.Core.Board;
using Tactics.Core.Items;
using Tactics.Core.Units;

namespace Tactics.Core.Runs;

public enum PureRunPhase
{
    Ready,
    PendingBattle,
    SliceCompleted,
    AwaitingLayerFourChoice,
    ResolvingLayerFourNode,
    ReadyForLayerFive,
    ReadyForLayerSix,
    AwaitingLayerSixChoice,
    ResolvingLayerSixNode,
    ReadyForBoss,
    Defeated,
    Abandoned
}

public enum PureRunOutcome
{
    SliceCompleted,
    BossVictory,
    Defeated,
    Abandoned
}

public sealed record PureRunPartyTemplate(
    string CharacterId,
    ContentId UnitContentId,
    ContentId StartingSkillContentId,
    UnitAttributes Attributes,
    int Level = 1,
    IReadOnlyList<ContentId>? StartingSkillChoices = null,
    bool SeededStartingSkill = false,
    IReadOnlyList<ContentId>? InherentSkills = null)
{
    public IReadOnlyList<ContentId> EffectiveStartingSkillChoices =>
        StartingSkillChoices is { Count: > 0 } ? StartingSkillChoices : [StartingSkillContentId];

    public IReadOnlyList<ContentId> EffectiveInherentSkills => InherentSkills ?? Array.Empty<ContentId>();
}

public sealed record PendingRunStartingSkillChoice(string CharacterId, ContentId SkillContentId);

public sealed record PendingRunSetup(
    int Seed,
    int CurrentCharacterIndex,
    IReadOnlyList<PendingRunStartingSkillChoice> Choices)
{
    public string? CurrentCharacterId { get; init; }
    public IReadOnlyList<string> SelectedCharacterIds { get; init; } = Array.Empty<string>();
}

public sealed record PureRunDefinition
{
    public PureRunDefinition(
        ContentId contentId,
        IEnumerable<ContentId> encounters,
        IEnumerable<PureRunPartyTemplate> party,
        ContentId? layerFourMapContentId = null)
    {
        ContentId = contentId;
        Encounters = encounters?.ToArray() ?? throw new ArgumentNullException(nameof(encounters));
        Party = party?.ToArray() ?? throw new ArgumentNullException(nameof(party));
        LayerFourMapContentId = layerFourMapContentId;
        if (Encounters.Count != 3 || Encounters.Distinct().Count() != 3)
            throw new ArgumentException("The Phase 6B slice requires exactly three unique encounters.", nameof(encounters));
        if (Party.Count is not (3 or 4 or 5) || Party.Select(item => item.CharacterId).Distinct(StringComparer.Ordinal).Count() != Party.Count)
            throw new ArgumentException("Pure Run requires three, four, or five unique candidate characters.", nameof(party));
        foreach (PureRunPartyTemplate template in Party)
        {
            IReadOnlyList<ContentId> choices = template.EffectiveStartingSkillChoices;
            if (choices.Count is not (1 or 3) || choices.Distinct().Count() != choices.Count ||
                !choices.Contains(template.StartingSkillContentId))
                throw new ArgumentException("Starting skill choices must contain the default and have one or three unique entries.", nameof(party));
        }
    }

    public ContentId ContentId { get; }
    public IReadOnlyList<ContentId> Encounters { get; }
    public IReadOnlyList<PureRunPartyTemplate> Party { get; }
    public int ActivePartySize => 3;
    public ContentId? LayerFourMapContentId { get; }
}

public sealed record RunEquipmentState(ItemInstanceId InstanceId, ContentId DefinitionId, EquipmentSlot Slot);
public sealed record RunLearnedSkillState(string BranchId, int Level, ContentId DefinitionId);

public sealed record RunCharacterState
{
    public RunCharacterState(
        string characterId,
        ContentId unitContentId,
        int level,
        UnitAttributes attributes,
        int currentHealth,
        int maxHealth,
        int currentMana,
        int maxMana,
        bool isDead,
        IReadOnlyList<ContentId> learnedSkills,
        IReadOnlyList<RunEquipmentState>? equipment = null,
        IReadOnlyList<BattleConsumableState>? carriedConsumables = null,
        IReadOnlyList<RunLearnedSkillState>? learnedSkillStates = null,
        ContentId? startingSkillContentId = null)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            throw new ArgumentException("Character ID cannot be empty.", nameof(characterId));
        if (level < 1 || maxHealth < 1 || maxMana < 0)
            throw new ArgumentOutOfRangeException(nameof(level));
        if (currentHealth < 0 || currentHealth > maxHealth || currentMana < 0 || currentMana > maxMana)
            throw new ArgumentOutOfRangeException(nameof(currentHealth));
        if (isDead != (currentHealth == 0))
            throw new ArgumentException("Dead state must agree with current health.", nameof(isDead));
        CharacterId = characterId.Trim();
        UnitContentId = unitContentId;
        Level = level;
        Attributes = attributes;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        CurrentMana = currentMana;
        MaxMana = maxMana;
        IsDead = isDead;
        LearnedSkills = learnedSkills?.Distinct().OrderBy(value => value.Value, StringComparer.Ordinal).ToArray()
            ?? throw new ArgumentNullException(nameof(learnedSkills));
        LearnedSkillStates = (learnedSkillStates ?? LearnedSkills.Select(ToLegacySkillState))
            .GroupBy(value => value.BranchId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(value => value.Level).First())
            .OrderBy(value => value.BranchId, StringComparer.Ordinal).ToArray();
        Equipment = equipment?.OrderBy(value => value.Slot).ToArray() ?? Array.Empty<RunEquipmentState>();
        CarriedConsumables = carriedConsumables?.OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal).ToArray()
            ?? Array.Empty<BattleConsumableState>();
        StartingSkillContentId = startingSkillContentId;
    }

    public string CharacterId { get; }
    public ContentId UnitContentId { get; }
    public int Level { get; }
    public UnitAttributes Attributes { get; }
    public int CurrentHealth { get; }
    public int MaxHealth { get; }
    public int CurrentMana { get; }
    public int MaxMana { get; }
    public bool IsDead { get; }
    public IReadOnlyList<ContentId> LearnedSkills { get; }
    public IReadOnlyList<RunLearnedSkillState> LearnedSkillStates { get; }
    public IReadOnlyList<RunEquipmentState> Equipment { get; }
    public IReadOnlyList<BattleConsumableState> CarriedConsumables { get; }
    public ContentId? StartingSkillContentId { get; }

    private static RunLearnedSkillState ToLegacySkillState(ContentId id)
    {
        string value = id.Value;
        if (value == "skill.poison-spear.lv1")
            return new RunLearnedSkillState("amazon.poison-spear", 1, id);
        int marker = value.LastIndexOf(".lv", StringComparison.Ordinal);
        int level = marker >= 0 && int.TryParse(value[(marker + 3)..], out int parsed) ? parsed : 1;
        string branch = marker >= 0 ? value["skill.".Length..marker] : value.StartsWith("skill.", StringComparison.Ordinal) ? value["skill.".Length..] : value;
        return new RunLearnedSkillState(branch, level, id);
    }
}

public sealed record PendingProgression(
    string TransactionKey,
    string EncounterId,
    string CharacterId,
    int AttributePoints = 1,
    UnitAttributes? ProposedAttributes = null,
    ContentId? SelectedSkillContentId = null,
    bool LevelApplied = false);

public sealed record PureRunSummary(
    string RunId,
    int Seed,
    PureRunOutcome Outcome,
    int BattlesCompleted,
    int EnemiesDefeated,
    int TotalGoldEarned,
    IReadOnlyList<ContentId> AcquiredItems,
    IReadOnlyList<string> DeadCharacters,
    IReadOnlyList<string> AppliedTransactionKeys,
    int NodesVisited = 0,
    int EventsCompleted = 0,
    bool BossDefeated = false,
    ContentId? TerminalEncounterId = null);

public sealed record RunEncounterCheckpoint(
    ContentId EncounterContentId,
    int EncounterIndex,
    long Revision,
    IReadOnlyList<RunCharacterState> Party,
    IReadOnlyList<BattleConsumableState> BackpackConsumables,
    IReadOnlyList<RunEquipmentState> BackpackEquipment);

public enum RunEscortLifecycle { Accepted, Traveling, BattlePending, Completed, Failed }

public sealed record RunEscortState(
    string QuestId,
    RunEscortLifecycle Lifecycle,
    bool ProtectedNpcAlive,
    string AcceptedNodeId,
    string DestinationNodeId,
    long Revision);

public enum RunAdventureLifecycle
{
    InitialExploration,
    MapActive
}

public enum RunAdventureEventContextKind
{
    None,
    CursedChestMimic,
    FallenAltarGuardian,
    LostVillagerEscort
}

public sealed record RunAdventureActorCell(string ActorId, GridPoint Cell);

/// <summary>Authoritative, save-backed state for the tile adventure presentation and event routing.</summary>
public sealed record RunAdventureState(
    RunAdventureLifecycle Lifecycle,
    ContentId BoardContentId,
    string LeaderId,
    IReadOnlyList<RunAdventureActorCell> ActorCells,
    RunAdventureEventContextKind PendingEventContext,
    string? PendingEventNodeId,
    string? PendingEventObjectId,
    long Revision,
    long LeaderRevision,
    long InteractionRevision,
    long ExitRevision,
    long SceneRevision);

public sealed record PureRunState
{
    public PureRunState(
        string runId,
        int seed,
        long revision,
        PureRunPhase phase,
        int encounterIndex,
        ContentId encounterContentId,
        IReadOnlyList<RunCharacterState> party,
        IReadOnlyList<BattleConsumableState>? backpackConsumables = null,
        IReadOnlyList<RunEquipmentState>? backpackEquipment = null,
        IReadOnlyList<PendingProgression>? pendingProgression = null,
        IReadOnlyList<string>? appliedTransactionKeys = null,
        int gold = 0,
        int battlesCompleted = 0,
        int enemiesDefeated = 0,
        IReadOnlyList<ContentId>? acquiredItems = null,
        RunEncounterCheckpoint? checkpoint = null,
        PureRunMapState? mapState = null,
        RunNodeTransaction? nodeTransaction = null,
        RunEscortState? escortState = null,
        RunAdventureState? adventureState = null)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID cannot be empty.", nameof(runId));
        if (revision < 1 || encounterIndex is < 0 or > 6 || gold is < 0 or > 50)
            throw new ArgumentOutOfRangeException(nameof(revision));
        RunId = runId.Trim();
        Seed = seed;
        Revision = revision;
        Phase = phase;
        EncounterIndex = encounterIndex;
        EncounterContentId = encounterContentId;
        Party = party?.ToArray() ?? throw new ArgumentNullException(nameof(party));
        if (Party.Count != 3 || Party.Select(item => item.CharacterId).Distinct(StringComparer.Ordinal).Count() != 3)
            throw new ArgumentException("Run party must contain three unique characters.", nameof(party));
        BackpackConsumables = backpackConsumables?.OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal).ToArray()
            ?? Array.Empty<BattleConsumableState>();
        BackpackEquipment = backpackEquipment?.OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal).ToArray()
            ?? Array.Empty<RunEquipmentState>();
        string[] instanceIds = BackpackConsumables.Select(value => value.InstanceId.Value)
            .Concat(BackpackEquipment.Select(value => value.InstanceId.Value))
            .Concat(Party.SelectMany(value => value.CarriedConsumables).Select(value => value.InstanceId.Value))
            .Concat(Party.SelectMany(value => value.Equipment).Select(value => value.InstanceId.Value)).ToArray();
        if (instanceIds.Distinct(StringComparer.Ordinal).Count() != instanceIds.Length)
            throw new ArgumentException("Run item instance identities must be globally unique.");
        PendingProgression = pendingProgression?.ToArray() ?? Array.Empty<PendingProgression>();
        AppliedTransactionKeys = appliedTransactionKeys?.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            ?? Array.Empty<string>();
        Gold = gold;
        BattlesCompleted = battlesCompleted;
        EnemiesDefeated = enemiesDefeated;
        AcquiredItems = acquiredItems?.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray() ?? Array.Empty<ContentId>();
        Checkpoint = checkpoint;
        MapState = mapState;
        NodeTransaction = nodeTransaction;
        EscortState = escortState;
        AdventureState = adventureState;
    }

    public string RunId { get; }
    public int Seed { get; }
    public long Revision { get; }
    public PureRunPhase Phase { get; }
    public int EncounterIndex { get; }
    public ContentId EncounterContentId { get; }
    public IReadOnlyList<RunCharacterState> Party { get; }
    public IReadOnlyList<BattleConsumableState> BackpackConsumables { get; }
    public IReadOnlyList<RunEquipmentState> BackpackEquipment { get; }
    public IReadOnlyList<PendingProgression> PendingProgression { get; }
    public IReadOnlyList<string> AppliedTransactionKeys { get; }
    public int Gold { get; }
    public int BattlesCompleted { get; }
    public int EnemiesDefeated { get; }
    public IReadOnlyList<ContentId> AcquiredItems { get; }
    public RunEncounterCheckpoint? Checkpoint { get; }
    public PureRunMapState? MapState { get; }
    public RunNodeTransaction? NodeTransaction { get; }
    public RunEscortState? EscortState { get; }
    public RunAdventureState? AdventureState { get; }
}

public sealed record BattlePartyResult(
    string CharacterId,
    int CurrentHealth,
    int CurrentMana,
    bool IsDead,
    IReadOnlyList<BattleConsumableState> CarriedConsumables,
    bool RunPermanentlyDead = false);

public sealed record PureRunBattleResult(
    string RunId,
    long CheckpointRevision,
    ContentId EncounterContentId,
    bool PlayerVictory,
    int TotalRounds,
    int EnemiesDefeated,
    IReadOnlyList<BattlePartyResult> Party);

public sealed record PureRunSettlementResult(
    bool Succeeded,
    string? RejectionCode,
    PureRunState? ActiveRun,
    PureRunSummary? TerminalSummary,
    bool WasDuplicate);
