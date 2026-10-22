using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class PureRunLegacyActStateProjectorTests
{
    private const int Seed = 37;
    private static readonly PureRunMapDefinition MapDefinition = PureRunActBlueprintTests.DemoMap();
    private static readonly PureRunActDefinition Act = PureRunActBlueprint.Resolve(MapDefinition, Seed).ActDefinition;
    private static readonly PureRunMapService MapService = new(MapDefinition);
    private static readonly string[] FixedPrefix = ["start", "layer_01_battle", "layer_02_battle", "layer_03_battle"];

    [Test]
    public void InitialExplorationIsTheCamp()
    {
        PureRunState run = Run(PureRunPhase.Ready, 0, adventure: new RunAdventureTransitionService().CreateInitial(Party()));
        AssertProjected(run, "start", PureRunNodeScenePhase.Exploring);
    }

    [Test]
    public void SetupFirstBattleEntersLayerOneDirectly()
    {
        PureRunState run = Run(PureRunPhase.PendingBattle, 0, board: Board("layer_01_battle"));
        AssertProjected(run, "layer_01_battle", PureRunNodeScenePhase.InCombat, "start");
    }

    [TestCase(1, "layer_01_battle")]
    [TestCase(2, "layer_02_battle")]
    public void SettledEarlyBattleRequiresTheResolvedBoard(int index, string node)
    {
        AssertProjected(Run(PureRunPhase.Ready, index, board: Board(node, resolved: true), battles: index),
            node, PureRunNodeScenePhase.Resolved, FixedPrefix.Take(index).ToArray());
        // Settlement saved but ResolveBoard never committed: the save is rejected, not guessed as Resolved.
        AssertRejected(Run(PureRunPhase.Ready, index, board: Board(node), battles: index), "act_state.inconsistent_resolution");
    }

    [Test]
    public void ExitStoredBeforeTheNextEncounterMeansTheImmediateCombatNodeWasEntered()
    {
        PureRunState run = Run(PureRunPhase.Ready, 1, board: Board("layer_02_battle"), battles: 1);
        AssertProjected(run, "layer_02_battle", PureRunNodeScenePhase.InCombat, "start", "layer_01_battle");
    }

    [Test]
    public void LayerFourChoiceWaitsOnTheResolvedThirdBattle()
    {
        PureRunState run = Run(PureRunPhase.AwaitingLayerFourChoice, 2, map: MapService.UnlockLayerFour(Seed),
            board: Board("layer_03_battle", resolved: true), battles: 3);
        AssertProjected(run, "layer_03_battle", PureRunNodeScenePhase.Resolved, FixedPrefix.Take(3).ToArray());
    }

    [Test]
    public void SelectedEventIsExploredThenFoughtInPlace()
    {
        PureRunMapResult begun = MapService.BeginNode(MapService.UnlockLayerFour(Seed), "layer_04_event");
        PureRunState exploring = Run(PureRunPhase.ResolvingLayerFourNode, 2, map: begun.State, transaction: begun.Transaction,
            board: Board("layer_04_event"), battles: 3);
        AssertProjected(exploring, "layer_04_event", PureRunNodeScenePhase.Exploring, FixedPrefix);

        PureRunState pending = new RunAdventureTransitionService().BeginEventBattle(
            Run(PureRunPhase.PendingBattle, 3, map: begun.State, transaction: begun.Transaction, board: Board("layer_04_event"), battles: 3),
            RunAdventureEventContextKind.CursedChestMimic, "layer_04_event", "cursed-chest");
        AssertProjected(pending, "layer_04_event", PureRunNodeScenePhase.InCombat, FixedPrefix);
    }

    [Test]
    public void CommittedLayerFourNodeIsResolvedAndBecomesTheLayerFiveRoute()
    {
        (PureRunMapState committed, RunNodeTransaction transaction) = Commit(MapService.UnlockLayerFour(Seed), "layer_04_store");
        AssertProjected(Run(PureRunPhase.ResolvingLayerFourNode, 2, map: committed, transaction: transaction,
            board: Board("layer_04_store", resolved: true), battles: 3), "layer_04_store", PureRunNodeScenePhase.Resolved, FixedPrefix);
        AssertRejected(Run(PureRunPhase.ResolvingLayerFourNode, 2, map: committed, transaction: transaction,
            board: Board("layer_04_store"), battles: 3), "act_state.inconsistent_resolution");
        AssertProjected(Run(PureRunPhase.ReadyForLayerFive, 2, map: MapService.AdvanceToLayerFive(committed), transaction: transaction,
            board: Board("layer_04_store", resolved: true), battles: 3), "layer_04_store", PureRunNodeScenePhase.Resolved, FixedPrefix);
        AssertProjected(Run(PureRunPhase.PendingBattle, 4, map: MapService.AdvanceToLayerFive(committed), transaction: transaction,
            board: Board("layer_05_battle"), battles: 3), "layer_05_battle", PureRunNodeScenePhase.InCombat, [.. FixedPrefix, "layer_04_store"]);
    }

    [Test]
    public void LayerSixAndBossFollowTheSavedChoices()
    {
        (PureRunMapState four, _) = Commit(MapService.UnlockLayerFour(Seed), "layer_04_rest");
        PureRunMapState six = MapService.UnlockLayerSix(MapService.AdvanceToLayerFive(four), Seed);
        string[] toFive = [.. FixedPrefix, "layer_04_rest"];
        AssertProjected(Run(PureRunPhase.AwaitingLayerSixChoice, 4, map: six, board: Board("layer_05_battle", resolved: true), battles: 5),
            "layer_05_battle", PureRunNodeScenePhase.Resolved, toFive);

        (PureRunMapState boss, RunNodeTransaction transaction) = Commit(six, "layer_06_treasure");
        string[] toSix = [.. toFive, "layer_05_battle"];
        AssertProjected(Run(PureRunPhase.ReadyForBoss, 4, map: boss, transaction: transaction,
            board: Board("layer_06_treasure", resolved: true), battles: 5), "layer_06_treasure", PureRunNodeScenePhase.Resolved, toSix);
        AssertProjected(Run(PureRunPhase.PendingBattle, 6, map: boss, transaction: transaction,
            board: Board("layer_07_battle"), battles: 5), "layer_07_battle", PureRunNodeScenePhase.InCombat, [.. toSix, "layer_06_treasure"]);
    }

    [Test]
    public void DefeatMarksTheFoughtNodeFailed()
    {
        PureRunMapResult begun = MapService.BeginNode(MapService.UnlockLayerFour(Seed), "layer_04_battle");
        AssertProjected(Run(PureRunPhase.Defeated, 3, map: begun.State, transaction: begun.Transaction,
            board: Board("layer_04_battle"), battles: 3), "layer_04_battle", PureRunNodeScenePhase.Failed, FixedPrefix);
    }

    [TestCase("layer_04_event", 2)]
    [TestCase("layer_06_event", 4)]
    public void PendingEventBattleIsInCombatAtTheEventNode(string node, int encounterIndex)
    {
        PureRunMapState map = MapService.UnlockLayerFour(Seed);
        if (node.StartsWith("layer_06", StringComparison.Ordinal))
            map = MapService.UnlockLayerSix(MapService.AdvanceToLayerFive(Commit(map, "layer_04_rest").State), Seed);
        PureRunMapResult begun = MapService.BeginNode(map, node);
        RunAdventureState adventure = Adventure(new ContentId("adventure-board.pure-run.event.cursedchestmimic")) with
        {
            PendingEventContext = RunAdventureEventContextKind.CursedChestMimic,
            PendingEventNodeId = node,
            PendingEventObjectId = "cursed-chest"
        };
        string[] departed = node == "layer_04_event" ? FixedPrefix : [.. FixedPrefix, "layer_04_rest", "layer_05_battle"];
        AssertProjected(Run(PureRunPhase.PendingBattle, encounterIndex, map: begun.State with { NodeLifecycle = RunNodeLifecycle.Pending },
            transaction: begun.Transaction, adventure: adventure, battles: 3), node, PureRunNodeScenePhase.InCombat, departed);
    }

    [Test]
    public void ContradictoryEvidenceIsRejectedInsteadOfGuessed()
    {
        PureRunMapResult begun = MapService.BeginNode(MapService.UnlockLayerFour(Seed), "layer_04_event");
        (PureRunMapState committed, RunNodeTransaction transaction) = Commit(MapService.UnlockLayerFour(Seed), "layer_04_store");
        PureRunMapState twoVisited = committed with { VisitedNodeIds = [.. committed.VisitedNodeIds, "layer_04_rest"] };
        var identity = new RunNodeSceneIdentity(Act.ActId, Act.DefinitionVersion, "layer_02_battle",
            Act.Nodes.Single(node => node.NodeId == "layer_02_battle").TemplateId);
        RunAdventureState mismatchedIdentity = Adventure(Board("layer_01_battle")) with { SceneIdentity = identity };

        Assert.Multiple(() =>
        {
            AssertRejected(Run(PureRunPhase.PendingBattle, 3, map: begun.State, transaction: begun.Transaction,
                board: Board("layer_02_battle"), battles: 3), "act_state.ambiguous_current");
            AssertRejected(Run(PureRunPhase.PendingBattle, 0, board: Board("layer_01_battle", resolved: true)),
                "act_state.inconsistent_resolution");
            AssertRejected(Run(PureRunPhase.PendingBattle, 4, map: MapService.UnlockLayerFour(Seed), board: Board("layer_05_battle"), battles: 3),
                "act_state.route_invalid");
            AssertRejected(Run(PureRunPhase.PendingBattle, 4, map: MapService.AdvanceToLayerFive(twoVisited), transaction: transaction,
                board: Board("layer_05_battle"), battles: 3), "act_state.route_invalid");
            AssertRejected(Run(PureRunPhase.PendingBattle, 3, map: MapService.UnlockLayerFour(Seed), board: Board("layer_04_event"), battles: 3),
                "act_state.ambiguous_current");
            AssertRejected(Run(PureRunPhase.PendingBattle, 0, adventure: mismatchedIdentity), "act_state.ambiguous_current");
            AssertRejected(Run(PureRunPhase.SliceCompleted, 2, battles: 3), "act_state.terminal_phase");
            AssertRejected(new RunAdventureTransitionService().BeginEventBattle(
                Run(PureRunPhase.ResolvingLayerFourNode, 2, map: begun.State, transaction: begun.Transaction, board: Board("layer_04_event"), battles: 3),
                RunAdventureEventContextKind.CursedChestMimic, "layer_04_event", "cursed-chest"), "act_state.inconsistent_resolution");
        });
    }

    [Test]
    public void ConsistencyReportsStatesThatDisagreeWithTheLegacyPhase()
    {
        PureRunState run = Run(PureRunPhase.PendingBattle, 0, board: Board("layer_01_battle"));
        PureRunActState state = PureRunLegacyActStateProjector.Project(run, Act).State!;
        PureRunActState camp = new PureRunNodeSceneTransitionService().Start(Act);
        Assert.Multiple(() =>
        {
            Assert.That(PureRunPhaseConsistency.Check(state, run, Act), Is.Null);
            Assert.That(PureRunPhaseConsistency.Check(camp, run, Act), Is.EqualTo("act_state.phase_mismatch"));
            Assert.That(PureRunPhaseConsistency.Check(new PureRunActState(Act.ActId, 2, camp.CurrentNodeId, camp.Nodes), run, Act),
                Is.EqualTo("act_state.definition_mismatch"));
        });
    }

    private static void AssertProjected(PureRunState run, string current, PureRunNodeScenePhase phase, params string[] departed)
    {
        PureRunActTransitionResult result = PureRunLegacyActStateProjector.Project(run, Act);
        Assert.That(result.Succeeded, Is.True, result.RejectionCode);
        PureRunActState state = result.State!;
        Assert.Multiple(() =>
        {
            Assert.That(state.CurrentNodeId, Is.EqualTo(current));
            Assert.That(state.Current.Phase, Is.EqualTo(phase));
            Assert.That(state.Nodes.Where(node => node.Departed).Select(node => node.NodeId), Is.EquivalentTo(departed));
            Assert.That(state.Validate(Act), Is.Null);
            Assert.That(PureRunPhaseConsistency.Check(state, run, Act), Is.Null);
        });
    }

    private static void AssertRejected(PureRunState run, string code)
    {
        PureRunActTransitionResult result = PureRunLegacyActStateProjector.Project(run, Act);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.RejectionCode, Is.EqualTo(code));
        Assert.That(result.State, Is.Null);
    }

    private static (PureRunMapState State, RunNodeTransaction Transaction) Commit(PureRunMapState choosing, string nodeId)
    {
        PureRunMapResult begun = MapService.BeginNode(choosing, nodeId);
        PureRunMapResult committed = MapService.CommitNode(begun.State, begun.Transaction!, []);
        return (committed.State, committed.Transaction!);
    }

    private static PureRunState Run(PureRunPhase phase, int index, PureRunMapState? map = null, RunNodeTransaction? transaction = null,
        ContentId? board = null, RunAdventureState? adventure = null, int battles = 0) =>
        new("run", Seed, 1, phase, index, new ContentId("encounter.pure-run.n1"), Party(), battlesCompleted: battles,
            mapState: map, nodeTransaction: transaction,
            adventureState: adventure ?? (board is { } id ? Adventure(id) : null));

    private static RunAdventureState Adventure(ContentId board) => new(RunAdventureLifecycle.MapActive, board, "mage",
        Party().Select((member, index) => new RunAdventureActorCell(member.CharacterId, new(1, index))).ToArray(),
        RunAdventureEventContextKind.None, null, null, 0, 0, 0, 0, 0);

    private static ContentId Board(string node, bool resolved = false) =>
        new($"adventure-board.pure-run.node.{node.Replace('_', '-')}{(resolved ? ".resolved" : string.Empty)}");

    private static RunCharacterState[] Party()
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        return new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(
            id, new ContentId("unit." + id), 1, attributes, 20, 20, 10, 10, false, [new ContentId("skill." + id)])).ToArray();
    }
}
