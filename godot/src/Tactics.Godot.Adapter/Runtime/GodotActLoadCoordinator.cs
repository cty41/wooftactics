using System.Diagnostics;
using Godot;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Main-thread, bounded-batch construction of the entire Act. The caller loads/deduplicates
/// formal resources first; missing content fails closed instead of creating placeholder maps.
/// This component does not commit a Run or acquire gameplay control.
/// </summary>
public partial class GodotActLoadCoordinator : Node
{
    private GodotActWorldHost? _host;
    private (string NodeId, AdventureMapTemplateResource Template)[] _pending = [];
    private int _next;
    private bool _started;

    public double BudgetMilliseconds { get; set; } = 4;
    public int MaximumNodesPerFrame { get; set; } = int.MaxValue;
    public bool IsLoading { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? FailedNodeId { get; private set; }
    public int PreparedCount => _next;
    public int TotalCount { get; private set; }
    public double LastBatchMilliseconds { get; private set; }
    public double MaximumBatchMilliseconds { get; private set; }
    public event Action<int, int>? ProgressChanged;
    public event Action? Prepared;
    public event Action<string>? Failed;

    public override void _Ready() => SetProcess(IsLoading);

    public void Start(GodotActWorldHost host, string runScope, PureRunActDefinition definition,
        IReadOnlyDictionary<ContentId, AdventureMapTemplateResource> templates)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(templates);
        if (_started) throw new InvalidOperationException("act.loader_already_started");
        if (!double.IsFinite(BudgetMilliseconds) || BudgetMilliseconds <= 0 || MaximumNodesPerFrame <= 0)
            throw new ArgumentOutOfRangeException(nameof(BudgetMilliseconds));
        // Resolve the full mapping before allocating any map. A shared template remains shared,
        // but each topology node receives its own stable formal map instance.
        var pending = new List<(string, AdventureMapTemplateResource)>();
        foreach (PureRunNodeSceneDefinition node in definition.Nodes)
        {
            if (!templates.TryGetValue(node.TemplateId, out AdventureMapTemplateResource? template) || template is null)
                throw new InvalidOperationException($"act.template_missing:{node.NodeId}:{node.TemplateId.Value}");
            if (template.ContentIdValue != node.TemplateId.Value)
                throw new InvalidOperationException($"act.template_mismatch:{node.NodeId}:{template.ContentIdValue}");
            template.ToCoreDefinition();
            pending.Add((node.NodeId, template));
        }
        host.BeginPreparation(runScope, definition);
        _started = true;
        _host = host;
        _pending = pending.ToArray();
        TotalCount = _pending.Length;
        IsLoading = true;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!CanContinue()) return;
        var watch = Stopwatch.StartNew();
        int built = 0;
        while (IsLoading && _next < _pending.Length && built < MaximumNodesPerFrame &&
               (built == 0 || watch.Elapsed.TotalMilliseconds < BudgetMilliseconds))
        {
            (string nodeId, AdventureMapTemplateResource template) = _pending[_next];
            if (!CanContinue()) return;
            try { _host!.PrepareNode(nodeId, template); }
            catch (Exception error)
            {
                FailedNodeId = nodeId;
                Fail($"act.prepare_failed:{nodeId}:{error.Message}");
                return;
            }
            _next++;
            built++;
            ProgressChanged?.Invoke(_next, TotalCount);
            // Subscribers may queue either node for deletion before _ExitTree runs.
            if (!CanContinue()) return;
        }
        LastBatchMilliseconds = watch.Elapsed.TotalMilliseconds;
        MaximumBatchMilliseconds = Math.Max(MaximumBatchMilliseconds, LastBatchMilliseconds);
        if (!CanContinue() || _next != _pending.Length) return;
        try { _host!.CompletePreparation(); }
        catch (Exception error) { Fail($"act.complete_failed:{error.Message}"); return; }
        IsLoading = false;
        SetProcess(false);
        _pending = [];
        Prepared?.Invoke();
    }

    private bool CanContinue()
    {
        if (!IsLoading) return false;
        if (!IsQueuedForDeletion() && _host is not null && GodotObject.IsInstanceValid(_host) &&
            !_host.IsQueuedForDeletion()) return true;
        Cancel();
        return false;
    }

    public void Cancel()
    {
        if (!IsLoading) return;
        IsLoading = false;
        SetProcess(false);
        _pending = [];
        if (_host is not null && GodotObject.IsInstanceValid(_host)) _host.Release();
    }

    private void Fail(string error)
    {
        ErrorCode = error;
        Cancel();
        Failed?.Invoke(error);
    }

    public override void _ExitTree() => Cancel();
}
