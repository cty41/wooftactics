#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

public partial class PoetAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try
        {
            PoetAssetFactory.Build();
            GD.Print("Poet assets generated through ResourceSaver.");
            Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            Quit(1);
        }
    }
}
#endif
