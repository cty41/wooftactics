using Tactics.Core.Content;

namespace Tactics.Core.Runs;

/// <summary>Save-backed scene identity, independent of encounter content reuse and display board names.</summary>
public sealed record RunNodeSceneIdentity
{
    public RunNodeSceneIdentity(ContentId actId, int definitionVersion, string nodeId, ContentId templateId)
    {
        if (actId == default || templateId == default) throw new ArgumentException("Scene content identities are required.");
        if (definitionVersion < 1) throw new ArgumentOutOfRangeException(nameof(definitionVersion));
        if (string.IsNullOrWhiteSpace(nodeId) || nodeId != nodeId.Trim())
            throw new ArgumentException("A canonical node identity is required.", nameof(nodeId));
        ActId = actId;
        DefinitionVersion = definitionVersion;
        NodeId = nodeId;
        TemplateId = templateId;
    }

    public ContentId ActId { get; }
    public int DefinitionVersion { get; }
    public string NodeId { get; }
    public ContentId TemplateId { get; }
}
