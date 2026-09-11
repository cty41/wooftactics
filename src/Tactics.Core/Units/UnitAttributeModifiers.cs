namespace Tactics.Core.Units;

/// <summary>Signed six-attribute delta used by temporary battle effects.</summary>
public readonly record struct UnitAttributeModifiers(
    int Strength = 0,
    int Agility = 0,
    int Constitution = 0,
    int Intelligence = 0,
    int Charisma = 0,
    int Luck = 0)
{
    public bool IsZero => Strength == 0 && Agility == 0 && Constitution == 0 &&
                          Intelligence == 0 && Charisma == 0 && Luck == 0;

    public static UnitAttributeModifiers operator +(UnitAttributeModifiers left, UnitAttributeModifiers right) => new(
        checked(left.Strength + right.Strength),
        checked(left.Agility + right.Agility),
        checked(left.Constitution + right.Constitution),
        checked(left.Intelligence + right.Intelligence),
        checked(left.Charisma + right.Charisma),
        checked(left.Luck + right.Luck));

    public UnitAttributes Apply(UnitAttributes basis) => new(
        Math.Max(1, checked(basis.Strength + Strength)),
        Math.Max(1, checked(basis.Agility + Agility)),
        Math.Max(1, checked(basis.Constitution + Constitution)),
        Math.Max(1, checked(basis.Intelligence + Intelligence)),
        Math.Max(1, checked(basis.Charisma + Charisma)),
        Math.Max(1, checked(basis.Luck + Luck)));
}
