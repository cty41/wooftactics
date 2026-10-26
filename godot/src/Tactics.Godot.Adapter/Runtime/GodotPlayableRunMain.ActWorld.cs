using Godot;
using Tactics.Application.Runs;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public partial class GodotPlayableRunMain
{
    private GodotActWorldHost? _actWorld;
    private GodotActLoadCoordinator? _actLoader;
    private GodotResolvedActContent? _actContent;
    private string? _actOwnerRunId;
    private int _actSeed;
    private bool _actBelongsToSetup;
    private long _actEpoch;
    private bool _actPreparationPending;

    public bool ActWorldReady => _actWorld is { IsPrepared: true };
    public GodotActWorldHost? ActWorld => _actWorld;

    private PureRunActBlueprint BuildHostContentPlan(int seed, PureRunState? pendingRun = null)
    {
        PureRunActBlueprint defaults = PureRunActBlueprint.Resolve(MapDefinition, seed);
        PureRunNodeContentBinding[] variants = PureRunActBlueprint.EnumerateContentVariants(MapDefinition).ToArray();
        var identities = new List<RunNodeSceneIdentity>();
        foreach ((string nodeId, RunAdventureEventContextKind context) in new[]
        {
            ("layer_04_event", RunAdventureEventContextKind.CursedChestMimic),
            ("layer_06_event", RunAdventureEventContextKind.FallenAltarGuardian)
        })
        {
            PureRunNodeContentBinding binding = variants.Single(value => value.NodeId == nodeId && value.EventContext == context);
            identities.Add(new RunNodeSceneIdentity(defaults.ActDefinition.ActId,
                defaults.ActDefinition.DefinitionVersion, nodeId, binding.TemplateId));
        }
        if (pendingRun is { Phase: PureRunPhase.PendingBattle })
        {
            var source = new PureRunSaveSnapshot(pendingRun.Revision, pendingRun, null);
            PureRunPendingSceneRecoveryResult recovery = PureRunPendingSceneRecovery.Recover(source, MapDefinition);
            // First-battle InitialExploration can be identity-less; keep seed defaults instead of
            // aborting Act preparation when Recover cannot prove a later node.
            if (recovery.Succeeded && recovery.Snapshot.ActiveRun?.Checkpoint?.SceneIdentity is { } current)
            {
                identities.RemoveAll(value => value.NodeId == current.NodeId);
                identities.Add(current);
            }
        }
        else if (pendingRun is not null && TryResolveTravelingEscortPresentation(pendingRun, variants,
                     defaults, out RunNodeSceneIdentity? escortIdentity))
        {
            identities.RemoveAll(value => value.NodeId == escortIdentity.NodeId);
            identities.Add(escortIdentity);
        }
        return PureRunActBlueprint.Restore(MapDefinition, seed, identities);
    }

    private bool TryResolveTravelingEscortPresentation(PureRunState run,
        IReadOnlyList<PureRunNodeContentBinding> variants, PureRunActBlueprint defaults,
        out RunNodeSceneIdentity identity)
    {
        identity = null!;
        if (run is not
            {
                Phase: PureRunPhase.ResolvingLayerFourNode or PureRunPhase.ResolvingLayerSixNode,
                EscortState: { Lifecycle: RunEscortLifecycle.Traveling, ProtectedNpcAlive: true } escort,
                NodeTransaction: { Committed: false, Kind: PureRunNodeKind.Mystery } transaction,
                MapState: { Phase: PureRunMapPhase.ResolvingNode, NodeLifecycle: RunNodeLifecycle.Selected } map
            } || string.IsNullOrWhiteSpace(escort.QuestId) || string.IsNullOrWhiteSpace(escort.AcceptedNodeId) ||
            string.IsNullOrWhiteSpace(transaction.TransactionKey) ||
            run.AppliedTransactionKeys.Contains(transaction.TransactionKey, StringComparer.Ordinal) ||
            escort.DestinationNodeId != transaction.NodeId || escort.AcceptedNodeId == escort.DestinationNodeId ||
            map.SelectedNodeId != transaction.NodeId || map.PendingNodeId != transaction.NodeId ||
            map.PendingTransactionKey != transaction.TransactionKey ||
            (map.MapContentId is { } mapId && mapId != MapDefinition.ContentId) ||
            (map.MapLayoutVersion != 0 && map.MapLayoutVersion != MapDefinition.LayoutVersion))
            return false;
        PureRunMapNodeDefinition? destination = MapDefinition.Nodes.SingleOrDefault(value => value.NodeId == transaction.NodeId);
        PureRunMapNodeDefinition? accepted = MapDefinition.Nodes.SingleOrDefault(value => value.NodeId == escort.AcceptedNodeId);
        int expectedLayer = run.Phase == PureRunPhase.ResolvingLayerSixNode ? 6 : 4;
        bool currentIsPredecessor = map.CurrentNodeId == transaction.NodeId ||
            MapDefinition.Connections.Any(edge => edge.FromNodeId == map.CurrentNodeId && edge.ToNodeId == transaction.NodeId);
        if (destination is null || destination.Kind != PureRunNodeKind.Mystery || destination.Layer != expectedLayer ||
            accepted is null || accepted.Layer >= destination.Layer || !currentIsPredecessor)
            return false;
        PureRunNodeContentBinding? binding = variants.FirstOrDefault(value => value.NodeId == transaction.NodeId &&
            value.EventContext == RunAdventureEventContextKind.LostVillagerEscort);
        if (binding is null) return false;
        identity = new RunNodeSceneIdentity(defaults.ActDefinition.ActId,
            defaults.ActDefinition.DefinitionVersion, transaction.NodeId, binding.TemplateId);
        return true;
    }

    private bool EnsureSetupActPrepared(PendingRunSetup setup)
    {
        if (_actBelongsToSetup && _actSeed == setup.Seed && ActWorldReady) return true;
        if (_actBelongsToSetup && _actSeed == setup.Seed && _actLoader is { IsLoading: true }) return false;
        PrepareActWorld(setup.Seed, null, ShowPartySelection);
        return false;
    }

    private bool EnsureRunActPrepared(PureRunState run, Action resume)
    {
        if (_actOwnerRunId == run.RunId && ActWorldReady) return true;
        if (_actOwnerRunId == run.RunId && _actLoader is { IsLoading: true }) return false;
        PrepareActWorld(run.Seed, run.RunId, resume, BuildHostContentPlan(run.Seed, run));
        return false;
    }

    private void RouteCommittedSetup(RunSessionResult result)
    {
        PureRunState? run = result.Snapshot?.ActiveRun;
        if (!result.Succeeded || run is null)
            throw new InvalidOperationException("act.setup_commit_proof_missing");
        if (_actBelongsToSetup && _actSeed == run.Seed && ActWorldReady)
        {
            _actBelongsToSetup = false;
            _actOwnerRunId = run.RunId;
        }
        RouteRunState(result.Snapshot!);
    }

    private void PrepareActWorld(int seed, string? runId, Action resume, PureRunActBlueprint? contentPlan = null)
    {
        ReleaseActWorld();
        long epoch = _actEpoch;
        _actPreparationPending = true;
        _actSeed = seed;
        _actOwnerRunId = runId;
        _actBelongsToSetup = runId is null;
        Control page = NewPage("PREPARING ACT", "Loading all formal maps for this Act. No later node loads during selection.");
        SetInputContext(runId is null ? RunInputContext.Setup : RunInputContext.Adventure);
        _status = LabelAt(page, "Resolving node content…", new Vector2(470, 430), 22);
        BuildPauseMenu(page, false);
        try
        {
            // The host content plan is presentation authority only. Recovery/projector evidence remains
            // a separate one-node proof and must never consume the baseline future-node overrides below.
            _actContent = GodotActContentCatalog.Load(contentPlan ?? BuildHostContentPlan(seed));
            var host = new GodotActWorldHost { Name = "ActWorldHost", Visible = false };
            _actWorld = host;
            // The world is deliberately outside _page, which CreatePage owns and destroys.
            AddChild(host);
            var loader = new GodotActLoadCoordinator { Name = "ActLoadCoordinator" };
            _actLoader = loader;
            AddChild(loader);
            loader.ProgressChanged += (count, total) =>
            {
                if (epoch == _actEpoch && ReferenceEquals(_page, page)) SetStatus($"Preparing maps {count}/{total}");
            };
            loader.Failed += error =>
            {
                if (epoch != _actEpoch) return;
                ShowActPreparationFailure(page, seed, runId, resume, contentPlan, error);
            };
            loader.Prepared += () =>
            {
                if (epoch != _actEpoch || IsQueuedForDeletion() || !ReferenceEquals(_actWorld, host)) return;
                if (!ReferenceEquals(_page, page)) { ReleaseActWorld(); return; }
                _actPreparationPending = false;
                _actLoader = null;
                loader.QueueFree();
                if (runId is null)
                {
                    if (SaveStore.Load().Snapshot?.PendingRunSetup?.Seed != seed) { ReleaseActWorld(); return; }
                    resume();
                }
                else
                {
                    // Re-read through the application repair projection, never reissue a raw legacy party.
                    var latest = _run?.ResumeRun();
                    if (latest?.Succeeded != true || latest.Snapshot?.ActiveRun?.RunId != runId)
                    { ReleaseActWorld(); return; }
                    RouteRunState(latest.Snapshot);
                }
            };
            loader.Start(host, runId ?? $"setup:{Guid.NewGuid():N}", _actContent.Blueprint.ActDefinition, _actContent.Templates);
        }
        catch (Exception error)
        {
            _actLoader?.Cancel();
            ShowActPreparationFailure(page, seed, runId, resume, contentPlan, error.Message);
        }
    }

    private void ShowActPreparationFailure(Control page, int seed, string? runId, Action resume,
        PureRunActBlueprint? contentPlan, string error)
    {
        if (!ReferenceEquals(_page, page) || IsQueuedForDeletion()) return;
        SetStatus($"Act preparation failed: {error}");
        page.AddChild(PlaceControl(Button("Retry Act Preparation", () => PrepareActWorld(seed, runId, resume, contentPlan)),
            new Vector2(580, 530), new Vector2(440, 64)));
    }

    private void HideActWorldForPage()
    {
        _adventureBoard?.CancelMovement("act-world-page-transition");
        if (_actWorld is null || !GodotObject.IsInstanceValid(_actWorld)) return;
        _actWorld.Visible = false;
        if (_actWorld.IsPrepared) _actWorld.DeactivateCurrent();
    }

    private void ReleaseActWorld()
    {
        _actEpoch++;
        if (_actLoader is not null && GodotObject.IsInstanceValid(_actLoader))
        {
            _actLoader.Cancel();
            _actLoader.QueueFree();
        }
        if (_actWorld is not null && GodotObject.IsInstanceValid(_actWorld))
        {
            _actWorld.Release();
            _actWorld.QueueFree();
        }
        _actLoader = null;
        _actWorld = null;
        _actContent = null;
        _actOwnerRunId = null;
        _actBelongsToSetup = false;
        _actPreparationPending = false;
    }
}
