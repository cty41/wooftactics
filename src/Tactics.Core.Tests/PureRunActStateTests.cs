using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Core.Tests;

public sealed class PureRunActStateTests
{
    private static PureRunActDefinition Act() => PureRunActBlueprint.Resolve(PureRunActBlueprintTests.DemoMap(), 37).ActDefinition;

    [Test]
    public void StartCoversEveryNodeWithOnlyTheCampEntered()
    {
        PureRunActDefinition act = Act();
        PureRunActState state = new PureRunNodeSceneTransitionService().Start(act);
        Assert.Multiple(() =>
        {
            Assert.That(state.Validate(act), Is.Null);
            Assert.That(state.CurrentNodeId, Is.EqualTo("start"));
            Assert.That(state.Current.Phase, Is.EqualTo(PureRunNodeScenePhase.Exploring));
            Assert.That(state.Nodes.Select(node => node.NodeId), Is.EquivalentTo(act.Nodes.Select(node => node.NodeId)));
            Assert.That(state.Nodes.Where(node => node.NodeId != "start").Select(node => node.Phase),
                Is.All.EqualTo(PureRunNodeScenePhase.Unentered));
        });
    }

    [TestCase(PureRunNodeScenePhase.Unentered)]
    [TestCase(PureRunNodeScenePhase.Exploring)]
    [TestCase(PureRunNodeScenePhase.InCombat)]
    [TestCase(PureRunNodeScenePhase.Failed)]
    public void OnlyResolvedNodesCanBeDeparted(PureRunNodeScenePhase phase)
    {
        ArgumentException? error = Assert.Throws<ArgumentException>(() => new PureRunNodeSceneState("layer_01_battle", phase, departed: true));
        Assert.That(error!.Message, Does.Contain("layer_01_battle"));
    }

    [Test]
    public void RejectsMissingDepartedOrUnenteredCurrentNode()
    {
        Assert.Throws<ArgumentException>(() => State("missing", Node("start", PureRunNodeScenePhase.Exploring)));
        Assert.Throws<ArgumentException>(() => State("start", Node("start", PureRunNodeScenePhase.Resolved, true)));
        ArgumentException? error = Assert.Throws<ArgumentException>(() => State("start", Node("start", PureRunNodeScenePhase.Unentered)));
        Assert.That(error!.Message, Does.Contain("start"));
    }

    [TestCase(PureRunNodeScenePhase.Exploring)]
    [TestCase(PureRunNodeScenePhase.InCombat)]
    [TestCase(PureRunNodeScenePhase.Failed)]
    public void ActivePhasesBelongOnlyToTheCurrentNode(PureRunNodeScenePhase phase)
    {
        ArgumentException? error = Assert.Throws<ArgumentException>(() => State("start",
            Node("start", PureRunNodeScenePhase.Exploring), Node("layer_01_battle", phase)));
        Assert.That(error!.Message, Does.Contain("layer_01_battle"));
    }

    [Test]
    public void ResolvedNonCurrentNodeMustHaveDeparted()
    {
        ArgumentException? error = Assert.Throws<ArgumentException>(() => State("layer_01_battle",
            Node("start", PureRunNodeScenePhase.Resolved), Node("layer_01_battle", PureRunNodeScenePhase.InCombat)));
        Assert.That(error!.Message, Does.Contain("'start'"));
    }

    [Test]
    public void RejectsDuplicateAndNullNodes()
    {
        Assert.Throws<ArgumentException>(() => State("start",
            Node("start", PureRunNodeScenePhase.Exploring), Node("start", PureRunNodeScenePhase.Exploring)));
        Assert.Throws<ArgumentException>(() => State("start", Node("start", PureRunNodeScenePhase.Exploring), null!));
        Assert.Throws<ArgumentException>(() => new PureRunActState(default, 1, "start", [Node("start", PureRunNodeScenePhase.Exploring)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PureRunActState(new("act-01"), 0, "start", [Node("start", PureRunNodeScenePhase.Exploring)]));
    }

    [Test]
    public void ValidateRejectsOtherActVersionAndIncompleteCoverage()
    {
        PureRunActDefinition act = Act();
        PureRunActState start = new PureRunNodeSceneTransitionService().Start(act);
        Assert.Multiple(() =>
        {
            Assert.That(new PureRunActState(new("act-02"), 1, "start", start.Nodes).Validate(act), Is.EqualTo("act_state.definition_mismatch"));
            Assert.That(new PureRunActState(act.ActId, 2, "start", start.Nodes).Validate(act), Is.EqualTo("act_state.definition_mismatch"));
            Assert.That(new PureRunActState(act.ActId, 1, "start", start.Nodes.Where(node => node.NodeId != "layer_07_battle"))
                .Validate(act), Is.EqualTo("act_state.coverage_mismatch"));
        });
    }

    [Test]
    public void ValidateRejectsDepartedNodesThatAreNotOneForwardRoute()
    {
        PureRunActDefinition act = Act();
        string[] fixedPrefix = ["start", "layer_01_battle", "layer_02_battle", "layer_03_battle"];
        PureRunActState Route(string current, params string[] departed) => new(act.ActId, 1, current,
            act.Nodes.Select(node => node.NodeId == current
                ? Node(node.NodeId, PureRunNodeScenePhase.InCombat)
                : departed.Contains(node.NodeId) ? Node(node.NodeId, PureRunNodeScenePhase.Resolved, true)
                : Node(node.NodeId, PureRunNodeScenePhase.Unentered)));
        Assert.Multiple(() =>
        {
            Assert.That(Route("layer_05_battle", [.. fixedPrefix, "layer_04_rest"]).Validate(act), Is.Null);
            Assert.That(Route("layer_05_battle", [.. fixedPrefix, "layer_04_rest", "layer_04_store"]).Validate(act),
                Is.EqualTo("act_state.route_invalid"));
            Assert.That(Route("layer_05_battle", fixedPrefix).Validate(act), Is.EqualTo("act_state.route_invalid"));
            Assert.That(Route("layer_02_battle", "layer_01_battle").Validate(act), Is.EqualTo("act_state.route_invalid"));
            Assert.That(Route("layer_02_battle", "start", "layer_01_battle", "layer_04_rest").Validate(act),
                Is.EqualTo("act_state.route_invalid"));
        });
    }

    private static PureRunNodeSceneState Node(string id, PureRunNodeScenePhase phase, bool departed = false) => new(id, phase, departed);
    private static PureRunActState State(string current, params PureRunNodeSceneState[] nodes) => new(new ContentId("act-01"), 1, current, nodes);
}
