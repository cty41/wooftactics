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

    [Test]
    public void TerminalTemplateHasNoOutgoingRouteButStillRequiresAnEntry()
    {
        AdventureMapTemplateDefinition terminal = Template() with { IsTerminal = true, Exits = [], ConnectionAnchors = [] };
        Assert.DoesNotThrow(terminal.Validate);
        Assert.Throws<ArgumentException>(() => (terminal with { Entries = [] }).Validate());
        Assert.Throws<ArgumentException>(() => (terminal with { Exits = Template().Exits }).Validate());
        Assert.Throws<ArgumentException>(() => (terminal with { ConnectionAnchors = Template().ConnectionAnchors }).Validate());
        Assert.Throws<ArgumentException>(() => (terminal with { IsTerminal = false }).Validate());
    }

    [Test]
    public void ExplorationSlotsCannotBeInsideAnIsolatedPocket()
    {
        AdventureMapTemplateDefinition template = Template() with
        {
            CandidateSlots = [new("candidate-1", new(1, 1))],
            Board = Template().Board with { BlockedCells = [new(0, 1), new(1, 0), new(2, 1), new(1, 2)] }
        };
        Assert.That(AdventureBoardPathfinder.FindPath(template.Board, template.Entries[0].Cell,
            template.Exits[0].Cell), Is.Not.Empty);
        Assert.Throws<ArgumentException>(template.Validate);
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
