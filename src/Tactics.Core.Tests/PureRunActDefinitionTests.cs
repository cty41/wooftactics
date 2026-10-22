using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Core.Tests;

[TestFixture]
public sealed class PureRunActDefinitionTests
{
    [Test]
    public void CompleteMappingReusesTopologyAndKeepsExplicitIdentities()
    {
        PureRunMapDefinition map = Map();
        PureRunNodeSceneDefinition[] scenes = Scenes().Reverse().ToArray();
        PureRunActDefinition act = Act(map, scenes);

        Assert.That(act.ActId, Is.EqualTo(new ContentId("act-01")));
        Assert.That(act.DefinitionVersion, Is.EqualTo(1));
        Assert.That(act.Map, Is.SameAs(map));
        Assert.That(act.StartNodeId, Is.EqualTo("start"));
        Assert.That(act.TerminalNodeId, Is.EqualTo("boss"));
        Assert.That(act.Nodes.Select(node => node.NodeId), Is.EqualTo(new[] { "boss", "fight", "start" }));
        Assert.That(act.Nodes.Single(node => node.NodeId == "start").EntryStrategy,
            Is.EqualTo(PureRunNodeEntryStrategy.Exploration));
        scenes[0] = Scene("replacement");
        Assert.That(act.Nodes.Select(node => node.NodeId), Does.Not.Contain("replacement"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void RejectsNonpositiveDefinitionVersion(int version) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PureRunActDefinition(new("act-01"), version, Map(), "start", "boss", Scenes()));

    [Test]
    public void RejectsDefaultContentIdentities()
    {
        Assert.Throws<ArgumentException>(() => new PureRunActDefinition(default, 1, Map(), "start", "boss", Scenes()));
        Assert.Throws<ArgumentException>(() => new PureRunNodeSceneDefinition("start", default, PureRunNodeEntryStrategy.Exploration));
        Assert.Throws<ArgumentException>(() => Act(new PureRunMapDefinition(default, 2, Map().Nodes, Map().Connections)));
        PureRunMapNodeDefinition[] nodes = Map().Nodes.Select(node => node with { ContentId = default }).ToArray();
        Assert.Throws<ArgumentException>(() => Act(new PureRunMapDefinition(new("map.test"), 2, nodes, Map().Connections)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void RejectsEmptyNodeIdentities(string? id)
    {
        Assert.Throws<ArgumentException>(() => new PureRunNodeSceneDefinition(id!, new("template.test"), PureRunNodeEntryStrategy.Exploration));
        Assert.Throws<ArgumentException>(() => new PureRunActDefinition(new("act-01"), 1, Map(), id!, "boss", Scenes()));
        Assert.Throws<ArgumentException>(() => new PureRunActDefinition(new("act-01"), 1, Map(), "start", id!, Scenes()));
    }

    [Test]
    public void RejectsNullInputsAndUnknownStrategy()
    {
        Assert.Throws<ArgumentNullException>(() => new PureRunActDefinition(new("act-01"), 1, null!, "start", "boss", Scenes()));
        Assert.Throws<ArgumentNullException>(() => Act(Map(), null!));
        Assert.Throws<ArgumentException>(() => Act(Map(), [null!, Scene("fight"), Scene("boss")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PureRunNodeSceneDefinition("start", new("template.test"), (PureRunNodeEntryStrategy)99));
    }

    [Test]
    public void RejectsMissingDuplicateAndUnknownMappings()
    {
        Assert.Throws<ArgumentException>(() => Act(Map(), Scenes().Take(2)));
        Assert.Throws<ArgumentException>(() => Act(Map(), [Scene("start", true), Scene("start", true), Scene("boss")]));
        Assert.Throws<ArgumentException>(() => Act(Map(), [Scene("start", true), Scene("unknown"), Scene("boss")]));
        Assert.Throws<ArgumentException>(() => Act(Map(), Scenes().Append(Scene("extra"))));
    }

    [Test]
    public void RejectsUnknownStartAndTerminal()
    {
        Assert.Throws<ArgumentException>(() => new PureRunActDefinition(new("act-01"), 1, Map(), "unknown", "boss", Scenes()));
        Assert.Throws<ArgumentException>(() => new PureRunActDefinition(new("act-01"), 1, Map(), "start", "unknown", Scenes()));
    }

    [TestCase(PureRunNodeKind.Battle)]
    [TestCase(PureRunNodeKind.Elite)]
    [TestCase(PureRunNodeKind.Boss)]
    public void CombatKindsRequireImmediateCombatExceptExplicitStart(PureRunNodeKind kind)
    {
        PureRunMapDefinition map = Map(kind);
        Assert.DoesNotThrow(() => Act(map));
        Assert.Throws<ArgumentException>(() => Act(map, [Scene("start", true), Scene("fight", true), Scene("boss")]));
        Assert.Throws<ArgumentException>(() => Act(map, [Scene("start"), Scene("fight"), Scene("boss")]));
    }

    [TestCase(PureRunNodeKind.Rest)]
    [TestCase(PureRunNodeKind.Store)]
    [TestCase(PureRunNodeKind.Mystery)]
    [TestCase(PureRunNodeKind.Treasure)]
    public void NoncombatKindsUseExploration(PureRunNodeKind kind)
    {
        Assert.DoesNotThrow(() => Act(Map(kind), [Scene("start", true), Scene("fight", true), Scene("boss")]));
        Assert.Throws<ArgumentException>(() => Act(Map(kind)));
    }

    [Test]
    public void LayerZeroDoesNotGiveAnotherBattleNodeTheStartExemption()
    {
        PureRunMapDefinition original = Map();
        PureRunMapDefinition map = new(new("map.test"), 2,
            original.Nodes.Select(node => node with { Layer = 0 }), original.Connections);
        Assert.Throws<ArgumentException>(() => Act(map, [Scene("start", true), Scene("fight", true), Scene("boss")]));
        Assert.DoesNotThrow(() => Act(map));
    }

    [Test]
    public void RejectsNodeUnreachableFromStartEvenWhenItCanReachTerminal()
    {
        Assert.Throws<ArgumentException>(() => Act(Map(edges: [new("start", "boss"), new("fight", "boss")])));
    }

    [Test]
    public void RejectsReachableDeadEndEvenWhenTerminalIsReachable()
    {
        Assert.Throws<ArgumentException>(() => Act(Map(edges: [new("start", "fight"), new("start", "boss")])));
    }

    [Test]
    public void RejectsCyclesEvenWhenAllNodesAreReachableAndCanReachTerminal()
    {
        Assert.Throws<ArgumentException>(() => Act(Map(edges:
            [new("start", "fight"), new("fight", "start"), new("fight", "boss"), new("boss", "fight")])));
    }

    [Test]
    public void RejectsIncomingEdgeToStart()
    {
        ArgumentException? error = Assert.Throws<ArgumentException>(() => Act(Map(edges:
            [new("start", "fight"), new("fight", "start"), new("fight", "boss")])));
        Assert.That(error!.Message, Does.Contain("start node cannot have incoming edges"));
    }

    [Test]
    public void RejectsOutgoingEdgeFromTerminal()
    {
        ArgumentException? error = Assert.Throws<ArgumentException>(() => Act(Map(edges:
            [new("start", "fight"), new("fight", "boss"), new("boss", "fight")])));
        Assert.That(error!.Message, Does.Contain("terminal node cannot have outgoing edges"));
    }

    [Test]
    public void RejectsReachableSideBranchThatRequiresRevisitingDepartedFight()
    {
        PureRunMapDefinition map = new(new("map.test"), 2,
            Map().Nodes.Append(new("side", 2, PureRunNodeKind.Rest, new("content.side"))),
            [new("start", "fight"), new("fight", "side"), new("side", "fight"), new("fight", "boss")]);
        ArgumentException? error = Assert.Throws<ArgumentException>(() => Act(map, Scenes().Append(Scene("side", true))));
        Assert.That(error!.Message, Does.Contain("must be acyclic"));
    }

    [Test]
    public void RejectsInternalSelfLoop()
    {
        ArgumentException? error = Assert.Throws<ArgumentException>(() => Act(Map(edges:
            [new("start", "fight"), new("fight", "fight"), new("fight", "boss")])));
        Assert.That(error!.Message, Does.Contain("must be acyclic"));
    }

    [Test]
    public void AllowsBranchingAndReconvergenceWithoutRevisits()
    {
        PureRunMapDefinition map = new(new("map.test"), 2,
            Map().Nodes.Append(new("side", 1, PureRunNodeKind.Rest, new("content.side"))),
            [new("start", "fight"), new("start", "side"), new("side", "fight"), new("fight", "boss")]);
        Assert.DoesNotThrow(() => Act(map, Scenes().Append(Scene("side", true))));
    }

    [Test]
    public void ExplicitStartDoesNotDependOnItsName()
    {
        PureRunMapDefinition map = new(new("map.test"), 2,
            [new("camp", 0, PureRunNodeKind.Battle, new("content.camp")), new("boss", 1, PureRunNodeKind.Boss, new("content.boss"))],
            [new("camp", "boss")]);
        Assert.DoesNotThrow(() => new PureRunActDefinition(new("act-01"), 1, map, "camp", "boss", [Scene("camp", true), Scene("boss")]));
    }

    private static PureRunActDefinition Act(PureRunMapDefinition map) => Act(map, Scenes());
    private static PureRunActDefinition Act(PureRunMapDefinition map, IEnumerable<PureRunNodeSceneDefinition> scenes) =>
        new(new("act-01"), 1, map, "start", "boss", scenes);
    private static PureRunNodeSceneDefinition Scene(string id, bool exploration = false) =>
        new(id, new("template.shared"), exploration ? PureRunNodeEntryStrategy.Exploration : PureRunNodeEntryStrategy.ImmediateCombat);
    private static PureRunNodeSceneDefinition[] Scenes() => [Scene("start", true), Scene("fight"), Scene("boss")];
    private static PureRunMapDefinition Map(PureRunNodeKind kind = PureRunNodeKind.Battle,
        IEnumerable<PureRunMapConnectionDefinition>? edges = null) => new(new("map.test"), 2,
        [new("start", 0, PureRunNodeKind.Battle, new("content.start")),
         new("fight", 1, kind, new("content.fight")),
         new("boss", 2, PureRunNodeKind.Boss, new("content.boss"))],
        edges ?? [new("start", "fight"), new("fight", "boss")]);
}
