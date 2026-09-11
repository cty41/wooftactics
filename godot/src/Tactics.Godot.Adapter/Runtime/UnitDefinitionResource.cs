using Godot;
using Tactics.Application.Units;
using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Stores one generated Pure Run Unit definition while keeping gameplay semantics in Core.
/// </summary>
[Tool]
[GlobalClass]
public partial class UnitDefinitionResource : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string SourceId { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public string Category { get; set; } = string.Empty;
    [Export] public string FamilyId { get; set; } = string.Empty;
    [Export] public string RoleId { get; set; } = string.Empty;
    [Export] public int Strength { get; set; }
    [Export] public int Agility { get; set; }
    [Export] public int Constitution { get; set; }
    [Export] public int Intelligence { get; set; }
    [Export] public int Charisma { get; set; }
    [Export] public int Luck { get; set; }
    [Export] public float Speed { get; set; }
    [Export] public int MovementTraitModifier { get; set; }
    [Export] public int MaxHealth { get; set; }
    [Export] public int MaxMana { get; set; }
    [Export] public int StartingMana { get; set; }
    [Export] public int MoveRange { get; set; }
    [Export] public float Initiative { get; set; }
    [Export] public string DerivedStatModeValue { get; set; } = "frozen-formula";
    [Export] public int AttackRange { get; set; }
    [Export] public float AttackFactor { get; set; }
    [Export] public float DefenceFactor { get; set; }
    [Export] public string MovementKindValue { get; set; } = "land";
    [Export] public bool CanProduceCorpse { get; set; }
    [Export] public string ActorContentIdValue { get; set; } = string.Empty;
    [Export] public PackedScene? ActorScene { get; set; }
    [Export] public Texture2D? DownRightTexture { get; set; }
    [Export] public Texture2D? UpLeftTexture { get; set; }
    [Export] public Texture2D? PortraitTextureOverride { get; set; }
    [Export] public bool HasPortraitRegion { get; set; }
    [Export] public Rect2 PortraitRegion { get; set; }
    [Export] public Texture2D? UnarmedDownRightTexture { get; set; }
    [Export] public Texture2D? UnarmedUpLeftTexture { get; set; }
    [Export] public Texture2D? DeathTexture { get; set; }
    [Export] public Texture2D? MeleeDownRightTexture { get; set; }
    [Export] public Texture2D? MeleeUpLeftTexture { get; set; }
    [Export] public Texture2D? RangedDownRightTexture { get; set; }
    [Export] public Texture2D? RangedUpLeftTexture { get; set; }
    [Export] public Texture2D? CastDownRightTexture { get; set; }
    [Export] public Texture2D? CastUpLeftTexture { get; set; }
    [Export] public Texture2D? HitDownRightTexture { get; set; }
    [Export] public Texture2D? HitUpLeftTexture { get; set; }
    [Export] public Texture2D? ShadowTexture { get; set; }
    [Export] public Vector2 DownRightBodyOffset { get; set; }
    [Export] public Vector2 UpLeftBodyOffset { get; set; }
    [Export] public Vector2 DeathBodyOffset { get; set; }
    [Export] public Vector2 ShadowOffset { get; set; }
    [Export] public Vector2 ShadowScale { get; set; } = Vector2.One;
    [Export] public float ShadowOpacity { get; set; } = 1f;
    [Export] public Color BodyTint { get; set; } = Colors.White;
    [Export] public string BodyTintModeValue { get; set; } = UnitBodyTintModes.Multiply;
    [Export] public Color BaseBodyColor { get; set; } = Colors.White;
    [Export] public ShaderMaterial? BodyTintMaterial { get; set; }
    [Export] public string[] DeferredDependencies { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Reconstructs and validates the engine-neutral definition represented by this resource.
    /// </summary>
    public UnitDefinition ToCoreDefinition()
    {
        ValidateVisualContract();
        UnitMovementKind movementKind = MovementKindValue switch
        {
            "land" => UnitMovementKind.Land,
            "air" => UnitMovementKind.Air,
            "swim" => UnitMovementKind.Swim,
            _ => throw new InvalidOperationException(
                $"Unit '{ContentIdValue}' has unknown movement kind '{MovementKindValue}'.")
        };
        UnitDerivedStatMode derivedStatMode = DerivedStatModeValue switch
        {
            "frozen-formula" => UnitDerivedStatMode.FrozenFormula,
            "explicit" => UnitDerivedStatMode.Explicit,
            _ => throw new InvalidOperationException(
                $"Unit '{ContentIdValue}' has unknown derived stat mode '{DerivedStatModeValue}'.")
        };
        return new UnitDefinition(
            new ContentId(ContentIdValue),
            SourceId,
            DisplayName,
            FamilyId,
            RoleId,
            new UnitAttributes(Strength, Agility, Constitution, Intelligence, Charisma, Luck),
            Speed,
            new UnitDerivedStats(MaxHealth, MaxMana, StartingMana, MoveRange, Initiative),
            AttackRange,
            AttackFactor,
            DefenceFactor,
            movementKind,
            CanProduceCorpse,
            derivedStatMode,
            MovementTraitModifier);
    }

    /// <summary>
    /// Rejects missing adapter references without mutating gameplay state.
    /// </summary>
    public void ValidateVisualContract()
    {
        if (SchemaVersion != 1)
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has unsupported schema {SchemaVersion}.");
        if (ActorContentIdValue != "packed-scene.unit-actor" || ActorScene is null)
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has no canonical actor scene.");
        if (DownRightTexture is null || UpLeftTexture is null || ShadowTexture is null)
            throw new InvalidOperationException($"Unit '{ContentIdValue}' is missing a required texture.");
        if (HasPortraitRegion &&
            (PortraitRegion.Size.X <= 0f || PortraitRegion.Size.Y <= 0f ||
             PortraitRegion.Position.X < 0f || PortraitRegion.Position.Y < 0f))
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has an invalid portrait crop region.");
        if ((UnarmedDownRightTexture is null) != (UnarmedUpLeftTexture is null) ||
            (ContentIdValue == "unit.pure-run.amazon") != (UnarmedDownRightTexture is not null))
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has an invalid unarmed texture contract.");
        ValidatePosePair(MeleeDownRightTexture, MeleeUpLeftTexture, "melee");
        ValidatePosePair(RangedDownRightTexture, RangedUpLeftTexture, "ranged");
        ValidatePosePair(CastDownRightTexture, CastUpLeftTexture, "cast");
        ValidatePosePair(HitDownRightTexture, HitUpLeftTexture, "hit");
        if (CanProduceCorpse != (DeathTexture is not null))
            throw new InvalidOperationException($"Unit '{ContentIdValue}' corpse and death texture contracts disagree.");
        float[] geometry =
        {
            DownRightBodyOffset.X,
            DownRightBodyOffset.Y,
            UpLeftBodyOffset.X,
            UpLeftBodyOffset.Y,
            DeathBodyOffset.X,
            DeathBodyOffset.Y,
            ShadowOffset.X,
            ShadowOffset.Y,
            ShadowScale.X,
            ShadowScale.Y,
            ShadowOpacity
        };
        if (geometry.Any(value => !float.IsFinite(value)) || ShadowScale.X <= 0f ||
            ShadowScale.Y <= 0f || !Mathf.IsEqualApprox(ShadowScale.X, ShadowScale.Y) ||
            ShadowOpacity < 0f || ShadowOpacity > 1f)
        {
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has invalid Sprite geometry.");
        }
        if (!float.IsFinite(BodyTint.R) || !float.IsFinite(BodyTint.G) ||
            !float.IsFinite(BodyTint.B) || !float.IsFinite(BodyTint.A))
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has a non-finite body tint.");
        if (!float.IsFinite(BaseBodyColor.R) || !float.IsFinite(BaseBodyColor.G) ||
            !float.IsFinite(BaseBodyColor.B) || !float.IsFinite(BaseBodyColor.A))
        {
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has a non-finite base body color.");
        }
        if (!UnitBodyTintModes.IsSupported(BodyTintModeValue))
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has an unknown body tint mode.");
        bool usesGoatBodyMask = BodyTintModeValue == UnitBodyTintModes.GoatBodyMaskV1;
        if ((FamilyId == "goat") != usesGoatBodyMask)
            throw new InvalidOperationException($"Unit '{ContentIdValue}' body tint mode disagrees with its family.");
        if (usesGoatBodyMask && (BodyTintMaterial?.Shader is null ||
            BodyTintMaterial.Shader.ResourcePath !=
                "res://src/Tactics.Godot.Adapter/Runtime/Shaders/GoatBodyTint.gdshader"))
        {
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has no canonical Goat body tint material.");
        }
        if (!usesGoatBodyMask && BodyTintMaterial is not null)
            throw new InvalidOperationException($"Unit '{ContentIdValue}' unexpectedly has a body tint material.");
        if (DeferredDependencies.Any(string.IsNullOrWhiteSpace) ||
            DeferredDependencies.Distinct(StringComparer.Ordinal).Count() != DeferredDependencies.Length)
        {
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has invalid deferred dependencies.");
        }
    }

    private void ValidatePosePair(Texture2D? downRight, Texture2D? upLeft, string family)
    {
        if ((downRight is null) != (upLeft is null))
            throw new InvalidOperationException($"Unit '{ContentIdValue}' has an incomplete {family} action pose pair.");
    }
}
