using Godot;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PureRunDefinitionResource : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string[] EncounterContentIds { get; set; } = Array.Empty<string>();
    [Export] public string[] CharacterIds { get; set; } = Array.Empty<string>();
    [Export] public string[] UnitContentIds { get; set; } = Array.Empty<string>();
    [Export] public string[] StartingSkillContentIds { get; set; } = Array.Empty<string>();
    [Export] public string[] StartingSkillChoiceContentIds { get; set; } = Array.Empty<string>();
    [Export] public int[] SeededStartingSkillFlags { get; set; } = Array.Empty<int>();
    [Export] public string[] InherentSkillContentIds { get; set; } = Array.Empty<string>();
    [Export] public int[] Strengths { get; set; } = Array.Empty<int>();
    [Export] public int[] Agilities { get; set; } = Array.Empty<int>();
    [Export] public int[] Constitutions { get; set; } = Array.Empty<int>();
    [Export] public int[] Intelligences { get; set; } = Array.Empty<int>();
    [Export] public int[] Charismas { get; set; } = Array.Empty<int>();
    [Export] public int[] Lucks { get; set; } = Array.Empty<int>();
    [Export] public string LayerFourMapContentId { get; set; } = string.Empty;

    public PureRunDefinition ToCoreDefinition()
    {
        int candidateCount = CharacterIds.Length;
        if (SchemaVersion is not (1 or 2) || EncounterContentIds.Length != 3 || candidateCount is not (3 or 4 or 5) ||
            UnitContentIds.Length != candidateCount || StartingSkillContentIds.Length != candidateCount ||
            SeededStartingSkillFlags.Any(value => value is not (0 or 1)))
            throw new InvalidOperationException("Pure Run definition resource shape is invalid.");
        int[][] attributeColumns = [Strengths, Agilities, Constitutions, Intelligences, Charismas, Lucks];
        if (SchemaVersion == 2 && attributeColumns.Any(values => values.Length != candidateCount))
            throw new InvalidOperationException("Pure Run v2 requires serialized attributes for every candidate.");
        UnitAttributes[] attributes = SchemaVersion == 2
            ? Enumerable.Range(0, candidateCount).Select(index => new UnitAttributes(
                Strengths[index], Agilities[index], Constitutions[index], Intelligences[index],
                Charismas[index], Lucks[index])).ToArray()
            : LegacyAttributes(candidateCount);
        string[] choiceValues = StartingSkillChoiceContentIds.Length == candidateCount * 3
            ? StartingSkillChoiceContentIds
            : DefaultStartingChoices(candidateCount);
        return new PureRunDefinition(new ContentId(ContentIdValue), EncounterContentIds.Select(value => new ContentId(value)),
            Enumerable.Range(0, candidateCount).Select(index => new PureRunPartyTemplate(CharacterIds[index],
                new ContentId(UnitContentIds[index]), new ContentId(StartingSkillContentIds[index]), attributes[index], 1,
                choiceValues.Skip(index * 3).Take(3).Select(value => new ContentId(value)).ToArray(),
                SeededStartingSkillFlags.Length == candidateCount && SeededStartingSkillFlags[index] == 1,
                InherentSkillContentIds.Length == candidateCount && !string.IsNullOrWhiteSpace(InherentSkillContentIds[index])
                    ? [new ContentId(InherentSkillContentIds[index])]
                    : Array.Empty<ContentId>())),
            string.IsNullOrWhiteSpace(LayerFourMapContentId) ? null : new ContentId(LayerFourMapContentId));
    }

    private static UnitAttributes[] LegacyAttributes(int candidateCount) =>
    [
        new(5, 5, 5, 6, 5, 5),
        new(5, 5, 5, 5, 6, 5),
        new(5, 6, 5, 5, 5, 5),
        .. (candidateCount >= 4 ? [new UnitAttributes(5, 5, 5, 5, 6, 5)] : Array.Empty<UnitAttributes>()),
        .. (candidateCount >= 5 ? [new UnitAttributes(6, 5, 5, 4, 6, 4)] : Array.Empty<UnitAttributes>())
    ];

    private static string[] DefaultStartingChoices(int candidateCount)
    {
        string[] existing =
        [
        "skill.mage.fireball.lv1", "skill.mage.ice-bolt.lv1", "skill.mage.lightning.lv1",
        "skill.necromancer.summon-skeleton.lv1", "skill.necromancer.amplify-damage.lv1", "skill.necromancer.bone-spear.lv1",
        "skill.amazon.thrust.lv1", "skill.poison-spear.lv1", "skill.amazon.combat-techniques.lv1"
        ];
        if (candidateCount == 3) return existing;
        string[] four = existing.Concat([
            "skill.demonbound.bane.lv1", "skill.demonbound.infernal-blast.lv1", "skill.demonbound.mindfulness.lv1"
        ]).ToArray();
        return candidateCount == 4 ? four : four.Concat([
            "skill.poet.xiake-xing.lv1", "skill.poet.jiang-jin-jiu.lv1", "skill.poet.moon-drink.lv1"
        ]).ToArray();
    }
}
