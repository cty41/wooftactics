using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Tests;

/// <summary>
/// 遍历一个界面的全部后代控件，按 UI 规则返回违规列表（空列表表示通过）。
/// 规则：字号和间距只取 Theme 档位；布局交给容器或全矩形锚点；文字对比度不低于 4.5:1。
/// 只检查“不违规”，不判断好不好看。
/// 被检查的界面必须已经挂进场景树：不在树里时 Theme 不会向下传递，字号和颜色会读到引擎默认值。
/// </summary>
public static class UiDesignRuleChecker
{
    public const float MinimumTextContrast = 4.5f;

    /// <param name="root">被检查界面的根节点；根节点自身的位置由上层决定，不参与布局检查。</param>
    /// <param name="background">文字所在的底色，用于计算对比度。</param>
    public static IReadOnlyList<string> Check(Control root, Color background)
    {
        var violations = new List<string>();
        foreach (Control control in Descendants(root))
        {
            string path = root.GetPathTo(control);
            CheckFontSize(control, path, violations);
            CheckSpacing(control, path, violations);
            CheckLayout(control, path, violations);
            if (control is Label label)
            {
                float contrast = ContrastRatio(label.GetThemeColor("font_color"), background);
                if (contrast < MinimumTextContrast)
                    violations.Add($"{path}: 文字对比度 {contrast:0.00}:1 低于 {MinimumTextContrast}:1");
            }
        }
        return violations;
    }

    /// <summary>WCAG 2.x 对比度；忽略透明度。</summary>
    public static float ContrastRatio(Color first, Color second)
    {
        float a = RelativeLuminance(first), b = RelativeLuminance(second);
        return (Math.Max(a, b) + .05f) / (Math.Min(a, b) + .05f);
    }

    private static void CheckFontSize(Control control, string path, List<string> violations)
    {
        if (control is not (Label or Button)) return;
        int size = control.GetThemeFontSize("font_size");
        if (!GodotTacticsTheme.FontSizeScale.Contains(size))
            violations.Add($"{path}: 字号 {size} 不在档位 {string.Join('/', GodotTacticsTheme.FontSizeScale)} 内");
    }

    private static void CheckSpacing(Control control, string path, List<string> violations)
    {
        string[] constants = control switch
        {
            BoxContainer => ["separation"],
            GridContainer or FlowContainer => ["h_separation", "v_separation"],
            MarginContainer => ["margin_left", "margin_top", "margin_right", "margin_bottom"],
            _ => []
        };
        foreach (string constant in constants)
        {
            int value = control.GetThemeConstant(constant);
            if (value != 0 && !GodotTacticsTheme.SpacingScale.Contains(value))
                violations.Add($"{path}: {constant}={value} 不在档位 {string.Join('/', GodotTacticsTheme.SpacingScale)} 内");
        }
    }

    private static void CheckLayout(Control control, string path, List<string> violations)
    {
        if (control.GetParent() is Container) return;
        bool fullRect = control.AnchorLeft == 0f && control.AnchorTop == 0f &&
            control.AnchorRight == 1f && control.AnchorBottom == 1f &&
            control.OffsetLeft == 0f && control.OffsetTop == 0f &&
            control.OffsetRight == 0f && control.OffsetBottom == 0f;
        if (!fullRect) violations.Add($"{path}: 使用了绝对坐标（父节点不是容器，也不是全矩形锚点）");
    }

    private static float RelativeLuminance(Color color)
    {
        static float Channel(float value) =>
            value <= .04045f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);
        return .2126f * Channel(color.R) + .7152f * Channel(color.G) + .0722f * Channel(color.B);
    }

    private static IEnumerable<Control> Descendants(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Control control) yield return control;
            foreach (Control nested in Descendants(child)) yield return nested;
        }
    }
}
