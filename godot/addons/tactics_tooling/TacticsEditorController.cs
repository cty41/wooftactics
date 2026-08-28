#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Editor;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class TacticsEditorController : Node, ISerializationListener
{
    internal const string InitializationMetadata = "tactics_tooling_initialized";
    private const string BridgeNodeName = "TacticsAuthoringEditorBridge";
    private const string WorkbenchNodeName = "TacticsContentWorkbench";
    private TacticsContentWorkbench? _workbench;
    private EditorUndoRedoManager? _undoRedo;
    private bool _headless;
    private bool _mainScreenVisible;
    private TacticsAuthoringEditorBridge? _authoringBridge;
    private int _readinessAttempts;
    private string _lastReadinessDiagnostic = string.Empty;
    private bool _shuttingDown;

    private EditorUndoRedoManager EditorUndoRedo =>
        _undoRedo ?? throw new InvalidOperationException("Tactics tooling controller requires Editor UndoRedo before entering the tree.");

    public void Configure(EditorUndoRedoManager undoRedo)
    {
        _undoRedo = undoRedo ?? throw new ArgumentNullException(nameof(undoRedo));
    }

    public void SetMainScreenVisible(bool visible)
    {
        _mainScreenVisible = visible;
        if (_workbench is not null && GodotObject.IsInstanceValid(_workbench))
            _workbench.Visible = visible;
    }

    public override void _EnterTree()
    {
        SetMeta(InitializationMetadata, false);
        GD.Print("[Tactics Tooling] Controller entering tree.");

        try
        {
            _shuttingDown = false;
            if (OS.GetCmdlineArgs().Contains("--build-poison-spear"))
            {
                PoisonSpearAssetFactory.BuildLv1();
                SetMeta(InitializationMetadata, true);
                GetTree().Quit();
                return;
            }

            // Headless import/generation runs can temporarily expose scripted resources as base Resource
            // while EditorFileSystem updates the C# global-class cache. The authoring UI has no headless
            // consumer, so keep the controller lifecycle observable without constructing its typed panels.
            if (DisplayServer.GetName() == "headless")
            {
                _headless = true;
                SetProcess(false);
                SetMeta(InitializationMetadata, true);
                GD.Print("[Tactics Tooling] Main screen skipped in headless editor mode.");
                return;
            }

            SetProcess(true);
            CreateBridge();
            SetMeta(InitializationMetadata, true);
        }
        catch (Exception exception)
        {
            GD.PushError($"[Tactics Tooling] Failed to initialize controller: {exception}");
            ShutdownForReload();
        }
    }

    public override void _ExitTree()
    {
        GD.Print("[Tactics Tooling] Controller exiting tree.");
        ShutdownForReload();
    }

    public void OnBeforeSerialize()
    {
        ShutdownForReload();
    }

    public void OnAfterDeserialize()
    {
        _shuttingDown = false;
        if (!_headless)
            CallDeferred(nameof(RestoreAfterAssemblyReload));
    }

    private void RestoreAfterAssemblyReload()
    {
        if (_shuttingDown || !IsInsideTree()) return;
        _shuttingDown = false;
        _authoringBridge ??= GetNodeOrNull<TacticsAuthoringEditorBridge>(BridgeNodeName);
        _workbench ??= EditorInterface.Singleton.GetEditorMainScreen()
            .GetNodeOrNull<TacticsContentWorkbench>(WorkbenchNodeName);
        if (_authoringBridge is not null && GodotObject.IsInstanceValid(_authoringBridge))
            _authoringBridge.RestartAfterReload(EditorUndoRedo);
        else
            CreateBridge();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_headless || _shuttingDown) return;
        if (EditorInterface.Singleton.GetResourceFilesystem().IsScanning()) return;
        if (_workbench is not null)
        {
            if (_authoringBridge is not null && !_authoringBridge.IsReady &&
                AuthoringEditorReadinessProbe.Probe().State == EditorResourceLoadState.Ready)
                _authoringBridge.MarkReady();
            return;
        }
        RegisterMainScreenWhenReady();
    }

    private void CreateBridge()
    {
        if (_authoringBridge is not null) return;
        _authoringBridge = new TacticsAuthoringEditorBridge { Name = BridgeNodeName };
        _authoringBridge.Configure(EditorUndoRedo);
        AddChild(_authoringBridge);
    }

    private void RegisterMainScreenWhenReady()
    {
        if (_headless || _shuttingDown) return;
        if (!IsInsideTree()) return;
        if (EditorInterface.Singleton.GetResourceFilesystem().IsScanning()) return;
        if (_workbench is not null) return;
        AuthoringEditorReadinessResult readiness = AuthoringEditorReadinessProbe.Probe();
        if (readiness.State == EditorResourceLoadState.InvalidResource)
        {
            _readinessAttempts = 0;
            if (!string.Equals(_lastReadinessDiagnostic, readiness.Diagnostic, StringComparison.Ordinal))
            {
                _lastReadinessDiagnostic = readiness.Diagnostic;
                GD.PushError($"[Tactics Tooling] Main screen readiness failed: {readiness.Diagnostic}");
            }
            return;
        }
        if (readiness.State == EditorResourceLoadState.ReloadPending)
        {
            _readinessAttempts++;
            if (_readinessAttempts == 1)
                GD.PushWarning("[Tactics Tooling] Main screen is waiting for C# Resource types after assembly reload.");
            if (_readinessAttempts >= ReloadSafeEditorResourceLoader.MaximumDeferredAttempts)
            {
                if (!string.Equals(_lastReadinessDiagnostic, readiness.Diagnostic, StringComparison.Ordinal))
                {
                    _lastReadinessDiagnostic = readiness.Diagnostic;
                    GD.PushError($"[Tactics Tooling] Main screen remained unavailable after {_readinessAttempts} deferred frames. {readiness.Diagnostic}");
                }
                return;
            }
            return;
        }
        _readinessAttempts = 0;
        _lastReadinessDiagnostic = string.Empty;
        _workbench = new TacticsContentWorkbench { Name = WorkbenchNodeName };
        _workbench.Configure(EditorUndoRedo);
        EditorInterface.Singleton.GetEditorMainScreen().AddChild(_workbench);
        _workbench.Visible = _mainScreenVisible;
        _authoringBridge?.MarkReady();
        GD.Print("[Tactics Tooling] Main screen registered after filesystem scan.");
    }

    public void ShutdownForReload()
    {
        if (_shuttingDown && _workbench is null && _authoringBridge is null) return;
        _shuttingDown = true;
        try
        {
            if (GodotObject.IsInstanceValid(this)) SetProcess(false);
        }
        catch (ObjectDisposedException)
        {
        }
        _readinessAttempts = 0;
        _lastReadinessDiagnostic = string.Empty;

        if (_workbench is not null && GodotObject.IsInstanceValid(_workbench))
        {
            _workbench.ShutdownForReload();
            _workbench.GetParent()?.RemoveChild(_workbench);
            _workbench.Free();
        }
        _workbench = null;

        if (_authoringBridge is not null && GodotObject.IsInstanceValid(_authoringBridge))
        {
            AuthoringBridgeShutdownResult result = _authoringBridge.ShutdownForReload(TimeSpan.FromSeconds(2));
            if (!result.Completed)
                GD.PushError($"[Tactics Tooling] {result.Diagnostic}");
            _authoringBridge.GetParent()?.RemoveChild(_authoringBridge);
            _authoringBridge.Free();
        }
        _authoringBridge = null;
    }
}
#endif
