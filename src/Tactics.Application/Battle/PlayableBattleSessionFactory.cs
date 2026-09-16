using Tactics.Application.Runs;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Skills;
using Tactics.Core.Turns;
using Tactics.Core.Units;

namespace Tactics.Application.Battle;

/// <summary>Composes migrated catalogs into a deterministic playable battle session.</summary>
public sealed class PlayableBattleSessionFactory
{
    public sealed record ProtectedNpcBattleConfig(ContentId UnitDefinitionId, GridPoint PreferredCell);
    private static readonly ContentId MagicAttackId = new("skill.basic.magic");
    private static readonly ContentId MeleeAttackId = new("skill.basic.melee");
    private static readonly ContentId PickupSpearId = new("skill.amazon.pickup-spear.lv1");
    private static readonly ContentId SkeletonUnitId = new("unit.pure-run.skeleton-warrior");
    private static readonly ContentId SkeletonMageUnitId = new("unit.pure-run.skeleton-mage");
    private static readonly ContentId FireDemonUnitId = new("unit.pure-run.fire-demon");
    private readonly EncounterResolver _encounters = new();

    public PlayableBattleSessionService Create(
        EncounterRequest request,
        EncounterDefinition encounter,
        BattleLayoutDefinition layout,
        IReadOnlyDictionary<ContentId, UnitDefinition> units,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills,
        IReadOnlyDictionary<ContentId, AiDefinition> aiDefinitions,
        PlayableBattleBalanceProfile? balance = null,
        PlayableEnemySpeedProfile? enemySpeed = null,
        IReadOnlyDictionary<ContentId, EquipmentDefinition>? equipment = null,
        ProtectedNpcBattleConfig? protectedNpc = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EncounterContentId != encounter.ContentId)
            throw new ArgumentException("Encounter request does not match the supplied definition.", nameof(encounter));
        ResolvedEncounter resolved = _encounters.Resolve(encounter, layout);
        if (request.Party.Count > layout.PartySpawns.Count)
            throw new ArgumentException("Party exceeds layout spawn capacity.", nameof(request));
        IReadOnlyDictionary<ContentId, SkillDefinition> playableSkills = skills.ToDictionary(
            item => item.Key, item => balance?.Apply(item.Value) ?? item.Value);

        var states = new List<BattleUnitState>();
        var skillsByUnit = new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>();
        var aiByUnit = new Dictionary<UnitInstanceId, AiDefinition>();
        var characterIds = new Dictionary<UnitInstanceId, string>();
        int partyIndex = 0;
        for (int index = 0; index < request.Party.Count; index++)
        {
            RunCharacterState character = request.Party[index];
            if (character.IsDead) continue;
            UnitDefinition definition = units[character.UnitContentId];
            var instanceId = new UnitInstanceId($"party-{character.CharacterId}");
            SkillDefinition[] learnedDefinitions = character.LearnedSkills
                .Select(id => playableSkills[id]).ToArray();
            SkillRole role = learnedDefinitions.Select(skill => skill.Role)
                .FirstOrDefault(value => value != SkillRole.Any);
            ContentId basicId = role is SkillRole.Amazon or SkillRole.Demonbound or SkillRole.Poet
                ? MeleeAttackId : MagicAttackId;
            IEnumerable<ContentId> learned=new[] { basicId }.Concat(character.LearnedSkills);
            if(role == SkillRole.Amazon) learned=learned.Append(PickupSpearId);
            SkillDefinition[] unitSkills = learned.Distinct().Select(id => playableSkills[id]).ToArray();
            BattleUnitState state = CreatePartyState(definition, character, instanceId, layout.PartySpawns[partyIndex], partyIndex,
                balance, equipment, role);
            if (role == SkillRole.Demonbound)
            {
                int mindfulnessLevel = learnedDefinitions
                    .Where(skill => skill.ExecutionKind == SkillExecutionKind.Mindfulness)
                    .Select(skill => skill.Level).DefaultIfEmpty(0).Max();
                state = state.WithDemonboundState(new DemonboundBattleState(mindfulnessLevel: mindfulnessLevel));
            }
            partyIndex++;
            states.Add(state);
            characterIds.Add(instanceId, character.CharacterId);
            skillsByUnit.Add(instanceId, unitSkills);
        }

        for (int index = 0; index < resolved.Enemies.Count; index++)
        {
            (EncounterMonsterDefinition monster, GridPoint cell) = resolved.Enemies[index];
            var instanceId = new UnitInstanceId($"enemy-{index:D2}");
            UnitDefinition enemyDefinition = units[monster.UnitId];
            BattleUnitState enemy = enemyDefinition.CreateBattleState(instanceId, cell, 1, request.Party.Count + index);
            int scaledHealth = (int)Math.Ceiling(enemy.MaxHealth * encounter.HealthMultiplier);
            enemy = enemy.WithHealthAndMana(scaledHealth, scaledHealth,
                enemy.MaxMana, Math.Min(enemy.MaxMana, Math.Max(enemy.CurrentMana, encounter.MinimumStartingMana)))
                .WithDamageOutputMultiplier(encounter.OutputMultiplier);
            states.Add(enemy);
            skillsByUnit.Add(instanceId, monster.SkillIds.Select(id => playableSkills[id]).ToArray());
            aiByUnit.Add(instanceId, aiDefinitions[monster.AiId]);
        }

        UnitInstanceId? protectedNpcUnitId = null;
        if (protectedNpc is not null)
        {
            protectedNpcUnitId = new UnitInstanceId("escort-lost-villager");
            UnitDefinition npcDefinition = units[protectedNpc.UnitDefinitionId];
            HashSet<GridPoint> occupied = states.Select(value => value.Unit.Position).ToHashSet();
            GridPoint spawn = Enumerable.Range(0, 10).SelectMany(y => Enumerable.Range(0, 10)
                    .Select(x => new GridPoint(x, y)))
                .Where(cell => !layout.BlockedCells.Contains(cell) && !occupied.Contains(cell))
                .OrderBy(cell => Math.Abs(cell.X - protectedNpc.PreferredCell.X) +
                                 Math.Abs(cell.Y - protectedNpc.PreferredCell.Y))
                .ThenBy(cell => cell.Y).ThenBy(cell => cell.X).First();
            UnitState facts = new(protectedNpcUnitId.Value, npcDefinition.ContentId, spawn,
                npcDefinition.DerivedStats.MoveRange, npcDefinition.DerivedStats.Initiative, 0, request.Party.Count, true,
                npcDefinition.MovementKind);
            BattleUnitState npc = new(facts, npcDefinition.DerivedStats.MaxHealth, npcDefinition.DerivedStats.MaxHealth,
                baseSpeed: npcDefinition.Speed,
                physicalAttack: 0, magicalAttack: 0, canProduceCorpse: false);
            states.Add(npc);
            skillsByUnit.Add(protectedNpcUnitId.Value, Array.Empty<SkillDefinition>());
            aiByUnit.Add(protectedNpcUnitId.Value, new AiDefinition(new ContentId("ai.escort.flee"),
                AiArchetype.Support, new AiProfileDefinition(0, 0, 0, 0),
                Array.Empty<ContentId>(), Array.Empty<ContentId>()));
        }

        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 10; x++)
        for (int y = 0; y < 10; y++)
        {
            var cell = new GridPoint(x, y);
            bool blocked = layout.BlockedCells.Contains(cell);
            cells[cell] = new CellState(blocksMovement: blocked, blocksLineOfSight: blocked,
                terrain: layout.ShallowWater.Contains(cell) ? TerrainKind.ShallowWater : TerrainKind.Ground);
        }
        UnitInstanceId[] order = InitiativeOrder.Sort(states.Select(state => new InitiativeEntry(
            state.Unit.InstanceId, state.Unit.Initiative, state.Unit.PlayerNumber, state.Unit.SpawnOrdinal)))
            .Select(entry => entry.UnitId).ToArray();
        var battle = new BattleState(new BoardSnapshot(cells), states, order,
            randomState: request.EffectiveRandomState);
        var summonControllers = new Dictionary<ContentId, SummonControllerDefinition>
        {
            [SkeletonUnitId] = new(aiDefinitions[new ContentId("ai.summon.basic-melee")],
                Levels(playableSkills, "skill.summon.skeleton-attack", 3), SkillExecutionKind.SummonSkeleton),
            [SkeletonMageUnitId] = new(aiDefinitions[new ContentId("ai.summon.fire-demon")],
                Levels(playableSkills, "skill.summon.skeleton-mage-fireball"), SkillExecutionKind.SummonSkeletonMage),
            [FireDemonUnitId] = new(aiDefinitions[new ContentId("ai.summon.fire-demon")],
                new Dictionary<int, SkillDefinition>
                {
                    [1] = playableSkills[new ContentId("skill.summon.fire-demon-attack")],
                    [2] = playableSkills[new ContentId("skill.summon.fire-demon-attack")]
                }, SkillExecutionKind.SummonFireDemon)
        };
        return new PlayableBattleSessionService(new PlayableBattleSessionContext(
            battle, 0, skillsByUnit, aiByUnit, playableSkills, request, characterIds, layout.BlockedCells,
            summonControllers, protectedNpcUnitId, layout.ShallowWater));
    }

    private static IReadOnlyDictionary<int, SkillDefinition> Levels(
        IReadOnlyDictionary<ContentId, SkillDefinition> skills, string prefix, int maximumLevel = 2) =>
        Enumerable.Range(1, maximumLevel).ToDictionary(level => level,
            level => skills[new ContentId(prefix + $".lv{level}")]);

    private static BattleUnitState CreatePartyState(
        UnitDefinition definition,
        RunCharacterState character,
        UnitInstanceId instanceId,
        GridPoint cell,
        int spawnOrdinal,
        PlayableBattleBalanceProfile? balance,
        IReadOnlyDictionary<ContentId, EquipmentDefinition>? equipment,
        SkillRole role)
    {
        EquipmentDefinition[] loadout = character.Equipment.Select(item =>
            equipment?.GetValueOrDefault(item.DefinitionId) ??
            throw new ArgumentException($"Equipment definition '{item.DefinitionId}' is unavailable.", nameof(equipment)))
            .ToArray();
        EquipmentStatProjection projection = EquipmentStatProjector.Project(character.Attributes, definition.Speed, loadout);
        UnitDerivedStats battleDerived = ResolvePartyDerivedStats(definition, projection);
        var facts = new UnitState(
            instanceId, definition.ContentId, cell, battleDerived.MoveRange,
            battleDerived.Initiative, 0, spawnOrdinal, !character.IsDead, definition.MovementKind,
            projection.Attributes, combatRole: role);
        IReadOnlyDictionary<ItemInstanceId, BattleConsumableState> consumables = character.CarriedConsumables
            .ToDictionary(item => item.InstanceId);
        (int physical, int magical) = balance?.Attacks(definition.ContentId) ?? (2, 2);
        BattleUnitState state=new BattleUnitState(
            facts, character.MaxHealth, character.CurrentHealth,
            maxMana: character.MaxMana, currentMana: character.CurrentMana,
            baseSpeed: definition.Speed, consumables: consumables,
            physicalAttack: physical, magicalAttack: magical,
            canProduceCorpse: definition.CanProduceCorpse,
            manaRecoveryPerTurn: projection.Attributes.Intelligence,
            primaryAttributeDamageBonus: CalculatePrimaryAttributeDamageBonus(projection.Attributes, role));
        int combatTechniquesLevel = character.LearnedSkills
            .Where(id => id.Value.StartsWith("skill.amazon.combat-techniques.lv", StringComparison.Ordinal))
            .Select(id => id.Value.EndsWith("lv2", StringComparison.Ordinal) ? 2 : 1)
            .DefaultIfEmpty(0).Max();
        return combatTechniquesLevel > 0 ? state.WithCombatTechniquesLevel(combatTechniquesLevel) : state;
    }

    public static int CalculatePrimaryAttributeDamageBonus(UnitAttributes attributes, SkillRole role)
    {
        int primary = role switch
        {
            SkillRole.Mage => attributes.Intelligence,
            SkillRole.Necromancer => attributes.Constitution,
            SkillRole.Demonbound => attributes.Charisma,
            SkillRole.Amazon => attributes.Agility,
            SkillRole.Poet => attributes.Strength,
            _ => 5
        };
        return Math.Max(0, primary - 5);
    }

    public static UnitDerivedStats ResolvePartyDerivedStats(
        UnitDefinition definition, EquipmentStatProjection projection) =>
        definition.DerivedStatMode == UnitDerivedStatMode.Explicit
            ? new UnitDerivedStats(projection.DerivedStats.MaxHealth, projection.DerivedStats.MaxMana,
                projection.DerivedStats.StartingMana, definition.DerivedStats.MoveRange,
                definition.DerivedStats.Initiative)
            : projection.DerivedStats;
}
