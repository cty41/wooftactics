using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

public sealed record RunSaveDecodeResultV12(bool Succeeded, string? ErrorCode, PureRunSaveSnapshot? Snapshot,
    int MigratedFromSchema, bool RequiresNewRun, bool RequiresNodeMigration);

/// <summary>
/// Explicit scene identity boundary. Legacy decoding does not guess a node from a reused encounter.
/// Production adoption requires the application to resolve legacy identity before saving or acquiring a scene lease.
/// </summary>
public static class RunSaveDocumentV12
{
    public const int SchemaVersion = 12;

    public static string Encode(PureRunSaveSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string? error = ValidateIdentities(snapshot.ActiveRun);
        if (error is not null) throw new ArgumentException(error, nameof(snapshot));
        PureRunState? run = snapshot.ActiveRun;
        PureRunSaveSnapshot legacy = snapshot with
        {
            ActiveRun = run is null ? null : Copy(run,
                run.AdventureState is null ? null : run.AdventureState with { SceneIdentity = null },
                run.Checkpoint is null ? null : run.Checkpoint with { SceneIdentity = null })
        };
        JsonObject root = Parse(RunSaveDocumentV11.Encode(legacy));
        JsonObject payload = Object(root, "payload");
        if (run is not null)
        {
            JsonObject active = Object(payload, "activeRun");
            Object(active, "adventureState")["sceneIdentity"] = WriteIdentity(run.AdventureState!.SceneIdentity!);
            if (run.Checkpoint is not null)
                Object(active, "checkpoint")["sceneIdentity"] = WriteIdentity(run.Checkpoint.SceneIdentity!);
        }
        root["schemaVersion"] = SchemaVersion;
        root["payloadSha256"] = Hash(payload);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
    }

    public static RunSaveDecodeResultV12 Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Fail("save.empty_document");
        try
        {
            JsonObject root = Parse(json);
            int version = root["schemaVersion"]?.GetValue<int>() ?? 0;
            if (version is < 1 or > SchemaVersion) return Fail("save.unsupported_schema");
            if (version < SchemaVersion)
            {
                RunSaveDecodeResultV11 legacy = RunSaveDocumentV11.Decode(json);
                // Some historical wrappers drop the inner RequiresNewRun flag after clearing ActiveRun.
                // Preserve that loss explicitly; never report a vanished committed run as a clean empty save.
                bool discardedRun = legacy.Succeeded && (root["payload"] as JsonObject)?["activeRun"] is JsonObject &&
                    legacy.Snapshot?.ActiveRun is null;
                return new(legacy.Succeeded, legacy.ErrorCode, legacy.Snapshot, version,
                    legacy.RequiresNewRun || discardedRun, legacy.Succeeded && legacy.Snapshot?.ActiveRun is not null);
            }

            JsonObject payload = Object(root, "payload");
            if (!EqualHash(root["payloadSha256"]?.GetValue<string>() ?? string.Empty, Hash(payload)))
                return Fail("save.payload_hash_mismatch");
            RunNodeSceneIdentity? current = null, checkpoint = null;
            if (payload["activeRun"] is JsonObject active)
            {
                if (active["adventureState"] is JsonObject adventure)
                {
                    current = ReadIdentity(adventure["sceneIdentity"]);
                    adventure.Remove("sceneIdentity");
                }
                if (active["checkpoint"] is JsonObject pending)
                {
                    checkpoint = ReadIdentity(pending["sceneIdentity"]);
                    pending.Remove("sceneIdentity");
                }
            }
            // Reuse established V11 validation/normalization for all existing payload fields.
            // Only a parsed copy is changed; no file or caller-owned state is mutated.
            root["schemaVersion"] = RunSaveDocumentV11.SchemaVersion;
            root["payloadSha256"] = Hash(payload);
            RunSaveDecodeResultV11 decoded = RunSaveDocumentV11.Decode(root.ToJsonString());
            if (!decoded.Succeeded || decoded.Snapshot is null) return Fail(decoded.ErrorCode ?? "save.invalid_payload");
            PureRunSaveSnapshot snapshot = decoded.Snapshot;
            if (snapshot.ActiveRun is PureRunState run)
            {
                run = Copy(run,
                    run.AdventureState is null ? null : run.AdventureState with { SceneIdentity = current },
                    run.Checkpoint is null ? null : run.Checkpoint with { SceneIdentity = checkpoint });
                string? error = ValidateIdentities(run);
                if (error is not null) return Fail(error);
                snapshot = snapshot with { ActiveRun = run };
            }
            return new(true, null, snapshot, 0, false, false);
        }
        catch (JsonException) { return Fail("save.invalid_json"); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        { return Fail("save.invalid_payload"); }
    }

    private static string? ValidateIdentities(PureRunState? run)
    {
        if (run is null) return null;
        RunNodeSceneIdentity? current = run.AdventureState?.SceneIdentity;
        if (current is null || (run.Checkpoint is not null && run.Checkpoint.SceneIdentity is null))
            return "save.scene_identity_missing";
        if (run.AdventureState!.Lifecycle != RunAdventureLifecycle.MapActive) return "save.legacy_lifecycle_unmigrated";
        if (run.Phase == PureRunPhase.PendingBattle && run.Checkpoint is null) return "save.invalid_payload";
        if (run.Checkpoint is not null && run.Checkpoint.SceneIdentity != current) return "save.scene_identity_mismatch";
        return null;
    }

    private static RunNodeSceneIdentity? ReadIdentity(JsonNode? node)
    {
        if (node is null) return null;
        if (node is not JsonObject identity) throw new ArgumentException("Scene identity must be an object.");
        return new(new ContentId(identity["actId"]?.GetValue<string>() ?? string.Empty),
            identity["definitionVersion"]?.GetValue<int>() ?? 0,
            identity["nodeId"]?.GetValue<string>() ?? string.Empty,
            new ContentId(identity["templateId"]?.GetValue<string>() ?? string.Empty));
    }

    private static JsonObject WriteIdentity(RunNodeSceneIdentity identity) => new()
    {
        ["actId"] = identity.ActId.Value,
        ["definitionVersion"] = identity.DefinitionVersion,
        ["nodeId"] = identity.NodeId,
        ["templateId"] = identity.TemplateId.Value
    };

    private static PureRunState Copy(PureRunState run, RunAdventureState? adventure, RunEncounterCheckpoint? checkpoint) =>
        new(run.RunId, run.Seed, run.Revision, run.Phase, run.EncounterIndex, run.EncounterContentId,
            run.Party, run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression,
            run.AppliedTransactionKeys, run.Gold, run.BattlesCompleted, run.EnemiesDefeated,
            run.AcquiredItems, checkpoint, run.MapState, run.NodeTransaction, run.EscortState, adventure);

    private static JsonObject Parse(string json) => JsonNode.Parse(json) as JsonObject ?? throw new JsonException("Invalid root.");
    private static JsonObject Object(JsonObject parent, string key) => parent[key] as JsonObject ?? throw new JsonException($"Invalid {key}.");
    private static string Hash(JsonObject payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false })))).ToLowerInvariant();
    private static bool EqualHash(string left, string right) => CryptographicOperations.FixedTimeEquals(
        Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static RunSaveDecodeResultV12 Fail(string error) => new(false, error, null, 0, false, false);
}
