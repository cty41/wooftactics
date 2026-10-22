using GdUnit4;
using Godot;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Editor;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public sealed class ActWorldHostGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void AllMapsMustBePreparedBeforeActivationAndTheirIdentitySurvivesSwitches()
    {
        var host = new GodotActWorldHost();
        try
        {
            host.BeginPreparation("run-a", Definition());
            GodotAdventureMapInstance camp = host.PrepareNode("start", CampTemplate());
            AssertThrown(() => host.CompletePreparation()).IsInstanceOf<InvalidOperationException>().HasMessage("act.maps_incomplete");
            AssertThrown(() => host.ActivateNode("start")).IsInstanceOf<InvalidOperationException>().HasMessage("act.not_ready");
            GodotAdventureMapInstance fight = host.PrepareNode("fight", BattleTemplate());
            ulong campSurface = camp.Surface.GetInstanceId();
            ulong fightSurface = fight.Surface.GetInstanceId();
            host.CompletePreparation();
            GodotNodeActivationLease first = host.ActivateNode("start");
            AssertThat(host.IsLeaseCurrent(first)).IsTrue();
            AssertThat(host.ActivateNode("start")).IsEqual(first);
            GodotNodeActivationLease second = host.ActivateNode("fight");
            AssertThat(host.IsLeaseCurrent(first)).IsFalse();
            AssertThat(host.IsLeaseCurrent(second)).IsTrue();
            AssertThat(host.Nodes.Values.Count(node => node.Mode == AdventureMapInstanceMode.Active)).IsEqual(1);
            AssertThat(camp.Mode).IsEqual(AdventureMapInstanceMode.Preview);
            AssertThat(camp.ProcessMode).IsEqual(Node.ProcessModeEnum.Disabled);
            AssertThat(fight.ProcessMode).IsEqual(Node.ProcessModeEnum.Inherit);
            AssertThat(camp.Surface.GetInstanceId()).IsEqual(campSurface);
            AssertThat(fight.Surface.GetInstanceId()).IsEqual(fightSurface);
            host.Visible = false;
            AssertThat(host.IsLeaseCurrent(second)).IsTrue(); // Rendering and gameplay authority are independent.
            host.DeactivateCurrent();
            AssertThat(host.IsLeaseCurrent(second)).IsFalse();
            AssertThat(host.Nodes.Values.All(node => node.ProcessMode == Node.ProcessModeEnum.Disabled)).IsTrue();
        }
        finally { host.Free(); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ARebuiltWorldRejectsAnOldLeaseEvenForTheSameRunAndNode()
    {
        var first = new GodotActWorldHost();
        var second = new GodotActWorldHost();
        try
        {
            foreach (GodotActWorldHost host in new[] { first, second })
            {
                host.BeginPreparation("same-run", Definition());
                host.PrepareNode("start", CampTemplate());
                host.PrepareNode("fight", BattleTemplate());
                host.CompletePreparation();
            }
            GodotNodeActivationLease old = first.ActivateNode("start");
            second.ActivateNode("start");
            AssertThat(second.IsLeaseCurrent(old)).IsFalse();
        }
        finally { first.Free(); second.Free(); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void InvalidOrDuplicatePreparationCannotReplaceAResidentMap()
    {
        var host = new GodotActWorldHost();
        try
        {
            host.BeginPreparation("run-a", Definition());
            AssertThrown(() => host.PrepareNode("start", BattleTemplate()))
                .IsInstanceOf<InvalidOperationException>();
            AssertThat(host.PreparedCount).IsEqual(0);
            GodotAdventureMapInstance camp = host.PrepareNode("start", CampTemplate());
            AssertThrown(() => host.PrepareNode("start", CampTemplate()))
                .IsInstanceOf<InvalidOperationException>();
            AssertThrown(() => camp.Activate()).IsInstanceOf<InvalidOperationException>().HasMessage("act.activation_requires_owner");
            AssertThrown(() => camp.Configure(CampTemplate()))
                .IsInstanceOf<InvalidOperationException>().HasMessage("act.owned_map_cannot_reconfigure");
            AssertThat(host.PreparedCount).IsEqual(1);
            AssertThat(host.Nodes["start"].GetInstanceId()).IsEqual(camp.GetInstanceId());
        }
        finally { host.Free(); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ReleaseInvalidatesLeasesImmediatelyAndFreesMapsWithoutReinitialization()
    {
        var host = new GodotActWorldHost();
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(host);
        try
        {
            host.BeginPreparation("run-a", Definition());
            GodotAdventureMapInstance camp = host.PrepareNode("start", CampTemplate());
            host.PrepareNode("fight", BattleTemplate());
            host.CompletePreparation();
            GodotNodeActivationLease lease = host.ActivateNode("start");
            host.Release();
            host.Release();
            AssertThat(host.IsLeaseCurrent(lease)).IsFalse();
            AssertThat(host.IsPrepared).IsFalse();
            AssertThat(host.Nodes.Count).IsEqual(0);
            AssertThat(camp.ProcessMode).IsEqual(Node.ProcessModeEnum.Disabled);
            AssertThrown(() => host.BeginPreparation("run-b", Definition())).IsInstanceOf<InvalidOperationException>();
            // ProcessFrame is emitted before that frame's deferred deletion flush.
            for (int frame = 0; frame < 4 && GodotObject.IsInstanceValid(camp); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(GodotObject.IsInstanceValid(camp)).IsFalse();
        }
        finally
        {
            host.QueueFree();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task LoaderBuildsBoundedBatchesAndDoesNotActivateAnyNode()
    {
        var host = new GodotActWorldHost();
        var loader = new GodotActLoadCoordinator { MaximumNodesPerFrame = 1 };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(host);
        tree.Root.AddChild(loader);
        try
        {
            int completions = 0;
            var progress = new List<int>();
            loader.Prepared += () => completions++;
            loader.ProgressChanged += (count, _) => progress.Add(count);
            loader.Start(host, "run-a", Definition(), Templates());
            AssertThat(host.PreparedCount).IsEqual(0);
            for (int frame = 0; frame < 10 && loader.IsLoading; frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(host.IsPrepared).IsTrue();
            AssertThat(host.PreparedCount).IsEqual(2);
            AssertThat(progress.ToArray()).ContainsExactly(1, 2);
            AssertThat(completions).IsEqual(1);
            AssertThat(host.ActiveNodeId is null).IsTrue();
            loader.Cancel(); // A completed loader no longer owns the world's lifetime.
            AssertThat(host.IsPrepared).IsTrue();
        }
        finally
        {
            loader.QueueFree();
            host.QueueFree();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LoaderCancellationDuringProgressCannotPublishPreparedOrAcceptLateTicks()
    {
        var host = new GodotActWorldHost();
        var loader = new GodotActLoadCoordinator { MaximumNodesPerFrame = 1 };
        try
        {
            int completions = 0;
            loader.Prepared += () => completions++;
            loader.ProgressChanged += (_, _) => loader.Cancel();
            loader.Start(host, "run-a", Definition(), Templates());
            loader._Process(0);
            loader._Process(0);
            AssertThat(loader.IsLoading).IsFalse();
            AssertThat(host.IsPrepared).IsFalse();
            AssertThat(host.Nodes.Count).IsEqual(0);
            AssertThat(completions).IsEqual(0);
        }
        finally { loader.Free(); host.Free(); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MissingTemplateFailsBeforeAllocatingAnyMap()
    {
        var host = new GodotActWorldHost();
        var loader = new GodotActLoadCoordinator();
        try
        {
            AssertThrown(() => loader.Start(host, "run-a", Definition(),
                new Dictionary<ContentId, AdventureMapTemplateResource>())).IsInstanceOf<InvalidOperationException>();
            AssertThat(host.Nodes.Count).IsEqual(0);
            AssertThat(loader.IsLoading).IsFalse();
        }
        finally { loader.Free(); host.Free(); }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WholeActValidationRejectsWrongExitTargetEntryAndTerminalIdentity()
    {
        foreach (string fault in new[] { "target", "entry", "terminal" })
        {
            var host = new GodotActWorldHost();
            try
            {
                AdventureMapTemplateResource camp = CampTemplate();
                AdventureMapTemplateResource fight = BattleTemplate();
                if (fault == "target") camp.ExitsValue = "exit-main@9,5@unknown@entry-main";
                if (fault == "entry") camp.ExitsValue = "exit-main@9,5@fight@missing-entry";
                if (fault == "terminal") fight = AdventureMapAssetFactory.CreateBasicBattleTemplate();
                host.BeginPreparation("run-a", Definition());
                host.PrepareNode("start", camp);
                host.PrepareNode("fight", fight);
                AssertThrown(host.CompletePreparation).IsInstanceOf<InvalidOperationException>();
                AssertThat(host.IsPrepared).IsFalse();
            }
            finally { host.Free(); }
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task QueueFreeInFinalProgressNeverPublishesPrepared()
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        foreach (bool deleteHost in new[] { false, true })
        {
            var host = new GodotActWorldHost();
            var loader = new GodotActLoadCoordinator();
            tree.Root.AddChild(host);
            tree.Root.AddChild(loader);
            int completions = 0;
            loader.Prepared += () => completions++;
            loader.ProgressChanged += (count, total) =>
            {
                if (count != total) return;
                if (deleteHost) host.QueueFree(); else loader.QueueFree();
            };
            loader.Start(host, "run-a", Definition(), Templates());
            loader._Process(0);
            if (loader.IsLoading) loader._Process(0);
            AssertThat(completions).IsEqual(0);
            AssertThat(host.IsPrepared).IsFalse();
            AssertThat(loader.IsLoading).IsFalse();
            if (!host.IsQueuedForDeletion()) host.QueueFree();
            if (!loader.IsQueuedForDeletion()) loader.QueueFree();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static IReadOnlyDictionary<ContentId, AdventureMapTemplateResource> Templates() =>
        new Dictionary<ContentId, AdventureMapTemplateResource>
        {
            [new ContentId(AdventureMapAssetFactory.StartCampContentId)] = CampTemplate(),
            [new ContentId(AdventureMapAssetFactory.BasicBattleContentId)] = BattleTemplate()
        };

    private static AdventureMapTemplateResource CampTemplate()
    {
        AdventureMapTemplateResource template = AdventureMapAssetFactory.CreateStartCampTemplate();
        template.ExitsValue = "exit-main@9,5@fight@entry-main";
        return template;
    }

    private static AdventureMapTemplateResource BattleTemplate()
    {
        AdventureMapTemplateResource template = AdventureMapAssetFactory.CreateBasicBattleTemplate();
        template.IsTerminal = true;
        template.ExitsValue = string.Empty;
        template.ConnectionAnchorsValue = string.Empty;
        return template;
    }

    private static PureRunActDefinition Definition()
    {
        var map = new PureRunMapDefinition(new ContentId("run-map.test-act"), 2,
            [new("start", 0, PureRunNodeKind.Battle, new ContentId("run.test")),
             new("fight", 1, PureRunNodeKind.Boss, new ContentId("encounter.test"))],
            [new("start", "fight")]);
        return new PureRunActDefinition(new ContentId("act-01"), 1, map, "start", "fight",
            [new("start", new ContentId(AdventureMapAssetFactory.StartCampContentId), PureRunNodeEntryStrategy.Exploration),
             new("fight", new ContentId(AdventureMapAssetFactory.BasicBattleContentId), PureRunNodeEntryStrategy.ImmediateCombat)]);
    }
}
