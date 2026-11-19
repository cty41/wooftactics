using Godot;
using Tactics.Application.Presentation;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Presentation-only floating combat text sourced from committed event facts.</summary>
public partial class GodotDamageNumberLayer : Node2D
{
    private readonly Queue<Label> _pool = new();
    private readonly HashSet<Label> _active = new();
    private readonly List<Tween> _tweens = new();
    private IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> _actors =
        new Dictionary<UnitInstanceId, GodotUnitActor>();
    private float _speed = 1f;
    private bool _paused;
    private readonly List<BattlePresentationNumber> _history = new();
    public int ActiveCount => _active.Count;
    public IReadOnlyList<BattlePresentationNumber> History => _history;
    public IReadOnlyList<Label> ActiveLabels => _active.ToArray();
    public float PlaybackSpeed => _speed;
    public bool IsPaused => _paused;

    public void Configure(IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors) => _actors = actors;

    public void Spawn(BattlePresentationNumber number)
    {
        _history.Add(number);
        if (!_actors.TryGetValue(number.TargetId, out GodotUnitActor? actor) || !GodotObject.IsInstanceValid(actor)) return;
        Label label = _pool.Count > 0 ? _pool.Dequeue() : CreateLabel();
        if (label.GetParent() is null) AddChild(label);
        label.Text = number.Text;
        label.Position = actor.HeadAnchorInParent() + new Vector2(-24, -8);
        Color color = ColorFor(number.Kind);
        label.Modulate = new Color(color.R, color.G, color.B, 0f);
        label.Scale = Vector2.One * (number.Kind == BattlePresentationNumberKind.Critical ? .5f : .65f);
        label.Visible = true;
        _active.Add(label);
        float lifetime = number.Kind == BattlePresentationNumberKind.Miss ? 1f : 1.5f;
        Vector2 origin = label.Position;
        Tween tween = CreateTween();
        tween.SetSpeedScale(_speed);
        if (_paused) tween.Pause();
        _tweens.Add(tween);
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", origin + new Vector2(0, -60), lifetime);
        tween.TweenProperty(label, "modulate:a", 1f, .2f);
        tween.TweenProperty(label, "scale", Vector2.One * (number.Kind == BattlePresentationNumberKind.Critical ? 1.5f : 1.2f), .12f);
        tween.TweenProperty(label, "scale", Vector2.One, .18f).SetDelay(.12f);
        tween.TweenProperty(label, "modulate:a", 0f, .3f).SetDelay(lifetime - .3f);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => { _tweens.Remove(tween); Recycle(label); }));
    }

    public void Clear()
    {
        foreach(Tween tween in _tweens.Where(GodotObject.IsInstanceValid))tween.Kill();
        _tweens.Clear();
        foreach (Label label in _active.ToArray()) Recycle(label);
    }

    public override void _ExitTree() => Clear();

    public void SetSpeed(float speed)
    {
        if (!GodotBattlePresentationPlayer.IsSupportedSpeed(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
        _speed = speed;
        foreach (Tween tween in _tweens.Where(GodotObject.IsInstanceValid)) tween.SetSpeedScale(speed);
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        foreach (Tween tween in _tweens.Where(GodotObject.IsInstanceValid))
        { if (paused) tween.Pause(); else tween.Play(); }
    }

    private static Label CreateLabel()
    {
        var label = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(96, 40),
            HorizontalAlignment = HorizontalAlignment.Center,
            ZIndex = 1200
        };
        label.AddThemeFontSizeOverride("font_size", GodotTacticsTheme.FontHeading);
        return label;
    }

    private void Recycle(Label label)
    {
        if (!_active.Remove(label)) return;
        label.Visible = false;
        label.Modulate = Colors.White;
        _pool.Enqueue(label);
    }

    private static Color ColorFor(BattlePresentationNumberKind kind) => kind switch
    {
        BattlePresentationNumberKind.Critical => new Color(1f, .86f, .2f),
        BattlePresentationNumberKind.Heal => new Color(.31f, 1f, .47f),
        BattlePresentationNumberKind.Mana => new Color(.35f, .65f, 1f),
        BattlePresentationNumberKind.Miss => new Color(.59f, .59f, .59f),
        _ => GodotTacticsTheme.TextPrimary
    };
}
