using Godot;
using Tactics.Application.Battle;
using Tactics.Application.Units;
using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Right-aligned presentation of the committed Core current-and-remaining initiative queue.</summary>
/// <remarks>
/// Each changed queue owns one tracked transition. Input remains locked while its fade, ellipsis pulse,
/// and deferred portrait movement run, and a replacement refresh transfers that lock without an unlock gap.
/// </remarks>
public partial class GodotInitiativeStrip : HBoxContainer
{
    private const int MaximumPortraits = 9;
    /// <summary>Current-round plus next-round portraits that fit the 565 px strip beside the round label.</summary>
    internal const int TotalPortraitBudget = 11;
    internal const float SlotSize = 34f;
    internal const float PortraitSize = 28f;
    internal const string NextRoundSeparatorName = "NextRoundSeparator";
    /// <summary>Portraits are hidden until their in-slot sizing is reworked; the side-coloured dots carry the order.</summary>
    internal const bool ShowPortraits = false;
    private static readonly Dictionary<(ulong Texture, Rect2 Region), Texture2D> CroppedPortraits = new();
    private static readonly Dictionary<ulong, Texture2D> TrimmedSprites = new();
    internal const double TransitionDurationSeconds = .22;
    private readonly Label _roundLabel = new();
    private readonly HBoxContainer _slots = new();
    private readonly List<UnitInstanceId> _visibleIds = new();
    private readonly List<InitiativePortraitSlot> _pendingMovementSlots = new();
    private Dictionary<UnitInstanceId, Vector2> _previousPositions = new();
    private string _hiddenFingerprint = string.Empty;
    private int _playerNumber;
    private Tween? _transition;
    private ulong _transitionVersion;
    private bool _transitionLocked;
    private int _transitionMovementCount;

    internal static int VisiblePortraitCount(int queueCount) => Math.Min(Math.Max(0, queueCount), MaximumPortraits);
    internal static bool ShowsEllipsis(int queueCount) => queueCount > MaximumPortraits;
    /// <summary>Next-round portraits get whatever budget the current round leaves; none while it overflows.</summary>
    internal static int VisibleNextRoundCount(int queueCount, int nextRoundCount) => ShowsEllipsis(queueCount)
        ? 0
        : Math.Min(Math.Max(0, nextRoundCount), TotalPortraitBudget - VisiblePortraitCount(queueCount));
    internal bool IsTransitionLocked => _transitionLocked;
    internal int TransitionMovementCount => _transitionMovementCount;

    public event Action<UnitInstanceId?>? HoverChanged;
    public event Action<bool>? TransitionLockChanged;

    public GodotInitiativeStrip()
    {
        Name = "InitiativeStrip";
        MouseFilter = MouseFilterEnum.Pass;
        Alignment = AlignmentMode.End;
        AddThemeConstantOverride("separation", 10);
        _roundLabel.VerticalAlignment = VerticalAlignment.Center;
        _roundLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _roundLabel.CustomMinimumSize = new Vector2(74, 46);
        _roundLabel.AddThemeColorOverride("font_color", new Color("e8dcc1"));
        _roundLabel.AddThemeFontSizeOverride("font_size", 16);
        _slots.Alignment = AlignmentMode.End;
        _slots.AddThemeConstantOverride("separation", 6);
        _slots.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_roundLabel);
        AddChild(_slots);
    }

    public void Configure(int playerNumber) => _playerNumber = playerNumber;

    public void Apply(
        int round,
        IReadOnlyList<BattleUiInitiativeEntry>? queue,
        IReadOnlyDictionary<ContentId, UnitDefinitionResource> definitions,
        IReadOnlyList<BattleUiInitiativeEntry>? nextRound = null)
    {
        _roundLabel.Text = $"第 {round} 回合";
        BattleUiInitiativeEntry[] ordered = queue?.ToArray() ?? Array.Empty<BattleUiInitiativeEntry>();
        BattleUiInitiativeEntry[] visible = ordered.Take(VisiblePortraitCount(ordered.Length)).ToArray();
        BattleUiInitiativeEntry[] upcoming = (nextRound ?? Array.Empty<BattleUiInitiativeEntry>())
            .Take(VisibleNextRoundCount(ordered.Length, nextRound?.Count ?? 0)).ToArray();
        string hidden = string.Join('|', ordered.Skip(MaximumPortraits).Select(item => item.UnitId.Value)) +
            "#" + string.Join('|', upcoming.Select(item => item.UnitId.Value));
        bool sequenceChanged = !_visibleIds.SequenceEqual(visible.Select(item => item.UnitId));
        bool hiddenChanged = hidden != _hiddenFingerprint;
        if (!sequenceChanged && !hiddenChanged) return;

        CancelTrackedTransition(releaseLock: false);
        SetTransitionLocked(true);
        _previousPositions = _slots.GetChildren().OfType<InitiativePortraitSlot>().Where(slot => !slot.IsNextRound)
            .ToDictionary(slot => slot.UnitId, slot => slot.Position);
        HoverChanged?.Invoke(null);
        _visibleIds.Clear();
        _visibleIds.AddRange(visible.Select(item => item.UnitId));
        _hiddenFingerprint = hidden;
        _pendingMovementSlots.Clear();
        foreach (Node child in _slots.GetChildren())
        {
            _slots.RemoveChild(child);
            child.QueueFree();
        }

        foreach (BattleUiInitiativeEntry item in visible)
        {
            definitions.TryGetValue(item.DefinitionId, out UnitDefinitionResource? definition);
            var slot = new InitiativePortraitSlot(item, _playerNumber, definition);
            slot.HoverChanged += hovered => HoverChanged?.Invoke(hovered ? item.UnitId : null);
            _slots.AddChild(slot);
            _pendingMovementSlots.Add(slot);
        }

        Control? ellipsis = null;
        if (ShowsEllipsis(ordered.Length))
        {
            ellipsis = CreateEllipsis();
            _slots.AddChild(ellipsis);
        }
        if (upcoming.Length > 0)
        {
            _slots.AddChild(CreateNextRoundSeparator());
            foreach (BattleUiInitiativeEntry item in upcoming)
            {
                definitions.TryGetValue(item.DefinitionId, out UnitDefinitionResource? definition);
                var slot = new InitiativePortraitSlot(item, _playerNumber, definition, isNextRound: true);
                slot.HoverChanged += hovered => HoverChanged?.Invoke(hovered ? item.UnitId : null);
                _slots.AddChild(slot);
            }
        }
        ulong transitionVersion = BeginTransition(ellipsis, hiddenChanged);
        Callable.From(() => AnimatePendingSlotMovement(transitionVersion)).CallDeferred();
    }

    public override void _ExitTree()
    {
        _pendingMovementSlots.Clear();
        CancelTrackedTransition(releaseLock: true);
    }

    internal bool TryGetPortraitTransition(
        UnitInstanceId unitId,
        out Vector2 source,
        out Vector2 position,
        out Vector2 target)
    {
        InitiativePortraitSlot? slot = _slots.GetChildren().OfType<InitiativePortraitSlot>()
            .FirstOrDefault(candidate => candidate.UnitId == unitId && !candidate.IsNextRound);
        source = slot?.TransitionSource ?? default;
        position = slot?.Position ?? default;
        target = slot?.TransitionTarget ?? default;
        return slot?.TransitionSource is not null && slot.TransitionTarget is not null;
    }

    private void AnimatePendingSlotMovement(ulong transitionVersion)
    {
        Tween? transition = _transition;
        if (transition is null || transitionVersion != _transitionVersion || !IsInsideTree()) return;

        foreach (InitiativePortraitSlot slot in _pendingMovementSlots)
        {
            Vector2 target = slot.Position;
            Vector2 source = _previousPositions.TryGetValue(slot.UnitId, out Vector2 previous)
                ? previous
                : target + new Vector2(48f, 0f);
            slot.TransitionSource = source;
            slot.TransitionTarget = target;
            slot.Position = source;
            transition.TweenProperty(slot, "position", target, TransitionDurationSeconds);
            _transitionMovementCount++;
        }
        _pendingMovementSlots.Clear();
        transition.Play();
    }

    private ulong BeginTransition(Control? ellipsis, bool pulseEllipsis)
    {
        ulong transitionVersion = ++_transitionVersion;
        Tween transition = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        transition.Pause();
        _transition = transition;
        _transitionMovementCount = 0;
        Modulate = new Color(1f, 1f, 1f, .55f);
        transition.TweenProperty(this, "modulate", Colors.White, TransitionDurationSeconds);
        if (ellipsis is not null && pulseEllipsis)
        {
            ellipsis.PivotOffset = ellipsis.Size * .5f;
            ellipsis.Scale = new Vector2(1.25f, 1.25f);
            transition.TweenProperty(ellipsis, "scale", Vector2.One, TransitionDurationSeconds);
        }
        transition.Finished += () => CompleteTransition(transition, transitionVersion);
        return transitionVersion;
    }

    private void CompleteTransition(Tween transition, ulong transitionVersion)
    {
        if (!ReferenceEquals(_transition, transition) || transitionVersion != _transitionVersion) return;
        _transition = null;
        _transitionMovementCount = 0;
        SetTransitionLocked(false);
    }

    private void CancelTrackedTransition(bool releaseLock)
    {
        Tween? transition = _transition;
        _transition = null;
        _transitionVersion++;
        _transitionMovementCount = 0;
        transition?.Kill();
        if (releaseLock) SetTransitionLocked(false);
    }

    private void SetTransitionLocked(bool locked)
    {
        if (_transitionLocked == locked) return;
        _transitionLocked = locked;
        TransitionLockChanged?.Invoke(locked);
    }

    /// <summary>
    /// The face crop as a standalone texture. An <c>AtlasTexture</c> would hand the circular-mask shader atlas-space
    /// UVs, so the mask would test the whole sprite sheet instead of the crop and the face would spill out uncut.
    /// </summary>
    internal static Texture2D? PortraitOf(UnitDefinitionResource? definition)
    {
        if (definition is null) return null;
        Texture2D? source = definition.PortraitTextureOverride ?? definition.DownRightTexture;
        if (source is null) return null;
        if (definition.PortraitTextureOverride is not null && !definition.HasPortraitRegion)
            return source;
        Rect2 region = definition.HasPortraitRegion
            ? definition.PortraitRegion
            : DefaultFaceRegion(source);
        return CropTexture(source, region);
    }

    /// <summary>The sprite cropped to its opaque pixels, so layout aligns the character rather than its transparent margin.</summary>
    internal static Texture2D TrimToContent(Texture2D source)
    {
        ulong id = source.GetInstanceId();
        if (TrimmedSprites.TryGetValue(id, out Texture2D? cached) && GodotObject.IsInstanceValid(cached)) return cached;
        Image? image = source.GetImage();
        if (image is null || image.IsEmpty()) return source;
        if (image.IsCompressed()) image.Decompress();
        Rect2I used = image.GetUsedRect();
        Texture2D trimmed = used.Size.X <= 0 || used.Size.Y <= 0 || used.Size == image.GetSize()
            ? source
            : CropTexture(source, new Rect2(used.Position, used.Size));
        TrimmedSprites[id] = trimmed;
        return trimmed;
    }

    internal static Texture2D CropTexture(Texture2D source, Rect2 region)
    {
        (ulong, Rect2) key = (source.GetInstanceId(), region);
        if (CroppedPortraits.TryGetValue(key, out Texture2D? cached) && GodotObject.IsInstanceValid(cached)) return cached;
        Image? image = source.GetImage();
        if (image is null || image.IsEmpty()) return source;
        if (image.IsCompressed()) image.Decompress();
        var rect = new Rect2I((Vector2I)region.Position, (Vector2I)region.Size)
            .Intersection(new Rect2I(Vector2I.Zero, image.GetSize()));
        if (rect.Size.X <= 0 || rect.Size.Y <= 0) return source;
        Texture2D cropped = ImageTexture.CreateFromImage(image.GetRegion(rect));
        CroppedPortraits[key] = cropped;
        return cropped;
    }

    /// <summary>Square head crop: upper part of the sprite, wide enough that ears and cheeks stay inside the circle.</summary>
    internal static Rect2 DefaultFaceRegion(Texture2D texture)
    {
        float width = texture.GetWidth();
        float height = texture.GetHeight();
        float side = MathF.Max(1f, MathF.Min(width, height) * .62f);
        return new Rect2((width - side) * .5f, MathF.Max(0f, height * .1f), side, side);
    }

    private static Control CreateNextRoundSeparator()
    {
        var label = new Label
        {
            Name = NextRoundSeparatorName,
            Text = "|",
            CustomMinimumSize = new Vector2(14, SlotSize),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TooltipText = "Next round",
            MouseFilter = MouseFilterEnum.Pass
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", new Color("8f8572"));
        return label;
    }

    private static Control CreateEllipsis()
    {
        var label = new Label
        {
            Text = "…",
            CustomMinimumSize = new Vector2(SlotSize, SlotSize),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 24);
        label.AddThemeColorOverride("font_color", new Color("c8b99c"));
        return label;
    }

    private sealed partial class InitiativePortraitSlot : Control
    {
        private readonly bool _friendly;
        private readonly bool _current;
        private bool _hovered;

        public event Action<bool>? HoverChanged;
        public UnitInstanceId UnitId { get; }
        public bool IsNextRound { get; }
        public Vector2? TransitionSource { get; set; }
        public Vector2? TransitionTarget { get; set; }

        public InitiativePortraitSlot(
            BattleUiInitiativeEntry item,
            int playerNumber,
            UnitDefinitionResource? definition,
            bool isNextRound = false)
        {
            UnitId = item.UnitId;
            IsNextRound = isNextRound;
            Name = isNextRound ? $"NextRound_{item.UnitId.Value}" : $"Slot_{item.UnitId.Value}";
            if (isNextRound) Modulate = new Color(1f, 1f, 1f, .62f);
            _friendly = item.PlayerNumber == playerNumber;
            _current = item.IsCurrent;
            CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            MouseDefaultCursorShape = CursorShape.PointingHand;
            MouseEntered += () => SetHovered(true);
            MouseExited += () => SetHovered(false);
            var portrait = new TextureRect
            {
                Name = "Portrait",
                Visible = ShowPortraits,
                Texture = PortraitOf(definition),
                Position = new Vector2((SlotSize - PortraitSize) * .5f, (SlotSize - PortraitSize) * .5f),
                Size = new Vector2(PortraitSize, PortraitSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
                Material = CircularMaskMaterial(definition)
            };
            AddChild(portrait);
        }

        public override void _Draw()
        {
            Vector2 center = Size * .5f;
            float radius = SlotSize * .5f - 1f;
            DrawCircle(center, radius, _friendly ? new Color("28643f") : new Color("7b2f35"));
            if (_current) DrawArc(center, radius - 1f, 0f, MathF.Tau, 48, new Color("f1c75b"), 3f, true);
            if (_hovered) DrawArc(center, radius, 0f, MathF.Tau, 48, Colors.White, 3f, true);
        }

        private void SetHovered(bool hovered)
        {
            if (_hovered == hovered) return;
            _hovered = hovered;
            QueueRedraw();
            HoverChanged?.Invoke(hovered);
        }

        private static ShaderMaterial CircularMaskMaterial(UnitDefinitionResource? definition)
        {
            var shader = new Shader
            {
                Code = """
                    shader_type canvas_item;
                    render_mode unshaded, blend_mix;
                    uniform int tint_mode = 0;
                    uniform vec4 body_tint : source_color = vec4(1.0);
                    uniform vec4 base_body_color : source_color = vec4(1.0);
                    void fragment() {
                        vec2 point = UV - vec2(0.5);
                        if (dot(point, point) > 0.25) { discard; }
                        vec4 source = texture(TEXTURE, UV);
                        if (tint_mode == 1) {
                            float source_distance = distance(source.rgb, base_body_color.rgb);
                            float mask = 1.0 - smoothstep(0.10, 0.28, source_distance);
                            float source_luminance = dot(source.rgb, vec3(0.299, 0.587, 0.114));
                            float base_luminance = max(dot(base_body_color.rgb, vec3(0.299, 0.587, 0.114)), 0.01);
                            vec3 recolored_body = body_tint.rgb * (source_luminance / base_luminance);
                            COLOR = vec4(mix(source.rgb, recolored_body, mask), source.a);
                        } else {
                            COLOR = source * body_tint;
                        }
                    }
                    """
            };
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("tint_mode",
                definition?.BodyTintModeValue == UnitBodyTintModes.GoatBodyMaskV1 ? 1 : 0);
            material.SetShaderParameter("body_tint", definition?.BodyTint ?? Colors.White);
            material.SetShaderParameter("base_body_color", definition?.BaseBodyColor ?? Colors.White);
            return material;
        }
    }
}
