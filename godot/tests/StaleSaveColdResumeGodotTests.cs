using GdUnit4;
using Godot;
using System.Reflection;
using System.Threading.Tasks;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

/// <summary>
/// Scripted-player coverage for MQA-GODOT-STALE-SAVE-RESET. Every write goes through the V11
/// document codec, and a fresh Main on the same store stands in for quitting and relaunching.
/// </summary>
[TestSuite]
public sealed class StaleSaveColdResumeGodotTests
{
    private const int Seed = 37;
    private const string SavePath = "memory://cold-resume-v11.json";

    [TestCase]
    [RequireGodotRuntime]
    public async Task FirstBattleSettlementSurvivesColdResume()
    {
        var store = new GodotRunSaveStore(new MemoryRunSaveFileSystem(), SavePath);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GodotPlayableRunMain ui = await StartSetupRun(store, tree, "cold-first-settlement");
        try
        {
            await SettleCurrentBattle(ui, store, tree);
            AssertThat(store.Load().Snapshot!.ActiveRun!.BattlesCompleted).IsEqual(1);
        }
        finally { await Free(ui, tree); }

        await AssertColdResume(store, tree, "cold-first-settlement-resume");
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task LayerFourRestSelectionSurvivesColdResume()
    {
        var store = new GodotRunSaveStore(new MemoryRunSaveFileSystem(), SavePath);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GodotPlayableRunMain ui = await StartSetupRun(store, tree, "cold-layer-four");
        try
        {
            await ClearFirstThreeLayers(ui, store, tree);
            await ExitTo(ui, store, tree, "layer_04_rest");
            PureRunState run = store.Load().Snapshot!.ActiveRun!;
            AssertThat(run.NodeTransaction?.NodeId).IsEqual("layer_04_rest");
        }
        finally { await Free(ui, tree); }

        await AssertColdResume(store, tree, "cold-layer-four-resume");
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task LayerFiveBattleCheckpointAndSettlementSurviveColdResume()
    {
        var store = new GodotRunSaveStore(new MemoryRunSaveFileSystem(), SavePath);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GodotPlayableRunMain ui = await StartSetupRun(store, tree, "cold-layer-five");
        try
        {
            await ClearFirstThreeLayers(ui, store, tree);
            await ExitTo(ui, store, tree, "layer_04_rest");
            InvokePrivate(ui, "CommitLayerFour", (Func<PureRunState, LayerFourNodeResolution>)(state =>
                new PureRunLayerFourNodeService().ConfirmRest(state)));
            AssertThat(store.Load().Snapshot!.ActiveRun!.Phase).IsEqual(PureRunPhase.ReadyForLayerFive);
            await ExitTo(ui, store, tree, "layer_05_battle");
            AssertThat(store.Load().Snapshot!.ActiveRun!.Phase).IsEqual(PureRunPhase.PendingBattle);
        }
        finally { await Free(ui, tree); }

        GodotPlayableRunMain resumed = await AssertColdResume(store, tree, "cold-layer-five-pending", keep: true);
        try
        {
            AssertThat(resumed.CaptureTestProbe().BattleActive).IsTrue();
            await SettleCurrentBattle(resumed, store, tree);
            AssertThat(store.Load().Snapshot!.ActiveRun!.Phase).IsNotEqual(PureRunPhase.PendingBattle);
        }
        finally { await Free(resumed, tree); }

        await AssertColdResume(store, tree, "cold-layer-five-settled");
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task LayerFourStoreOfferAndPurchaseSurviveColdResumeWithoutRerolling()
    {
        var store = new GodotRunSaveStore(new MemoryRunSaveFileSystem(), SavePath);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GodotPlayableRunMain ui = await StartSetupRun(store, tree, "cold-store");
        try
        {
            await ClearFirstThreeLayers(ui, store, tree);
            await ExitTo(ui, store, tree, "layer_04_store");
            InvokePrivate(ui, "ShowStore", store.Load().Snapshot!.ActiveRun!);
            AssertThat(store.Load().Snapshot!.ActiveRun!.MapState!.StoreOffers!.Count > 0).IsTrue();
        }
        finally { await Free(ui, tree); }

        string opened = OffersFingerprint(store);
        GodotPlayableRunMain resumed = await AssertColdResume(store, tree, "cold-store-opened", keep: true);
        try
        {
            InvokePrivate(resumed, "ShowStore", store.Load().Snapshot!.ActiveRun!);
            AssertThat(OffersFingerprint(store)).IsEqual(opened);

            PureRunState run = store.Load().Snapshot!.ActiveRun!;
            RunStoreOfferState? affordable = run.MapState!.StoreOffers!
                .Where(offer => !offer.Purchased && offer.Price <= run.Gold).OrderBy(offer => offer.Price).FirstOrDefault();
            AssertThat(affordable).IsNotNull();
            InvokePrivate(resumed, "PurchaseStore", affordable!.InstanceId);
            AssertThat(store.Load().Snapshot!.ActiveRun!.MapState!.StoreOffers!
                .Single(offer => offer.InstanceId == affordable.InstanceId).Purchased).IsTrue();
        }
        finally { await Free(resumed, tree); }

        string purchased = OffersFingerprint(store);
        GodotPlayableRunMain again = await AssertColdResume(store, tree, "cold-store-purchased", keep: true);
        try
        {
            InvokePrivate(again, "ShowStore", store.Load().Snapshot!.ActiveRun!);
            AssertThat(OffersFingerprint(store)).IsEqual(purchased);
        }
        finally { await Free(again, tree); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task LayerFourEventSelectionSurvivesColdResume()
    {
        var store = new GodotRunSaveStore(new MemoryRunSaveFileSystem(), SavePath);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GodotPlayableRunMain ui = await StartSetupRun(store, tree, "cold-event");
        try
        {
            await ClearFirstThreeLayers(ui, store, tree);
            await ExitTo(ui, store, tree, "layer_04_event");
            AssertThat(store.Load().Snapshot!.ActiveRun!.NodeTransaction?.NodeId).IsEqual("layer_04_event");
        }
        finally { await Free(ui, tree); }

        await AssertColdResume(store, tree, "cold-event-resume");
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task LayerSixNodeSelectionSurvivesColdResume()
    {
        var store = new GodotRunSaveStore(new MemoryRunSaveFileSystem(), SavePath);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GodotPlayableRunMain ui = await StartSetupRun(store, tree, "cold-layer-six");
        try
        {
            await ClearFirstThreeLayers(ui, store, tree);
            await ExitTo(ui, store, tree, "layer_04_rest");
            InvokePrivate(ui, "CommitLayerFour", (Func<PureRunState, LayerFourNodeResolution>)(state =>
                new PureRunLayerFourNodeService().ConfirmRest(state)));
            await ExitTo(ui, store, tree, "layer_05_battle");
            await SettleCurrentBattle(ui, store, tree);
            await ExitTo(ui, store, tree, "layer_06_rest");
            AssertThat(store.Load().Snapshot!.ActiveRun!.NodeTransaction?.NodeId).IsEqual("layer_06_rest");
        }
        finally { await Free(ui, tree); }

        await AssertColdResume(store, tree, "cold-layer-six-resume");
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task OldBuildSettledRunWithUnresolvedBoardIsClearedWithNotice()
    {
        var store = new GodotRunSaveStore(new MemoryRunSaveFileSystem(), SavePath);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GodotPlayableRunMain ui = await StartSetupRun(store, tree, "cold-old-board");
        try
        {
            await SettleCurrentBattle(ui, store, tree);
            Rewrite(store, run => With(run, adventure: run.AdventureState! with
            {
                BoardContentId = new ContentId("adventure-board.pure-run.node.layer-01-battle")
            }));
        }
        finally { await Free(ui, tree); }

        await AssertClearedWithNotice(store, tree, "cold-old-board-resume");
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task OldBuildCheckpointCarryingADeadMemberIsClearedWithNotice()
    {
        var store = new GodotRunSaveStore(new MemoryRunSaveFileSystem(), SavePath);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GodotPlayableRunMain ui = await StartSetupRun(store, tree, "cold-old-dead");
        try
        {
            AssertThat(store.Load().Snapshot!.ActiveRun!.Phase).IsEqual(PureRunPhase.PendingBattle);
            Rewrite(store, run =>
            {
                RunCharacterState[] party = run.Party.Select((member, index) => index == 0 ? AsDead(member) : member).ToArray();
                return With(run, party: party, checkpoint: run.Checkpoint! with { Party = party });
            });
        }
        finally { await Free(ui, tree); }

        await AssertClearedWithNotice(store, tree, "cold-old-dead-resume");
    }

    private static async Task AssertClearedWithNotice(IRunSaveStore store, SceneTree tree, string id)
    {
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, Seed, id));
        tree.Root.AddChild(ui);
        try
        {
            for (int frame = 0; frame < 180 && ui.CaptureTestProbe().PageTitle != "PURE RUN"; frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            string resetMessage = (string)typeof(GodotPlayableRunMain)
                .GetField("StaleSaveResetMessage", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            AssertThat(store.Load().Snapshot?.ActiveRun).IsNull();
            AssertThat(SafeStatus(ui)).IsEqual(resetMessage);
            AssertThat(ui.CaptureTestProbe().PageTitle).IsEqual("PURE RUN");
            AssertThat(FindButton(ui, button => button.Text == "Continue")!.Disabled).IsTrue();

            await Press(ui, tree, button => button.Text == "New Run");
            for (int frame = 0; frame < 180 && !ui.CaptureTestProbe().PageTitle.Contains("START CAMP", StringComparison.Ordinal); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(ui.CaptureTestProbe().PageTitle).Contains("START CAMP");
        }
        finally { await Free(ui, tree); }
    }

    private static string OffersFingerprint(IRunSaveStore store) => string.Join('|',
        store.Load().Snapshot!.ActiveRun!.MapState!.StoreOffers!
            .Select(offer => $"{offer.InstanceId.Value}:{offer.ContentId.Value}:{offer.Price}:{offer.Purchased}"));

    private static void Rewrite(IRunSaveStore store, Func<PureRunState, PureRunState> change)
    {
        PureRunSaveSnapshot current = store.Load().Snapshot!;
        PureRunState next = change(current.ActiveRun!);
        RunStoreResult saved = store.Save(new PureRunSaveSnapshot(next.Revision, next, current.TerminalSummary), current.Revision);
        if (!saved.Succeeded) throw new InvalidOperationException($"Legacy fixture rejected: {saved.ErrorCode}");
    }

    private static PureRunState With(PureRunState run, RunCharacterState[]? party = null,
        RunEncounterCheckpoint? checkpoint = null, RunAdventureState? adventure = null) => new(run.RunId, run.Seed,
        run.Revision + 1, run.Phase, run.EncounterIndex, run.EncounterContentId, party ?? run.Party.ToArray(),
        run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression, run.AppliedTransactionKeys, run.Gold,
        run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems, checkpoint ?? run.Checkpoint, run.MapState,
        run.NodeTransaction, run.EscortState, adventure ?? run.AdventureState);

    private static RunCharacterState AsDead(RunCharacterState member) => new(member.CharacterId, member.UnitContentId,
        member.Level, member.Attributes, 0, member.MaxHealth, member.CurrentMana, member.MaxMana, true,
        member.LearnedSkills, member.Equipment, member.CarriedConsumables, member.LearnedSkillStates,
        member.StartingSkillContentId);

    private static async Task<GodotPlayableRunMain> StartSetupRun(IRunSaveStore store, SceneTree tree, string id)
    {
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, Seed, id));
        tree.Root.AddChild(ui);
        PureRunSessionService service = Field<PureRunSessionService>(ui, "_run");
        service.ChooseParty(["pure_run_amazon", "pure_run_demonbound", "pure_run_mage"]);
        service.ChooseStartingSkill("pure_run_amazon", new ContentId("skill.amazon.thrust.lv1"));
        RunSessionResult setup = service.ChooseStartingSkill("pure_run_mage", new ContentId("skill.mage.fireball.lv1"));
        if (!setup.Succeeded) throw new InvalidOperationException($"Setup failed: {setup.ErrorCode}");
        InvokePrivate(ui, "RouteCommittedSetup", setup);
        for (int frame = 0; frame < 180 && !ui.ActWorldReady; frame++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        PureRunState ready = store.Load().Snapshot?.ActiveRun
            ?? throw new InvalidOperationException($"Setup did not persist a ready run: {SafeStatus(ui)}");
        InvokePrivate(ui, "HandleAdventureExit", ready, "layer_01_battle");
        for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (!ui.CaptureTestProbe().BattleActive) await EnterBattle(ui, store, tree, "layer_01_battle");
        await WaitForBattle(ui, tree);
        return ui;
    }

    private static async Task ClearFirstThreeLayers(GodotPlayableRunMain ui, IRunSaveStore store, SceneTree tree)
    {
        await SettleCurrentBattle(ui, store, tree);
        foreach (string node in new[] { "layer_02_battle", "layer_03_battle" })
        {
            await ExitTo(ui, store, tree, node);
            await WaitForBattle(ui, tree);
            await SettleCurrentBattle(ui, store, tree);
        }
        AssertThat(store.Load().Snapshot!.ActiveRun!.BattlesCompleted).IsEqual(3);
    }

    private static async Task SettleCurrentBattle(GodotPlayableRunMain ui, IRunSaveStore store, SceneTree tree)
    {
        PureRunState pending = store.Load().Snapshot!.ActiveRun!;
        RunEncounterCheckpoint checkpoint = pending.Checkpoint
            ?? throw new InvalidOperationException($"No checkpoint in phase {pending.Phase}");
        var victory = new PureRunBattleResult(pending.RunId, checkpoint.Revision, checkpoint.EncounterContentId,
            true, 3, 3, pending.Party.Select(member => new BattlePartyResult(member.CharacterId,
                member.MaxHealth, member.MaxMana, false, member.CarriedConsumables)).ToArray());
        long before = store.Load().Snapshot!.Revision;
        object diagnostic = typeof(GodotPlayableRunMain)
            .GetMethod("CompleteBattle", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ui, [victory])!;
        for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (store.Load().Snapshot!.Revision <= before)
            throw new InvalidOperationException($"Settlement did not save: {diagnostic}");
        await CompletePendingProgression(ui, store, tree);
    }

    private static async Task CompletePendingProgression(GodotPlayableRunMain ui, IRunSaveStore store, SceneTree tree)
    {
        for (int guard = 0; guard < 8; guard++)
        {
            PureRunState run = store.Load().Snapshot!.ActiveRun!;
            if (run.PendingProgression.Count == 0) return;
            if (FindButton(ui, button => button.Name == "ProgressionAttribute_Strength") is null)
                InvokePrivate(ui, "ShowProgression", run, run.PendingProgression[0]);
            await Press(ui, tree, button => button.Name == "ProgressionAttribute_Strength");
            long before = store.Load().Snapshot!.Revision;
            await Press(ui, tree, button => button.Name == "ProgressionSkillChoice_0" || button.Text == "Confirm Attribute");
            if (store.Load().Snapshot!.Revision <= before)
                throw new InvalidOperationException($"Progression did not save: {SafeStatus(ui)}");
        }
        throw new InvalidOperationException("Pending progression did not drain.");
    }

    private static Button? FindButton(Node root, Func<Button, bool> match)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Button button && GodotObject.IsInstanceValid(button) && !button.IsQueuedForDeletion() && match(button))
                return button;
            if (FindButton(child, match) is Button nested) return nested;
        }
        return null;
    }

    private static async Task Press(GodotPlayableRunMain ui, SceneTree tree, Func<Button, bool> match)
    {
        Button button = FindButton(ui, match)
            ?? throw new InvalidOperationException($"Button missing on page {ui.CaptureTestProbe().PageTitle}");
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static async Task ExitTo(GodotPlayableRunMain ui, IRunSaveStore store, SceneTree tree, string node)
    {
        long before = store.Load().Snapshot!.Revision;
        InvokePrivate(ui, "HandleAdventureExit", store.Load().Snapshot!.ActiveRun!, node);
        for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (store.Load().Snapshot!.Revision <= before)
            throw new InvalidOperationException($"Exit to {node} did not save: {SafeStatus(ui)}");
        if (!ui.CaptureTestProbe().BattleActive &&
            ui.CaptureTestProbe().PageTitle.Contains("TILE BATTLE", StringComparison.Ordinal))
            await EnterBattle(ui, store, tree, node);
    }

    /// <summary>
    /// The scripted player's "Begin battle" click. It is explicit, runs once, and must write exactly
    /// one checkpoint CAS so waiting can never hide a battle the game failed to open.
    /// </summary>
    private static async Task EnterBattle(GodotPlayableRunMain ui, IRunSaveStore store, SceneTree tree, string node)
    {
        string begin = node switch
        {
            "layer_05_battle" => "BeginLayerFive",
            "layer_07_battle" => "BeginBoss",
            _ => "BeginReadyEncounter"
        };
        long before = store.Load().Snapshot!.Revision;
        InvokePrivate(ui, begin);
        for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(store.Load().Snapshot!.Revision).IsEqual(before + 1);
    }

    private static async Task<GodotPlayableRunMain> AssertColdResume(IRunSaveStore store, SceneTree tree, string id,
        bool keep = false)
    {
        PureRunSaveSnapshot written = store.Load().Snapshot!;
        var ui = new GodotPlayableRunMain();
        ui.ConfigureTestContext(new GodotPlayableRunTestContext(store, Seed, id));
        tree.Root.AddChild(ui);
        try
        {
            for (int frame = 0; frame < 180 && !ui.ActWorldReady; frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            PureRunState? active = store.Load().Snapshot?.ActiveRun;
            string? status = Field<Label?>(ui, "_status")?.Text;
            if (active is null)
                throw new InvalidOperationException($"Cold resume cleared the run: status={status}");
            AssertThat(active.RunId).IsEqual(written.ActiveRun!.RunId);
            AssertThat(active.Revision).IsEqual(written.ActiveRun.Revision);
            string resetMessage = (string)typeof(GodotPlayableRunMain)
                .GetField("StaleSaveResetMessage", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            AssertThat(status).IsNotEqual(resetMessage);
            AssertThat(ui.CaptureTestProbe().PageTitle).IsNotEqual("PURE RUN");
            AssertThat(Field<PureRunSessionService>(ui, "_run").ResumeRun().ErrorCode)
                .IsNotEqual(PureRunSessionService.StaleSaveReset);
        }
        catch
        {
            await Free(ui, tree);
            throw;
        }
        if (!keep) await Free(ui, tree);
        return ui;
    }

    private static async Task WaitForBattle(GodotPlayableRunMain ui, SceneTree tree)
    {
        for (int frame = 0; frame < 180; frame++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (ui.ActWorldReady && ui.CaptureTestProbe().BattleActive) return;
        }
        throw new InvalidOperationException(
            $"Battle not ready: page={ui.CaptureTestProbe().PageTitle}, status={SafeStatus(ui)}");
    }

    private static async Task Free(GodotPlayableRunMain ui, SceneTree tree)
    {
        if (!GodotObject.IsInstanceValid(ui)) return;
        ui.QueueFree();
        for (int frame = 0; frame < 4 && GodotObject.IsInstanceValid(ui); frame++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static string SafeStatus(GodotPlayableRunMain ui)
    {
        Label? status = Field<Label?>(ui, "_status");
        return status is not null && GodotObject.IsInstanceValid(status)
            ? status.Text
            : ui.CaptureTestProbe().PageTitle;
    }

    private static T Field<T>(GodotPlayableRunMain target, string name) => (T)typeof(GodotPlayableRunMain)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void InvokePrivate(GodotPlayableRunMain target, string name, params object[] arguments) =>
        typeof(GodotPlayableRunMain).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(target, arguments);
}
