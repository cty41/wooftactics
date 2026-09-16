using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

public sealed record RunSaveDecodeResultV11(bool Succeeded, string? ErrorCode, PureRunSaveSnapshot? Snapshot,
    int MigratedFromSchema, bool RequiresNewRun);

/// <summary>
/// Node-level recovery save boundary. Actor cells and presentation state are process-local.
/// Contract: ROGUELIKE-NODE-RECOVERY-001 (approved target).
/// </summary>
public static class RunSaveDocumentV11
{
    public const int SchemaVersion = 11;

    public static string Encode(PureRunSaveSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        PureRunSaveSnapshot recovered = NormalizeRecovery(snapshot);
        JsonObject root = ParseObject(RunSaveDocumentV10.Encode(recovered));
        root["schemaVersion"] = SchemaVersion;
        JsonObject payload = RequireObject(root, "payload");
        RemoveSessionState(payload);
        root["payloadSha256"] = PayloadHash(payload);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
    }

    public static RunSaveDecodeResultV11 Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(false, "save.empty_document", null, 0, false);
        try
        {
            JsonObject root = ParseObject(json);
            int version = root["schemaVersion"]?.GetValue<int>() ?? 0;
            if (version is < 1 or > SchemaVersion)
                return new(false, "save.unsupported_schema", null, 0, false);

            RunSaveDecodeResultV10 decoded;
            if (version == SchemaVersion)
            {
                JsonObject payload = RequireObject(root, "payload");
                string persistedHash = root["payloadSha256"]?.GetValue<string>() ?? string.Empty;
                if (!FixedEquals(persistedHash, PayloadHash(payload)))
                    return new(false, "save.payload_hash_mismatch", null, 0, false);
                root["schemaVersion"] = RunSaveDocumentV10.SchemaVersion;
                RestoreLegacyShape(payload);
                root["payloadSha256"] = PayloadHash(payload);
                decoded = RunSaveDocumentV10.Decode(root.ToJsonString());
            }
            else
            {
                decoded = RunSaveDocumentV10.Decode(json);
            }

            if (!decoded.Succeeded || decoded.Snapshot is null)
                return new(false, decoded.ErrorCode, null, version < SchemaVersion ? version : 0, decoded.RequiresNewRun);
            PureRunSaveSnapshot normalized = NormalizeRecovery(decoded.Snapshot);
            return new(true, null, normalized, version < SchemaVersion ? version : 0, decoded.RequiresNewRun);
        }
        catch (JsonException)
        {
            return new(false, "save.invalid_json", null, 0, false);
        }
        catch (ArgumentException)
        {
            return new(false, "save.invalid_payload", null, 0, false);
        }
    }

    private static PureRunSaveSnapshot NormalizeRecovery(PureRunSaveSnapshot snapshot)
    {
        PureRunState? run = snapshot.ActiveRun;
        if (run?.AdventureState is null) return RunSaveNormalizer.Normalize(snapshot);
        string? leader = run.Party.FirstOrDefault(value => !value.IsDead && value.CharacterId == run.AdventureState.LeaderId)?.CharacterId
            ?? run.Party.FirstOrDefault(value => !value.IsDead)?.CharacterId;
        if (leader is null) throw new ArgumentException("Active run has no living legal leader.", nameof(snapshot));
        RunAdventureState adventure = run.AdventureState with { LeaderId = leader, ActorCells = Array.Empty<RunAdventureActorCell>() };
        PureRunState recovered = new(run.RunId, run.Seed, run.Revision, run.Phase, run.EncounterIndex,
            run.EncounterContentId, run.Party, run.BackpackConsumables, run.BackpackEquipment,
            run.PendingProgression, run.AppliedTransactionKeys, run.Gold, run.BattlesCompleted,
            run.EnemiesDefeated, run.AcquiredItems, run.Checkpoint, run.MapState, run.NodeTransaction,
            run.EscortState, adventure);
        return RunSaveNormalizer.Normalize(snapshot with { ActiveRun = recovered });
    }

    private static void RemoveSessionState(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (string name in obj.Select(value => value.Key).ToArray())
            {
                if (name.Equals("actorCells", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("camera", StringComparison.OrdinalIgnoreCase))
                    obj.Remove(name);
                else if (obj[name] is JsonNode child)
                    RemoveSessionState(child);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
                if (child is not null) RemoveSessionState(child);
        }
    }

    private static void RestoreLegacyShape(JsonObject payload)
    {
        if (payload["activeRun"] is JsonObject active && active["adventureState"] is JsonObject adventure &&
            !adventure.ContainsKey("actorCells"))
            adventure["actorCells"] = new JsonArray();
    }

    private static string PayloadHash(JsonObject payload)
    {
        string canonical = payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static bool FixedEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.ASCII.GetBytes(left ?? string.Empty);
        byte[] rightBytes = Encoding.ASCII.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static JsonObject ParseObject(string json) => JsonNode.Parse(json) as JsonObject
        ?? throw new JsonException("Save root must be an object.");

    private static JsonObject RequireObject(JsonObject parent, string name) => parent[name] as JsonObject
        ?? throw new JsonException($"Save field '{name}' must be an object.");
}
