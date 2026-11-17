using Godot;
using System.Threading.Tasks;

namespace Tactics.Godot.Tests;

/// <summary>
/// Opt-in page evidence for the manual acceptance ledger. Does nothing unless <c>TACTICS_QA_SCREENSHOT_DIR</c> is
/// set and the run has a real renderer, so headless gates neither write files nor change behaviour. A screenshot is
/// evidence for a human reviewer; it is never an acceptance verdict.
/// </summary>
internal static class ManualQaScreenshot
{
    public const string DirectoryVariable = "TACTICS_QA_SCREENSHOT_DIR";

    /// <summary>Returns the written file path, or null when capture is off or unavailable.</summary>
    public static async Task<string?> Capture(SceneTree tree, string name)
    {
        string? directory = System.Environment.GetEnvironmentVariable(DirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory) || DisplayServer.GetName() == "headless") return null;
        // Let the freshly built page lay out and draw before reading the frame back.
        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Image? image = tree.Root.GetTexture()?.GetImage();
        if (image is null || image.IsEmpty()) return null;
        System.IO.Directory.CreateDirectory(directory);
        string path = System.IO.Path.Combine(directory, name + ".png");
        return image.SavePng(path) == Error.Ok ? path : null;
    }
}
