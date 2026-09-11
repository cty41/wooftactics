#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>Generates the Godot-owned functional Poet content through ResourceSaver.</summary>
public static class PoetAssetFactory
{
    private const string Root = "res://content/poet";
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string RunPath = "res://content/runs/PureRunThreeEncounterV1.tres";
    private const string BalancePath = "res://content/ui/PlayableLv1BalanceProfile.tres";

    private sealed record SkillData(
        string Id, string Name, int Level, int Mana, int MinRange, int MaxRange,
        string Execution, int Damage, string DamageKind, string Branch,
        string Prerequisite = "", string PrerequisiteBranch = "",
        string StatusId = "", int StatusDuration = 0,
        int AreaRadius = 0, int RepeatChance = 0, int RepeatDamage = 0,
        int HealingTicks = 0, int HealingBase = 0, int AttributeModifier = 0,
        int DirectHitCharges = 0, int KillManaRefund = 0, int MaxUsesPerTurn = 0,
        bool CanCrit = false);

    public static void Build()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Root));
        var generated = new List<GodotResourceEntry>();
        var pendingResources = new List<(Resource Resource, string Path)>();
        foreach (SkillData data in Definitions())
        {
            string path = $"{Root}/{FileName(data.Id)}.tres";
            var resource = new SkillDefinitionResource
            {
                SchemaVersion = 1,
                ContentIdValue = data.Id,
                SourceId = $"godot.{data.Id}",
                DisplayName = data.Name,
                Description = data.Name,
                RoleValue = "Poet",
                KindValue = "Active",
                Level = data.Level,
                ManaCost = data.Mana,
                MinRange = data.MinRange,
                MaxRange = data.MaxRange,
                ExecutionKindValue = data.Execution,
                Damage = data.Damage,
                DamageKindValue = data.DamageKind,
                StatusContentIdValue = data.StatusId,
                StatusDuration = data.StatusDuration,
                IsBasicAbility = false,
                MaxUsesPerTurn = data.MaxUsesPerTurn,
                CanCrit = data.CanCrit,
                BranchId = data.Branch,
                PrerequisiteContentIdValue = data.Prerequisite,
                PrerequisiteBranchId = !string.IsNullOrWhiteSpace(data.PrerequisiteBranch)
                    ? data.PrerequisiteBranch : data.Level > 1 ? data.Branch : string.Empty,
                GrowthVisible = true,
                RequiredAttribute = "Strength",
                MinimumAttribute = data.Level > 1 || !string.IsNullOrWhiteSpace(data.PrerequisiteBranch) ? 7 : 5,
                AreaRadius = data.AreaRadius,
                RepeatChancePercent = data.RepeatChance,
                RepeatDamagePercent = data.RepeatDamage,
                HealingTickCount = data.HealingTicks,
                HealingBase = data.HealingBase,
                AttributeModifier = data.AttributeModifier,
                DirectHitCharges = data.DirectHitCharges,
                KillManaRefund = data.KillManaRefund,
                RetreatDistance = 1,
                SummonDefinitionIdValue = data.Execution == "PoetDecoyRetreat"
                    ? "unit.pure-run.poet-decoy" : string.Empty,
                AllowsEmptyTarget = data.Execution == "PoetSwordRain",
                AuthoringSourceKindValue = "GodotAuthored"
            };
            resource.ToCoreDefinition();
            pendingResources.Add((resource, path));
            string[] references = new[] { data.Prerequisite, data.StatusId,
                    resource.SummonDefinitionIdValue }
                .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            generated.Add(Entry(data.Id, "skill", path, references));
        }

        StatusDefinitionResource[] statuses =
        [
            new()
            {
                SchemaVersion = 2, ContentIdValue = "status.poet.wine-heal",
                SourceId = "godot.status.poet.wine-heal", DefaultDuration = 3, CanAct = true,
                PolarityValue = "Beneficial", EffectKindValue = "None", TriggerTimingValue = "TurnStart",
                RefreshStrategyValue = "RefreshDuration", ElementKindValue = "None",
                DamageCategoryValue = "Magic"
            },
            new()
            {
                SchemaVersion = 2, ContentIdValue = "status.poet.agility-verse",
                SourceId = "godot.status.poet.agility-verse", DefaultDuration = 1, CanAct = true,
                PolarityValue = "Beneficial", EffectKindValue = "None", TriggerTimingValue = "None",
                RefreshStrategyValue = "RefreshDuration", ElementKindValue = "None",
                DamageCategoryValue = "Magic"
            }
        ];
        foreach (StatusDefinitionResource status in statuses)
        {
            _ = status.ToCoreDefinition();
            string path = $"{Root}/{FileName(status.ContentIdValue)}.tres";
            pendingResources.Add((status, path));
            generated.Add(Entry(status.ContentIdValue, "buff", path, Array.Empty<string>()));
        }

        UnitDefinitionResource visualTemplate = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/demonbound/PureRunDemonbound.tres", string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Poet placeholder requires the generated Demonbound visual template.");
        UnitDefinitionResource poet = (UnitDefinitionResource)visualTemplate.Duplicate(true);
        poet.ContentIdValue = "unit.pure-run.poet";
        poet.SourceId = "godot.poet";
        poet.DisplayName = "诗人";
        poet.FamilyId = "player";
        poet.RoleId = "poet";
        poet.Strength = 6; poet.Agility = 5; poet.Constitution = 5;
        poet.Intelligence = 4; poet.Charisma = 6; poet.Luck = 4;
        poet.Speed = 5; poet.MaxHealth = 20; poet.MaxMana = 18; poet.StartingMana = 6;
        poet.MoveRange = 4; poet.Initiative = 10; poet.MovementTraitModifier = 0;
        poet.DerivedStatModeValue = "frozen-formula";
        poet.BodyTint = new Color(.48f, .72f, 1f);
        poet.BaseBodyColor = poet.BodyTint;
        ApplyDefaultPortraitCrop(poet);
        poet.ToCoreDefinition();
        const string poetPath = Root + "/PureRunPoet.tres";
        pendingResources.Add((poet, poetPath));
        generated.Add(Entry(poet.ContentIdValue, "unit", poetPath, ["packed-scene.unit-actor"]));

        UnitDefinitionResource decoy = (UnitDefinitionResource)poet.Duplicate(true);
        decoy.ContentIdValue = "unit.pure-run.poet-decoy";
        decoy.SourceId = "godot.poet-decoy";
        decoy.DisplayName = "Poet Afterimage";
        decoy.FamilyId = "summon";
        decoy.RoleId = "poet-decoy";
        decoy.DerivedStatModeValue = "explicit";
        decoy.Strength = 1; decoy.Agility = 1; decoy.Constitution = 1;
        decoy.Intelligence = 1; decoy.Charisma = 1; decoy.Luck = 1;
        decoy.Speed = 0; decoy.MaxHealth = 2; decoy.MaxMana = 0; decoy.StartingMana = 0;
        decoy.MoveRange = 0; decoy.Initiative = 0;
        decoy.CanProduceCorpse = false;
        decoy.DeathTexture = null;
        decoy.BodyTint = new Color(.36f, .84f, 1f, .72f);
        decoy.BaseBodyColor = decoy.BodyTint;
        decoy.ToCoreDefinition();
        const string decoyPath = Root + "/PoetAfterimage.tres";
        pendingResources.Add((decoy, decoyPath));
        generated.Add(Entry(decoy.ContentIdValue, "unit", decoyPath, ["packed-scene.unit-actor"]));

        PureRunDefinitionResource run = ResourceLoader.Load<PureRunDefinitionResource>(RunPath,
            string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Pure Run definition missing.");
        ConfigurePureRun(run, poet.ContentIdValue);
        run.ToCoreDefinition();

        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>(CatalogPath,
            string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Catalog missing.");
        ApplyCatalogUpdates(catalog, generated, run);

        PlayableLv1BalanceProfileResource balance = ResourceLoader.Load<PlayableLv1BalanceProfileResource>(
            BalancePath, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Playable balance profile missing.");
        var unitAttacks = Enumerable.Range(0, balance.UnitContentIds.Length).ToDictionary(
            index => balance.UnitContentIds[index],
            index => (Physical: balance.UnitPhysicalAttacks[index], Magical: balance.UnitMagicalAttacks[index]),
            StringComparer.Ordinal);
        unitAttacks[poet.ContentIdValue] = (4, 2);
        unitAttacks[decoy.ContentIdValue] = (0, 0);
        string[] orderedUnits = unitAttacks.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        balance.UnitContentIds = orderedUnits;
        balance.UnitPhysicalAttacks = orderedUnits.Select(id => unitAttacks[id].Physical).ToArray();
        balance.UnitMagicalAttacks = orderedUnits.Select(id => unitAttacks[id].Magical).ToArray();
        _ = balance.ToCoreProfile();

        // Complete every semantic validation before the first production write. ResourceSaver failures
        // still surface immediately, while invalid later shared resources can no longer leave partial Poet files.
        foreach ((Resource resource, string path) in pendingResources)
            Save(resource, path);
        Save(catalog, CatalogPath);
        Save(run, RunPath);
        Save(balance, BalancePath);
    }

    internal static void ApplyDefaultPortraitCrop(UnitDefinitionResource unit)
    {
        unit.PortraitTextureOverride = null;
        unit.HasPortraitRegion = false;
        unit.PortraitRegion = default;
    }

    internal static void ConfigurePureRun(PureRunDefinitionResource run, string poetContentId)
    {
        run.SchemaVersion = 2;
        run.CharacterIds = ["pure_run_mage", "pure_run_necromancer", "pure_run_amazon", "pure_run_demonbound", "pure_run_poet"];
        run.UnitContentIds = ["unit.pure-run.mage", "unit.pure-run.necromancer", "unit.pure-run.amazon", "unit.pure-run.demonbound", poetContentId];
        run.StartingSkillContentIds = ["skill.mage.fireball.lv1", "skill.necromancer.summon-skeleton.lv1", "skill.amazon.thrust.lv1", "skill.demonbound.bane.lv1", "skill.poet.xiake-xing.lv1"];
        run.StartingSkillChoiceContentIds = PureRunStartingChoices();
        run.SeededStartingSkillFlags = [0, 0, 0, 1, 1];
        run.InherentSkillContentIds = ["", "", "", "skill.demonbound.meditation", ""];
        run.Strengths = [4, 5, 5, 6, 6];
        run.Agilities = [5, 3, 5, 4, 5];
        run.Constitutions = [3, 6, 5, 6, 5];
        run.Intelligences = [6, 6, 5, 6, 4];
        run.Charismas = [6, 6, 5, 6, 6];
        run.Lucks = [6, 4, 5, 2, 4];
    }

    internal static void ApplyCatalogUpdates(
        GodotResourceCatalog catalog,
        IEnumerable<GodotResourceEntry> generated,
        PureRunDefinitionResource run)
    {
        GodotResourceEntry[] generatedEntries = generated.Select(Copy).ToArray();
        Dictionary<string, GodotResourceEntry> entries = catalog.Entries.ToDictionary(
            value => value.ContentIdValue, Copy, StringComparer.Ordinal);
        foreach (GodotResourceEntry entry in generatedEntries)
            entries[entry.ContentIdValue] = entry;

        if (!entries.TryGetValue(run.ContentIdValue, out GodotResourceEntry? runEntry))
            throw new InvalidOperationException($"Catalog has no Pure Run root entry '{run.ContentIdValue}'.");
        runEntry.ReferenceContentIds = PureRunRootReferences(run, generatedEntries);
        catalog.Entries = entries.Values.OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray();
    }

    internal static string[] PureRunRootReferences(
        PureRunDefinitionResource run,
        IEnumerable<GodotResourceEntry> generated) =>
        run.EncounterContentIds
            .Append(run.LayerFourMapContentId)
            .Concat(run.UnitContentIds)
            .Concat(run.StartingSkillContentIds)
            .Concat(run.StartingSkillChoiceContentIds)
            .Concat(run.InherentSkillContentIds)
            .Concat(generated.Select(value => value.ContentIdValue))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static SkillData[] Definitions() =>
    [
        new("skill.poet.xiake-xing.lv1", "侠客行", 1, 6, 1, 3, "PoetCharge", 1, "Physical", "poet.sword-immortal", CanCrit:true),
        new("skill.poet.xiake-xing.lv2", "侠客行", 2, 6, 1, 4, "PoetCharge", 1, "Physical", "poet.sword-immortal", "skill.poet.xiake-xing.lv1", CanCrit:true),
        new("skill.poet.xiake-xing.lv3", "侠客行", 3, 6, 1, 5, "PoetCharge", 1, "Physical", "poet.sword-immortal", "skill.poet.xiake-xing.lv2", KillManaRefund:6, CanCrit:true),
        new("skill.poet.sword-rain.lv1", "剑雨", 1, 12, 0, 4, "PoetSwordRain", 3, "Physical", "poet.sword-rain", "skill.poet.xiake-xing.lv1", "poet.sword-immortal", AreaRadius:2, CanCrit:true),
        new("skill.poet.sword-rain.lv2", "剑雨", 2, 12, 0, 4, "PoetSwordRain", 3, "Physical", "poet.sword-rain", "skill.poet.sword-rain.lv1", AreaRadius:2, RepeatChance:25, RepeatDamage:50, CanCrit:true),

        new("skill.poet.jiang-jin-jiu.lv1", "将进酒", 1, 6, 0, 0, "PoetWineHeal", 0, "None", "poet.poetry-immortal", StatusId:"status.poet.wine-heal", StatusDuration:3, AreaRadius:2, HealingTicks:3, HealingBase:3),
        new("skill.poet.jiang-jin-jiu.lv2", "将进酒", 2, 6, 0, 0, "PoetWineHeal", 0, "None", "poet.poetry-immortal", "skill.poet.jiang-jin-jiu.lv1", StatusId:"status.poet.wine-heal", StatusDuration:3, AreaRadius:2, HealingTicks:3, HealingBase:6),
        new("skill.poet.jiang-jin-jiu.lv3", "将进酒", 3, 5, 0, 0, "PoetWineHeal", 0, "None", "poet.poetry-immortal", "skill.poet.jiang-jin-jiu.lv2", StatusId:"status.poet.wine-heal", StatusDuration:3, AreaRadius:2, HealingTicks:3, HealingBase:6),
        new("skill.poet.road-is-hard.lv1", "行路难", 1, 8, 0, 0, "PoetAgilityVerse", 0, "None", "poet.road-is-hard", "skill.poet.jiang-jin-jiu.lv1", "poet.poetry-immortal", StatusId:"status.poet.agility-verse", StatusDuration:1, AreaRadius:2, AttributeModifier:2),
        new("skill.poet.road-is-hard.lv2", "行路难", 2, 8, 0, 0, "PoetAgilityVerse", 0, "None", "poet.road-is-hard", "skill.poet.road-is-hard.lv1", StatusId:"status.poet.agility-verse", StatusDuration:1, AreaRadius:2, AttributeModifier:4),

        new("skill.poet.moon-drink.lv1", "月下独酌", 1, 6, 0, 0, "PoetMoonDrink", 0, "None", "poet.wine-immortal", MaxUsesPerTurn:1),
        new("skill.poet.moon-drink.lv2", "月下独酌", 2, 4, 0, 0, "PoetMoonDrink", 0, "None", "poet.wine-immortal", "skill.poet.moon-drink.lv1", MaxUsesPerTurn:1),
        new("skill.poet.moon-drink.lv3", "月下独酌", 3, 4, 0, 0, "PoetMoonDrink", 0, "None", "poet.wine-immortal", "skill.poet.moon-drink.lv2", MaxUsesPerTurn:1),
        new("skill.poet.mountain-dialogue.lv1", "山中与幽人对酌", 1, 5, 0, 0, "PoetDecoyRetreat", 0, "None", "poet.mountain-dialogue", "skill.poet.moon-drink.lv1", "poet.wine-immortal", DirectHitCharges:1),
        new("skill.poet.mountain-dialogue.lv2", "山中与幽人对酌", 2, 5, 0, 0, "PoetDecoyRetreat", 0, "None", "poet.mountain-dialogue", "skill.poet.mountain-dialogue.lv1", DirectHitCharges:2)
    ];

    private static string[] PureRunStartingChoices() =>
    [
        "skill.mage.fireball.lv1", "skill.mage.ice-bolt.lv1", "skill.mage.lightning.lv1",
        "skill.necromancer.summon-skeleton.lv1", "skill.necromancer.amplify-damage.lv1", "skill.necromancer.bone-spear.lv1",
        "skill.amazon.thrust.lv1", "skill.poison-spear.lv1", "skill.amazon.combat-techniques.lv1",
        "skill.demonbound.bane.lv1", "skill.demonbound.infernal-blast.lv1", "skill.demonbound.mindfulness.lv1",
        "skill.poet.xiake-xing.lv1", "skill.poet.jiang-jin-jiu.lv1", "skill.poet.moon-drink.lv1"
    ];

    private static string FileName(string id) => string.Concat(id.Split('.', '-', '_')
        .Select(value => char.ToUpperInvariant(value[0]) + value[1..]));

    private static GodotResourceEntry Entry(string id, string type, string path, string[] references) => new()
    {
        ContentIdValue = id,
        ResourceTypeIdValue = type,
        ResourceUidValue = ResourceUid.IdToText(Uid(path)),
        DiagnosticPathValue = path,
        SchemaVersion = 1,
        ReferenceContentIds = references.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray()
    };

    private static GodotResourceEntry Copy(GodotResourceEntry value) => new()
    {
        ContentIdValue = value.ContentIdValue,
        ResourceTypeIdValue = value.ResourceTypeIdValue,
        ResourceUidValue = value.ResourceUidValue,
        DiagnosticPathValue = value.DiagnosticPathValue,
        SchemaVersion = value.SchemaVersion,
        ReferenceContentIds = value.ReferenceContentIds.ToArray()
    };

    private static long Uid(string path)
    {
        string text = ResourceUid.PathToUid(path);
        long uid = text.StartsWith("uid://") ? ResourceUid.TextToId(text) : ResourceUid.CreateIdForPath(path);
        if (!ResourceUid.HasId(uid)) ResourceUid.AddId(uid, path);
        return uid;
    }

    private static void Save(Resource resource, string path) =>
        DeterministicResourceSaver.Save(resource, path, Uid(path));
}
#endif
