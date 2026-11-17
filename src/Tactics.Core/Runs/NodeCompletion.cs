namespace Tactics.Core.Runs;

/// <summary>
/// One finished node step. Battle, rest, store, a skill check and a chest are different completions of the same
/// question: does this step owe the standard level-up? The run flow only has to spend whatever was owed.
/// </summary>
public abstract class NodeCompletion
{
    /// <summary>The owed level-up, or null when this completion grants none.</summary>
    public abstract PendingProgression? OfferGrowth(IReadOnlyList<RunCharacterState> party);

    /// <summary>
    /// Appends <paramref name="offer"/> unless this step already owes that level-up. A repeated settlement therefore
    /// cannot grant a second one.
    /// </summary>
    public static PendingProgression[] Apply(IReadOnlyList<PendingProgression> existing, PendingProgression? offer)
    {
        ArgumentNullException.ThrowIfNull(existing);
        if (offer is null || existing.Any(value =>
                string.Equals(value.TransactionKey, offer.TransactionKey, StringComparison.Ordinal)))
            return existing.ToArray();
        return existing.Append(offer).ToArray();
    }
}

/// <summary>A won battle, including an event that entered combat. Owes the standard level-up when someone qualifies.</summary>
public sealed class BattleVictoryCompletion : NodeCompletion
{
    private readonly PureRunBattleResult _result;
    private readonly string _sourceKey;

    public BattleVictoryCompletion(PureRunBattleResult result, string sourceKey)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(sourceKey))
            throw new ArgumentException("Progression source key cannot be empty.", nameof(sourceKey));
        _result = result;
        _sourceKey = sourceKey;
    }

    public override PendingProgression? OfferGrowth(IReadOnlyList<RunCharacterState> party)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (!_result.PlayerVictory) return null;
        string? target = PureRunSettlementService.SelectProgressionTarget(party,
            _result.Party.Where(member => !member.IsDead).Select(member => member.CharacterId));
        return target is null
            ? null
            : new PendingProgression($"progression:{_sourceKey}", _result.EncounterContentId.Value, target);
    }
}

/// <summary>The run-ending boss victory. It owes no level-up because the run is already over.</summary>
public sealed class BossVictoryCompletion : NodeCompletion
{
    public override PendingProgression? OfferGrowth(IReadOnlyList<RunCharacterState> party) => null;
}

/// <summary>A confirmed rest. The default answer is that rest does not owe a level-up.</summary>
public sealed class RestCompletion : NodeCompletion
{
    public override PendingProgression? OfferGrowth(IReadOnlyList<RunCharacterState> party) => null;
}

/// <summary>Leaving a store. The default answer is that the store does not owe a level-up.</summary>
public sealed class StoreCompletion : NodeCompletion
{
    public override PendingProgression? OfferGrowth(IReadOnlyList<RunCharacterState> party) => null;
}

/// <summary>A confirmed skill-check event. The default answer is that the check does not owe a level-up.</summary>
public sealed class MysteryChoiceCompletion : NodeCompletion
{
    public override PendingProgression? OfferGrowth(IReadOnlyList<RunCharacterState> party) => null;
}

/// <summary>
/// A confirmed chest. Owes the standard level-up only when <see cref="GrantsGrowth"/> is set; living party members
/// are the eligible set, since a chest has no battle casualty list.
/// </summary>
public sealed class TreasureCompletion : NodeCompletion
{
    private readonly string _transactionKey;

    public TreasureCompletion(string transactionKey, bool grantsGrowth)
    {
        if (string.IsNullOrWhiteSpace(transactionKey))
            throw new ArgumentException("Treasure transaction key cannot be empty.", nameof(transactionKey));
        _transactionKey = transactionKey;
        GrantsGrowth = grantsGrowth;
    }

    public bool GrantsGrowth { get; }

    public override PendingProgression? OfferGrowth(IReadOnlyList<RunCharacterState> party)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (!GrantsGrowth) return null;
        string? target = PureRunSettlementService.SelectProgressionTarget(party,
            party.Where(character => !character.IsDead).Select(character => character.CharacterId));
        return target is null
            ? null
            : new PendingProgression($"progression:{_transactionKey}", _transactionKey, target);
    }
}
