using Godot;
using Tactics.Core.Board;
using Tactics.Core.Encounters;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public partial class GodotPlayableRunMain
{
    private bool HasResidentBattleTemplate(RunNodeSceneIdentity identity) =>
        _actWorld is { IsPrepared: true } host && GodotObject.IsInstanceValid(host) && !host.IsQueuedForDeletion() &&
        host.Definition.ActId == identity.ActId && host.Definition.DefinitionVersion == identity.DefinitionVersion &&
        host.Nodes.TryGetValue(identity.NodeId, out var map) &&
        map.Surface.Template?.ToCoreDefinition().ContentId == identity.TemplateId;

    private bool HasResidentEventTemplate(RunNodeSceneIdentity identity, RunAdventureEventContextKind context)
    {
        var template = _actWorld!.Nodes[identity.NodeId].Surface.Template!.ToCoreDefinition();
        (string objectId, AdventureObjectKind kind) = context switch
        {
            RunAdventureEventContextKind.CursedChestMimic => ("cursed-chest", AdventureObjectKind.Chest),
            RunAdventureEventContextKind.FallenAltarGuardian => ("fallen-altar", AdventureObjectKind.Altar),
            RunAdventureEventContextKind.LostVillagerEscort => ("lost-villager", AdventureObjectKind.Npc),
            _ => (string.Empty, AdventureObjectKind.Event)
        };
        return objectId.Length > 0 && template.Board.Objects.Count(value => value.Kind != AdventureObjectKind.Exit) == 1 &&
            template.Board.Objects.Any(value => value.ObjectId == objectId && value.Kind == kind &&
                value.Cell == new GridPoint(5, 5));
    }

    private bool HasResidentBattleLayout(RunNodeSceneIdentity identity, BattleLayoutDefinition layout)
    {
        var template = _actWorld!.Nodes[identity.NodeId].Surface.Template!.ToCoreDefinition();
        return template.PlayerBattleSlots.Select(slot => slot.Cell).SequenceEqual(layout.PartySpawns) &&
            template.EnemyBattleSlots.Select(slot => slot.Cell).SequenceEqual(layout.EnemySpawns) &&
            template.Board.BlockedCells.ToHashSet().SetEquals(layout.BlockedCells) &&
            (template.ShallowWaterCells ?? Array.Empty<GridPoint>()).ToHashSet().SetEquals(layout.ShallowWater);
    }

    private void BindResidentBattleSurface(RunNodeSceneIdentity identity)
    {
        if (!HasResidentBattleTemplate(identity) || _board is null)
            throw new InvalidOperationException("battle.resident_template_mismatch");
        GodotActWorldHost host = _actWorld!;
        GodotNodeActivationLease lease = host.ActivateNode(identity.NodeId);
        GodotIsometricTileMapSurface surface = host.Nodes[identity.NodeId].Surface;
        foreach (var node in host.Nodes) node.Value.Visible = node.Key == identity.NodeId;

        // Preserve the existing board-local actor/VFX coordinate contract by fitting the world camera
        // to it. Never transform/reconfigure the terrain layer or move an owned map out of its host.
        Vector2 sourceOrigin = surface.CellCenter(new GridPoint(0, 0));
        var sourceBasis = new Transform2D(surface.CellCenter(new GridPoint(1, 0)) - sourceOrigin,
            surface.CellCenter(new GridPoint(0, 1)) - sourceOrigin, sourceOrigin);
        Vector2 targetOrigin = IsometricBattleBoardLayout.GridToScreen(new GridPoint(0, 0));
        var targetBasis = new Transform2D(IsometricBattleBoardLayout.GridToScreen(new GridPoint(1, 0)) - targetOrigin,
            IsometricBattleBoardLayout.GridToScreen(new GridPoint(0, 1)) - targetOrigin, targetOrigin);
        Transform2D hostViewport = host.GetGlobalTransformWithCanvas();
        Transform2D parentViewport = hostViewport * host.Transform.AffineInverse();
        Transform2D surfaceToHost = hostViewport.AffineInverse() * surface.GetGlobalTransformWithCanvas();
        host.Transform = parentViewport.AffineInverse() * _board.GetGlobalTransformWithCanvas() *
            targetBasis * sourceBasis.AffineInverse() * surfaceToHost.AffineInverse();
        host.Visible = true;
        _board.BindResidentSurface(host, lease);
    }
}
