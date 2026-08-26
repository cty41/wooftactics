using System.Text.Json.Serialization;
using Tactics.Core.Content;
using Tactics.Core.Items;

namespace Tactics.Core.Runs;

public enum PureRunNodeKind { Battle, Elite, Boss, Rest, Store, Mystery, Treasure }
public enum PureRunMapPhase { Locked, ChoosingLayerFour, ResolvingNode, ReadyForLayerFive, ReadyForLayerSix, ChoosingLayerSix, ReadyForBoss, Completed }
public enum RunNodeLifecycle { Available, Selected, Pending, Resolved, Committed }
public enum RunEventAttribute { None, Strength, Agility, Constitution, Intelligence, Charisma, Luck }

public sealed record PureRunMapNodeDefinition(
    string NodeId,
    int Layer,
    PureRunNodeKind Kind,
    ContentId ContentId,
    string? Title = null,
    float Lane = 0);

public sealed record PureRunMapConnectionDefinition(string FromNodeId, string ToNodeId);

public sealed record PureRunMapDefinition
{
    public PureRunMapDefinition(ContentId contentId, int layoutVersion, IEnumerable<PureRunMapNodeDefinition> nodes,
        IEnumerable<PureRunMapConnectionDefinition>? connections = null)
    {
        ContentId = contentId;
        LayoutVersion = layoutVersion;
        Nodes = nodes?.OrderBy(node => node.NodeId, StringComparer.Ordinal).ToArray()
            ?? throw new ArgumentNullException(nameof(nodes));
        Connections = connections?.OrderBy(value => value.FromNodeId, StringComparer.Ordinal)
            .ThenBy(value => value.ToNodeId, StringComparer.Ordinal).ToArray() ?? Array.Empty<PureRunMapConnectionDefinition>();
        if (LayoutVersion < 2 || Nodes.Count == 0 || Nodes.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count() != Nodes.Count)
            throw new ArgumentException("Map nodes must have a supported layout version and unique identities.", nameof(nodes));
        HashSet<string> nodeIds = Nodes.Select(value => value.NodeId).ToHashSet(StringComparer.Ordinal);
        if (Connections.Any(edge => !nodeIds.Contains(edge.FromNodeId) || !nodeIds.Contains(edge.ToNodeId)) ||
            Connections.Distinct().Count() != Connections.Count)
            throw new ArgumentException("Map connections must be unique and reference known nodes.", nameof(connections));
    }

    public ContentId ContentId { get; }
    public int LayoutVersion { get; }
    public IReadOnlyList<PureRunMapNodeDefinition> Nodes { get; }
    public IReadOnlyList<PureRunMapConnectionDefinition> Connections { get; }
}

public sealed record WeightedContentDefinition(ContentId ContentId, int Weight);

public sealed record PureRunTreasureDefinition(
    ContentId ContentId,
    int GoldMinimum,
    int GoldMaximum,
    IReadOnlyList<WeightedContentDefinition> Equipment,
    IReadOnlyList<WeightedContentDefinition> Consumables,
    IReadOnlyList<WeightedContentDefinition> Buffs)
{
    public void Validate()
    {
        if (GoldMinimum < 0 || GoldMaximum < GoldMinimum || GoldMaximum > 50)
            throw new ArgumentOutOfRangeException(nameof(GoldMinimum));
        if (Equipment.Concat(Consumables).Concat(Buffs).Any(value => value.Weight <= 0))
            throw new ArgumentException("Treasure weights must be positive.");
    }
}

public sealed record RunTreasureResolutionState(
    string NodeId,
    int Gold,
    ContentId? EquipmentContentId,
    ContentId? ConsumableContentId,
    ContentId? BuffContentId,
    string TargetCharacterId,
    bool Confirmed = false);

public sealed record PureRunMapState(
    PureRunMapPhase Phase,
    string CurrentNodeId,
    IReadOnlyList<string> ReachableNodeIds,
    IReadOnlyList<string> VisitedNodeIds,
    IReadOnlyDictionary<string, string> MysteryEventAssignments,
    string? PendingNodeId = null,
    string? PendingTransactionKey = null,
    string? SelectedNodeId = null,
    RunNodeLifecycle NodeLifecycle = RunNodeLifecycle.Available,
    IReadOnlyList<RunStoreOfferState>? StoreOffers = null,
    RunMysteryResolutionState? MysteryResolution = null,
    IReadOnlyList<RunPersistentStatusState>? PendingStatuses = null,
    IReadOnlyDictionary<string, string>? MysteryAdjudicatorAssignments = null,
    ContentId? MapContentId = null,
    int MapLayoutVersion = 0,
    RunTreasureResolutionState? TreasureResolution = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, PureRunNodeIntelState>? NodeIntelStates = null);

public sealed record RunStoreOfferState(
    ContentId ContentId,
    int Price,
    bool IsConsumable,
    ItemInstanceId InstanceId,
    bool Purchased = false);

public sealed record RunMysteryResolutionState(
    string EventId,
    string OptionId,
    string CharacterId,
    int SuccessRate,
    int Roll,
    bool Succeeded,
    string Effect,
    int Amount,
    ContentId? EffectContentId = null,
    bool Confirmed = false);

public sealed record RunPersistentStatusState(string CharacterId, ContentId StatusId, int Duration);

public sealed record RunNodeTransaction(
    string TransactionKey,
    string NodeId,
    PureRunNodeKind Kind,
    bool Committed = false);

public sealed record PureRunMapResult(bool Succeeded, string? RejectionCode, PureRunMapState State, RunNodeTransaction? Transaction = null, bool WasDuplicate = false);
