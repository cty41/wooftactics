using NUnit.Framework;
using Tactics.Core.Runs;

namespace Tactics.Core.Tests;

public sealed class PureRunNodeSceneTransitionServiceTests
{
    private readonly PureRunNodeSceneTransitionService _service = new();
    private static PureRunActDefinition Act() => PureRunActBlueprint.Resolve(PureRunActBlueprintTests.DemoMap(), 37).ActDefinition;

    [Test]
    public void FullForwardRouteFromCampToBossUsesEachEntryStrategy()
    {
        PureRunActDefinition act = Act();
        PureRunActState state = _service.Start(act);
        foreach (string battle in new[] { "layer_01_battle", "layer_02_battle", "layer_03_battle" })
            state = Win(Enter(state, act, battle, PureRunNodeScenePhase.InCombat), act);

        state = Enter(state, act, "layer_04_event", PureRunNodeScenePhase.Exploring);
        state = Ok(_service.BeginCombat(state, act));
        Assert.That(state.Current.Phase, Is.EqualTo(PureRunNodeScenePhase.InCombat));
        state = Win(state, act);
        state = Win(Enter(state, act, "layer_05_battle", PureRunNodeScenePhase.InCombat), act);
        state = Win(Enter(state, act, "layer_06_store", PureRunNodeScenePhase.Exploring), act);
        state = Win(Enter(state, act, "layer_07_battle", PureRunNodeScenePhase.InCombat), act);

        Assert.Multiple(() =>
        {
            Assert.That(state.Validate(act), Is.Null);
            Assert.That(state.Current.Phase, Is.EqualTo(PureRunNodeScenePhase.Resolved));
            Assert.That(PureRunNodeSceneTransitionService.CanExit(state, act), Is.False);
            Assert.That(PureRunNodeSceneTransitionService.ImmediateSuccessors(state, act), Is.Empty);
            Assert.That(state.Nodes.Where(node => node.Departed).Select(node => node.NodeId), Is.EquivalentTo(new[]
                { "start", "layer_01_battle", "layer_02_battle", "layer_03_battle", "layer_04_event", "layer_05_battle", "layer_06_store" }));
            Assert.That(state.Nodes.Count(node => node.Phase == PureRunNodeScenePhase.Unentered), Is.EqualTo(8));
        });
    }

    [Test]
    public void SuccessorsOnlyListUnenteredDirectTargets()
    {
        PureRunActDefinition act = Act();
        PureRunActState state = _service.Start(act);
        Assert.That(PureRunNodeSceneTransitionService.ImmediateSuccessors(state, act).Select(node => node.NodeId),
            Is.EqualTo(new[] { "layer_01_battle" }));
        foreach (string battle in new[] { "layer_01_battle", "layer_02_battle", "layer_03_battle" })
            state = Win(Enter(state, act, battle, PureRunNodeScenePhase.InCombat), act);
        Assert.That(PureRunNodeSceneTransitionService.ImmediateSuccessors(state, act).Select(node => node.NodeId),
            Is.EquivalentTo(act.Map.Nodes.Where(node => node.Layer == 4).Select(node => node.NodeId)));
    }

    [Test]
    public void EnterRejectsSkippedBackwardAndLockedTargetsWithoutChangingState()
    {
        PureRunActDefinition act = Act();
        PureRunActState start = _service.Start(act);
        AssertRejected(_service.Enter(start, act, "layer_02_battle"), "act_state.not_immediate_successor", start);
        AssertRejected(_service.Enter(start, act, "start"), "act_state.target_already_current", start);
        AssertRejected(_service.Enter(start, act, "unknown"), "act_state.not_immediate_successor", start);

        PureRunActState fighting = Enter(start, act, "layer_01_battle", PureRunNodeScenePhase.InCombat);
        AssertRejected(_service.Enter(fighting, act, "layer_02_battle"), "act_state.exit_locked", fighting);
        PureRunActState won = Win(fighting, act);
        AssertRejected(_service.Enter(won, act, "start"), "act_state.not_immediate_successor", won);
        AssertRejected(_service.Enter(won, act, "layer_01_battle"), "act_state.target_already_current", won);
    }

    [Test]
    public void CombatTriggerIsOnlyForExplorationEventsNotCampOrImmediateNodes()
    {
        PureRunActDefinition act = Act();
        PureRunActState start = _service.Start(act);
        AssertRejected(_service.BeginCombat(start, act), "act_state.combat_not_triggerable", start);
        AssertRejected(_service.Resolve(start, act), "act_state.start_not_resolvable", start);
        PureRunActState fighting = Enter(start, act, "layer_01_battle", PureRunNodeScenePhase.InCombat);
        AssertRejected(_service.BeginCombat(fighting, act), "act_state.combat_not_triggerable", fighting);
    }

    [Test]
    public void DefeatIsTerminalForTheNodeAndOnlyComesFromCombat()
    {
        PureRunActDefinition act = Act();
        PureRunActState start = _service.Start(act);
        AssertRejected(_service.Fail(start, act), "act_state.phase_invalid", start);
        PureRunActState failed = Ok(_service.Fail(Enter(start, act, "layer_01_battle", PureRunNodeScenePhase.InCombat), act));
        Assert.Multiple(() =>
        {
            Assert.That(failed.Current.Phase, Is.EqualTo(PureRunNodeScenePhase.Failed));
            Assert.That(PureRunNodeSceneTransitionService.CanExit(failed, act), Is.False);
        });
        AssertRejected(_service.Resolve(failed, act), "act_state.phase_invalid", failed);
        AssertRejected(_service.Enter(failed, act, "layer_02_battle"), "act_state.exit_locked", failed);
    }

    [Test]
    public void RepeatedTransitionsReturnTheSameStateAsDuplicates()
    {
        PureRunActDefinition act = Act();
        PureRunActState fighting = Enter(_service.Start(act), act, "layer_01_battle", PureRunNodeScenePhase.InCombat);
        AssertDuplicate(_service.Enter(fighting, act, "layer_01_battle"), fighting);
        PureRunActState won = Win(fighting, act);
        AssertDuplicate(_service.Resolve(won, act), won);

        PureRunActState failed = Ok(_service.Fail(fighting, act));
        AssertDuplicate(_service.Fail(failed, act), failed);

        PureRunActState atEvent = won;
        foreach (string battle in new[] { "layer_02_battle", "layer_03_battle" })
            atEvent = Win(Enter(atEvent, act, battle, PureRunNodeScenePhase.InCombat), act);
        atEvent = Ok(_service.BeginCombat(Enter(atEvent, act, "layer_04_event", PureRunNodeScenePhase.Exploring), act));
        AssertDuplicate(_service.BeginCombat(atEvent, act), atEvent);
    }

    [Test]
    public void TransitionsRejectStatesFromAnotherDefinitionVersion()
    {
        PureRunActDefinition act = Act();
        PureRunActState start = _service.Start(act);
        var foreign = new PureRunActState(act.ActId, 2, start.CurrentNodeId, start.Nodes);
        AssertRejected(_service.Enter(foreign, act, "layer_01_battle"), "act_state.definition_mismatch", foreign);
    }

    private PureRunActState Enter(PureRunActState state, PureRunActDefinition act, string target, PureRunNodeScenePhase expected)
    {
        PureRunActState entered = Ok(_service.Enter(state, act, target));
        Assert.That(entered.CurrentNodeId, Is.EqualTo(target));
        Assert.That(entered.Current.Phase, Is.EqualTo(expected));
        Assert.That(entered.Node(state.CurrentNodeId).Departed, Is.True);
        return entered;
    }

    private PureRunActState Win(PureRunActState state, PureRunActDefinition act) => Ok(_service.Resolve(state, act));

    private static PureRunActState Ok(PureRunActTransitionResult result)
    {
        Assert.That(result.Succeeded, Is.True, result.RejectionCode);
        Assert.That(result.WasDuplicate, Is.False);
        return result.State!;
    }

    private static void AssertRejected(PureRunActTransitionResult result, string code, PureRunActState original)
    {
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.RejectionCode, Is.EqualTo(code));
        Assert.That(result.State, Is.SameAs(original));
    }

    private static void AssertDuplicate(PureRunActTransitionResult result, PureRunActState original)
    {
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.WasDuplicate, Is.True);
        Assert.That(result.State, Is.SameAs(original));
    }
}
