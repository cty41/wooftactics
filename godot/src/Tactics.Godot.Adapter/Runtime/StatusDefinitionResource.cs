using Godot;
using Tactics.Core.Content;
using Tactics.Core.Statuses;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Stores one generated Buff definition without copying its audit-only icon payload.
/// </summary>
[GlobalClass]
public partial class StatusDefinitionResource : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string SourceId { get; set; } = string.Empty;
    [Export] public int DefaultDuration { get; set; }
    [Export] public bool CanAct { get; set; } = true;
    [Export] public string PolarityValue { get; set; } = string.Empty;
    [Export] public string EffectKindValue { get; set; } = string.Empty;
    [Export] public string TriggerTimingValue { get; set; } = string.Empty;
    [Export] public string RefreshStrategyValue { get; set; } = string.Empty;
    [Export] public string CurseCategory { get; set; } = string.Empty;
    [Export] public float DamagePerTurn { get; set; }
    [Export] public string ElementKindValue { get; set; } = string.Empty;
    [Export] public string DamageCategoryValue { get; set; } = string.Empty;
    [Export] public float SpeedModifier { get; set; }
    [Export] public float DamageReductionPercent { get; set; }
    [Export] public string MeleeRetaliationStatusIdValue { get; set; } = string.Empty;
    [Export] public int MeleeRetaliationDuration { get; set; }
    [Export] public int InitiativeModifier { get; set; }
    [Export] public int MovementModifier { get; set; }
    [Export] public int FrozenTotalDamage { get; set; }
    [Export] public int StrengthModifier { get; set; }
    [Export] public int AgilityModifier { get; set; }
    [Export] public int ConstitutionModifier { get; set; }
    [Export] public int IntelligenceModifier { get; set; }
    [Export] public int CharismaModifier { get; set; }
    [Export] public int LuckModifier { get; set; }
    [Export] public int FrozenTotalHealing { get; set; }
    [Export] public string SourcePath { get; set; } = string.Empty;
    [Export] public string SourceGuid { get; set; } = string.Empty;
    [Export] public long SourceLocalFileId { get; set; }
    [Export] public string IconSourcePath { get; set; } = string.Empty;
    [Export] public string IconSourceGuid { get; set; } = string.Empty;
    [Export] public long IconSourceLocalFileId { get; set; }
    [Export] public string IconDependencyHash { get; set; } = string.Empty;
    [Export] public bool IconPayloadCopied { get; set; }

    public StatusDefinition ToCoreDefinition()
    {
        if (SchemaVersion is not (1 or 2))
            throw new InvalidOperationException($"Status '{ContentIdValue}' has unsupported schema {SchemaVersion}.");
        if (IconPayloadCopied)
            throw new InvalidOperationException($"Status '{ContentIdValue}' copied an audit-only icon payload.");
        if (SchemaVersion == 1 &&
            (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(SourceGuid) || SourceLocalFileId <= 0))
            throw new InvalidOperationException($"Status '{ContentIdValue}' has invalid frozen source audit fields.");
        bool hasIcon = !string.IsNullOrEmpty(IconSourcePath) || !string.IsNullOrEmpty(IconSourceGuid) ||
                       IconSourceLocalFileId != 0 || !string.IsNullOrEmpty(IconDependencyHash);
        if (hasIcon && (string.IsNullOrWhiteSpace(IconSourcePath) || string.IsNullOrWhiteSpace(IconSourceGuid) ||
            IconSourceLocalFileId <= 0 || string.IsNullOrWhiteSpace(IconDependencyHash)))
        {
            throw new InvalidOperationException($"Status '{ContentIdValue}' has an incomplete icon audit.");
        }

        return new StatusDefinition(
            new ContentId(ContentIdValue),
            SourceId,
            DefaultDuration,
            CanAct,
            Parse<StatusPolarity>(PolarityValue),
            Parse<StatusEffectKind>(EffectKindValue),
            Parse<StatusTriggerTiming>(TriggerTimingValue),
            Parse<StatusRefreshStrategy>(RefreshStrategyValue),
            CurseCategory,
            DamagePerTurn,
            Parse<StatusElementKind>(ElementKindValue),
            Parse<StatusDamageCategory>(DamageCategoryValue),
            SpeedModifier,
            DamageReductionPercent,
            string.IsNullOrEmpty(MeleeRetaliationStatusIdValue)
                ? null
                : new ContentId(MeleeRetaliationStatusIdValue),
            MeleeRetaliationDuration,
            initiativeModifier: InitiativeModifier,
            movementModifier: MovementModifier,
            frozenTotalDamage: FrozenTotalDamage,
            attributeModifiers: new UnitAttributeModifiers(
                StrengthModifier, AgilityModifier, ConstitutionModifier,
                IntelligenceModifier, CharismaModifier, LuckModifier),
            frozenTotalHealing: FrozenTotalHealing);
    }

    private static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: false, out T result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidOperationException($"Unknown {typeof(T).Name} value '{value}'.");
}
