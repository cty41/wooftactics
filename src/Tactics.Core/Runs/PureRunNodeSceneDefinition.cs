using Tactics.Core.Content;

namespace Tactics.Core.Runs;

public enum PureRunNodeEntryStrategy { Exploration, ImmediateCombat }

/// <summary>Engine-neutral mapping from a topology node to its formal map template.</summary>
public sealed record PureRunNodeSceneDefinition
{
    public PureRunNodeSceneDefinition(string nodeId, ContentId templateId, PureRunNodeEntryStrategy entryStrategy)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("Node identity cannot be empty.", nameof(nodeId));
        if (string.IsNullOrWhiteSpace(templateId.Value))
            throw new ArgumentException("Template identity cannot be default or empty.", nameof(templateId));
        if (!Enum.IsDefined(entryStrategy))
            throw new ArgumentOutOfRangeException(nameof(entryStrategy));

        NodeId = nodeId;
        TemplateId = templateId;
        EntryStrategy = entryStrategy;
    }

    public string NodeId { get; }
    public ContentId TemplateId { get; }
    public PureRunNodeEntryStrategy EntryStrategy { get; }
}
