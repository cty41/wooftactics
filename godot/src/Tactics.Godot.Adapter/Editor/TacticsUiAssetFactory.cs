#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>
/// 通过 ResourceSaver 生成 UI 试点资产：可在编辑器里预览的 Theme，以及场景版当前单位面板。
/// 运行时仍然使用 <see cref="GodotTacticsTheme.Create"/>；这里的 Theme 资源只是它的落盘副本。
/// </summary>
public static class TacticsUiAssetFactory
{
    public const string ThemePath = "res://content/ui/TacticsTheme.tres";
    public const string ActiveUnitPanelScenePath = "res://content/ui/BattleActiveUnitPanel.tscn";
    private static readonly Vector2 ActiveUnitPanelSize = new(414, 174);

    public static void Build()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://content/ui"));
        SaveTheme(ThemePath);
        // 场景生成后归人工在编辑器里调整，所以只在文件不存在时写入，避免覆盖手动修改。
        if (!File.Exists(ProjectSettings.GlobalizePath(ActiveUnitPanelScenePath)))
            SaveActiveUnitPanelScene(ActiveUnitPanelScenePath, ThemePath);
    }

    public static void SaveTheme(string path)
    {
        using Theme theme = GodotTacticsTheme.Create();
        // 子资源默认使用随机 ID；固定为“类型_条目”才能保证重复生成时文件逐字节相同。
        foreach (string type in theme.GetStyleboxTypeList())
        {
            foreach (string name in theme.GetStyleboxList(type))
                theme.GetStylebox(name, type).ResourceSceneUniqueId = $"{type}_{name}";
        }
        DeterministicResourceSaver.Save(theme, path, Uid(path));
    }

    public static void SaveActiveUnitPanelScene(string path, string themePath)
    {
        Theme theme = ResourceLoader.Load<Theme>(themePath)
            ?? throw new InvalidOperationException($"UI theme resource is missing: {themePath}.");
        var root = new GodotBattleActiveUnitPanel
        {
            Name = "BattleActiveUnitPanel",
            Size = ActiveUnitPanelSize,
            Theme = theme,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        try
        {
            root.BuildLayout();
            ApplyEditorPreview(root);
            // PackedScene 只保存 Owner 指向根节点的后代。
            AssignOwner(root, root);
            var packed = new PackedScene();
            Error pack = packed.Pack(root);
            if (pack != Error.Ok) throw new InvalidOperationException($"Cannot pack active unit panel scene: {pack}.");
            DeterministicResourceSaver.Save(packed, path, Uid(path));
        }
        finally
        {
            root.Free();
        }
    }

    // 占位内容只为了在编辑器里能看到排版效果；运行时 Bind 会全部覆盖。
    private static void ApplyEditorPreview(Node root)
    {
        SetText(root, "ActiveUnitName", "Unit Name");
        SetText(root, "HealthValue", "16/20");
        SetText(root, "ManaValue", "7/18");
        SetText(root, "SpecialValue", "Corruption  5/10");
        SetBar(root, "HealthBar", 16, 20);
        SetBar(root, "ManaBar", 7, 18);
        SetBar(root, "SpecialBar", 5, 10);
    }

    private static void SetText(Node root, string name, string text) =>
        ((Label)root.FindChild(name, true, false)).Text = text;

    private static void SetBar(Node root, string name, int value, int maximum)
    {
        var bar = (ProgressBar)root.FindChild(name, true, false);
        bar.MaxValue = maximum;
        bar.Value = value;
        if (bar.GetThemeStylebox("fill") is { } fill) fill.ResourceSceneUniqueId = name + "_fill";
    }

    private static void AssignOwner(Node node, Node owner)
    {
        foreach (Node child in node.GetChildren())
        {
            child.Owner = owner;
            AssignOwner(child, owner);
        }
    }

    private static long Uid(string path)
    {
        string absolute = ProjectSettings.GlobalizePath(path);
        if (File.Exists(absolute))
        {
            string header = File.ReadLines(absolute).FirstOrDefault() ?? string.Empty;
            int marker = header.IndexOf("uid=\"", StringComparison.Ordinal);
            int start = marker + 5;
            int end = marker >= 0 ? header.IndexOf('"', start) : -1;
            if (end > start)
            {
                long persisted = ResourceUid.TextToId(header[start..end]);
                if (persisted != ResourceUid.InvalidId)
                {
                    if (!ResourceUid.HasId(persisted)) ResourceUid.AddId(persisted, path);
                    return persisted;
                }
            }
        }
        long uid = ResourceUid.CreateIdForPath(path);
        if (!ResourceUid.HasId(uid)) ResourceUid.AddId(uid, path);
        return uid;
    }
}
#endif
