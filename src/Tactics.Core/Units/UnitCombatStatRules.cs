using Tactics.Core.Skills;

namespace Tactics.Core.Units;

/// <summary>Calculates the approved six-attribute combat contract.</summary>
public static class UnitCombatStatRules
{
    public const string ContractId = "ATTR-SECONDARY-STATS-001";

    public static int Accuracy(UnitAttributes attributes) => checked(100 + (attributes.Agility - 5) * 5);
    public static int Dodge(UnitAttributes attributes) => Math.Clamp(checked(5 + (attributes.Luck - 5) * 5), 0, 50);
    public static int CriticalChance(UnitAttributes attributes) => Math.Clamp(checked(10 + (attributes.Luck - 5) * 3), 0, 80);
    public static decimal CriticalMultiplier(UnitAttributes attributes) =>
        Math.Clamp(1.5m + (attributes.Strength - 5) * 0.05m, 1.25m, 2m);

    public static int AttributeContribution(UnitAttributes effectiveAttributes, SkillRole role,
        SkillEffectScalingKind effectKind, bool multiHit = false)
    {
        if (effectKind == SkillEffectScalingKind.None || role == SkillRole.Any)
            return 0;

        int divisor = effectKind is SkillEffectScalingKind.RangedPhysical or SkillEffectScalingKind.Magical ? 2 : 1;
        int contribution;
        if (role == SkillRole.Amazon)
        {
            int effectiveAdded = checked(Total(effectiveAttributes) - 30);
            contribution = 5 / 2 + (divisor == 1 ? effectiveAdded : FloorDiv(effectiveAdded, 2));
        }
        else
        {
            int primary = role switch
            {
                SkillRole.Mage => effectiveAttributes.Intelligence,
                SkillRole.Necromancer => effectiveAttributes.Constitution,
                SkillRole.Demonbound => effectiveAttributes.Charisma,
                SkillRole.Poet => effectiveAttributes.Strength,
                _ => 0
            };
            contribution = FloorDiv(primary, divisor);
        }

        contribution = Math.Max(1, contribution);
        return multiHit ? FloorDiv(contribution, 2) : contribution;
    }

    public static int PermanentUniversalGrowth(UnitAttributes permanentAttributes) =>
        checked(Total(permanentAttributes) - 30);

    public static bool MeetsUniversalTier(UnitAttributes permanentAttributes, int tier) => tier switch
    {
        <= 1 => true,
        2 => PermanentUniversalGrowth(permanentAttributes) >= 2,
        _ => PermanentUniversalGrowth(permanentAttributes) >= 4
    };

    public static int Total(UnitAttributes value) => checked(value.Strength + value.Agility +
        value.Constitution + value.Intelligence + value.Charisma + value.Luck);

    private static int FloorDiv(int value, int divisor) => (int)Math.Floor(value / (double)divisor);
}
