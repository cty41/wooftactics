using Godot;
using Tactics.Core.Board;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Post-combat free exploration on the node's own resident map: same Surface as the fight, with the direct
/// successor maps visible and joined to the top-right rim exits by route lines.
/// </summary>
public partial class GodotPlayableRunMain
{
    private const string PostBattleRoutesName = "PostBattleRoutes";

    private Dictionary<string, Transform2D> _hostBaselineTransforms = new();
    private GodotActWorldHost? _hostBaselineHost;

    /// <summary>Undoes the post-battle atlas layout: original transforms back, route lines removed.</summary>
    private void RestoreHostBaseline(GodotActWorldHost host)
    {
        if (_hostBaselineHost != host) return;
        foreach ((string id, Transform2D transform) in _hostBaselineTransforms)
        {
            if (!host.Nodes.TryGetValue(id, out GodotAdventureMapInstance? map)) continue;
            map.Transform = transform;
            if (map.GetNodeOrNull(PostBattleRoutesName) is Node routes)
            {
                map.RemoveChild(routes);
                routes.QueueFree();
            }
        }
    }

    /// <summary>Resolves the node's resident map only when it still matches the authored Act binding.</summary>
    private bool TryGetResidentNodeMap(string nodeId, out GodotAdventureMapInstance map, out AdventureMapTemplateDefinition template)
    {
        map = null!;
        template = null!;
        if (_actContent is null || !_actContent.Blueprint.Bindings.TryGetValue(nodeId, out PureRunNodeContentBinding? binding) ||
            _actWorld is not { IsPrepared: true } host || !GodotObject.IsInstanceValid(host) || host.IsQueuedForDeletion() ||
            !host.Nodes.TryGetValue(nodeId, out GodotAdventureMapInstance? found) ||
            found.Surface.Template?.ToCoreDefinition() is not AdventureMapTemplateDefinition definition ||
            definition.ContentId != binding.TemplateId) return false;
        map = found;
        template = definition;
        return true;
    }

    /// <summary>
    /// Lays the Act's maps out on the isometric atlas, shows only the current map and its allowed successors, draws
    /// one route line per exit and tells the board which points the camera must keep in frame.
    /// </summary>
    private void PreparePostBattleWorld(string nodeId, AdventureMapTemplateDefinition template, IReadOnlyCollection<string> allowedTargets,
        GodotAdventureBoardView board)
    {
        GodotActWorldHost host = _actWorld!;
        GodotAdventureMapInstance current = host.Nodes[nodeId];
        Rect2 bounds = GodotBattleBoardFitter.BoardBounds();
        var layout = GodotAdventureAtlasLayout.Project(MapDefinition, bounds.Size);
        // Remember the pre-layout transforms once so other pages (single-map exploration, battle, Overview) get
        // them back; layout itself assigns absolute values, so repeating this call is idempotent.
        if (_hostBaselineHost != host)
        {
            _hostBaselineTransforms = host.Nodes.ToDictionary(value => value.Key, value => value.Value.Transform);
            _hostBaselineHost = host;
        }
        // Identity-scaled placement: the board view's fit compensates for each map's own transform.
        foreach ((string id, GodotAdventureMapInstance map) in host.Nodes)
            if (layout.TryGetValue(id, out GodotAdventureAtlasNodeLayout? node))
                map.Transform = new Transform2D(0f, node.WorldBounds.Position - bounds.Position);

        foreach ((string id, GodotAdventureMapInstance map) in host.Nodes)
            map.Visible = id == nodeId || allowedTargets.Contains(id, StringComparer.Ordinal);
        host.Visible = true;

        if (current.GetNodeOrNull(PostBattleRoutesName) is Node previous)
        {
            current.RemoveChild(previous);
            previous.QueueFree();
        }
        var routes = new Node2D { Name = PostBattleRoutesName, ZIndex = 60 };
        current.AddChild(routes);
        var extra = new List<Vector2>();
        Transform2D toCurrent = current.Transform.AffineInverse();
        foreach (AdventureMapExitAnchor exit in template.Exits.Where(value => allowedTargets.Contains(value.TargetNodeId, StringComparer.Ordinal)))
        {
            GodotAdventureMapInstance target = host.Nodes[exit.TargetNodeId];
            AdventureMapTemplateDefinition targetTemplate = target.Surface.Template!.ToCoreDefinition();
            AdventureMapAnchor entry = targetTemplate.Entries.Single(value => value.AnchorId == exit.TargetEntryId);
            Vector2 centre = current.Surface.CellCenter(exit.Cell);
            Vector2 rim = centre + (centre - current.Surface.CellCenter(new GridPoint(exit.Cell.X - 1, exit.Cell.Y))) * .5f;
            Vector2 to = toCurrent * target.Transform * target.Surface.CellCenter(entry.Cell);
            routes.AddChild(new Line2D
            {
                Name = $"Route_{nodeId}_{exit.TargetNodeId}", Width = 5f, DefaultColor = new Color("ffb347"),
                Points = [rim, to]
            });
            Rect2 targetBounds = GodotBattleBoardFitter.BoardBounds();
            foreach (Vector2 corner in new[] { targetBounds.Position, targetBounds.Position + new Vector2(targetBounds.Size.X, 0),
                         targetBounds.Position + new Vector2(0, targetBounds.Size.Y), targetBounds.End })
                extra.Add(toCurrent * target.Transform * corner);
        }
        board.FrameExtraPoints = extra;
    }

    /// <summary>Resolved node exploration on the node's resident map; false when the binding does not match.</summary>
    private bool TryShowResidentResolvedNode(PureRunState run, string nodeId, Control root)
    {
        if (TryPrepareResidentExploration(run, nodeId, true) is not { } resident) return false;
        run = resident.Run;
        AdventureBoardDefinition definition = resident.Definition;
        _battle = null;
        _adventureBoard = new GodotAdventureBoardView { Name = "ResolvedAdventureNodeBoard" };
        _adventureLastBoardContentId = definition.ContentId.Value;
        root.AddChild(_adventureBoard);
        BindResidentExploration(_adventureBoard, nodeId, resident, ExitTargets(definition));
        _adventureBoard.SetBoard(definition);
        WireAdventureMovement(run, definition, ShowResolvedAdventureNode);
        _adventureBoard.ObjectPressed += pressed => HandleExitPointer(run, definition, pressed);
        return true;
    }
}
