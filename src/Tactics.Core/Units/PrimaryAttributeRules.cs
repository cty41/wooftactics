using Tactics.Core.Skills;

namespace Tactics.Core.Units;

/// <summary>The attribute a class is built around; <see cref="None"/> marks all-rounders and non-class units.</summary>
public enum UnitPrimaryAttribute
{
    None,
    Strength,
    Agility,
    Constitution,
    Intelligence,
    Charisma,
    Luck
}

/// <summary>
/// Single source for which attribute each class scales with. Authored unit data
/// (<see cref="UnitDefinition.PrimaryAttribute"/>) must agree with it; a test enforces that.
/// </summary>
public static class PrimaryAttributeRules
{
    /// <summary>The class's identity attribute (all-rounder Amazon has none).</summary>
    public static UnitPrimaryAttribute For(SkillRole role) => role switch
    {
        SkillRole.Mage => UnitPrimaryAttribute.Intelligence,
        SkillRole.Necromancer => UnitPrimaryAttribute.Constitution,
        SkillRole.Demonbound => UnitPrimaryAttribute.Charisma,
        SkillRole.Poet => UnitPrimaryAttribute.Strength,
        _ => UnitPrimaryAttribute.None
    };

    /// <summary>
    /// The attribute whose value drives the primary damage bonus. Identical to <see cref="For"/> except the
    /// all-rounder Amazon, whose bonus is keyed to Agility while it still has no primary attribute identity.
    /// </summary>
    public static UnitPrimaryAttribute DamageBonusSource(SkillRole role) =>
        role == SkillRole.Amazon ? UnitPrimaryAttribute.Agility : For(role);

    public static int Value(UnitAttributes attributes, UnitPrimaryAttribute attribute) => attribute switch
    {
        UnitPrimaryAttribute.Strength => attributes.Strength,
        UnitPrimaryAttribute.Agility => attributes.Agility,
        UnitPrimaryAttribute.Constitution => attributes.Constitution,
        UnitPrimaryAttribute.Intelligence => attributes.Intelligence,
        UnitPrimaryAttribute.Charisma => attributes.Charisma,
        UnitPrimaryAttribute.Luck => attributes.Luck,
        _ => 0
    };
}
