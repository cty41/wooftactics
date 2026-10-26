#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>Creates the Godot-authored Maw Bat vertical slice through ResourceSaver.</summary>
public static class MawBatAssetFactory
{
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string UnitCatalogPath = "res://content/units/ContentCatalog.tres";
    private const string AiCatalogPath = "res://content/ai_encounters/ContentCatalog.tres";
    private const string UnitPath = "res://content/units/PureRunMawBat.tres";
    private const string SkillPath = "res://content/ai_encounters/SkillEnemyMawBatBiteLv1.tres";
    private const string AiPath = "res://content/ai_encounters/AiPureRunMawBatPredatoryDiver.tres";
    private const string LayoutPath = "res://content/ai_encounters/BattleLayoutPureRunN2ShallowWater.tres";
    private const string EncounterPath = "res://content/ai_encounters/EncounterPureRunN2.tres";
    // Same living virtual-landing pivot as UnitDefinitionDraft / UnitAssetFactory (foot of 256 canvas).
    private const float LivingPivotX = 0.5f;
    private const float LivingPivotY = 0.078125f;

    public static void Build()
    {
        PackedScene actor = Load<PackedScene>("res://content/units/UnitActor.tscn");
        UnitDefinitionResource template = Load<UnitDefinitionResource>("res://content/units/PureRunGoatRanged.tres");
        var unit = (UnitDefinitionResource)template.Duplicate(true);
        unit.ContentIdValue = "unit.pure-run.maw-bat"; unit.SourceId = "godot.maw_bat";
        unit.DisplayName = "大嘴蝠"; unit.Category = "Enemy"; unit.FamilyId = "bat"; unit.RoleId = "predatory-diver";
        unit.Strength = 5; unit.Agility = 6; unit.Constitution = 4; unit.Intelligence = 2; unit.Charisma = 2; unit.Luck = 3;
        unit.Speed = 12; unit.MaxHealth = 14; unit.MaxMana = 0; unit.StartingMana = 0; unit.MoveRange = 5; unit.Initiative = 24;
        unit.DerivedStatModeValue = "explicit"; unit.AttackRange = 1; unit.AttackFactor = 1; unit.DefenceFactor = 1;
        unit.MovementKindValue = "air"; unit.CanProduceCorpse = true; unit.ActorContentIdValue = "packed-scene.unit-actor"; unit.ActorScene = actor;
        unit.DownRightTexture = Load<Texture2D>("res://assets/units/tomb_maw_bat_ranged_color_v06.png");
        unit.UpLeftTexture = Load<Texture2D>("res://assets/units/tomb_maw_bat_ranged_color_ul_v01.png");
        unit.DeathTexture = Load<Texture2D>("res://assets/units/tomb_maw_bat_ranged_death_color_v02.png");
        unit.MeleeDownRightTexture = Load<Texture2D>("res://assets/units/actions/tomb_maw_bat_melee_bite_attack_dr_v01.png");
        unit.MeleeUpLeftTexture = Load<Texture2D>("res://assets/units/actions/tomb_maw_bat_melee_bite_attack_ul_v01.png");
        unit.HitDownRightTexture = Load<Texture2D>("res://assets/units/actions/tomb_maw_bat_hit_dr_v01.png");
        unit.HitUpLeftTexture = Load<Texture2D>("res://assets/units/actions/tomb_maw_bat_hit_ul_v01.png");
        unit.UnarmedDownRightTexture = unit.UnarmedUpLeftTexture = null;
        unit.RangedDownRightTexture = unit.RangedUpLeftTexture = unit.CastDownRightTexture = unit.CastUpLeftTexture = null;
        unit.BodyTintModeValue = "multiply"; unit.BodyTintMaterial = null; unit.BodyTint = Colors.White; unit.BaseBodyColor = Colors.White;
        // Living poses share the ground-unit virtual landing pivot; death uses corpse AABB center (zero offset).
        unit.DownRightBodyOffset = SpriteOffset(unit.DownRightTexture!, LivingPivotX, LivingPivotY);
        unit.UpLeftBodyOffset = SpriteOffset(unit.UpLeftTexture!, LivingPivotX, LivingPivotY);
        unit.DeathBodyOffset = Vector2.Zero;
        unit.ToCoreDefinition(); Save(unit, UnitPath);

        var skill = new SkillDefinitionResource
        {
            ContentIdValue = "skill.enemy.maw-bat-bite.lv1", SourceId = "godot.maw_bat_bite", DisplayName = "咬击",
            Description = "近身咬击，并按实际生命伤害的 50% 恢复自身生命。", RoleValue = "Any", KindValue = "Active",
            Level = 1, ManaCost = 0, MinRange = 1, MaxRange = 1, ExecutionKindValue = "DirectAttack",
            Damage = 4, DamageKindValue = "Physical", IsBasicAbility = false, MaxUsesPerTurn = 1, CanCrit = true,
            BranchId = "enemy.maw-bat-bite", LifeStealPercent = 50, AuthoringSourceKindValue = "GodotAuthored"
        };
        skill.ToCoreDefinition(); Save(skill, SkillPath);

        var ai = new AiDefinitionResource
        {
            ContentIdValue = "ai.pure-run.maw-bat-predatory-diver", ArchetypeValue = "PredatoryDiver",
            SkillContentIds = new[] { skill.ContentIdValue }, PatternSkillContentIds = Array.Empty<string>(),
            DistanceWeight = 1, DamageWeight = 1, MaximumEngageCandidatesPerTarget = 3,
            PreferredMinimumRange = 1, PreferredMaximumRange = 1,
            DecisionGraphHash = "godot.maw-bat-predatory-diver.v1",
            DecisionGraphJson = """
                {
                  "dependencyHash": "godot.maw-bat-predatory-diver.v1",
                  "nodes": [
                    { "nodeId": "1", "kind": "intent", "type": "BasicAttack", "enabled": true, "basePriority": 25.0 },
                    { "nodeId": "2", "kind": "intent", "type": "Engage", "enabled": true, "basePriority": 15.0 },
                    { "nodeId": "3", "kind": "intent", "type": "HoldPosition", "enabled": true, "basePriority": 1.0 }
                  ],
                  "edges": []
                }
                """
        };
        _ = ai.ToCoreDefinition(); Save(ai, AiPath);

        var layout = new BattleLayoutResource
        {
            SchemaVersion = 2, ContentIdValue = "battle-layout.pure-run.n2-shallow-water",
            PartySpawnsValue = "1,4;1,5;2,4", EnemySpawnsValue = "6,4;7,3;7,5;8,4", BlockedCellsValue = string.Empty,
            ShallowWaterCellsValue = "3,2;3,3;4,3;4,4;4,5;5,4;5,5;5,6"
        };
        _ = layout.ToCoreDefinition(); Save(layout, LayoutPath);

        EncounterDefinitionResource encounter = Load<EncounterDefinitionResource>(EncounterPath);
        encounter.LayoutContentId = layout.ContentIdValue;
        encounter.MonsterUnitContentIds = new[] { unit.ContentIdValue, "unit.pure-run.goat-ranged", "unit.pure-run.goat-support" };
        encounter.MonsterAiContentIds = new[] { ai.ContentIdValue, "ai.pure-run.ranged", "ai.pure-run.support" };
        Save(encounter, EncounterPath);

        UpsertCatalog(UnitCatalogPath, new[] { Entry(unit.ContentIdValue, "unit", UnitPath, "packed-scene.unit-actor") });
        UpsertCatalog(AiCatalogPath, new[]
        {
            Entry(skill.ContentIdValue, "skill", SkillPath), Entry(ai.ContentIdValue, "ai", AiPath, skill.ContentIdValue),
            Entry(layout.ContentIdValue, "battle-layout", LayoutPath),
            Entry(encounter.ContentIdValue, "encounter", EncounterPath, layout.ContentIdValue, unit.ContentIdValue,
                "unit.pure-run.goat-ranged", "unit.pure-run.goat-support", ai.ContentIdValue, "ai.pure-run.ranged", "ai.pure-run.support")
        });
        UpsertCatalog(CatalogPath, new[]
        {
            Entry(unit.ContentIdValue, "unit", UnitPath, "packed-scene.unit-actor"),
            Entry(skill.ContentIdValue, "skill", SkillPath), Entry(ai.ContentIdValue, "ai", AiPath, skill.ContentIdValue),
            Entry(layout.ContentIdValue, "battle-layout", LayoutPath),
            Entry(encounter.ContentIdValue, "encounter", EncounterPath, layout.ContentIdValue, unit.ContentIdValue,
                "unit.pure-run.goat-ranged", "unit.pure-run.goat-support", ai.ContentIdValue, "ai.pure-run.ranged", "ai.pure-run.support")
        });
    }

    private static void UpsertCatalog(string path, IEnumerable<GodotResourceEntry> additions)
    {
        GodotResourceCatalog catalog = Load<GodotResourceCatalog>(path);
        Dictionary<string, GodotResourceEntry> entries = catalog.Entries.ToDictionary(value => value.ContentIdValue, Copy, StringComparer.Ordinal);
        foreach (GodotResourceEntry entry in additions) entries[entry.ContentIdValue] = entry;
        var updated = new GodotResourceCatalog { Entries = entries.Values.OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray() };
        Save(updated, path); updated.Validate();
    }

    private static T Load<T>(string path) where T : Resource => ResourceLoader.Load<T>(path, string.Empty,
        ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Missing resource '{path}'.");
    private static GodotResourceEntry Entry(string id, string type, string path, params string[] references) => new()
    {
        ContentIdValue = id, ResourceTypeIdValue = type, ResourceUidValue = ResourceUid.IdToText(Uid(path)),
        DiagnosticPathValue = path, SchemaVersion = type == "battle-layout" ? 2 : 1,
        ReferenceContentIds = references.Order(StringComparer.Ordinal).ToArray()
    };
    private static GodotResourceEntry Copy(GodotResourceEntry value) => new()
    {
        ContentIdValue = value.ContentIdValue, ResourceTypeIdValue = value.ResourceTypeIdValue,
        ResourceUidValue = value.ResourceUidValue, DiagnosticPathValue = value.DiagnosticPathValue,
        SchemaVersion = value.SchemaVersion, ReferenceContentIds = value.ReferenceContentIds.ToArray()
    };
    private static long Uid(string path)
    {
        string text = ResourceUid.PathToUid(path); long uid = text.StartsWith("uid://", StringComparison.Ordinal)
            ? ResourceUid.TextToId(text) : ResourceUid.CreateIdForPath(path);
        if (!ResourceUid.HasId(uid)) ResourceUid.AddId(uid, path); return uid;
    }
    private static void Save(Resource value, string path) => DeterministicResourceSaver.Save(value, path, Uid(path));

    private static Vector2 SpriteOffset(Texture2D texture, float pivotX, float pivotY) => new(
        texture.GetWidth() * (0.5f - pivotX),
        texture.GetHeight() * (pivotY - 0.5f));
}
#endif
