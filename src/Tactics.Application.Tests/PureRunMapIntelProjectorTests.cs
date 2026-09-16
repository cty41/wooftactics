using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunMapIntelProjectorTests
{
    [Test]
    public void PlanningExposesTypeAndTopologyWhileOnlyAvailableNodesReceiveTacticalPreview()
    {
        PureRunMapDefinition map = new(new ContentId("map.intel"), 2,
        [
            new("start", 0, PureRunNodeKind.Rest, new ContentId("node.start")),
            new("available", 1, PureRunNodeKind.Battle, new ContentId("node.available")),
            new("future", 2, PureRunNodeKind.Boss, new ContentId("node.future"))
        ], [new("start", "available"), new("available", "future")]);
        PureRunState run = Run(new(PureRunMapPhase.ChoosingLayerFour, "start", ["available"], ["start"],
            new Dictionary<string, string>()));

        PureRunMapSnapshot snapshot = new PureRunFlowProjector().ProjectMap(run, Definition(), map);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Nodes.Single(node => node.NodeId == "available").IntelState,
                Is.EqualTo(PureRunNodeIntelState.TacticalPreview));
            Assert.That(snapshot.Nodes.Single(node => node.NodeId == "future").IntelState,
                Is.EqualTo(PureRunNodeIntelState.Planning));
            Assert.That(snapshot.Nodes.Single(node => node.NodeId == "future").Kind, Is.EqualTo(PureRunNodeKind.Boss));
            Assert.That(snapshot.Connections.Select(edge => (edge.FromNodeId, edge.ToNodeId)),
                Does.Contain(("available", "future")));
        });
    }

    private static PureRunState Run(PureRunMapState map)
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        RunCharacterState[] party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(
            id, new ContentId("unit." + id), 1, attributes, 20, 20, 10, 10, false, [new ContentId("skill." + id)])).ToArray();
        return new PureRunState("run", 7, 1, PureRunPhase.Ready, 0, new ContentId("encounter.n1"), party,
            mapState: map);
    }

    private static PureRunDefinition Definition() => new(new ContentId("run.definition"),
        [new("encounter.1"), new("encounter.2"), new("encounter.3")],
        new[] { "mage", "necro", "amazon" }.Select(id => new PureRunPartyTemplate(id, new ContentId("unit." + id),
            new ContentId("skill." + id), new UnitAttributes(5, 5, 5, 5, 5, 5))));
}
