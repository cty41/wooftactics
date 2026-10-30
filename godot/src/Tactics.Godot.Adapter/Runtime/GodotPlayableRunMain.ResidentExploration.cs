using Godot;
using Tactics.Application.Runs;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// One entry point for every tile-exploration page (start camp, battle entry, rest, store, treasure, event and
/// resolved pages): borrow the node's formal resident map from the Act host instead of drawing a placeholder board.
/// </summary>
public partial class GodotPlayableRunMain
{
    /// <summary>Everything a page needs to show a node on its resident map.</summary>
    private sealed record ResidentExploration(GodotAdventureMapInstance Map, AdventureMapTemplateDefinition Template,
        PureRunState Run, AdventureBoardDefinition Definition);

    /// <summary>
    /// Builds the exploration board from the node's resident template. Returns null when no resident world exists
    /// (headless contexts) or when the prepared map does not match the authored binding; the latter is reported
    /// loudly so a placeholder board can never replace a formal map unnoticed.
    /// </summary>
    /// <param name="exitsUnlocked">Whether the page lets the leader leave; locked exits stay visible but inert.</param>
    /// <param name="keepObject">Optional filter over template objects (for example a resolved escort NPC).</param>
    /// <param name="extraObjects">Page-owned objects that are not part of the template (for example the encounter marker).</param>
    private ResidentExploration? TryPrepareResidentExploration(PureRunState run, string nodeId, bool exitsUnlocked,
        RunAdventureEventContextKind eventContext = RunAdventureEventContextKind.None,
        Func<AdventureBoardObject, bool>? keepObject = null,
        Func<AdventureMapTemplateDefinition, IReadOnlyList<AdventureBoardObject>>? extraObjects = null)
    {
        if (_actWorld is null) return null;
        if (!TryGetResidentNodeMap(nodeId, out GodotAdventureMapInstance map, out AdventureMapTemplateDefinition template) ||
            (eventContext != RunAdventureEventContextKind.None &&
             _actContent!.Blueprint.Bindings[nodeId].EventContext != eventContext))
        {
            string reason = $"exploration.resident_template_mismatch:{nodeId}";
            GD.PushWarning($"{reason} — falling back to the placeholder board.");
            SetStatus(reason);
            return null;
        }

        HashSet<string> allowedTargets = CreateExitObjects(run, nodeId, exitsUnlocked)
            .Select(value => value.TargetNodeId!).ToHashSet(StringComparer.Ordinal);
        var objects = new List<AdventureBoardObject>();
        foreach (AdventureBoardObject value in template.Board.Objects)
        {
            if (keepObject is not null && !keepObject(value)) continue;
            if (value.Kind != AdventureObjectKind.Exit) { objects.Add(value); continue; }
            if (value.TargetNodeId is string target && allowedTargets.Contains(target))
                objects.Add(value with { IsLocked = !exitsUnlocked });
        }
        if (extraObjects is not null) objects.AddRange(extraObjects(template));

        AdventureBoardDefinition board = template.Board with
        {
            ContentId = run.AdventureState!.BoardContentId,
            Objects = objects.ToArray()
        };
        AdventureActorPlacement[] actors = run.AdventureState.ActorCells
            .Select(value => new AdventureActorPlacement(value.ActorId, value.Cell)).ToArray();
        if (!IsPlacementValid(board, actors))
        {
            // Rendering is read-only: seating happens in the enter-node transaction (EnterBoardSeated /
            // EnsurePartySeated). A still-invalid placement here means that step was skipped, so say so loudly.
            string reason = $"exploration.party_placement_invalid:{nodeId}";
            GD.PushWarning($"{reason} — resident map not shown.");
            SetStatus(reason);
            return null;
        }
        return new ResidentExploration(map, template, run, board with { Actors = actors });
    }

    /// <summary>
    /// Enter-node transaction step: enters the board and, when the node has a matching resident template, seats the
    /// party on its authored entry slots in the same state transition, so one CAS save covers both.
    /// </summary>
    private PureRunState EnterBoardSeated(PureRunState state, ContentId boardId, string nodeId)
    {
        var service = new RunAdventureTransitionService();
        PureRunState entered = service.EnterBoard(state, boardId);
        return SeatPartyOnTemplate(service, entered, nodeId);
    }

    private PureRunState SeatPartyOnTemplate(RunAdventureTransitionService service, PureRunState run, string nodeId)
    {
        if (!TryGetResidentNodeMap(nodeId, out _, out AdventureMapTemplateDefinition template)) return run;
        AdventureActorPlacement[] current = run.AdventureState!.ActorCells
            .Select(value => new AdventureActorPlacement(value.ActorId, value.Cell)).ToArray();
        if (IsPlacementValid(template.Board, current)) return run;
        return service.PlaceParty(run, AdventureExplorationSession.Restore(run, template).ActorCells.ToArray());
    }

    /// <summary>
    /// For pages that start on an already-entered board (the start camp): seats the party only when the current
    /// placement is invalid, with one CAS save; a valid placement performs no save at all.
    /// </summary>
    private bool TryEnsurePartySeated(ref PureRunState run, string nodeId)
    {
        if (_run is null || run.AdventureState is null) return true;
        PureRunState seatedPreview;
        try { seatedPreview = SeatPartyOnTemplate(new RunAdventureTransitionService(), run, nodeId); }
        catch (InvalidOperationException error) { SetStatus(error.Message); return false; }
        if (ReferenceEquals(seatedPreview, run)) return true;
        RunSessionResult placed = _run.ApplyMutation(state =>
        {
            try { return new RunMutationResult(true, null, SeatPartyOnTemplate(new RunAdventureTransitionService(), state, nodeId)); }
            catch (InvalidOperationException error) { return new RunMutationResult(false, error.Message, state); }
        });
        if (!placed.Succeeded) { SetStatus(placed.ErrorCode); return false; }
        run = placed.Snapshot!.ActiveRun!;
        return true;
    }

    private static bool IsPlacementValid(AdventureBoardDefinition board, IReadOnlyList<AdventureActorPlacement> actors)
    {
        if (actors.Any(actor => board.Objects.Any(value => value.Cell == actor.Cell))) return false;
        try { (board with { Actors = actors }).Validate(); return true; }
        catch (ArgumentException) { return false; }
    }

    /// <summary>
    /// Puts the page's board on the node's resident surface. With <paramref name="successorTargets"/> the Act atlas is
    /// laid out and the direct successor maps stay visible with route lines (post-battle); otherwise only this map shows.
    /// </summary>
    private void BindResidentExploration(GodotAdventureBoardView board, string nodeId, ResidentExploration context,
        IReadOnlyCollection<string>? successorTargets = null)
    {
        GodotActWorldHost host = _actWorld!;
        board.ActorUnitDefinitions = ResidentActorUnits(context.Run);
        GodotNodeActivationLease lease = host.ActivateNode(nodeId);
        if (successorTargets is not null) PreparePostBattleWorld(nodeId, context.Template, successorTargets, board);
        else
        {
            RestoreHostBaseline(host);
            foreach (var node in host.Nodes) node.Value.Visible = node.Key == nodeId;
            host.Visible = true;
        }
        board.BindResidentSurface(host, lease, context.Map.Surface);
    }

    /// <summary>Battle-sprite presentation for each party member, keyed by the adventure actor id.</summary>
    private Dictionary<string, UnitDefinitionResource> ResidentActorUnits(PureRunState run)
    {
        var units = new Dictionary<string, UnitDefinitionResource>(StringComparer.Ordinal);
        foreach (RunCharacterState member in run.Party)
            if (_unitResources.TryGetValue(member.UnitContentId, out UnitDefinitionResource? definition))
                units[member.CharacterId] = definition;
        return units;
    }

    private static string[] ExitTargets(AdventureBoardDefinition definition) =>
        definition.Objects.Where(value => value.Kind == AdventureObjectKind.Exit && value.TargetNodeId is not null)
            .Select(value => value.TargetNodeId!).ToArray();

    /// <summary>
    /// Last-resort board for headless contexts and for variants the prepared Act host does not carry.
    /// The single remaining placeholder: a walled 10x10 field with no formal terrain.
    /// </summary>
    private static AdventureBoardDefinition CreatePlaceholderBoard(ContentId boardId, IReadOnlyList<AdventureActorPlacement> actors,
        IReadOnlyList<AdventureBoardObject> objects, GridPoint exitCell)
    {
        GridPoint[] perimeter = Enumerable.Range(0, 10).SelectMany(value =>
                new[] { new GridPoint(value, 0), new GridPoint(value, 9) })
            .Concat(Enumerable.Range(1, 8).SelectMany(value => new[] { new GridPoint(0, value), new GridPoint(9, value) }))
            .Distinct().ToArray();
        return new AdventureBoardDefinition(boardId, 10, 10, perimeter, objects, actors, new GridPoint(1, 5), exitCell);
    }
}
