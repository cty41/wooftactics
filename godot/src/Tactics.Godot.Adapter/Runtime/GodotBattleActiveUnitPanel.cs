using Godot;
using Tactics.Application.Battle;

namespace Tactics.Godot.Adapter.Runtime;

public enum GodotBattleSpecialResourceStage
{
    Low,
    Elevated,
    Critical
}

public sealed record GodotBattleSpecialResourceView(
    string Label,
    int Current,
    int Maximum,
    GodotBattleSpecialResourceStage Stage,
    bool Pulsing);

/// <summary>Persistent top-left status card for the current battle actor.</summary>
public partial class GodotBattleActiveUnitPanel : Control
{
    public const int CorruptionMaximum = 10;
    public static readonly Color LowCorruptionColor = new("65576f");
    public static readonly Color ElevatedCorruptionColor = new("8444a3");
    public static readonly Color CriticalCorruptionColor = new("c33772");

    /// <summary>Inner layout area: the 430x190 BattleUnitPanel card minus its 8 px inset on each side.</summary>
    internal static readonly Vector2 ContentSize = new(414, 174);
    internal const float PortraitSide = 96f;
    internal const float PortraitLeft = 12f;
    internal const float ColumnLeft = PortraitLeft + PortraitSide + 16f;
    internal const float BarWidth = 266f;
    private const float NameHeight = 32f;
    private const float NameGap = 4f;
    private const float RowHeight = 34f;
    private const float RowGap = 8f;

    private TextureRect? _portrait;
    private VBoxContainer? _rows;
    private Label? _name;
    private ProgressBar? _health;
    private ProgressBar? _mana;
    private ProgressBar? _special;
    private Label? _healthText;
    private Label? _manaText;
    private Label? _specialText;
    private Control? _specialRow;
    private bool _pulseSpecial;
    private double _pulseTime;

    public override void _Ready()
    {
        if (_portrait is not null) return;
        MouseFilter = MouseFilterEnum.Ignore;

        _portrait = new TextureRect
        {
            Name = "ActiveUnitPortrait",
            Size = new Vector2(PortraitSide, PortraitSide),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_portrait);

        _name = new Label
        {
            Name = "ActiveUnitName",
            Size = new Vector2(BarWidth, NameHeight),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _name.AddThemeFontSizeOverride("font_size", 22);
        AddChild(_name);

        var rows = new VBoxContainer
        {
            Name = "ActiveUnitResourceRows",
            Size = new Vector2(BarWidth, 126),
            MouseFilter = MouseFilterEnum.Ignore
        };
        rows.AddThemeConstantOverride("separation", (int)RowGap);
        AddChild(rows);
        _rows = rows;

        Control healthRow;
        (_health, _healthText, healthRow) = CreateResourceRow("Health", new Color("b95f66"));
        rows.AddChild(healthRow);
        Control manaRow;
        (_mana, _manaText, manaRow) = CreateResourceRow("Mana", new Color("527cc7"));
        rows.AddChild(manaRow);
        (_special, _specialText, _specialRow) = CreateResourceRow("Special", LowCorruptionColor);
        rows.AddChild(_specialRow);
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_specialRow is null || !_pulseSpecial)
        {
            if (_specialRow is not null) _specialRow.Modulate = Colors.White;
            return;
        }

        _pulseTime += delta;
        float alpha = .78f + .22f * (float)((Math.Sin(_pulseTime * 5d) + 1d) * .5d);
        _specialRow.Modulate = new Color(1.18f, .82f, 1.12f, alpha);
    }

    public void Bind(UnitDefinitionResource definition, BattleUiUnitSnapshot unit)
    {
        EnsureReady();
        _portrait!.Texture = definition.DownRightTexture is { } sprite ? GodotInitiativeStrip.TrimToContent(sprite) : null;
        _name!.Text = definition.DisplayName;
        BindBar(_health!, _healthText!, unit.CurrentHealth, unit.MaxHealth, string.Empty, new Color("b95f66"));
        BindBar(_mana!, _manaText!, unit.CurrentMana, unit.MaxMana, string.Empty, new Color("527cc7"));

        GodotBattleSpecialResourceView? special = ProjectSpecialResource(unit);
        _specialRow!.Visible = special is not null;
        _pulseSpecial = special?.Pulsing == true;
        _pulseTime = 0d;
        if (special is not null)
        {
            string suffix = special.Pulsing ? "  POSSESSED" : string.Empty;
            BindBar(_special!, _specialText!, special.Current, special.Maximum,
                $"{special.Label}  ", ColorFor(special.Stage), suffix);
        }
        else
        {
            _specialRow.Modulate = Colors.White;
        }

        LayoutBlock(special is not null ? 3 : 2);
        Visible = true;
    }

    /// <summary>Vertically centres the name-plus-bars column and the portrait on one shared midline.</summary>
    private void LayoutBlock(int rowCount)
    {
        float rowsHeight = rowCount * RowHeight + (rowCount - 1) * RowGap;
        float blockHeight = NameHeight + NameGap + rowsHeight;
        float top = (ContentSize.Y - blockHeight) * .5f;
        _name!.Position = new Vector2(ColumnLeft, top);
        _rows!.Position = new Vector2(ColumnLeft, top + NameHeight + NameGap);
        _rows.Size = new Vector2(BarWidth, rowsHeight);
        _portrait!.Position = new Vector2(PortraitLeft, top + (blockHeight - PortraitSide) * .5f);
    }

    public void Clear()
    {
        _pulseSpecial = false;
        if (_specialRow is not null) _specialRow.Modulate = Colors.White;
        Visible = false;
    }

    public static GodotBattleSpecialResourceView? ProjectSpecialResource(BattleUiUnitSnapshot unit)
    {
        if (unit.Corruption is not int corruption) return null;
        int current = Math.Clamp(corruption, 0, CorruptionMaximum);
        return new GodotBattleSpecialResourceView("Corruption", current, CorruptionMaximum,
            StageFor(current), unit.IsPossessed);
    }

    public static GodotBattleSpecialResourceStage StageFor(int corruption) => corruption switch
    {
        <= 4 => GodotBattleSpecialResourceStage.Low,
        <= 8 => GodotBattleSpecialResourceStage.Elevated,
        _ => GodotBattleSpecialResourceStage.Critical
    };

    public static Color ColorFor(GodotBattleSpecialResourceStage stage) => stage switch
    {
        GodotBattleSpecialResourceStage.Low => LowCorruptionColor,
        GodotBattleSpecialResourceStage.Elevated => ElevatedCorruptionColor,
        GodotBattleSpecialResourceStage.Critical => CriticalCorruptionColor,
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    private void EnsureReady()
    {
        if (_portrait is null) _Ready();
    }

    private static (ProgressBar Bar, Label Text, Control Row) CreateResourceRow(string name, Color fill)
    {
        var row = new Control
        {
            Name = name + "ResourceRow",
            CustomMinimumSize = new Vector2(BarWidth, RowHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        var bar = new ProgressBar
        {
            Name = name + "Bar",
            Position = Vector2.Zero,
            Size = new Vector2(BarWidth, RowHeight),
            ShowPercentage = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        ApplyBarStyle(bar, fill);
        row.AddChild(bar);
        var text = new Label
        {
            Name = name + "Value",
            Position = Vector2.Zero,
            Size = new Vector2(BarWidth, RowHeight),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        text.AddThemeFontSizeOverride("font_size", 18);
        row.AddChild(text);
        return (bar, text, row);
    }

    private static void BindBar(ProgressBar bar, Label text, int current, int maximum, string prefix, Color fill,
        string suffix = "")
    {
        int safeMaximum = Math.Max(1, maximum);
        int safeCurrent = Math.Clamp(current, 0, safeMaximum);
        bar.MaxValue = safeMaximum;
        bar.Value = safeCurrent;
        ApplyBarStyle(bar, fill);
        text.Text = $"{prefix}{safeCurrent}/{safeMaximum}{suffix}";
    }

    private static void ApplyBarStyle(ProgressBar bar, Color fill)
    {
        bar.AddThemeStyleboxOverride("background", BarBox(new Color("20252a"), new Color("6c7176")));
        bar.AddThemeStyleboxOverride("fill", BarBox(fill, fill.Lightened(.18f)));
    }

    private static StyleBoxFlat BarBox(Color background, Color border) => new()
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
}
