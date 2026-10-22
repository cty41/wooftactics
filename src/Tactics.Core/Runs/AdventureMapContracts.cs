using Tactics.Core.Board;
using Tactics.Core.Content;

namespace Tactics.Core.Runs;

/// <summary>Stable identifiers for the engine-neutral roguelike map contracts.</summary>
public static class AdventureMapContractIds
{
    public const string StartFlow = "ROGUELIKE-START-FLOW-001";
    public const string MapCamera = "ROGUELIKE-MAP-CAMERA-001";
    public const string Template = "ROGUELIKE-MAP-TEMPLATE-001";
    public const string NodeIntel = "ROGUELIKE-NODE-INTEL-001";
    public const string NodeRecovery = "ROGUELIKE-NODE-RECOVERY-001";
}

public static class AdventureMapStateLayers
{
    public const string Planning = "planning";
    public const string TacticalPreview = "tactical-preview";
    public const string Current = "current";
    public const string Completed = "completed";
    public const string EventActive = "event-active";
    public const string Combat = "combat";
    public const string Resolved = "resolved";

    public static readonly IReadOnlyList<string> Required = [Planning, TacticalPreview, Current, Completed];
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(
        [Planning, TacticalPreview, Current, Completed, EventActive, Combat, Resolved], StringComparer.Ordinal);
}

public sealed record AdventureMapSlot(string SlotId, GridPoint Cell);
public sealed record AdventureMapAnchor(string AnchorId, GridPoint Cell);
public sealed record AdventureMapExitAnchor(string ExitId, GridPoint Cell, string TargetNodeId, string TargetEntryId);

/// <summary>
/// Engine-neutral definition of one fixed 10x10 playable map. Godot resources are projections of this contract.
/// Contract: ROGUELIKE-MAP-TEMPLATE-001 (approved target).
/// </summary>
public sealed record AdventureMapTemplateDefinition(
    ContentId ContentId,
    AdventureBoardDefinition Board,
    IReadOnlyList<AdventureMapSlot> CandidateSlots,
    IReadOnlyList<AdventureMapSlot> PartyEntrySlots,
    IReadOnlyList<AdventureMapSlot> PlayerBattleSlots,
    IReadOnlyList<AdventureMapSlot> EnemyBattleSlots,
    IReadOnlyList<AdventureMapAnchor> Entries,
    IReadOnlyList<AdventureMapExitAnchor> Exits,
    IReadOnlyList<AdventureMapAnchor> ConnectionAnchors,
    AdventureMapAnchor CameraFocusAnchor,
    AdventureMapAnchor AtlasBoundsAnchor,
    IReadOnlyList<string> StateLayerIds,
    bool IsTerminal = false,
    IReadOnlyList<GridPoint>? ShallowWaterCells = null)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Board);
        Board.Validate();
        IReadOnlyList<GridPoint> water = ShallowWaterCells ?? Array.Empty<GridPoint>();
        if (water.Distinct().Count() != water.Count ||
            water.Any(cell => !Board.Contains(cell) || Board.BlockedCells.Contains(cell)))
            throw new ArgumentException("Shallow water cells must be unique, inside the board and not blocked.", nameof(ShallowWaterCells));
        RequireSlots(CandidateSlots, nameof(CandidateSlots));
        RequireSlots(PartyEntrySlots, nameof(PartyEntrySlots));
        RequireSlots(PlayerBattleSlots, nameof(PlayerBattleSlots));
        RequireSlots(EnemyBattleSlots, nameof(EnemyBattleSlots));
        if (Entries.Count == 0)
            throw new ArgumentException("Map templates require entry anchors.");
        if (IsTerminal)
        {
            if (Exits.Count != 0 || ConnectionAnchors.Count != 0 || Board.Objects.Any(value => value.Kind == AdventureObjectKind.Exit))
                throw new ArgumentException("Terminal map templates cannot have outgoing exits or connection anchors.");
        }
        else if (Exits.Count == 0 || ConnectionAnchors.Count == 0)
            throw new ArgumentException("Non-terminal map templates require exit and connection anchors.");

        AdventureMapSlot[] slots = CandidateSlots.Concat(PartyEntrySlots).Concat(PlayerBattleSlots)
            .Concat(EnemyBattleSlots).ToArray();
        if (slots.Select(value => value.SlotId).Distinct(StringComparer.Ordinal).Count() != slots.Length ||
            slots.Select(value => value.Cell).Distinct().Count() != slots.Length)
            throw new ArgumentException("Map template slots must have unique ids and cells.");

        GridPoint[] anchors = Entries.Select(value => value.Cell).Concat(Exits.Select(value => value.Cell))
            .Concat(ConnectionAnchors.Select(value => value.Cell)).Append(CameraFocusAnchor.Cell)
            .Append(AtlasBoundsAnchor.Cell).ToArray();
        if (slots.Any(value => !Board.Contains(value.Cell)) || anchors.Any(cell => !Board.Contains(cell)))
            throw new ArgumentException("Map template slots and anchors must be inside the board.");
        if (Entries.Select(value => value.AnchorId).Distinct(StringComparer.Ordinal).Count() != Entries.Count ||
            Exits.Select(value => value.ExitId).Distinct(StringComparer.Ordinal).Count() != Exits.Count ||
            ConnectionAnchors.Select(value => value.AnchorId).Distinct(StringComparer.Ordinal).Count() != ConnectionAnchors.Count)
            throw new ArgumentException("Map template anchor ids must be unique within their role.");
        if (Exits.Any(value => string.IsNullOrWhiteSpace(value.TargetNodeId) || string.IsNullOrWhiteSpace(value.TargetEntryId)))
            throw new ArgumentException("Every exit must bind a target node and target entry.");
        if (StateLayerIds.Count == 0 || StateLayerIds.Distinct(StringComparer.Ordinal).Count() != StateLayerIds.Count ||
            StateLayerIds.Any(value => !AdventureMapStateLayers.Known.Contains(value)) ||
            AdventureMapStateLayers.Required.Any(required => !StateLayerIds.Contains(required, StringComparer.Ordinal)))
            throw new ArgumentException("Map template state layers are missing, duplicated, or unknown.");

        foreach (AdventureMapAnchor entry in Entries)
        foreach (AdventureMapSlot slot in CandidateSlots.Concat(PartyEntrySlots))
            if (AdventureBoardPathfinder.FindPath(Board, entry.Cell, slot.Cell).Count == 0)
                throw new ArgumentException("Exploration slots must be reachable from every entry.");

        foreach (AdventureMapAnchor entry in Entries)
        foreach (AdventureMapExitAnchor exit in Exits)
            if (AdventureBoardPathfinder.FindPath(Board, entry.Cell, exit.Cell).Count == 0)
                throw new ArgumentException("Every entry must be able to reach every exit.");
    }

    private static void RequireSlots(IReadOnlyList<AdventureMapSlot>? slots, string name)
    {
        if (slots is null || slots.Count == 0) throw new ArgumentException("Map template requires slots.", name);
    }
}

/// <summary>Planning information persisted per route node. Contract: ROGUELIKE-NODE-INTEL-001.</summary>
public enum PureRunNodeIntelState { Planning, TacticalPreview, Current, Completed }

public sealed record ExitIntelSnapshot(
    string ExitId,
    string TargetNodeId,
    PureRunNodeKind TargetKind,
    PureRunNodeIntelState IntelState,
    string Direction,
    string? ThreatLevel = null,
    string? EnemyFamily = null,
    IReadOnlyList<AdventureObjectKind>? ObjectKinds = null);

/// <summary>
/// Process-local actor placement. It is rebuilt from template slots and is never part of a Run save.
/// Contract: ROGUELIKE-NODE-RECOVERY-001 (approved target).
/// </summary>
public sealed class AdventureExplorationSession
{
    private AdventureExplorationSession(string runId, ContentId boardContentId, string leaderId,
        IReadOnlyList<RunAdventureActorCell> actorCells)
    {
        RunId = runId;
        BoardContentId = boardContentId;
        LeaderId = leaderId;
        ActorCells = actorCells;
    }

    public string RunId { get; }
    public ContentId BoardContentId { get; }
    public string LeaderId { get; private set; }
    public IReadOnlyList<RunAdventureActorCell> ActorCells { get; private set; }

    public static AdventureExplorationSession Restore(PureRunState run, AdventureMapTemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(template);
        template.Validate();
        return Restore(run, template.PartyEntrySlots.Select(value => value.Cell).ToArray());
    }

    public static AdventureExplorationSession Restore(PureRunState run, IReadOnlyList<GridPoint> entrySlots)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(entrySlots);
        RunAdventureState adventure = run.AdventureState ?? throw new InvalidOperationException("adventure.state_missing");
        if (entrySlots.Count < run.Party.Count)
            throw new ArgumentException("Board does not provide enough party entry slots.", nameof(entrySlots));
        RunCharacterState leader = run.Party.FirstOrDefault(value => !value.IsDead && value.CharacterId == adventure.LeaderId)
            ?? run.Party.FirstOrDefault(value => !value.IsDead)
            ?? throw new InvalidOperationException("adventure.no_living_leader");
        RunAdventureActorCell[] cells = run.Party.Select((member, index) =>
            new RunAdventureActorCell(member.CharacterId, entrySlots[index])).ToArray();
        return new AdventureExplorationSession(run.RunId, adventure.BoardContentId, leader.CharacterId, cells);
    }

    public bool Matches(PureRunState run) => run.RunId == RunId && run.AdventureState?.BoardContentId == BoardContentId &&
        run.Party.Select(value => value.CharacterId).Order(StringComparer.Ordinal)
            .SequenceEqual(ActorCells.Select(value => value.ActorId).Order(StringComparer.Ordinal));

    public GridPoint CellOf(string actorId) => ActorCells.Single(value => value.ActorId == actorId).Cell;

    public void SetLeader(string actorId)
    {
        if (!ActorCells.Any(value => value.ActorId == actorId)) throw new InvalidOperationException("adventure.actor_not_found");
        LeaderId = actorId;
    }

    public void SetCell(string actorId, GridPoint cell)
    {
        if (!ActorCells.Any(value => value.ActorId == actorId)) throw new InvalidOperationException("adventure.actor_not_found");
        ActorCells = ActorCells.Select(value => value.ActorId == actorId ? value with { Cell = cell } : value).ToArray();
    }

    public AdventureActorPlacement[] Placements() => ActorCells
        .OrderBy(value => value.ActorId, StringComparer.Ordinal)
        .Select(value => new AdventureActorPlacement(value.ActorId, value.Cell)).ToArray();

    public PureRunState Hydrate(PureRunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        RunAdventureState adventure = run.AdventureState ?? throw new InvalidOperationException("adventure.state_missing");
        if (adventure.BoardContentId != BoardContentId) throw new InvalidOperationException("adventure.session_board_mismatch");
        RunAdventureState hydrated = adventure with { LeaderId = LeaderId, ActorCells = ActorCells.ToArray() };
        return new PureRunState(run.RunId, run.Seed, run.Revision, run.Phase, run.EncounterIndex,
            run.EncounterContentId, run.Party, run.BackpackConsumables, run.BackpackEquipment,
            run.PendingProgression, run.AppliedTransactionKeys, run.Gold, run.BattlesCompleted,
            run.EnemiesDefeated, run.AcquiredItems, run.Checkpoint, run.MapState, run.NodeTransaction,
            run.EscortState, hydrated);
    }
}
