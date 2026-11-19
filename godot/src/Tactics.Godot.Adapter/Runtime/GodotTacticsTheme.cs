using Godot;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Builds the native Godot equivalent of the project's Unity UI Toolkit visual language.</summary>
public static class GodotTacticsTheme
{
    public const string PrimaryButton = "TacticsPrimaryButton";
    public const string SecondaryButton = "TacticsSecondaryButton";
    public const string CompactButton = "TacticsCompactButton";
    public const string ActionButton = "TacticsActionButton";
    public const string Panel = "TacticsPanel";
    public const string Card = "TacticsCard";
    public const string HeadingLabel = "TacticsHeadingLabel";
    public const string SmallLabel = "TacticsSmallLabel";
    public const string ResourceBar = "TacticsResourceBar";

    // UI 规则档位：新界面的间距与字号只能取这些值（见 .agents/docs/ui-skill-survey.md）。
    public const int SpaceXs = 4;
    public const int SpaceSm = 8;
    public const int SpaceMd = 16;
    public const int SpaceLg = 24;
    public const int SpaceXl = 32;
    public const int FontSmall = 14;
    public const int FontBody = 18;
    public const int FontHeading = 22;
    public const int FontTitle = 28;
    public static readonly int[] SpacingScale = [SpaceXs, SpaceSm, SpaceMd, SpaceLg, SpaceXl];
    public static readonly int[] FontSizeScale = [FontSmall, FontBody, FontHeading, FontTitle];

    /// <summary>把任意字号收到档位上。距离相同的时候取更小的一档，避免正文跳成标题。</summary>
    public static int SnapFontSize(int size)
    {
        if (FontSizeScale.Contains(size)) return size;
        int nearest = FontSmall;
        int best = int.MaxValue;
        foreach (int candidate in FontSizeScale)
        {
            int distance = Math.Abs(candidate - size);
            if (distance < best || distance == best && candidate < nearest)
            {
                best = distance;
                nearest = candidate;
            }
        }
        return nearest;
    }

    public static readonly Color Background = new("090d10");
    public static readonly Color PanelBackground = new(0.02f, 0.02f, 0.02f, 0.94f);
    public static readonly Color PanelSoft = new(0.08f, 0.08f, 0.08f, 0.90f);
    public static readonly Color CardBackground = new(0.15f, 0.15f, 0.15f, 0.94f);
    public static readonly Color Accent = new("f9874b");
    public static readonly Color AccentHover = new("ffa064");
    public static readonly Color TextPrimary = new("f4f4f2");
    public static readonly Color TextSecondary = new("b6b8b9");
    public static readonly Color DisabledText = new("737679");
    public static readonly Color DisabledBackground = new(0.10f, 0.10f, 0.10f, 0.84f);

    public static Theme Create()
    {
        var theme = new Theme();
        theme.SetColor("font_color", "Label", TextPrimary);
        theme.SetColor("font_shadow_color", "Label", new Color(0, 0, 0, .65f));
        theme.SetConstant("shadow_offset_x", "Label", 1);
        theme.SetConstant("shadow_offset_y", "Label", 1);
        theme.SetFontSize("font_size", "Label", FontBody);
        theme.SetTypeVariation(HeadingLabel, "Label");
        theme.SetTypeVariation(SmallLabel, "Label");
        theme.SetFontSize("font_size", HeadingLabel, FontHeading);
        theme.SetFontSize("font_size", SmallLabel, FontSmall);

        // 资源条用变体而不是 ProgressBar 基础类型，避免改变其他界面里已有进度条的外观。
        theme.SetTypeVariation(ResourceBar, "ProgressBar");
        theme.SetStylebox("background", ResourceBar, ResourceBarBox(new Color("20252a"), new Color("6c7176")));
        theme.SetStylebox("fill", ResourceBar, ResourceBarFill(new Color("6c7176")));

        theme.SetTypeVariation(PrimaryButton, "Button");
        theme.SetTypeVariation(SecondaryButton, "Button");
        theme.SetTypeVariation(CompactButton, "Button");
        theme.SetTypeVariation(ActionButton, "Button");
        ConfigureButton(theme, "Button", PanelSoft, Accent, 1, 8, 18);
        ConfigureButton(theme, PrimaryButton, PanelSoft, Accent, 2, 8, 20);
        ConfigureButton(theme, SecondaryButton, PanelSoft, Accent, 1, 8, 18);
        ConfigureButton(theme, CompactButton, PanelSoft, Accent, 1, 6, 15);
        ConfigureButton(theme, ActionButton, CardBackground, Accent, 1, 6, 15);

        theme.SetTypeVariation(Panel, "PanelContainer");
        theme.SetTypeVariation(Card, "PanelContainer");
        theme.SetStylebox("panel", "PanelContainer", Box(PanelBackground, Accent, 2, 12, 20));
        theme.SetStylebox("panel", Panel, Box(PanelBackground, Accent, 2, 12, 20));
        theme.SetStylebox("panel", Card, Box(CardBackground, new Color("5a5a5a"), 1, 8, 14));
        return theme;
    }

    private static void ConfigureButton(Theme theme, string type, Color normalColor, Color borderColor,
        int borderWidth, int radius, int fontSize, Color? fontColor = null)
    {
        theme.SetColor("font_color", type, fontColor ?? TextPrimary);
        theme.SetColor("font_hover_color", type, TextPrimary);
        theme.SetColor("font_pressed_color", type, TextPrimary);
        theme.SetColor("font_focus_color", type, TextPrimary);
        theme.SetColor("font_disabled_color", type, DisabledText);
        theme.SetFontSize("font_size", type, fontSize);
        theme.SetStylebox("normal", type, Box(normalColor, borderColor, borderWidth, radius, 12));
        theme.SetStylebox("hover", type, Box(new Color(0.12f, 0.12f, 0.12f, .96f), AccentHover, 2, radius, 12));
        theme.SetStylebox("pressed", type, Box(new Color(0.20f, 0.10f, 0.06f, .98f), Accent, 2, radius, 12));
        theme.SetStylebox("focus", type, Box(new Color(0, 0, 0, 0), AccentHover, 2, radius, 10));
        theme.SetStylebox("disabled", type, Box(DisabledBackground, new Color("45484a"), 1, radius, 12));
    }

    /// <summary>按状态颜色生成资源条填充样式；边框取填充色的提亮版本。</summary>
    public static StyleBoxFlat ResourceBarFill(Color fill) => ResourceBarBox(fill, fill.Lightened(.18f));

    private static StyleBoxFlat ResourceBarBox(Color background, Color border) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = 2,
        BorderWidthTop = 2,
        BorderWidthRight = 2,
        BorderWidthBottom = 2,
        CornerRadiusTopLeft = 7,
        CornerRadiusTopRight = 7,
        CornerRadiusBottomLeft = 7,
        CornerRadiusBottomRight = 7,
        AntiAliasing = true
    };

    private static StyleBoxFlat Box(Color background, Color border, int borderWidth, int radius, float margin) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = margin,
        ContentMarginTop = margin * .55f,
        ContentMarginRight = margin,
        ContentMarginBottom = margin * .55f,
        AntiAliasing = true,
    };
}
