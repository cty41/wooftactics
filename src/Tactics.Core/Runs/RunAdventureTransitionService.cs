using Tactics.Core.Board;
using Tactics.Core.Content;

namespace Tactics.Core.Runs;

/// <summary>Deterministic transitions for the save-backed tile adventure state.</summary>
public sealed class RunAdventureTransitionService
{
    public static readonly ContentId InitialBoardContentId = new("adventure-board.pure-run.initial");

    public RunAdventureState CreateInitial(IReadOnlyList<RunCharacterState> party)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (party.Count != 3) throw new ArgumentException("Adventure requires exactly three party members.", nameof(party));
        GridPoint[] cells = [new(2, 5), new(1, 4), new(1, 6)];
        return new RunAdventureState(RunAdventureLifecycle.InitialExploration, InitialBoardContentId,
            party[0].CharacterId, party.Select((member, index) => new RunAdventureActorCell(member.CharacterId, cells[index])).ToArray(),
            RunAdventureEventContextKind.None, null, null, 0, 0, 0, 0, 0);
    }

    public PureRunState SelectLeader(PureRunState run, string actorId)
    {
        RunAdventureState state = Require(run);
        if (!state.ActorCells.Any(value => value.ActorId == actorId)) throw new InvalidOperationException("adventure.leader_not_found");
        if (state.LeaderId == actorId) return run;
        return Copy(run, state with { LeaderId = actorId, LeaderRevision = state.LeaderRevision + 1 });
    }

    public PureRunState MoveLeader(PureRunState run, AdventureBoardDefinition board, GridPoint destination)
    {
        RunAdventureState state = Require(run);
        RunAdventureActorCell leader = state.ActorCells.Single(value => value.ActorId == state.LeaderId);
        if (leader.Cell == destination) return run;
        GridPoint[] occupied = state.ActorCells.Where(value => value.ActorId != state.LeaderId).Select(value => value.Cell).ToArray();
        if (AdventureBoardPathfinder.FindPath(board, leader.Cell, destination, occupied).Count == 0)
            throw new InvalidOperationException("adventure.destination_unreachable");
        return Copy(run, state with
        {
            ActorCells = state.ActorCells.Select(value => value.ActorId == state.LeaderId ? value with { Cell = destination } : value).ToArray(),
            InteractionRevision = state.InteractionRevision + 1
        });
    }

    public PureRunState EnterBoard(PureRunState run, ContentId boardId)
    {
        RunAdventureState state = Require(run);
        GridPoint[] cells = [new(2, 5), new(1, 4), new(1, 6)];
        return Copy(run, state with
        {
            Lifecycle = RunAdventureLifecycle.MapActive,
            BoardContentId = boardId,
            LeaderId = run.Party[0].CharacterId,
            ActorCells = run.Party.Select((member, index) => new RunAdventureActorCell(member.CharacterId, cells[index])).ToArray(),
            SceneRevision = state.SceneRevision + 1
        });
    }

    public PureRunState ResolveBoard(PureRunState run)
    {
        RunAdventureState state = Require(run);
        if (state.BoardContentId.Value.EndsWith(".resolved", StringComparison.Ordinal)) return run;
        return Copy(run, state with
        {
            BoardContentId = new ContentId(state.BoardContentId.Value + ".resolved"),
            SceneRevision = state.SceneRevision + 1
        });
    }

    public PureRunState CommitExit(PureRunState run, PureRunMapDefinition map, string targetNodeId)
    {
        ArgumentNullException.ThrowIfNull(map);
        RunAdventureState state = Require(run);
        string currentNodeId = CurrentNodeId(run);
        bool direct = map.Connections.Any(edge => edge.FromNodeId == currentNodeId && edge.ToNodeId == targetNodeId);
        if (!direct) throw new InvalidOperationException("adventure.exit_not_immediate_successor");
        if (!IsExitUnlocked(run)) throw new InvalidOperationException("adventure.exit_locked");
        return Copy(run, state with { ExitRevision = state.ExitRevision + 1 });
    }

    public static IReadOnlyList<PureRunMapNodeDefinition> ImmediateSuccessors(PureRunState run, PureRunMapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        string current = CurrentNodeId(run);
        HashSet<string> allowed = run.MapState?.ReachableNodeIds.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        return map.Connections.Where(edge => edge.FromNodeId == current)
            .Select(edge => map.Nodes.Single(node => node.NodeId == edge.ToNodeId))
            .Where(node => allowed.Count == 0 || allowed.Contains(node.NodeId) || node.Layer is 1 or 2 or 3 or 5 or 7)
            .OrderBy(node => node.Lane).ThenBy(node => node.NodeId, StringComparer.Ordinal).ToArray();
    }

    public static bool IsExitUnlocked(PureRunState run)
    {
        if (run.AdventureState?.Lifecycle == RunAdventureLifecycle.InitialExploration) return true;
        if (run.NodeTransaction?.Kind == PureRunNodeKind.Store && run.Phase is PureRunPhase.ResolvingLayerFourNode or PureRunPhase.ResolvingLayerSixNode)
            return true;
        return run.AdventureState?.BoardContentId.Value.EndsWith(".resolved", StringComparison.Ordinal) == true ||
            run.NodeTransaction?.Committed == true ||
            run.Phase is PureRunPhase.AwaitingLayerFourChoice or PureRunPhase.ReadyForLayerFive or
                PureRunPhase.AwaitingLayerSixChoice or PureRunPhase.ReadyForBoss;
    }

    public static string CurrentNodeId(PureRunState run)
    {
        if (run.AdventureState?.Lifecycle == RunAdventureLifecycle.InitialExploration) return "start";
        const string marker = ".node.";
        string? boardValue = run.AdventureState?.BoardContentId.Value;
        int markerIndex = boardValue?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
        if (markerIndex >= 0)
        {
            string nodeId = boardValue![(markerIndex + marker.Length)..];
            nodeId = nodeId.EndsWith(".resolved", StringComparison.Ordinal)
                ? nodeId[..^".resolved".Length]
                : nodeId;
            return nodeId.Replace('-', '_');
        }
        if (run.NodeTransaction is { Committed: false } transaction) return transaction.NodeId;
        if (run.MapState is { } map) return map.CurrentNodeId;
        if (run.NodeTransaction is { } committedTransaction) return committedTransaction.NodeId;
        return run.BattlesCompleted switch
        {
            <= 0 => "start",
            1 => "layer_01_battle",
            2 => "layer_02_battle",
            _ => "layer_03_battle"
        };
    }

    public PureRunState BeginEventBattle(PureRunState run, RunAdventureEventContextKind context, string nodeId, string objectId)
    {
        if (context == RunAdventureEventContextKind.None) throw new ArgumentException("Event context cannot be None.", nameof(context));
        RunAdventureState state = Require(run);
        if (state.PendingEventContext != RunAdventureEventContextKind.None) throw new InvalidOperationException("adventure.event_already_pending");
        return Copy(run, state with { PendingEventContext = context, PendingEventNodeId = Required(nodeId), PendingEventObjectId = Required(objectId), InteractionRevision = state.InteractionRevision + 1 });
    }

    public PureRunState ResolveEventBattle(PureRunState run)
    {
        RunAdventureState state = Require(run);
        if (state.PendingEventContext == RunAdventureEventContextKind.None) return run;
        return Copy(run, state with
        {
            BoardContentId = new ContentId(state.BoardContentId.Value.EndsWith(".resolved", StringComparison.Ordinal)
                ? state.BoardContentId.Value : state.BoardContentId.Value + ".resolved"),
            PendingEventContext = RunAdventureEventContextKind.None,
            PendingEventNodeId = null,
            PendingEventObjectId = null,
            SceneRevision = state.SceneRevision + 1
        });
    }

    public static bool IsAdjacent(GridPoint actor, GridPoint target) => Math.Abs(actor.X - target.X) + Math.Abs(actor.Y - target.Y) == 1;

    private static RunAdventureState Require(PureRunState run) => run.AdventureState ?? throw new InvalidOperationException("adventure.state_missing");
    private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value cannot be empty.") : value.Trim();

    private static PureRunState Copy(PureRunState run, RunAdventureState adventure) => new(
        run.RunId, run.Seed, run.Revision + 1, run.Phase, run.EncounterIndex, run.EncounterContentId, run.Party,
        run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression, run.AppliedTransactionKeys, run.Gold,
        run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems, run.Checkpoint, run.MapState, run.NodeTransaction,
        run.EscortState, adventure with { Revision = adventure.Revision + 1 });
}
