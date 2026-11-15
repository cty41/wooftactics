#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class DemonboundAssetFactory
{
    private const string Root = "res://content/demonbound";
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string RunPath = "res://content/runs/PureRunThreeEncounterV1.tres";
    private const string BalancePath = "res://content/ui/PlayableLv1BalanceProfile.tres";
    private const string BanePresentationPath = Root + "/BanePresentation.tres";
    private const string BaneDebuffPath = Root + "/BaneDamageOutputReduction.tres";

    private sealed record SkillData(string Id, string Name, string Kind, int Level, int Mana,
        int MinRange, int MaxRange, string Execution, int Damage, string DamageKind, string Branch,
        int Corruption, string Prerequisite = "", string StatusId = "", int StatusDuration = 0,
        int StatusChance = 100, bool Hidden = false, bool GrowthVisible = true,
        string PrerequisiteBranch = "", bool ScaleFromPrimaryAttribute = false);

    public static void Build()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Root));
        SkillData[] definitions = Definitions();
        var generated = new List<GodotResourceEntry>();
        foreach (SkillData data in definitions)
        {
            string path = $"{Root}/{FileName(data.Id)}.tres";
            var resource = new SkillDefinitionResource
            {
                SchemaVersion = 1, ContentIdValue = data.Id, SourceId = $"godot.{data.Id}",
                DisplayName = data.Name, Description = data.Name, RoleValue = "Demonbound", KindValue = data.Kind,
                Level = data.Level, ManaCost = data.Mana, MinRange = data.MinRange, MaxRange = data.MaxRange,
                ExecutionKindValue = data.Execution, Damage = data.Damage, DamageKindValue = data.DamageKind,
                StatusContentIdValue = data.StatusId, StatusDuration = data.StatusDuration,
                IsBasicAbility = false, MaxUsesPerTurn = data.Execution == "Meditation" ? 1 : 0,
                CanCrit = data.Execution is not ("Mindfulness" or "Meditation" or "DemonicRegeneration"),
                BranchId = data.Branch, PrerequisiteContentIdValue = data.Prerequisite,
                PrerequisiteBranchId = !string.IsNullOrWhiteSpace(data.PrerequisiteBranch)
                    ? data.PrerequisiteBranch : data.Level > 1 ? data.Branch : string.Empty,
                GrowthVisible = data.GrowthVisible, RequiredAttribute = data.Execution == "Meditation" ? string.Empty : "Charisma",
                MinimumAttribute = data.Execution == "Meditation" ? 0 :
                    !string.IsNullOrWhiteSpace(data.PrerequisiteBranch) || data.Level > 1 ? 7 : 5,
                IgnoreLineOfSight = data.Execution is "Bane" or "Cleave" or "InfernalBlast" or "Hellfire",
                StatusChancePercent = data.StatusChance, CorruptionCost = data.Corruption,
                DamageScalingValue = data.ScaleFromPrimaryAttribute
                    ? "PrimaryAttributeAboveNeutral" : "None",
                Hidden = data.Hidden, AuthoringSourceKindValue = "GodotAuthored"
            };
            AttributeSkillBalanceOverrides.Apply(resource);
            resource.ToCoreDefinition();
            Save(resource, path);
            string[] references = new[] { data.Prerequisite, data.StatusId }
                .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            generated.Add(Entry(data.Id, "skill", path, references));
        }

        var baneDebuff = new StatusDefinitionResource
        {
            ContentIdValue = "buff.demonbound.bane-debuff",
            SourceId = "godot.buff.demonbound.bane-debuff",
            DefaultDuration = 1,
            CanAct = true,
            PolarityValue = "Harmful",
            EffectKindValue = "DamageOutputReduction",
            TriggerTimingValue = "None",
            RefreshStrategyValue = "RefreshDuration",
            ElementKindValue = "Dark",
            DamageCategoryValue = "Magic",
            SourcePath = "GodotAuthored/Demonbound/BaneDamageOutputReduction",
            SourceGuid = "2ec889592b4a4f02b9592f23cd56a12b",
            SourceLocalFileId = 1
        };
        _ = baneDebuff.ToCoreDefinition();
        Save(baneDebuff, BaneDebuffPath);
        generated.Add(Entry(baneDebuff.ContentIdValue, "buff", BaneDebuffPath, Array.Empty<string>()));

        var banePresentation = new SkillPresentationResource
        {
            ContentIdValue = "presentation.skill.demonbound.bane",
            SkillBranch = "demonbound.bane",
            ProgrammaticKind = "bane-crescent",
            PrimaryColor = new Color(.72f, .2f, 1f),
            SecondaryColor = new Color(1f, .45f, .95f),
            TravelDuration = .28f,
            ImpactDuration = .16f
        };
        Save(banePresentation, BanePresentationPath);
        generated.Add(Entry(banePresentation.ContentIdValue, "presentation", BanePresentationPath, Array.Empty<string>()));

        const string unitPath = Root + "/PureRunDemonbound.tres";
        UnitDefinitionResource template = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/units/PureRunAmazon.tres", string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Demonbound placeholder template is missing.");
        UnitDefinitionResource unit = (UnitDefinitionResource)template.Duplicate(true);
        unit.ContentIdValue = "unit.pure-run.demonbound"; unit.SourceId = "godot.demonbound";
        unit.DisplayName = "Demonbound"; unit.FamilyId = "player"; unit.RoleId = "demonbound";
        unit.Strength = 6; unit.Agility = 4; unit.Constitution = 6; unit.Intelligence = 6; unit.Charisma = 6; unit.Luck = 2;
        unit.Speed = 4; unit.MaxHealth = 24; unit.MaxMana = 18; unit.StartingMana = 6; unit.MoveRange = 5; unit.Initiative = 8;
        unit.MovementTraitModifier = 0;
        UnitAssetFactory.ApplyPrimaryAttribute(unit);
        unit.DerivedStatModeValue = "explicit";
        unit.DownRightTexture = Texture("res://assets/units/doge_demonbound.png");
        unit.UpLeftTexture = Texture("res://assets/units/doge_demonbound_ul.png");
        unit.DeathTexture = Texture("res://assets/units/doge_demonbound_death.png");
        unit.MeleeDownRightTexture = Texture("res://assets/units/actions/doge_demonbound_melee_attack_dr.png");
        unit.MeleeUpLeftTexture = Texture("res://assets/units/actions/doge_demonbound_melee_attack_ul.png");
        unit.CastDownRightTexture = Texture("res://assets/units/actions/doge_demonbound_cast_dr.png");
        unit.CastUpLeftTexture = Texture("res://assets/units/actions/doge_demonbound_cast_ul.png");
        unit.HitDownRightTexture = Texture("res://assets/units/actions/doge_demonbound_hit_dr.png");
        unit.HitUpLeftTexture = Texture("res://assets/units/actions/doge_demonbound_hit_ul.png");
        unit.UnarmedDownRightTexture = null; unit.UnarmedUpLeftTexture = null;
        unit.RangedDownRightTexture = null; unit.RangedUpLeftTexture = null;
        unit.BodyTint = new Color(.72f, .58f, .86f); unit.BaseBodyColor = new Color(.72f, .58f, .86f);
        unit.ToCoreDefinition(); Save(unit, unitPath);
        generated.Add(Entry(unit.ContentIdValue, "unit", unitPath, new[] { "packed-scene.unit-actor" }));

        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>(CatalogPath,
            string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Catalog missing.");
        Dictionary<string, GodotResourceEntry> entries = catalog.Entries.ToDictionary(value => value.ContentIdValue,
            Copy, StringComparer.Ordinal);
        foreach (GodotResourceEntry entry in generated) entries[entry.ContentIdValue] = entry;
        Save(new GodotResourceCatalog { Entries = entries.Values.OrderBy(value => value.ContentIdValue,
            StringComparer.Ordinal).ToArray() }, CatalogPath);

        PureRunDefinitionResource run = ResourceLoader.Load<PureRunDefinitionResource>(RunPath,
            string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Pure Run definition missing.");
        run.SchemaVersion = 2;
        run.CharacterIds = run.CharacterIds.Concat(new[] { "pure_run_demonbound" }).Distinct().ToArray();
        run.UnitContentIds = run.UnitContentIds.Concat(new[] { unit.ContentIdValue }).Distinct().ToArray();
        run.StartingSkillContentIds = run.StartingSkillContentIds.Concat(new[] { "skill.demonbound.bane.lv1" }).Distinct().ToArray();
        run.StartingSkillChoiceContentIds = run.StartingSkillChoiceContentIds.Concat(new[]
        {
            "skill.demonbound.bane.lv1", "skill.demonbound.infernal-blast.lv1", "skill.demonbound.mindfulness.lv1"
        }).Distinct().ToArray();
        run.SeededStartingSkillFlags = new[] { 0, 0, 0, 1 };
        run.InherentSkillContentIds = new[] { "", "", "", "skill.demonbound.meditation" };
        run.Strengths = new[] { 4, 5, 5, 6 };
        run.Agilities = new[] { 5, 3, 5, 4 };
        run.Constitutions = new[] { 3, 6, 5, 6 };
        run.Intelligences = new[] { 6, 6, 5, 6 };
        run.Charismas = new[] { 6, 6, 5, 6 };
        run.Lucks = new[] { 6, 4, 5, 2 };
        run.ToCoreDefinition(); Save(run, RunPath);

        PlayableLv1BalanceProfileResource balance = ResourceLoader.Load<PlayableLv1BalanceProfileResource>(
            BalancePath, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Playable balance profile missing.");
        var unitAttacks = Enumerable.Range(0, balance.UnitContentIds.Length).ToDictionary(
            index => balance.UnitContentIds[index],
            index => (Physical: balance.UnitPhysicalAttacks[index], Magical: balance.UnitMagicalAttacks[index]),
            StringComparer.Ordinal);
        unitAttacks[unit.ContentIdValue] = (4, 2);
        string[] orderedUnits = unitAttacks.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        balance.UnitContentIds = orderedUnits;
        balance.UnitPhysicalAttacks = orderedUnits.Select(id => unitAttacks[id].Physical).ToArray();
        balance.UnitMagicalAttacks = orderedUnits.Select(id => unitAttacks[id].Magical).ToArray();
        _ = balance.ToCoreProfile(); Save(balance, BalancePath);
    }

    private static SkillData[] Definitions() => new[]
    {
        new SkillData("skill.demonbound.meditation", "Meditation", "Utility", 1, 0, 0, 0, "Meditation", 0, "None", "demonbound.meditation", 0, Hidden:true, GrowthVisible:false),
        new SkillData("skill.demonbound.bane.lv1", "Hex: Bane", "Active", 1, 3, 1, 1, "Bane", 5, "Magical", "demonbound.bane", 3, ScaleFromPrimaryAttribute:true),
        new SkillData("skill.demonbound.bane.lv2", "Hex: Bane", "Active", 2, 3, 1, 1, "Bane", 6, "Magical", "demonbound.bane", 3, "skill.demonbound.bane.lv1", "buff.demonbound.bane-debuff", 1, ScaleFromPrimaryAttribute:true),
        new SkillData("skill.demonbound.bane.lv3", "Hex: Bane", "Active", 3, 3, 1, 1, "Bane", 7, "Magical", "demonbound.bane", 3, "skill.demonbound.bane.lv2", "buff.demonbound.bane-debuff", 2, ScaleFromPrimaryAttribute:true),
        new SkillData("skill.demonbound.cleave.lv1", "Cleave", "Active", 1, 4, 1, 1, "Cleave", 6, "Physical", "demonbound.cleave", 2, "skill.demonbound.bane.lv1", PrerequisiteBranch:"demonbound.bane"),
        new SkillData("skill.demonbound.cleave.lv2", "Cleave", "Active", 2, 4, 1, 1, "Cleave", 6, "Physical", "demonbound.cleave", 2, "skill.demonbound.cleave.lv1"),
        new SkillData("skill.demonbound.infernal-blast.lv1", "Infernal Blast", "Active", 1, 5, 1, 1, "InfernalBlast", 4, "Magical", "demonbound.infernal-blast", 3),
        new SkillData("skill.demonbound.infernal-blast.lv2", "Infernal Blast", "Active", 2, 3, 1, 1, "InfernalBlast", 4, "Magical", "demonbound.infernal-blast", 3, "skill.demonbound.infernal-blast.lv1"),
        new SkillData("skill.demonbound.infernal-blast.lv3", "Infernal Blast", "Active", 3, 3, 1, 1, "InfernalBlast", 4, "Magical", "demonbound.infernal-blast", 4, "skill.demonbound.infernal-blast.lv2"),
        new SkillData("skill.demonbound.hellfire.lv1", "Hellfire", "Active", 1, 5, 0, 0, "Hellfire", 5, "Magical", "demonbound.hellfire", 4, "skill.demonbound.infernal-blast.lv1", "buff.stun", 1, 10, PrerequisiteBranch:"demonbound.infernal-blast"),
        new SkillData("skill.demonbound.hellfire.lv2", "Hellfire", "Active", 2, 5, 0, 0, "Hellfire", 5, "Magical", "demonbound.hellfire", 4, "skill.demonbound.hellfire.lv1", "buff.stun", 1, 40),
        new SkillData("skill.demonbound.mindfulness.lv1", "Mindfulness", "Passive", 1, 0, 0, 0, "Mindfulness", 0, "None", "demonbound.mindfulness", 0),
        new SkillData("skill.demonbound.mindfulness.lv2", "Mindfulness", "Passive", 2, 0, 0, 0, "Mindfulness", 0, "None", "demonbound.mindfulness", 0, "skill.demonbound.mindfulness.lv1"),
        new SkillData("skill.demonbound.mindfulness.lv3", "Mindfulness", "Passive", 3, 0, 0, 0, "Mindfulness", 0, "None", "demonbound.mindfulness", 0, "skill.demonbound.mindfulness.lv2"),
        new SkillData("skill.demonbound.regeneration.lv1", "Demonic Regeneration", "Active", 1, 5, 0, 0, "DemonicRegeneration", 0, "None", "demonbound.regeneration", 5, "skill.demonbound.mindfulness.lv1", PrerequisiteBranch:"demonbound.mindfulness"),
        new SkillData("skill.demonbound.regeneration.lv2", "Demonic Regeneration", "Active", 2, 5, 0, 0, "DemonicRegeneration", 0, "None", "demonbound.regeneration", 6, "skill.demonbound.regeneration.lv1")
    };

    private static string FileName(string id) => string.Concat(id["skill.".Length..].Split('.', '-')
        .Select(value => char.ToUpperInvariant(value[0]) + value[1..]));
    private static GodotResourceEntry Entry(string id, string type, string path, string[] references) => new()
    {
        ContentIdValue=id, ResourceTypeIdValue=type, ResourceUidValue=ResourceUid.IdToText(Uid(path)),
        DiagnosticPathValue=path, SchemaVersion=1, ReferenceContentIds=references
    };
    private static GodotResourceEntry Copy(GodotResourceEntry value) => new()
    {
        ContentIdValue=value.ContentIdValue, ResourceTypeIdValue=value.ResourceTypeIdValue,
        ResourceUidValue=value.ResourceUidValue, DiagnosticPathValue=value.DiagnosticPathValue,
        SchemaVersion=value.SchemaVersion, ReferenceContentIds=value.ReferenceContentIds.ToArray()
    };
    private static long Uid(string path) { string text=ResourceUid.PathToUid(path); long uid=text.StartsWith("uid://")?ResourceUid.TextToId(text):ResourceUid.CreateIdForPath(path); if(!ResourceUid.HasId(uid))ResourceUid.AddId(uid,path); return uid; }
    private static Texture2D Texture(string path) => ResourceLoader.Load<Texture2D>(path,
        string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Demonbound texture is missing: {path}");
    private static void Save(Resource resource,string path)=>DeterministicResourceSaver.Save(resource,path,Uid(path));
}
#endif
