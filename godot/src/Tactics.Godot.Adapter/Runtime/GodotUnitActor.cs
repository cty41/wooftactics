using Godot;
using Tactics.Application.Units;
using Tactics.Application.Battle;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Cardinal presentation directions supported by the shared Unit actor.
/// </summary>
public enum GodotUnitFacing
{
    South,
    North,
    East,
    West
}

public enum GodotUnitActionPose { Melee, Ranged, Cast, Hit }

/// <summary>
/// Shared presentation-only actor for generated Unit definitions.
/// </summary>
[GlobalClass]
public partial class GodotUnitActor : Node2D
{
    [Export] public Sprite2D? Shadow { get; set; }
    [Export] public Sprite2D? Body { get; set; }
    public GodotUnitStatusOverlay? StatusOverlay { get; private set; }

    private UnitDefinitionResource? _definition;
    private GodotUnitActionPose? _actionPose;
    private Node2D? _flightLayer;
    private double _flightTime;
    private double _flightPhase;
    private double _deathDescent;

    public GodotUnitFacing Facing { get; private set; } = GodotUnitFacing.South;
    public bool IsShowingDeath { get; private set; }
    public GodotUnitFacing PresentationFacing => Facing;
    public bool IsBodyTintEnabled { get; private set; } = true;
    public bool IsSpearHeld { get; private set; } = true;
    public bool UsesGoatBodyMaskTint =>
        _definition?.BodyTintModeValue == UnitBodyTintModes.GoatBodyMaskV1;
    public bool IsAirborne => _definition?.MovementKindValue == "air" && !IsShowingDeath;
    public string DefinitionId => _definition?.ContentIdValue ?? string.Empty;

    /// <summary>
    /// Applies presentation data from a generated definition without touching Core state.
    /// </summary>
    public void Configure(UnitDefinitionResource definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.ValidateVisualContract();
        EnsureNodes();
        _definition = definition;
        ConfigureFlightLayer();
        Shadow!.Texture = definition.ShadowTexture;
        Shadow.Position = definition.ShadowOffset;
        Shadow.Scale = definition.ShadowScale;
        Shadow.Modulate = new Color(1f, 1f, 1f, definition.ShadowOpacity);
        Shadow.FlipH = false;
        IsBodyTintEnabled = true;
        ApplyTint();
        ApplyVisual();
    }

    public void ConfigureInstanceIdentity(string instanceId)
    {
        uint hash = 2166136261;
        foreach (char value in instanceId ?? string.Empty) hash = (hash ^ value) * 16777619;
        _flightPhase = hash / (double)uint.MaxValue * Math.Tau;
    }

    public override void _Process(double delta)
    {
        if (_flightLayer is null) return;
        if (IsAirborne)
        {
            _flightTime += delta;
            _flightLayer.Position = new Vector2(0, (float)(Math.Sin(_flightPhase + _flightTime * Math.Tau / 1.4) * 3));
        }
        else if (IsShowingDeath && _deathDescent < .18)
        {
            _deathDescent = Math.Min(.18, _deathDescent + delta);
            _flightLayer.Position = new Vector2(0, (float)(_deathDescent / .18 * 12));
        }
    }

    /// <summary>Applies presentation-only contact tuning without changing the generated Unit definition.</summary>
    public void ConfigurePresentation(StandardUnitPresentationResource profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (_definition is null)
            throw new InvalidOperationException("Unit actor must be configured before presentation tuning.");
        EnsureNodes();
        Shadow!.Position = _definition.ShadowOffset + new Vector2(0f, profile.ShadowContactOffsetY);
    }

    public void RestoreTransientBodyPose()
    {
        if (_definition is null) return;
        if (Body is null) return;
        Body.Position = Vector2.Zero;
        Body.Rotation = 0f;
        Body.Scale = Vector2.One;
        ApplyTint();
    }

    public void SetActionPose(GodotUnitActionPose? pose)
    {
        _actionPose = pose;
        ApplyVisual();
    }

    public void SetFacing(GodotUnitFacing facing)
    {
        if (!Enum.IsDefined(facing))
            throw new ArgumentOutOfRangeException(nameof(facing));
        Facing = facing;
        ApplyVisual();
    }

    public void SetDeathVisual(bool enabled)
    {
        if (enabled && !IsShowingDeath) _deathDescent = 0;
        IsShowingDeath = enabled && _definition?.DeathTexture is not null;
        if (enabled) _actionPose = null;
        if (enabled && StatusOverlay is not null)
            StatusOverlay.Apply(Array.Empty<BattleUiStatusSnapshot>());
        ApplyVisual();
    }

    public void SetAirMoveOverlay(bool enabled)
    {
        if (_definition?.MovementKindValue == "air") ZIndex = enabled ? 100 : 0;
    }

    public void SetSpearHeld(bool held)
    {
        IsSpearHeld = held;
        ApplyVisual();
    }

    /// <summary>
    /// Toggles presentation-only tinting for visual comparison without changing gameplay state.
    /// </summary>
    public void SetBodyTintEnabled(bool enabled)
    {
        IsBodyTintEnabled = enabled;
        ApplyTint();
    }

    public void SetStatuses(IReadOnlyList<BattleUiStatusSnapshot>? statuses,int maximumVisible=4,float pulseDuration=.22f)
    {
        if(StatusOverlay is null){StatusOverlay=new GodotUnitStatusOverlay{ZIndex=50};AddChild(StatusOverlay);}
        StatusOverlay.MaximumVisible=maximumVisible;StatusOverlay.PulseDuration=pulseDuration;StatusOverlay.Apply(statuses);
    }

    public Rect2 VisualBoundsInParent()
    {
        if (Body?.Texture is null) return new Rect2(Position - new Vector2(20, 40), new Vector2(40, 60));
        Rect2 local = Body.GetRect();
        Transform2D transform = Transform * Body.Transform;
        Vector2[] corners =
        [
            transform * local.Position,
            transform * new Vector2(local.End.X, local.Position.Y),
            transform * local.End,
            transform * new Vector2(local.Position.X, local.End.Y)
        ];
        float left = corners.Min(value => value.X), top = corners.Min(value => value.Y);
        float right = corners.Max(value => value.X), bottom = corners.Max(value => value.Y);
        return new Rect2(left, top, right - left, bottom - top);
    }

    public Vector2 HeadAnchorInParent()
    {
        Rect2 bounds = VisualBoundsInParent();
        return new Vector2(bounds.GetCenter().X, bounds.Position.Y + Math.Min(8f, bounds.Size.Y * .12f));
    }

    public bool ContainsOpaquePoint(Vector2 parentPoint, float alphaThreshold = 0.1f)
    {
        if (Body?.Texture is null || !Visible || !Body.Visible) return false;
        Vector2 actorLocal = Transform.AffineInverse() * parentPoint;
        Vector2 bodyLocal = Body.Transform.AffineInverse() * actorLocal;
        Rect2 rect = Body.GetRect();
        if (!rect.HasPoint(bodyLocal) || rect.Size.X <= 0f || rect.Size.Y <= 0f) return false;
        float u = Mathf.Clamp((bodyLocal.X - rect.Position.X) / rect.Size.X, 0f, .999999f);
        float v = Mathf.Clamp((bodyLocal.Y - rect.Position.Y) / rect.Size.Y, 0f, .999999f);
        if (Body.FlipH) u = 1f - u;
        if (Body.FlipV) v = 1f - v;
        Image image = Body.Texture.GetImage();
        if (image.IsEmpty()) return false;
        int x = Math.Clamp((int)(u * image.GetWidth()), 0, image.GetWidth() - 1);
        int y = Math.Clamp((int)(v * image.GetHeight()), 0, image.GetHeight() - 1);
        return image.GetPixel(x, y).A >= alphaThreshold;
    }

    private void ApplyVisual()
    {
        if (_definition is null)
            return;
        EnsureNodes();
        bool usesUpLeft = Facing is GodotUnitFacing.North or GodotUnitFacing.East;
        Texture2D living = usesUpLeft
            ? !IsSpearHeld && _definition.UnarmedUpLeftTexture is not null ? _definition.UnarmedUpLeftTexture : _definition.UpLeftTexture!
            : !IsSpearHeld && _definition.UnarmedDownRightTexture is not null ? _definition.UnarmedDownRightTexture : _definition.DownRightTexture!;
        Texture2D? action = IsShowingDeath ? null : ResolveActionTexture(usesUpLeft);
        Body!.Texture = IsShowingDeath ? _definition.DeathTexture : action ?? living;
        Body.FlipH = !IsShowingDeath &&
            (Facing == GodotUnitFacing.East || Facing == GodotUnitFacing.West);
        Vector2 offset = IsShowingDeath
            ? _definition.DeathBodyOffset
            : usesUpLeft
                ? _definition.UpLeftBodyOffset
                : _definition.DownRightBodyOffset;
        Body.Offset = Body.FlipH ? new Vector2(-offset.X, offset.Y) : offset;
        Shadow!.FlipH = false;
    }

    private Texture2D? ResolveActionTexture(bool usesUpLeft) => _actionPose switch
    {
        GodotUnitActionPose.Melee when IsSpearHeld => usesUpLeft ? _definition?.MeleeUpLeftTexture : _definition?.MeleeDownRightTexture,
        GodotUnitActionPose.Ranged when IsSpearHeld => usesUpLeft ? _definition?.RangedUpLeftTexture : _definition?.RangedDownRightTexture,
        GodotUnitActionPose.Cast => usesUpLeft ? _definition?.CastUpLeftTexture : _definition?.CastDownRightTexture,
        GodotUnitActionPose.Hit => usesUpLeft ? _definition?.HitUpLeftTexture : _definition?.HitDownRightTexture,
        _ => null
    };

    private void ApplyTint()
    {
        if (_definition is null)
            return;
        EnsureNodes();
        bool usesGoatBodyMask =
            _definition.BodyTintModeValue == UnitBodyTintModes.GoatBodyMaskV1;
        Body!.Material = usesGoatBodyMask && IsBodyTintEnabled
            ? _definition.BodyTintMaterial
            : null;
        Body.Modulate = usesGoatBodyMask || !IsBodyTintEnabled
            ? Colors.White
            : _definition.BodyTint;
    }

    private void EnsureNodes()
    {
        if (Body is null || Shadow is null)
            throw new InvalidOperationException("Godot Unit actor is missing its Body or Shadow node.");
    }


    private void ConfigureFlightLayer()
    {
        if (_definition?.MovementKindValue != "air" || Body is null || _flightLayer is not null) return;
        _flightLayer = new Node2D { Name = "FlightVisualOffset" };
        AddChild(_flightLayer);
        Body.Owner = null;
        Body.Reparent(_flightLayer, keepGlobalTransform: false);
        SetProcess(true);
    }
}
