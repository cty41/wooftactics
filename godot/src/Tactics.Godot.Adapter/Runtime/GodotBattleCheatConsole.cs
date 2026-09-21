using Godot;
using Tactics.Application.Battle;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Non-modal diagnostic overlay matching the Unity cheat-console input boundary.</summary>
public partial class GodotBattleCheatConsole : PanelContainer
{
    private readonly RichTextLabel _log = new();
    private readonly OptionButton _filter = new();
    private readonly Label _copyStatus = new();
    private readonly ITextClipboard _clipboard;
    private IReadOnlyList<BattleUiLogEntry> _entries = Array.Empty<BattleUiLogEntry>();
    public event Action? ClearRequested;

    public GodotBattleCheatConsole() : this(new GodotTextClipboard()) { }

    internal GodotBattleCheatConsole(ITextClipboard clipboard) =>
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

    public override void _Ready()
    {
        Position = Vector2.Zero; Size = new Vector2(1600, 225); ZIndex = 2000;
        MouseFilter = MouseFilterEnum.Stop; Visible = false;
        var panel = new VBoxContainer(); AddChild(panel);
        _copyStatus.Name = "CopyStatus";
        var header = new HBoxContainer(); panel.AddChild(header);
        foreach (string name in new[] { "All", "Gameplay", "AI", "Rejected" }) _filter.AddItem(name);
        _filter.ItemSelected += _ => Refresh(); header.AddChild(_filter);
        var clear = new Button { Text = "Clear" }; clear.Pressed += () => ClearRequested?.Invoke(); header.AddChild(clear);
        var copyVisible = new Button { Text = "Copy Visible" }; copyVisible.Pressed += CopyVisible; header.AddChild(copyVisible);
        var copyAll = new Button { Text = "Copy All" }; copyAll.Pressed += CopyAll; header.AddChild(copyAll);
        header.AddChild(_copyStatus);
        header.AddChild(new Label { Text = "CheatConsole (` toggles; drag/right-click/Ctrl+C; gameplay remains live)", MouseFilter = MouseFilterEnum.Ignore });
        _log.CustomMinimumSize = new Vector2(1560, 165); _log.ScrollActive = true; _log.MouseFilter = MouseFilterEnum.Stop;
        _log.SelectionEnabled = true;
        panel.AddChild(_log);
    }

    public void SetEntries(IReadOnlyList<BattleUiLogEntry> entries) { _entries = entries; Refresh(); }

    public void SetOpen(bool open)
    {
        if (open) _copyStatus.Text = string.Empty;
        Visible = open;
    }

    private void Refresh()
    {
        if (!IsInsideTree()) return;
        _log.Text = RenderVisible();
    }

    internal string RenderVisible() => Render(FilterEntries().TakeLast(100));
    internal string RenderAll() => Render(_entries);

    private IEnumerable<BattleUiLogEntry> FilterEntries() => _filter.Selected switch
        {
            1 => _entries.Where(item => item.Category == BattleUiLogCategory.Gameplay),
            2 => _entries.Where(item => item.Category == BattleUiLogCategory.Ai),
            3 => _entries.Where(item => item.Category == BattleUiLogCategory.Rejected),
            _ => _entries
        };

    private static string Render(IEnumerable<BattleUiLogEntry> values) =>
        string.Join('\n', values.Select(item => $"[{item.Category}] {item.Message} [{item.EventType}]"));

    private void CopyVisible() => Copy(RenderVisible());
    private void CopyAll() => Copy(RenderAll());

    private void Copy(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _copyStatus.Text = "Nothing to copy";
            return;
        }
        try
        {
            _clipboard.SetText(text);
            _copyStatus.Text = $"Copied {text.Count(value => value == '\n') + 1} lines";
        }
        catch (Exception exception)
        {
            _copyStatus.Text = $"Copy failed: {exception.GetType().Name}";
        }
    }
}
