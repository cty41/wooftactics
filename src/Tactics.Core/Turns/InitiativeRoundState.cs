using System.Collections.ObjectModel;
using Tactics.Core.Units;

namespace Tactics.Core.Turns;

/// <summary>
/// Immutable authoritative partition for one initiative round.
/// </summary>
/// <remarks>
/// Entries whose turns have started remain in chronological acted order. Initiative and eligibility changes
/// can only reorder the remaining partition. When remaining is exhausted, <see cref="TakeNext"/> rebuilds the
/// next round from the caller-provided eligible entries.
/// </remarks>
public sealed class InitiativeRoundState
{
    private readonly IReadOnlyList<InitiativeEntry> _actedOrder;
    private readonly IReadOnlyList<InitiativeEntry> _remaining;
    private readonly IReadOnlySet<UnitInstanceId> _acted;

    private InitiativeRoundState(
        InitiativeEntry? current,
        IEnumerable<InitiativeEntry> actedOrder,
        IEnumerable<InitiativeEntry> remaining)
    {
        Current = current;
        InitiativeEntry[] acted = actedOrder.ToArray();
        _actedOrder = Array.AsReadOnly(acted);
        _remaining = Array.AsReadOnly(remaining.ToArray());
        _acted = new ReadOnlySet<UnitInstanceId>(acted.Select(entry => entry.UnitId).ToHashSet());
    }

    /// <summary>Gets the unit whose turn has started most recently.</summary>
    public InitiativeEntry? Current { get; }

    /// <summary>Gets units whose turns started this round, in chronological order.</summary>
    public IReadOnlyList<InitiativeEntry> ActedOrder => _actedOrder;

    /// <summary>Gets units that have not started a turn this round, in current initiative order.</summary>
    public IReadOnlyList<InitiativeEntry> Remaining => _remaining;

    /// <summary>Gets stable IDs whose turns started this round, including <see cref="Current"/>.</summary>
    public IReadOnlySet<UnitInstanceId> Acted => _acted;

    /// <summary>Starts an empty-current round from all eligible units.</summary>
    public static InitiativeRoundState StartRound(IEnumerable<InitiativeEntry> eligibleUnits)
    {
        InitiativeEntry[] entries = MaterializeUnique(eligibleUnits);
        return new InitiativeRoundState(null, Array.Empty<InitiativeEntry>(), InitiativeOrder.Sort(entries));
    }

    /// <summary>
    /// Restores the legacy ordered/current-index representation at an integration boundary.
    /// New production mutations should preserve this type directly instead of round-tripping through indexes.
    /// </summary>
    public static InitiativeRoundState RestoreOrderedRound(
        IEnumerable<InitiativeEntry> orderedUnits,
        int activeIndex)
    {
        InitiativeEntry[] entries = MaterializeUnique(orderedUnits);
        if (entries.Length == 0)
            throw new ArgumentException("Initiative round must contain at least one entry.", nameof(orderedUnits));
        if (activeIndex < 0 || activeIndex >= entries.Length)
            throw new ArgumentOutOfRangeException(nameof(activeIndex));

        InitiativeEntry[] acted = entries.Take(activeIndex + 1).ToArray();
        return new InitiativeRoundState(acted[^1], acted, entries.Skip(activeIndex + 1));
    }

    /// <summary>Synchronizes eligibility and starts the next available turn.</summary>
    public InitiativeTakeNextResult TakeNext(IEnumerable<InitiativeEntry> eligibleUnits)
    {
        InitiativeEntry[] eligible = MaterializeUnique(eligibleUnits);
        InitiativeRoundState synchronized = Synchronize(eligible);
        bool startedNewRound = false;

        if (synchronized._remaining.Count == 0)
        {
            synchronized = StartRound(eligible);
            startedNewRound = true;
        }

        if (synchronized._remaining.Count == 0)
            return new InitiativeTakeNextResult(synchronized, null, startedNewRound);

        InitiativeEntry current = synchronized._remaining[0];
        InitiativeEntry[] acted = synchronized._actedOrder.Append(current).ToArray();
        var next = new InitiativeRoundState(current, acted, synchronized._remaining.Skip(1));
        return new InitiativeTakeNextResult(next, current, startedNewRound);
    }

    /// <summary>
    /// Refreshes all eligible facts, removes ineligible units, and adds newly eligible units to remaining.
    /// Current and acted chronology stay stable; only remaining is sorted.
    /// </summary>
    public InitiativeRoundState Synchronize(IEnumerable<InitiativeEntry> eligibleUnits)
    {
        InitiativeEntry[] eligibleArray = MaterializeUnique(eligibleUnits);
        IReadOnlyDictionary<UnitInstanceId, InitiativeEntry> eligible = eligibleArray
            .ToDictionary(entry => entry.UnitId);

        UnitInstanceId? currentId = Current?.UnitId;
        InitiativeEntry[] acted = _actedOrder
            .Where(entry => eligible.ContainsKey(entry.UnitId) || currentId == entry.UnitId)
            .Select(entry => eligible.TryGetValue(entry.UnitId, out InitiativeEntry refreshed) ? refreshed : entry)
            .ToArray();
        HashSet<UnitInstanceId> actedIds = acted.Select(entry => entry.UnitId).ToHashSet();
        InitiativeEntry? current = currentId is UnitInstanceId id
            ? acted.LastOrDefault(entry => entry.UnitId == id)
            : null;

        var remaining = _remaining
            .Where(entry => eligible.ContainsKey(entry.UnitId))
            .Select(entry => eligible[entry.UnitId])
            .ToList();
        var remainingIds = remaining.Select(entry => entry.UnitId).ToHashSet();

        foreach (InitiativeEntry entry in eligibleArray)
        {
            if (actedIds.Contains(entry.UnitId) || remainingIds.Contains(entry.UnitId))
                continue;
            remaining.Add(entry);
            remainingIds.Add(entry.UnitId);
        }

        return new InitiativeRoundState(current, acted, InitiativeOrder.Sort(remaining));
    }

    /// <summary>Refreshes one pending entry. Retained for focused callers and golden vectors.</summary>
    public InitiativeRoundState NotifyInitiativeChanged(InitiativeEntry changedUnit)
    {
        int index = _remaining
            .Select((entry, candidateIndex) => (entry, candidateIndex))
            .Where(candidate => candidate.entry.UnitId == changedUnit.UnitId)
            .Select(candidate => candidate.candidateIndex)
            .DefaultIfEmpty(-1)
            .First();
        if (index < 0)
            return this;

        InitiativeEntry[] remaining = _remaining.ToArray();
        remaining[index] = changedUnit;
        return new InitiativeRoundState(Current, _actedOrder, InitiativeOrder.Sort(remaining));
    }

    /// <summary>Returns current followed by remaining, for actionable HUD projection.</summary>
    public IReadOnlyList<InitiativeEntry> GetCurrentRoundOrder(bool includeCurrent = true)
    {
        if (!includeCurrent || Current is null)
            return _remaining;
        return Array.AsReadOnly(new[] { Current.Value }.Concat(_remaining).ToArray());
    }

    /// <summary>Returns acted chronology followed by remaining for legacy flat-order projection.</summary>
    public IReadOnlyList<InitiativeEntry> GetFullRoundOrder() =>
        Array.AsReadOnly(_actedOrder.Concat(_remaining).ToArray());

    public InitiativeRoundState Reset() =>
        new(null, Array.Empty<InitiativeEntry>(), Array.Empty<InitiativeEntry>());

    private static InitiativeEntry[] MaterializeUnique(IEnumerable<InitiativeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        InitiativeEntry[] materialized = entries.ToArray();
        if (materialized.Any(entry => !float.IsFinite(entry.Initiative) || entry.Initiative < 0f))
            throw new ArgumentOutOfRangeException(nameof(entries), "Initiative must be finite and non-negative.");
        if (materialized.Select(entry => entry.UnitId).Distinct().Count() != materialized.Length)
            throw new ArgumentException("Initiative participants must have unique unit IDs.", nameof(entries));
        return materialized;
    }
}

public sealed record InitiativeTakeNextResult(
    InitiativeRoundState State,
    InitiativeEntry? Current,
    bool StartedNewRound);
