using Tactics.Core.Skills;
using Tactics.Core.Units;
using NUnit.Framework;

namespace Tactics.Core.Tests;

public sealed class PrimaryAttributeRulesTests
{
    [TestCase(SkillRole.Mage, UnitPrimaryAttribute.Intelligence)]
    [TestCase(SkillRole.Necromancer, UnitPrimaryAttribute.Constitution)]
    [TestCase(SkillRole.Demonbound, UnitPrimaryAttribute.Charisma)]
    [TestCase(SkillRole.Poet, UnitPrimaryAttribute.Strength)]
    [TestCase(SkillRole.Amazon, UnitPrimaryAttribute.None)]
    [TestCase(SkillRole.Any, UnitPrimaryAttribute.None)]
    public void ForMapsEachClassToItsIdentityAttribute(SkillRole role, UnitPrimaryAttribute expected) =>
        Assert.That(PrimaryAttributeRules.For(role), Is.EqualTo(expected));

    [Test]
    public void AmazonDamageBonusIsKeyedToAgilityWithoutGainingAnIdentity() =>
        Assert.That(PrimaryAttributeRules.DamageBonusSource(SkillRole.Amazon), Is.EqualTo(UnitPrimaryAttribute.Agility));

    [Test]
    public void ValueReadsTheNamedAttributeAndNoneIsZero()
    {
        var attributes = new UnitAttributes(1, 2, 3, 4, 5, 6);
        Assert.That(PrimaryAttributeRules.Value(attributes, UnitPrimaryAttribute.Intelligence), Is.EqualTo(4));
        Assert.That(PrimaryAttributeRules.Value(attributes, UnitPrimaryAttribute.None), Is.EqualTo(0));
    }
}
