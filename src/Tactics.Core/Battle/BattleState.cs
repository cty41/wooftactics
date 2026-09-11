using System.Collections.ObjectModel;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Randomness;
using Tactics.Core.Turns;
using Tactics.Core.Units;

namespace Tactics.Core.Battle;

/// <summary>Immutable deterministic battle snapshot.</summary>
public sealed class BattleState
{
    private IReadOnlyDictionary<UnitInstanceId, BattleUnitState> _units = null!;
    private IReadOnlyList<UnitInstanceId> _turnOrder = null!;
    private IReadOnlyDictionary<UnitInstanceId, GridPoint> _droppedSpears = null!;
    private IReadOnlyCollection<GridPoint> _corpses = null!;
    private InitiativeRoundState _initiativeRound = null!;

    /// <summary>
    /// Legacy flat-order integration constructor. Production mutations preserve an authoritative
    /// <see cref="InitiativeRoundState"/> instead of mutating indexes directly.
    /// </summary>
    public BattleState(
        BoardSnapshot board,
        IEnumerable<BattleUnitState> units,
        IReadOnlyList<UnitInstanceId> turnOrder,
        int round = 1,
        int activeIndex = 0,
        ulong randomState = 0,
        IReadOnlyDictionary<UnitInstanceId, GridPoint>? droppedSpears = null,
        IReadOnlyCollection<GridPoint>? corpses = null)
    {
        ArgumentNullException.ThrowIfNull(turnOrder);
        IReadOnlyDictionary<UnitInstanceId, BattleUnitState> unitMap = ValidateUnits(board, units);
        if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
        if (turnOrder.Count == 0) throw new ArgumentException("Turn order must contain at least one unit.", nameof(turnOrder));
        if (activeIndex < 0 || activeIndex >= turnOrder.Count) throw new ArgumentOutOfRangeException(nameof(activeIndex));
        UnitInstanceId[] order = turnOrder.ToArray();
        if (order.Distinct().Count() != order.Length)
            throw new ArgumentException("Turn order cannot contain duplicate unit IDs.", nameof(turnOrder));
        if (order.Any(unitId => !unitMap.ContainsKey(unitId)))
            throw new ArgumentException("Turn order references a unit that is not present in battle state.", nameof(turnOrder));

        InitiativeRoundState restored = InitiativeRoundState.RestoreOrderedRound(
            order.Select(id => Entry(unitMap[id])), activeIndex);
        Initialize(board, unitMap, restored.Synchronize(EligibleEntries(unitMap.Values)), round, randomState,
            droppedSpears, corpses);
    }

    private BattleState(
        BoardSnapshot board,
        IReadOnlyDictionary<UnitInstanceId, BattleUnitState> units,
        InitiativeRoundState initiativeRound,
        int round,
        ulong randomState,
        IReadOnlyDictionary<UnitInstanceId, GridPoint>? droppedSpears,
        IReadOnlyCollection<GridPoint>? corpses)
    {
        if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
        Initialize(board, units, initiativeRound, round, randomState, droppedSpears, corpses);
    }

    private void Initialize(
        BoardSnapshot board,
        IReadOnlyDictionary<UnitInstanceId, BattleUnitState> units,
        InitiativeRoundState initiativeRound,
        int round,
        ulong randomState,
        IReadOnlyDictionary<UnitInstanceId, GridPoint>? droppedSpears,
        IReadOnlyCollection<GridPoint>? corpses)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(initiativeRound);
        if (initiativeRound.Current is not InitiativeEntry current)
            throw new ArgumentException("Battle initiative must have a current unit.", nameof(initiativeRound));
        if (!units.ContainsKey(current.UnitId))
            throw new ArgumentException("Current initiative unit is absent from battle units.", nameof(initiativeRound));

        Board = board;
        _units = new ReadOnlyDictionary<UnitInstanceId, BattleUnitState>(new Dictionary<UnitInstanceId, BattleUnitState>(units));
        _initiativeRound = initiativeRound;
        UnitInstanceId[] flat = initiativeRound.GetFullRoundOrder().Select(entry => entry.UnitId).ToArray();
        if (flat.Length == 0 || flat.Any(id => !units.ContainsKey(id)))
            throw new ArgumentException("Initiative projection references an unavailable unit.", nameof(initiativeRound));
        _turnOrder = Array.AsReadOnly(flat);
        Round = round;
        ActiveIndex = Array.IndexOf(flat, current.UnitId);
        if (ActiveIndex < 0) throw new ArgumentException("Current unit is absent from initiative projection.", nameof(initiativeRound));
        RandomState = randomState;

        var spearMap = new Dictionary<UnitInstanceId, GridPoint>();
        foreach ((UnitInstanceId ownerId, GridPoint cell) in droppedSpears ?? new Dictionary<UnitInstanceId, GridPoint>())
        {
            if (!units.ContainsKey(ownerId)) throw new ArgumentException($"Dropped spear owner '{ownerId}' is absent.", nameof(droppedSpears));
            if (!board.Contains(cell)) throw new ArgumentException($"Dropped spear for '{ownerId}' is outside the board.", nameof(droppedSpears));
            if (spearMap.Values.Contains(cell)) throw new ArgumentException($"Multiple dropped spears occupy '{cell}'.", nameof(droppedSpears));
            spearMap.Add(ownerId, cell);
        }
        _droppedSpears = new ReadOnlyDictionary<UnitInstanceId, GridPoint>(spearMap);

        GridPoint[] corpseArray = (corpses ?? Array.Empty<GridPoint>()).Distinct()
            .OrderBy(cell => cell.X).ThenBy(cell => cell.Y).ToArray();
        if (corpseArray.Any(cell => !board.Contains(cell)))
            throw new ArgumentException("Corpse is outside the board.", nameof(corpses));
        _corpses = Array.AsReadOnly(corpseArray);
    }

    public BoardSnapshot Board { get; private set; } = null!;
    public IReadOnlyDictionary<UnitInstanceId, BattleUnitState> Units => _units;
    /// <summary>Legacy acted-plus-remaining projection. New UI should consume <see cref="InitiativeRound"/>.</summary>
    public IReadOnlyList<UnitInstanceId> TurnOrder => _turnOrder;
    public InitiativeRoundState InitiativeRound => _initiativeRound;
    public int Round { get; private set; }
    public int ActiveIndex { get; private set; }
    public UnitInstanceId ActiveUnitId => _initiativeRound.Current!.Value.UnitId;
    public ulong RandomState { get; private set; }
    public IReadOnlyDictionary<UnitInstanceId, GridPoint> DroppedSpears => _droppedSpears;
    public IReadOnlyCollection<GridPoint> Corpses => _corpses;
    public string RandomAlgorithmId => DeterministicRandom.AlgorithmId;

    public bool TryGetUnit(UnitInstanceId unitId, out BattleUnitState? unit) => _units.TryGetValue(unitId, out unit);

    /// <summary>
    /// Replaces one unit and atomically synchronizes eligibility and every pending initiative entry.
    /// </summary>
    public BattleState WithUnit(BattleUnitState unit)
    {
        if (!_units.ContainsKey(unit.Unit.InstanceId))
            throw new ArgumentException("Cannot add a new unit through WithUnit.", nameof(unit));
        var units = new Dictionary<UnitInstanceId, BattleUnitState>(_units) { [unit.Unit.InstanceId] = unit };
        InitiativeRoundState initiative = _initiativeRound.Synchronize(EligibleEntries(units.Values));
        return Copy(units, initiative);
    }

    public BattleState WithInitiativeChanged(BattleUnitState unit) => WithUnit(unit);

    /// <summary>Completes the current turn and starts the next eligible turn.</summary>
    public BattleState AdvanceTurn()
    {
        InitiativeEntry[] eligible = EligibleEntries(_units.Values).ToArray();
        if (eligible.Length == 0)
            return this;

        InitiativeTakeNextResult taken = _initiativeRound.TakeNext(eligible);
        if (taken.Current is not InitiativeEntry incoming)
            return this;

        var units = new Dictionary<UnitInstanceId, BattleUnitState>(_units);
        units[incoming.UnitId] = units[incoming.UnitId].PrepareForTurn();
        int nextRound = checked(Round + (taken.StartedNewRound ? 1 : 0));
        return new BattleState(Board, units, taken.State, nextRound, RandomState, _droppedSpears, _corpses);
    }

    public bool TryGetDroppedSpear(UnitInstanceId ownerId, out GridPoint cell) => _droppedSpears.TryGetValue(ownerId, out cell);

    public BattleState WithDroppedSpear(UnitInstanceId ownerId, GridPoint cell)
    {
        if (!_units.ContainsKey(ownerId)) throw new ArgumentException("Dropped spear owner is not in battle.", nameof(ownerId));
        if (_droppedSpears.ContainsKey(ownerId)) throw new InvalidOperationException($"Unit '{ownerId}' already has a dropped spear.");
        if (!Board.Contains(cell) || Board.GetCell(cell).BlocksMovement ||
            _units.Values.Any(unit => unit.IsAlive && unit.Unit.Position == cell) ||
            _droppedSpears.Values.Contains(cell) || _corpses.Contains(cell))
            throw new InvalidOperationException($"Cell '{cell}' cannot receive a dropped spear.");
        var spears = new Dictionary<UnitInstanceId, GridPoint>(_droppedSpears) { [ownerId] = cell };
        return Copy(droppedSpears: spears);
    }

    public BattleState WithoutDroppedSpear(UnitInstanceId ownerId)
    {
        var spears = new Dictionary<UnitInstanceId, GridPoint>(_droppedSpears);
        spears.Remove(ownerId);
        return Copy(droppedSpears: spears);
    }

    public BattleState WithRandomState(ulong randomState) => Copy(randomState: randomState);

    public BattleState WithCorpse(GridPoint cell)
    {
        if (!Board.Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
        return Copy(corpses: _corpses.Append(cell).ToArray());
    }

    public BattleState WithoutCorpse(GridPoint cell) =>
        Copy(corpses: _corpses.Where(value => value != cell).ToArray());

    public BattleState WithSummon(BattleUnitState summon, int maximumPerOwner = 1, string summonCategory = "")
    {
        if (summon.SummonOwnerId is not UnitInstanceId ownerId || !_units.ContainsKey(ownerId))
            throw new ArgumentException("Summon must reference an owner in the battle.", nameof(summon));
        if (_units.ContainsKey(summon.Unit.InstanceId)) throw new ArgumentException("Summon ID already exists.", nameof(summon));
        if (maximumPerOwner <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPerOwner));

        var units = new Dictionary<UnitInstanceId, BattleUnitState>(_units);
        string category = string.IsNullOrWhiteSpace(summonCategory) ? summon.SummonCategory : summonCategory;
        BattleUnitState[] existing = units.Values
            .Where(unit => unit.SummonOwnerId == ownerId &&
                           (string.IsNullOrEmpty(category) || unit.SummonCategory == category))
            .OrderBy(unit => unit.Unit.SpawnOrdinal).ToArray();
        while (existing.Length >= maximumPerOwner)
        {
            units.Remove(existing[0].Unit.InstanceId);
            existing = existing.Skip(1).ToArray();
        }
        units.Add(summon.Unit.InstanceId, summon);

        InitiativeRoundState initiative = _initiativeRound.Synchronize(EligibleEntries(units.Values));
        return Copy(units, initiative);
    }

    public BoardSnapshot CreateMovementBoard(UnitInstanceId movingUnitId) => Board.WithOccupancy(
        _units.Values.Where(unit => unit.IsAlive && unit.Unit.InstanceId != movingUnitId)
            .Select(unit => unit.Unit.Position)
            .Concat(_droppedSpears.Values)
            .Concat(_corpses));

    private BattleState Copy(
        IReadOnlyDictionary<UnitInstanceId, BattleUnitState>? units = null,
        InitiativeRoundState? initiative = null,
        ulong? randomState = null,
        IReadOnlyDictionary<UnitInstanceId, GridPoint>? droppedSpears = null,
        IReadOnlyCollection<GridPoint>? corpses = null) =>
        new(Board, units ?? _units, initiative ?? _initiativeRound, Round, randomState ?? RandomState,
            droppedSpears ?? _droppedSpears, corpses ?? _corpses);

    private static IReadOnlyDictionary<UnitInstanceId, BattleUnitState> ValidateUnits(
        BoardSnapshot board,
        IEnumerable<BattleUnitState> units)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(units);
        var map = new Dictionary<UnitInstanceId, BattleUnitState>();
        foreach (BattleUnitState unit in units)
        {
            if (!map.TryAdd(unit.Unit.InstanceId, unit))
                throw new ArgumentException($"Duplicate battle unit ID: {unit.Unit.InstanceId}.", nameof(units));
            if (!board.Contains(unit.Unit.Position))
                throw new ArgumentException($"Unit {unit.Unit.InstanceId} is outside the board.", nameof(units));
        }
        return new ReadOnlyDictionary<UnitInstanceId, BattleUnitState>(map);
    }

    private static IEnumerable<InitiativeEntry> EligibleEntries(IEnumerable<BattleUnitState> units) =>
        units.Where(ParticipatesInTurnOrder).Select(Entry);

    private static bool ParticipatesInTurnOrder(BattleUnitState unit) =>
        unit.IsAlive && !string.Equals(unit.SummonCategory, "Decoy", StringComparison.Ordinal);

    private static InitiativeEntry Entry(BattleUnitState unit) => new(
        unit.Unit.InstanceId,
        unit.Unit.Initiative,
        unit.Unit.PlayerNumber,
        unit.Unit.SpawnOrdinal);
}
