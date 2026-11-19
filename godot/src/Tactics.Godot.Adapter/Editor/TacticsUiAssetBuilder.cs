#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>Headless 入口：godot --headless --script 运行 <see cref="TacticsUiAssetFactory"/>。</summary>
[Tool]
public partial class TacticsUiAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try { TacticsUiAssetFactory.Build(); Quit(0); }
        catch (Exception exception) { GD.PushError(exception.ToString()); Quit(1); }
    }
}
#endif
