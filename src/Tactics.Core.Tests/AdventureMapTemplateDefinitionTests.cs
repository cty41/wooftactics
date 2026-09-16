using NUnit.Framework;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Core.Tests;

public sealed class AdventureMapTemplateDefinitionTests
{
    [Test]
    public void ValidTemplateAcceptsRequiredAnchorsSlotsAndKnownStateLayers() => Template().Validate();

    [Test]
    public void RejectsWrongSizeDuplicateOrOutOfBoundsSlots()
    {
        Assert.Throws<ArgumentException>(() => (Template() with { Board = Template().Board with { Width = 9 } }).Validate());
        Assert.Throws<ArgumentException>(() => (Template() with
        {
            PartyEntrySlots = [new("party-1", new(1, 2)), new("party-2", new(1, 2))]
        }).Validate());
        Assert.Throws<ArgumentException>(() => (Template() with
        {
            EnemyBattleSlots = [new("enemy-1", new(10, 2))]
        }).Validate());
    }

    [Test]
    public void RejectsUnreachableExitMissingRequiredSlotsAndUnknownStateLayer()
    {
        AdventureMapTemplateDefinition template = Template();
        GridPoint exit = template.Exits[0].Cell;
        GridPoint[] sealedExit = [new(exit.X - 1, exit.Y), new(exit.X + 1, exit.Y), new(exit.X, exit.Y - 1), new(exit.X, exit.Y + 1)];
        Assert.Throws<ArgumentException>(() => (template with
        {
            Board = template.Board with { BlockedCells = sealedExit }
        }).Validate());
        Assert.Throws<ArgumentException>(() => (template with { CandidateSlots = [] }).Validate());
        Assert.Throws<ArgumentException>(() => (template with { PartyEntrySlots = [] }).Validate());
        Assert.Throws<ArgumentException>(() => (template with { PlayerBattleSlots = [] }).Validate());
        Assert.Throws<ArgumentException>(() => (template with { EnemyBattleSlots = [] }).Validate());
        Assert.Throws<ArgumentException>(() => (template with { StateLayerIds = ["current", "unknown-layer"] }).Validate());
    }

    private static AdventureMapTemplateDefinition Template()
    {
        var board = new AdventureBoardDefinition(new ContentId("adventure-map.test"), 10, 10, [], [], [], new(1, 5), new(8, 5));
        return new AdventureMapTemplateDefinition(
            new ContentId("adventure-map-template.test"), board,
            [new("candidate-1", new(2, 2))],
            [new("party-1", new(1, 4)), new("party-2", new(1, 5)), new("party-3", new(1, 6))],
            [new("player-1", new(2, 4))], [new("enemy-1", new(7, 4))],
            [new("entry-main", new(1, 5))],
            [new("exit-main", new(8, 5), "next", "entry-main")],
            [new("connection-main", new(9, 5))],
            new("camera-focus", new(5, 5)), new("atlas-bounds", new(9, 9)),
            AdventureMapStateLayers.Required);
    }
}
