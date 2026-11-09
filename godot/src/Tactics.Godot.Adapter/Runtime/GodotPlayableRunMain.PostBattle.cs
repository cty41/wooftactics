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
    internal const string PostBattleRouteTargetMissing = "postbattle.route_target_missing";
    internal const string PostBattleRouteEntryMissing = "postbattle.route_entry_missing";
    internal const string PostBattleRouteTemplateMissing = "postbattle.route_template_missing";

    /// <summary>Diagnostic codes raised while laying out the latest post-battle page (tests and logs read them).</summary>
    private readonly List<string> _postBattleDiagnostics = new();

    private void ReportPostBattleDiagnostic(string code)
    {
        _postBattleDiagnostics.Add(code);
        GD.PushWarning($"{code} — route skipped.");
    }

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
        _postBattleDiagnostics.Clear();
        _postBattleOverviewPoints = Array.Empty<Vector2>();
        if (!host.Nodes.TryGetValue(nodeId, out GodotAdventureMapInstance? current))
        {
            ReportPostBattleDiagnostic($"postbattle.current_node_missing:{nodeId}");
            return;
        }
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
        // Route lanes and template exit cells are authored separately; hand the successors' lane slots out in the
        // exits' rim order (grid y) so no two route lines cross.
        string[] byExit = template.Exits.Where(value => allowedTargets.Contains(value.TargetNodeId, StringComparer.Ordinal) &&
                                                        host.Nodes.ContainsKey(value.TargetNodeId) && layout.ContainsKey(value.TargetNodeId))
            .OrderBy(value => value.Cell.Y).Select(value => value.TargetNodeId).Distinct(StringComparer.Ordinal).ToArray();
        Transform2D[] slots = byExit.OrderBy(id => layout[id].Lane).Select(id => host.Nodes[id].Transform).ToArray();
        for (int index = 0; index < byExit.Length; index++)
            host.Nodes[byExit[index]].Transform = slots[index];

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
            // A route whose target map or entry anchor is missing is skipped with a diagnostic; the remaining
            // routes and the camera framing still work.
            if (!host.Nodes.TryGetValue(exit.TargetNodeId, out GodotAdventureMapInstance? target))
            {
                ReportPostBattleDiagnostic($"{PostBattleRouteTargetMissing}:{nodeId}->{exit.TargetNodeId}");
                continue;
            }
            if (target.Surface.Template is null)
            {
                ReportPostBattleDiagnostic($"{PostBattleRouteTemplateMissing}:{nodeId}->{exit.TargetNodeId}");
                continue;
            }
            AdventureMapAnchor? entry = target.Surface.Template.ToCoreDefinition().Entries
                .FirstOrDefault(value => value.AnchorId == exit.TargetEntryId);
            if (entry is null)
            {
                ReportPostBattleDiagnostic($"{PostBattleRouteEntryMissing}:{nodeId}->{exit.TargetNodeId}:{exit.TargetEntryId}");
                continue;
            }
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
        // The camera starts on the fought map like the battle did; Overview widens it to the successor routes.
        _postBattleOverviewPoints = extra;
        board.FrameExtraPoints = Array.Empty<Vector2>();
    }

    /// <summary>Overview text-hint slot: on the status line, left of the shared Inventory button.</summary>
    internal static readonly Rect2 PostBattleOverviewButtonRect = new(
        new Vector2(ExplorationInventoryButtonRect.Position.X - 260, ExplorationInventoryButtonRect.Position.Y + 12), new Vector2(240, 34));
    internal const string PostBattleOverviewLabel = "[M] Overview";
    internal const string PostBattleCurrentMapLabel = "[M] Back to map";

    private IReadOnlyList<Vector2> _postBattleOverviewPoints = Array.Empty<Vector2>();
    private LinkButton? _postBattleOverviewButton;

    private void AddPostBattleOverviewButton(Control root, GodotAdventureBoardView board)
    {
        if (_postBattleOverviewPoints.Count == 0) return;
        var button = new LinkButton
        {
            Name = "PostBattleOverviewButton",
            Text = PostBattleOverviewLabel,
            Underline = LinkButton.UnderlineMode.OnHover,
            FocusMode = FocusModeEnum.None,
            Position = PostBattleOverviewButtonRect.Position,
            Size = PostBattleOverviewButtonRect.Size,
            ZIndex = StartPageUiZIndex
        };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeColorOverride("font_color", PrimaryAttributeColor);
        button.AddThemeColorOverride("font_hover_color", PrimaryAttributeColor.Lightened(.25f));
        button.Pressed += () => TogglePostBattleOverview(board);
        _postBattleOverviewButton = button;
        root.AddChild(button);
    }

    private void TogglePostBattleOverview(GodotAdventureBoardView board)
    {
        if (!GodotObject.IsInstanceValid(board) || _postBattleOverviewButton is not { } button || !GodotObject.IsInstanceValid(button)) return;
        bool overview = board.FrameExtraPoints.Count == 0;
        board.FrameExtraPoints = overview ? _postBattleOverviewPoints : Array.Empty<Vector2>();
        board.RefitCamera();
        button.Text = overview ? PostBattleCurrentMapLabel : PostBattleOverviewLabel;
    }

    /// <summary>M toggles the post-battle overview while its button is on screen.</summary>
    private bool TryHandlePostBattleOverviewKey(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.M } ||
            _postBattleOverviewButton is not { } button || !GodotObject.IsInstanceValid(button) ||
            button.IsQueuedForDeletion() || !button.IsVisibleInTree() || _adventureBoard is not { } board) return false;
        TogglePostBattleOverview(board);
        return true;
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
        AddPostBattleOverviewButton(root, _adventureBoard);
        return true;
    }
}
