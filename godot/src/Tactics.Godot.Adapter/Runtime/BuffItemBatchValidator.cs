using Godot;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Statuses;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record BuffItemBatchValidation(
    int BatchCatalogEntryCount,
    int GlobalCatalogEntryCount,
    int StatusCount,
    int ConsumableCount,
    int EquipmentCount);

/// <summary>
/// Validates generated Buff/Item resources, external Poison ownership, and Core runtime composition.
/// </summary>
public static class BuffItemBatchValidator
{
    public static BuffItemBatchValidation Validate(
        GodotResourceCatalog batchCatalog,
        GodotResourceCatalog globalCatalog)
    {
        ArgumentNullException.ThrowIfNull(batchCatalog);
        ArgumentNullException.ThrowIfNull(globalCatalog);
        GodotCatalogCompilation batch = GodotCatalogCompiler.Compile(batchCatalog);
        GodotCatalogCompilation global = GodotCatalogCompiler.Compile(globalCatalog);
        if (batch.Snapshot.Entries.Count != 29 || global.Snapshot.Entries.Count is not (47 or 58 or 73 or 74 or 101 or 108 or 114 or 115 or 116 or 119 or 123 or 124 or 125 or 131 or 132 or 141 or 142 or 143 or 160 or 161 or 162 or 166 or 185))
            throw new InvalidOperationException("Buff/Item or canonical global Catalog entry count is invalid.");
        if (global.Snapshot.Entries.Keys.Distinct().Count() != global.Snapshot.Entries.Count)
            throw new InvalidOperationException("Canonical global Catalog contains duplicate ContentIds.");

        GodotResourceEntry[] statusEntries = batchCatalog.Entries
            .Where(entry => entry.ResourceTypeIdValue == "buff")
            .OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal)
            .ToArray();
        GodotResourceEntry[] itemEntries = batchCatalog.Entries
            .Where(entry => entry.ResourceTypeIdValue == "item")
            .OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal)
            .ToArray();
        if (statusEntries.Length != 14 || itemEntries.Length != 15)
            throw new InvalidOperationException("Buff/Item Catalog must contain 14 Buffs and 15 Items.");

        var statuses = new Dictionary<ContentId, StatusDefinition>();
        foreach (GodotResourceEntry entry in statusEntries)
        {
            Resource resource = batch.Resources.Resources[new ContentId(entry.ContentIdValue)];
            StatusDefinition definition = resource switch
            {
                StatusDefinitionResource status => status.ToCoreDefinition(),
                PoisonBuffResource poison when entry.ContentIdValue == "buff.poison" => PoisonDefinition(poison),
                _ => throw new InvalidOperationException(
                    $"Buff '{entry.ContentIdValue}' has the wrong generated Resource type.")
            };
            statuses.Add(definition.ContentId, definition);
        }

        ConsumableDefinition[] consumables = itemEntries
            .Select(entry => batch.Resources.Resources[new ContentId(entry.ContentIdValue)])
            .OfType<ConsumableDefinitionResource>()
            .Select(resource => resource.ToCoreDefinition())
            .OrderBy(definition => definition.ContentId.Value, StringComparer.Ordinal)
            .ToArray();
        EquipmentDefinition[] equipment = itemEntries
            .Select(entry => batch.Resources.Resources[new ContentId(entry.ContentIdValue)])
            .OfType<EquipmentDefinitionResource>()
            .Select(resource => resource.ToCoreDefinition())
            .OrderBy(definition => definition.ContentId.Value, StringComparer.Ordinal)
            .ToArray();
        if (consumables.Length != 3 || equipment.Length != 12)
            throw new InvalidOperationException("Generated Item Resource types are incomplete.");
        if (batchCatalog.Entries.Single(entry => entry.ContentIdValue == "buff.poison").DiagnosticPathValue !=
            "res://content/poison_spear/PoisonBuff.tres")
        {
            throw new InvalidOperationException("Buff/Item batch created a second Poison resource.");
        }

        ValidateStatusRuntime(statuses);
        ValidateConsumableRuntime(consumables);
        ValidateEquipmentRuntime(equipment);
        return new BuffItemBatchValidation(
            batch.Snapshot.Entries.Count,
            global.Snapshot.Entries.Count,
            statuses.Count,
            consumables.Length,
            equipment.Length);
    }

    private static StatusDefinition PoisonDefinition(PoisonBuffResource poison)
    {
        if (poison.SchemaVersion != 1 || poison.DefaultDuration != 3 || poison.DamagePerTurn != 2 ||
            poison.EffectType != "Poison" || poison.Polarity != "Harmful" ||
            poison.RefreshStrategy != "AddDuration" || poison.TriggerTiming != "TurnStart")
        {
            throw new InvalidOperationException("Externally owned Poison resource drifted from the frozen contract.");
        }
        return new StatusDefinition(
            poison.ContentId,
            "Poison",
            poison.DefaultDuration,
            canAct: true,
            StatusPolarity.Harmful,
            StatusEffectKind.Poison,
            StatusTriggerTiming.TurnStart,
            StatusRefreshStrategy.AddDuration,
            damagePerTurn: poison.DamagePerTurn);
    }

    private static void ValidateStatusRuntime(IReadOnlyDictionary<ContentId, StatusDefinition> statuses)
    {
        var runtime = new StatusRuntimeService();
        BattleUnitState target = Unit("validation.status.target", new GridPoint(3, 1), player: 1);
        UnitInstanceId source = new("validation.status.source");
        target = runtime.Apply(target, statuses[new ContentId("buff.poison")], source).Unit;
        target = runtime.Apply(target, statuses[new ContentId("buff.ignite")], source).Unit;
        target = runtime.Apply(target, statuses[new ContentId("buff.slow")], source).Unit;
        if (target.Statuses[new ContentId("buff.poison")].RemainingTurns != 3 ||
            target.Statuses[new ContentId("buff.ignite")].StackCount != 2 ||
            target.Unit.Initiative != 6f)
        {
            throw new InvalidOperationException("Generated Status definitions fail the deterministic runtime contract.");
        }
        BattleUnitState cleansed = runtime.RemoveHarmful(target, out IReadOnlyList<ContentId> removed);
        if (removed.Count != 3 || cleansed.Statuses.Count != 0 || cleansed.Unit.Initiative != 10f)
            throw new InvalidOperationException("Generated Status definitions fail harmful cleanse or Slow restoration.");
    }

    private static void ValidateConsumableRuntime(IReadOnlyList<ConsumableDefinition> consumables)
    {
        ConsumableDefinition life = consumables.Single(definition =>
            definition.ContentId == new ContentId("item.consumable.life-potion"));
        ItemInstanceId itemId = new("validation.life-potion.0");
        BattleUnitState actor = Unit("validation.item.actor", new GridPoint(1, 1), player: 0)
            .WithConsumable(new BattleConsumableState(itemId, life.ContentId, life.MaxCharges, life.MaxCharges));
        BattleUnitState target = Unit("validation.item.target", new GridPoint(2, 1), player: 0).WithHealth(12);
        BattleState state = State(actor, target);
        BattleTransition transition = new BattleTransitionService().Apply(
            state,
            new UseConsumableCommand(actor.Unit.InstanceId, target.Unit.InstanceId, itemId, life));
        if (!transition.Succeeded || transition.State.Units[target.Unit.InstanceId].CurrentHealth != 20 ||
            transition.State.Units[actor.Unit.InstanceId].Consumables[itemId].RemainingCharges != 0)
        {
            throw new InvalidOperationException("Generated Consumable definitions fail deterministic use resolution.");
        }
    }

    private static void ValidateEquipmentRuntime(IReadOnlyList<EquipmentDefinition> equipment)
    {
        EquipmentDefinition sword = equipment.Single(definition =>
            definition.ContentId == new ContentId("item.equipment.sword-01"));
        EquipmentDefinition armor = equipment.Single(definition =>
            definition.ContentId == new ContentId("item.equipment.leather-armor-01"));
        EquipmentStatProjection projection = EquipmentStatProjector.Project(
            new UnitAttributes(5, 5, 5, 5, 5, 5),
            5f,
            new[] { sword, armor });
        if (projection.Attributes != new UnitAttributes(10, 7, 8, 5, 5, 5) ||
            projection.DerivedStats != UnitDerivedStatRules.Calculate(projection.Attributes, 5f))
        {
            throw new InvalidOperationException("Generated Equipment definitions fail the derived-stat projection.");
        }
    }

    private static BattleUnitState Unit(string id, GridPoint point, int player) => new(
        new UnitState(
            new UnitInstanceId(id),
            new ContentId(player == 0 ? "unit.validation.party" : "unit.validation.enemy"),
            point,
            moveRange: 3,
            initiative: 10f,
            player,
            spawnOrdinal: player),
        maxHealth: 20,
        currentHealth: 20,
        maxMana: 10,
        currentMana: 10,
        baseSpeed: 5f);

    private static BattleState State(params BattleUnitState[] units)
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        return new BattleState(
            new BoardSnapshot(cells),
            units,
            units.Select(unit => unit.Unit.InstanceId).ToArray());
    }
}
