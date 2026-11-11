using Tactics.Core.Content;
using Tactics.Core.Items;

namespace Tactics.Core.Runs;

public sealed class PureRunSettlementService
{
    public const string ContractId = "pure-run-settlement-v1";
    private const int GoldCap = 50;
    /// <summary>Design cap carried over from <c>SkillSystem.MaxCharacterLevel</c>; a character at the cap no longer levels up.</summary>
    public const int MaxCharacterLevel = 12;

    public PureRunSettlementResult Apply(
        PureRunDefinition definition,
        PureRunState state,
        PureRunBattleResult result,
        IReadOnlyList<ContentId> consumableDropPool,
        PureRunMapDefinition? mapDefinition = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(consumableDropPool);
        string transactionKey = $"battle:{result.EncounterContentId.Value}:settlement";
        if (state.AppliedTransactionKeys.Contains(transactionKey, StringComparer.Ordinal))
            return new PureRunSettlementResult(true, null, state, null, true);
        string? rejection = Validate(state, result);
        if (rejection is not null)
            return new PureRunSettlementResult(false, rejection, state, null, false);

        RunCharacterState[] party = MergeParty(state, result, recoverAfterVictory: result.PlayerVictory);
        string[] transactions = state.AppliedTransactionKeys.Append(transactionKey)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!result.PlayerVictory)
        {
            PureRunSummary defeated = CreateSummary(state, PureRunOutcome.Defeated, party, transactions);
            return new PureRunSettlementResult(true, null, null, defeated, false);
        }

        int reward = CalculateGold(result.TotalRounds);
        int nextGold = Math.Min(GoldCap, checked(state.Gold + reward));
        ContentId? drop = RollDrop(state.Seed, state.EncounterContentId, consumableDropPool);
        IReadOnlyList<ContentId> acquired = drop is null
            ? state.AcquiredItems
            : state.AcquiredItems.Append(drop.Value).OrderBy(value => value.Value, StringComparer.Ordinal).ToArray();
        BattleConsumableState[] backpack = state.BackpackConsumables.ToArray();
        if (drop is not null)
        {
            string instanceId = $"drop-{state.EncounterIndex + 1}-{drop.Value.Value}";
            backpack = backpack.Append(new BattleConsumableState(
                new ItemInstanceId(instanceId), drop.Value, remainingCharges: 1, maxCharges: 1)).ToArray();
        }

        string? progressionTarget = ProgressionTarget(party, result);
        PendingProgression[] progression = progressionTarget is null
            ? state.PendingProgression.ToArray()
            : state.PendingProgression.Append(new PendingProgression(
                $"progression:{state.EncounterContentId.Value}", state.EncounterContentId.Value, progressionTarget)).ToArray();
        int battles = state.BattlesCompleted + 1;
        int defeatedEnemies = checked(state.EnemiesDefeated + result.EnemiesDefeated);
        if (state.EncounterIndex == definition.Encounters.Count - 1)
        {
            if (definition.LayerFourMapContentId is not null)
            {
                if (mapDefinition is null || mapDefinition.ContentId != definition.LayerFourMapContentId.Value)
                    return new PureRunSettlementResult(false, "run.layer_four_map_invalid", state, null, false);
                PureRunMapState mapState = new PureRunMapService(mapDefinition).UnlockLayerFour(state.Seed);
                var awaitingMap = new PureRunState(
                    state.RunId, state.Seed, state.Revision + 1, PureRunPhase.AwaitingLayerFourChoice,
                    state.EncounterIndex, state.EncounterContentId, party, backpack, state.BackpackEquipment,
                    progression, transactions, nextGold, battles, defeatedEnemies, acquired,
                    mapState: mapState, escortState: state.EscortState, adventureState: state.AdventureState);
                return new PureRunSettlementResult(true, null, awaitingMap, null, false);
            }
            var terminalState = new PureRunState(
                state.RunId, state.Seed, state.Revision + 1, PureRunPhase.SliceCompleted,
                state.EncounterIndex, state.EncounterContentId, party, backpack, state.BackpackEquipment,
                progression, transactions, nextGold, battles, defeatedEnemies, acquired,
                escortState: state.EscortState, adventureState: state.AdventureState);
            return new PureRunSettlementResult(
                true, null, null, CreateSummary(terminalState, PureRunOutcome.SliceCompleted, party, transactions), false);
        }

        int nextIndex = state.EncounterIndex + 1;
        var next = new PureRunState(
            state.RunId, state.Seed, state.Revision + 1, PureRunPhase.Ready,
            nextIndex, definition.Encounters[nextIndex], party, backpack, state.BackpackEquipment,
            progression, transactions, nextGold, battles, defeatedEnemies, acquired,
            escortState: state.EscortState, adventureState: state.AdventureState);
        return new PureRunSettlementResult(true, null, next, null, false);
    }

    public PureRunSummary Abandon(PureRunState state) =>
        CreateSummary(state, PureRunOutcome.Abandoned, state.Party, state.AppliedTransactionKeys);

    public PureRunSummary DefeatOutsideBattle(PureRunState state) =>
        CreateSummary(state, PureRunOutcome.Defeated, state.Party, state.AppliedTransactionKeys);

    public PureRunSettlementResult ApplyLayerFour(PureRunState state, PureRunBattleResult result,
        IReadOnlyList<ContentId> consumableDropPool)
    {
        if (state.NodeTransaction is not { Kind: PureRunNodeKind.Battle or PureRunNodeKind.Mystery } transaction ||
            state.MapState?.SelectedNodeId != transaction.NodeId)
            return new(false, "node.transaction_invalid", state, null, false);
        string battleKey = $"battle:{result.EncounterContentId.Value}:settlement";
        if (state.AppliedTransactionKeys.Contains(transaction.TransactionKey, StringComparer.Ordinal))
            return new(true, null, state, null, true);
        string? rejection = Validate(state, result);
        if (rejection is not null) return new(false, rejection, state, null, false);
        RunCharacterState[] party = MergeParty(state, result, result.PlayerVictory);
        string[] transactions = state.AppliedTransactionKeys.Append(battleKey).Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!result.PlayerVictory)
        {
            var defeatedState = new PureRunState(state.RunId, state.Seed, state.Revision + 1,
                PureRunPhase.Defeated, state.EncounterIndex, state.EncounterContentId, party,
                state.BackpackConsumables, state.BackpackEquipment, state.PendingProgression, transactions,
                state.Gold, state.BattlesCompleted, state.EnemiesDefeated, state.AcquiredItems,
                mapState: state.MapState, nodeTransaction: state.NodeTransaction, escortState: state.EscortState,
                adventureState: state.AdventureState);
            return new(true, null, null, CreateSummary(defeatedState, PureRunOutcome.Defeated, party, transactions), false);
        }
        int gold = Math.Min(GoldCap, checked(state.Gold + CalculateGold(result.TotalRounds)));
        bool elite = state.EncounterIndex is 4 or 5;
        ContentId? drop = RollDrop(state.Seed, result.EncounterContentId, consumableDropPool, elite ? 0.30 : 0.25);
        BattleConsumableState[] backpack = state.BackpackConsumables.ToArray();
        IReadOnlyList<ContentId> acquired = state.AcquiredItems;
        if (drop is ContentId item)
        {
            backpack = backpack.Append(new BattleConsumableState(
                new ItemInstanceId($"drop-{state.BattlesCompleted + 1}-{item.Value}"), item, 1, 1)).ToArray();
            acquired = acquired.Append(item).OrderBy(value => value.Value, StringComparer.Ordinal).ToArray();
        }
        // Keyed by the node transaction: the same encounter can settle on more than one node of a run.
        PendingProgression[] progression = ProgressionTarget(party, result) is string target
            ? state.PendingProgression.Append(new PendingProgression(
                $"progression:{transaction.TransactionKey}", result.EncounterContentId.Value, target)).ToArray()
            : state.PendingProgression.ToArray();
        bool layerSix = transaction.NodeId.StartsWith("layer_06_", StringComparison.Ordinal);
        PureRunMapState map = state.MapState! with
        {
            Phase = layerSix ? PureRunMapPhase.ReadyForBoss : PureRunMapPhase.ReadyForLayerFive, CurrentNodeId = transaction.NodeId,
            ReachableNodeIds = Array.Empty<string>(),
            VisitedNodeIds = state.MapState!.VisitedNodeIds.Append(transaction.NodeId).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            PendingNodeId = null, PendingTransactionKey = null, NodeLifecycle = RunNodeLifecycle.Committed
        };
        transactions = transactions.Append(transaction.TransactionKey).Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var completed = new PureRunState(state.RunId, state.Seed, state.Revision + 1,
            layerSix ? PureRunPhase.ReadyForBoss : PureRunPhase.ReadyForLayerFive, state.EncounterIndex, state.EncounterContentId, party, backpack,
            state.BackpackEquipment, progression, transactions, gold, state.BattlesCompleted + 1,
            checked(state.EnemiesDefeated + result.EnemiesDefeated), acquired, mapState: map,
            nodeTransaction: transaction with { Committed = true }, escortState: state.EscortState,
            adventureState: state.AdventureState);
        return new(true, null, completed, null, false);
    }

    public PureRunSettlementResult ApplyFixedLateBattle(PureRunState state, PureRunBattleResult result,
        IReadOnlyList<ContentId> consumableDropPool, PureRunPhase victoryPhase, bool boss)
    {
        string battleKey = $"battle:{result.EncounterContentId.Value}:settlement";
        if (state.AppliedTransactionKeys.Contains(battleKey, StringComparer.Ordinal))
            return new(true, null, state, null, true);
        string? rejection = Validate(state, result);
        if (rejection is not null) return new(false, rejection, state, null, false);
        RunCharacterState[] party = MergeParty(state, result, result.PlayerVictory);
        string[] transactions = state.AppliedTransactionKeys.Append(battleKey).Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!result.PlayerVictory)
            return new(true, null, null, CreateSummary(state, PureRunOutcome.Defeated, party, transactions), false);

        int gold = Math.Min(GoldCap, checked(state.Gold + CalculateGold(result.TotalRounds)));
        ContentId? drop = RollDrop(state.Seed, result.EncounterContentId, consumableDropPool, boss ? 0.25 : 0.30);
        BattleConsumableState[] backpack = state.BackpackConsumables.ToArray();
        IReadOnlyList<ContentId> acquired = state.AcquiredItems;
        if (drop is ContentId item)
        {
            backpack = backpack.Append(new BattleConsumableState(
                new ItemInstanceId($"drop-{state.EncounterIndex + 1}-{item.Value}"), item, 1, 1)).ToArray();
            acquired = acquired.Append(item).OrderBy(value => value.Value, StringComparer.Ordinal).ToArray();
        }
        int battles = state.BattlesCompleted + 1;
        int defeated = checked(state.EnemiesDefeated + result.EnemiesDefeated);
        if (boss)
        {
            var won = new PureRunState(state.RunId, state.Seed, state.Revision + 1, PureRunPhase.SliceCompleted,
                state.EncounterIndex, state.EncounterContentId, party, backpack, state.BackpackEquipment,
                state.PendingProgression, transactions, gold, battles, defeated, acquired, mapState: state.MapState,
                escortState: state.EscortState, adventureState: state.AdventureState);
            PureRunSummary summary = CreateSummary(won, PureRunOutcome.BossVictory, party, transactions) with
            { BossDefeated = true, TerminalEncounterId = result.EncounterContentId };
            return new(true, null, null, summary, false);
        }
        string target = ProgressionTarget(party, result) ?? string.Empty;
        PendingProgression[] progression = string.IsNullOrEmpty(target) ? state.PendingProgression.ToArray() :
            state.PendingProgression.Append(new PendingProgression($"progression:{result.EncounterContentId.Value}",
                result.EncounterContentId.Value, target)).ToArray();
        var next = new PureRunState(state.RunId, state.Seed, state.Revision + 1, victoryPhase,
            state.EncounterIndex, state.EncounterContentId, party, backpack, state.BackpackEquipment, progression,
            transactions, gold, battles, defeated, acquired, mapState: state.MapState,
            escortState: state.EscortState, adventureState: state.AdventureState);
        return new(true, null, next, null, false);
    }

    /// <summary>
    /// The one character a victory levels up: the lowest level among those still standing when the battle ended,
    /// ties broken by party order. A character who was down at the end is excluded even though the post-victory
    /// recovery in <see cref="MergeParty"/> has already put them back on their feet. Characters at
    /// <see cref="MaxCharacterLevel"/> are skipped. Null when nobody qualifies.
    /// </summary>
    private static string? ProgressionTarget(RunCharacterState[] party, PureRunBattleResult result)
    {
        HashSet<string> standing = result.Party.Where(member => !member.IsDead)
            .Select(member => member.CharacterId).ToHashSet(StringComparer.Ordinal);
        return party.Where(character => !character.IsDead && character.Level < MaxCharacterLevel &&
                standing.Contains(character.CharacterId))
            .OrderBy(character => character.Level)
            .ThenBy(character => Array.FindIndex(party, candidate => candidate.CharacterId == character.CharacterId))
            .Select(character => character.CharacterId).FirstOrDefault();
    }

    private static string? Validate(PureRunState state, PureRunBattleResult result)
    {
        if (state.Phase != PureRunPhase.PendingBattle || state.Checkpoint is null)
            return "run.no_pending_battle";
        if (!string.Equals(state.RunId, result.RunId, StringComparison.Ordinal))
            return "run.result_run_mismatch";
        if (state.Checkpoint.Revision != result.CheckpointRevision)
            return "run.result_revision_mismatch";
        if (state.EncounterContentId != result.EncounterContentId)
            return "run.result_encounter_mismatch";
        if (result.TotalRounds < 1 || result.EnemiesDefeated < 0)
            return "run.result_invalid_totals";
        HashSet<string> checkpointIds = state.Checkpoint.Party.Select(item => item.CharacterId)
            .ToHashSet(StringComparer.Ordinal);
        if (result.Party.Count != checkpointIds.Count ||
            result.Party.Select(item => item.CharacterId).Distinct(StringComparer.Ordinal).Count() != checkpointIds.Count ||
            !result.Party.All(item => checkpointIds.Contains(item.CharacterId)))
            return "run.result_invalid_party";
        foreach (BattlePartyResult member in result.Party)
        {
            RunCharacterState? prior = state.Checkpoint.Party.FirstOrDefault(item => item.CharacterId == member.CharacterId);
            if (prior is null || member.CurrentHealth < 0 || member.CurrentHealth > prior.MaxHealth ||
                member.CurrentMana < 0 || member.CurrentMana > prior.MaxMana || member.IsDead != (member.CurrentHealth == 0) ||
                member.RunPermanentlyDead && !member.IsDead)
                return "run.result_invalid_character";
            if (member.CarriedConsumables.Any(item => item.RemainingCharges < 0 || item.RemainingCharges > item.MaxCharges))
                return "run.result_invalid_consumable";
        }
        return null;
    }

    private static RunCharacterState[] MergeParty(PureRunState state, PureRunBattleResult result, bool recoverAfterVictory)
    {
        if (state.Checkpoint is null)
            return state.Party.ToArray();
        Dictionary<string, BattlePartyResult> battleOutcomes = result.Party
            .ToDictionary(item => item.CharacterId, StringComparer.Ordinal);
        // Deceased roster members not in this battle's active party keep their
        // tombstone exactly as-is (equipment/consumables intentionally retained
        // for the terminal Summary; they never participate in later battles).
        IEnumerable<RunCharacterState> tombstones = state.Party.Where(character => character.IsDead);
        RunCharacterState[] survivors = state.Checkpoint.Party.Select(prior =>
        {
            BattlePartyResult current = battleOutcomes[prior.CharacterId];
            bool wasDead = current.IsDead;
            int health = recoverAfterVictory && !current.RunPermanentlyDead ? Math.Min(prior.MaxHealth,
                checked(current.CurrentHealth + prior.Attributes.Constitution * 2)) : current.CurrentHealth;
            int mana = recoverAfterVictory ? Math.Min(prior.MaxMana,
                checked(current.CurrentMana + prior.Attributes.Charisma)) : current.CurrentMana;
            bool dead = health == 0;
            return new RunCharacterState(
                prior.CharacterId, prior.UnitContentId, prior.Level, prior.Attributes,
                health, prior.MaxHealth, mana, prior.MaxMana, dead, prior.LearnedSkills,
                wasDead ? Array.Empty<RunEquipmentState>() : prior.Equipment,
                wasDead ? Array.Empty<BattleConsumableState>() : current.CarriedConsumables,
                prior.LearnedSkillStates, prior.StartingSkillContentId);
        }).ToArray();
        // Preserve state.Party order (tombstones stay in their roster positions)
        // so downstream UI/sequence and replay order are stable.
        Dictionary<string, RunCharacterState> merged = survivors.Concat(tombstones)
            .ToDictionary(item => item.CharacterId, StringComparer.Ordinal);
        return state.Party.Select(prior => merged[prior.CharacterId]).ToArray();
    }

    private static int CalculateGold(int rounds) => 3 + (rounds switch
    {
        <= 3 => 5,
        <= 5 => 3,
        <= 10 => 1,
        _ => 0
    });

    private static ContentId? RollDrop(int runSeed, ContentId encounter, IReadOnlyList<ContentId> pool, double probability = 0.25)
    {
        if (pool.Count == 0)
            return null;
        int chanceSeed = DeriveSeed(runSeed, $"battle-drop:{encounter.Value}");
        var chance = new Random(chanceSeed);
        if (chance.NextDouble() >= probability)
            return null;
        int itemSeed = DeriveSeed(runSeed, $"battle-drop-item:{encounter.Value}");
        return pool.OrderBy(value => value.Value, StringComparer.Ordinal).ElementAt(new Random(itemSeed).Next(pool.Count));
    }

    public static int DeriveSeed(int runSeed, string streamName, int ordinal = 0)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)runSeed) * 16777619u;
            hash = (hash ^ (uint)ordinal) * 16777619u;
            foreach (char character in streamName ?? string.Empty)
                hash = (hash ^ character) * 16777619u;
            return (int)hash;
        }
    }

    private static PureRunSummary CreateSummary(
        PureRunState state,
        PureRunOutcome outcome,
        IReadOnlyList<RunCharacterState> party,
        IReadOnlyList<string> transactions) => new(
            state.RunId, state.Seed, outcome, state.BattlesCompleted, state.EnemiesDefeated,
            state.Gold, state.AcquiredItems,
            party.Where(character => character.IsDead).Select(character => character.CharacterId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            transactions.OrderBy(value => value, StringComparer.Ordinal).ToArray());
}
