#if TOOLS
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

internal readonly record struct AuthoringBridgeShutdownResult(
    bool Completed,
    int PendingRequestCount,
    string Diagnostic);

[Tool]
public partial class TacticsAuthoringEditorBridge : Node, ISerializationListener
{
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private NamedPipeAuthoringServer? _server;
    private EditorUndoRedoManager? _undoRedo;
    private string _token = string.Empty;
    private string _pipeName = string.Empty;
    private string _descriptorPath = string.Empty;
    private string _projectRoot = string.Empty;
    private bool _ready;
    private int _shutdown;
    private bool _allowMissingUndoRedoForLifecycleTest;
    private TacticsAuthoringEditorService? _authoring;
    private TacticsAuthoringEditorService Authoring =>
        _authoring ?? throw new InvalidOperationException("Authoring bridge service is not active.");

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    internal void ConfigureForLifecycleTest() => _allowMissingUndoRedoForLifecycleTest = true;
    internal bool IsReady => _ready;
    public override void _EnterTree()
    {
        StartBridge(requireUndoRedo: true);
    }

    public void OnBeforeSerialize()
    {
        ShutdownForReload(TimeSpan.FromSeconds(2));
    }

    public void OnAfterDeserialize()
    {
        // Godot invokes this while other tool scripts may still have old handles.
        // TacticsEditorController defers the actual restart until deserialization finishes.
    }

    internal void RestartAfterReload(EditorUndoRedoManager undoRedo)
    {
        Configure(undoRedo);
        Interlocked.Exchange(ref _shutdown, 0);
        StartBridge(requireUndoRedo: false);
    }

    private void StartBridge(bool requireUndoRedo)
    {
        if (_server is not null || Volatile.Read(ref _shutdown) != 0) return;
        if (requireUndoRedo && _undoRedo is null && !_allowMissingUndoRedoForLifecycleTest)
            throw new InvalidOperationException("Authoring bridge requires Editor UndoRedo.");
        _token = Guid.NewGuid().ToString("N"); _pipeName = $"tactics-authoring-{System.Environment.ProcessId}-{Guid.NewGuid():N}";
        _authoring = new TacticsAuthoringEditorService();
        _projectRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://..")).TrimEnd(Path.DirectorySeparatorChar);
        _descriptorPath = ProjectSettings.GlobalizePath($"res://.godot/tactics-authoring-session-{System.Environment.ProcessId}.json");
        RemoveDeadDescriptors();
        _ready = false;
        _server = new NamedPipeAuthoringServer(_pipeName);
        WriteDescriptor("initializing");
        SetProcess(true);
        GD.Print($"[Tactics Tooling] Authoring bridge initializing on {_pipeName}.");
    }
    public override void _ExitTree() => ShutdownForReload(TimeSpan.FromSeconds(2));

    internal AuthoringBridgeShutdownResult ShutdownForReload(TimeSpan timeout)
    {
        if (Interlocked.Exchange(ref _shutdown, 1) != 0)
            return new AuthoringBridgeShutdownResult(_server is null, 0,
                "Authoring bridge shutdown was already requested.");

        _ready = false;
        try { WriteDescriptor("reloading"); } catch { }
        string pipeState = _server?.State ?? "none";
        bool stopped = _server?.Shutdown(timeout) ?? true;
        int pending = _server?.PendingRequestCount ?? 0;
        _server?.Dispose();
        _server = null;
        _authoring = null;
        if (!string.IsNullOrWhiteSpace(_descriptorPath) && File.Exists(_descriptorPath)) File.Delete(_descriptorPath);
        return new AuthoringBridgeShutdownResult(stopped, pending,
            $"Authoring bridge stopped synchronously; pipe={pipeState}, timeoutBudget={timeout.TotalMilliseconds:0}ms.");
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (Volatile.Read(ref _shutdown) != 0) return;
        try
        {
            if (_server?.TryReadRequest(out string requestJson) != true) return;
            string response;
            try { response = HandleOnMainThread(requestJson); }
            catch (Exception error) { response = ErrorJson(error.Message).ToJsonString(); }
            _server.WriteResponse(response);
        }
        catch (Exception error)
        {
            _server?.AbortConnection();
            GD.PushError($"[Tactics Tooling] Authoring bridge request failed: {error.Message}");
        }
    }

    private string HandleOnMainThread(string json)
    {
        if (!_ready || EditorInterface.Singleton.GetResourceFilesystem().IsScanning()) throw new InvalidOperationException("Editor bridge is not ready (filesystem scan or reload in progress).");
        using JsonDocument payload = JsonDocument.Parse(json); JsonElement root = payload.RootElement;
        if (root.GetProperty("sessionToken").GetString() != _token) throw new UnauthorizedAccessException("Authoring session token mismatch.");
        if (!string.Equals(Path.GetFullPath(root.GetProperty("projectRoot").GetString()!).TrimEnd(Path.DirectorySeparatorChar), _projectRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Authoring project root mismatch.");
        string tool = root.GetProperty("tool").GetString()!; JsonElement arguments = root.GetProperty("arguments");
        JsonObject response = tool switch
        {
            "tactics_authoring_list" => List(arguments),
            "tactics_authoring_get" => Get(arguments),
            "tactics_authoring_validate" => Validate(arguments),
            "tactics_authoring_apply" => Apply(arguments),
            "tactics_authoring_preview" => Preview(arguments),
            "tactics_authoring_reference_audit" => ReferenceAudit(arguments),
            _ => throw new InvalidOperationException($"Unknown authoring tool '{tool}'.")
        };
        return response.ToJsonString();
    }

    private JsonObject List(JsonElement arguments)
    {
        string? kind = arguments.TryGetProperty("kind", out JsonElement supplied) ? supplied.GetString() : null;
        StoredAuthoringDocument[] documents = Authoring.List(kind).ToArray();
        return new JsonObject
        {
            ["succeeded"] = true,
            ["documents"] = new JsonArray(documents.Select(value => (JsonNode)new JsonObject
            {
                ["kind"] = value.Entry.ResourceTypeIdValue,
                ["contentId"] = value.Document.ContentId,
                ["revision"] = value.Revision,
                ["path"] = value.Entry.DiagnosticPathValue,
                ["diagnostics"] = new JsonArray()
            }).ToArray())
        };
    }
    private JsonObject Get(JsonElement arguments)
    {
        StoredAuthoringDocument stored = LoadDocument(arguments);
        return new JsonObject { ["succeeded"] = true, ["kind"] = stored.Entry.ResourceTypeIdValue,
            ["contentId"] = stored.Document.ContentId, ["revision"] = stored.Revision,
            ["snapshot"] = stored.Snapshot, ["dependencies"] = StringArray(stored.Document.Dependencies) };
    }
    private JsonObject Validate(JsonElement arguments)
    {
        if ((arguments.TryGetProperty("changes", out JsonElement changes) && changes.ValueKind == JsonValueKind.Array) ||
            (arguments.TryGetProperty("lifecycle", out JsonElement lifecycle) && lifecycle.ValueKind == JsonValueKind.Array))
        {
            BridgeBatchPayload payload = ParseBatchArguments(arguments, Guid.NewGuid().ToString("N"), includeStoredBefore: false, out _);
            Authoring.ValidateBatch(BuildBatch(payload));
            return new JsonObject { ["succeeded"] = true, ["batch"] = true,
                ["diagnostics"] = new JsonArray(), ["changeCount"] = payload.Changes.Length,
                ["lifecycleCount"] = payload.Lifecycle.Length };
        }
        StoredAuthoringDocument stored = LoadDocument(arguments); string snapshot = arguments.TryGetProperty("snapshot", out JsonElement supplied) && supplied.ValueKind == JsonValueKind.String ? supplied.GetString()! : stored.Snapshot;
        string? expected = arguments.TryGetProperty("expectedRevision", out JsonElement expectedValue) && expectedValue.ValueKind == JsonValueKind.String ? expectedValue.GetString() : null;
        AuthoringValidationResult validation = Authoring.Validate(stored.Entry.ResourceTypeIdValue, stored.Document.ContentId, snapshot, expected);
        return ValidationJson(validation, stored.Document.ContentId);
    }
    private JsonObject Apply(JsonElement arguments)
    {
        if ((arguments.TryGetProperty("changes", out JsonElement changes) && changes.ValueKind == JsonValueKind.Array) ||
            (arguments.TryGetProperty("lifecycle", out JsonElement lifecycle) && lifecycle.ValueKind == JsonValueKind.Array))
            return ApplyBatch(arguments);
        StoredAuthoringDocument stored = LoadDocument(arguments); string expected = arguments.GetProperty("expectedRevision").GetString()!, afterJson = arguments.GetProperty("snapshot").GetString()!;
        AuthoringValidationResult validation = Authoring.Validate(stored.Entry.ResourceTypeIdValue, stored.Document.ContentId, afterJson, expected); if (!validation.Succeeded) throw new InvalidOperationException(string.Join("; ", validation.Diagnostics.Select(value => value.Message)));
        _undoRedo!.CreateAction($"MCP apply {stored.Entry.ContentIdValue}", UndoRedo.MergeMode.Disable, stored.Resource); _undoRedo.AddDoMethod(this, MethodName.ApplySerializedDocument, stored.Entry.ResourceTypeIdValue, stored.Entry.ContentIdValue, expected, afterJson); _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedDocument, stored.Entry.ResourceTypeIdValue, stored.Entry.ContentIdValue, validation.PredictedRevision, stored.Snapshot); _undoRedo.CommitAction();
        StoredAuthoringDocument applied = Authoring.Get(stored.Entry.ResourceTypeIdValue, stored.Entry.ContentIdValue);
        return new JsonObject { ["succeeded"] = true, ["contentId"] = applied.Document.ContentId,
            ["revision"] = applied.Revision, ["modified"] = StringArray([applied.Entry.DiagnosticPathValue]),
            ["evidence"] = "EditorUndoRedo+staged-ResourceSaver+typed-reload" };
    }
    private JsonObject ApplyBatch(JsonElement arguments)
    {
        string changeId = Guid.NewGuid().ToString("N");
        BridgeBatchPayload payload = ParseBatchArguments(arguments, changeId, includeStoredBefore: true, out BridgeDocumentChange[] beforeValues);
        List<BridgeDocumentChange> after = payload.Changes.ToList(); List<BridgeAssetChange> assets = payload.Lifecycle.ToList();
        if (after.Count == 0 && assets.Count == 0)
            throw new InvalidOperationException("MCP batch apply requires document or lifecycle changes.");
        AuthoringBatchChangeSet batch = BuildBatch(payload);
        Authoring.ValidateBatch(batch);
        string afterPayload = SerializeBatch(payload);
        string beforePayload = SerializeBatch(new BridgeBatchPayload(Guid.NewGuid().ToString("N"), beforeValues, Array.Empty<BridgeAssetChange>()));
        _undoRedo!.CreateAction($"MCP apply {after.Count + assets.Count} authored changes", UndoRedo.MergeMode.Disable);
        _undoRedo.AddDoMethod(this, MethodName.ApplySerializedAuthoringBatch, afterPayload);
        if (assets.Count > 0)
            _undoRedo.AddUndoMethod(this, MethodName.UndoSerializedLifecycleBatch, changeId);
        else
            _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedAuthoringBatch, beforePayload);
        _undoRedo.CommitAction();
        HashSet<string> deletedIds = assets.Where(value => ParseAssetKind(value.Operation) == AuthoringAssetChangeKind.Delete)
            .Select(value => value.ContentId).ToHashSet(StringComparer.Ordinal);
        string[] resultIds = after.Select(value => value.ContentId)
            .Concat(assets.Where(value => ParseAssetKind(value.Operation) is AuthoringAssetChangeKind.Create or AuthoringAssetChangeKind.Duplicate)
                .Select(value => value.ContentId))
            .Distinct(StringComparer.Ordinal).Where(value => !deletedIds.Contains(value)).ToArray();
        StoredAuthoringDocument[] applied = resultIds.Select(value =>
        {
            BridgeDocumentChange? change = after.FirstOrDefault(item => item.ContentId == value);
            BridgeAssetChange lifecycleChange = assets.FirstOrDefault(item => item.ContentId == value)!;
            string? kind = change?.Kind ?? lifecycleChange?.ResourceType;
            if (kind is null && lifecycleChange?.SourceContentId is { } sourceId)
                kind = Authoring.List().Single(item => item.Document.ContentId == sourceId).Entry.ResourceTypeIdValue;
            if (kind is null) throw new InvalidOperationException($"Lifecycle Resource type missing for '{value}'.");
            return Authoring.Get(kind, value);
        }).ToArray();
        var revisions = new JsonObject();
        foreach (StoredAuthoringDocument value in applied) revisions[value.Document.ContentId] = value.Revision;
        return new JsonObject
        {
            ["succeeded"] = true, ["revisions"] = revisions,
            ["created"] = StringArray(assets.Where(value => ParseAssetKind(value.Operation) is AuthoringAssetChangeKind.Create or AuthoringAssetChangeKind.Duplicate).Select(value => value.ContentId)),
            ["modified"] = StringArray(after.Where(value => value.ExpectedRevision != "new").Select(value => value.ContentId)),
            ["deleted"] = StringArray(deletedIds),
            ["resources"] = new JsonArray(applied.Select(value => (JsonNode)new JsonObject
            {
                ["contentId"] = value.Document.ContentId, ["path"] = value.Entry.DiagnosticPathValue,
                ["uid"] = value.Entry.ResourceUidValue, ["revision"] = value.Revision, ["reloadValidated"] = true
            }).ToArray()),
            ["evidence"] = "single-EditorUndoRedo+atomic-Resource-Catalog-UID-ledger+typed-reload"
        };
    }

    private BridgeBatchPayload ParseBatchArguments(JsonElement arguments, string changeId, bool includeStoredBefore,
        out BridgeDocumentChange[] before)
    {
        var after = new List<BridgeDocumentChange>(); var beforeValues = new List<BridgeDocumentChange>();
        if (arguments.TryGetProperty("changes", out JsonElement changes))
        foreach (JsonElement change in changes.EnumerateArray())
        {
            string kind = change.GetProperty("kind").GetString()!, contentId = change.GetProperty("contentId").GetString()!;
            string expected = change.GetProperty("expectedRevision").GetString()!, snapshot = change.GetProperty("snapshot").GetString()!;
            after.Add(new BridgeDocumentChange(kind, contentId, expected, snapshot));
            if (includeStoredBefore && expected != "new")
            {
                StoredAuthoringDocument stored = Authoring.Get(kind, contentId);
                AuthoringValidationResult validation = Authoring.Validate(kind, contentId, snapshot, expected);
                if (!validation.Succeeded) throw new InvalidOperationException(string.Join("; ", validation.Diagnostics.Select(value => value.Message)));
                beforeValues.Add(new BridgeDocumentChange(kind, contentId, validation.PredictedRevision, stored.Snapshot));
            }
        }
        var assets = new List<BridgeAssetChange>();
        if (arguments.TryGetProperty("lifecycle", out JsonElement lifecycle))
        foreach (JsonElement item in lifecycle.EnumerateArray())
        {
            var asset = new BridgeAssetChange(item.GetProperty("operation").GetString()!, item.GetProperty("contentId").GetString()!,
                OptionalString(item, "sourceContentId"), OptionalString(item, "resourceType"), OptionalString(item, "path"),
                OptionalString(item, "expectedReferenceRevision"), OptionalString(item, "initialSnapshot"));
            assets.Add(asset);
            if (asset.InitialSnapshot is not null)
            {
                AuthoringAssetChangeKind operation = ParseAssetKind(asset.Operation);
                if (operation is not (AuthoringAssetChangeKind.Create or AuthoringAssetChangeKind.Duplicate))
                    throw new InvalidOperationException($"Lifecycle '{asset.ContentId}' can only provide initialSnapshot for create or duplicate.");
                if (after.Any(value => value.ContentId == asset.ContentId))
                    throw new InvalidOperationException($"Lifecycle '{asset.ContentId}' cannot provide both initialSnapshot and a document change.");
                if (asset.ResourceType is null) throw new InvalidOperationException($"Lifecycle '{asset.ContentId}' initialSnapshot requires resourceType.");
                after.Add(new BridgeDocumentChange(asset.ResourceType, asset.ContentId, "new", asset.InitialSnapshot));
            }
        }
        before = beforeValues.ToArray(); return new BridgeBatchPayload(changeId, after.ToArray(), assets.ToArray());
    }
    private JsonObject Preview(JsonElement arguments)
    {
        StoredAuthoringDocument stored = LoadDocument(arguments); string snapshotText = arguments.TryGetProperty("snapshot", out JsonElement snapshot) && snapshot.ValueKind == JsonValueKind.String ? snapshot.GetString()! : stored.Snapshot;
        int seedValue = arguments.TryGetProperty("seed", out JsonElement seed) ? seed.GetInt32() : 0;
        AuthoringValidationResult validation = Authoring.Validate(stored.Entry.ResourceTypeIdValue, stored.Document.ContentId, snapshotText);
        object? evidence = null;
        if (validation.Succeeded && stored.Entry.ResourceTypeIdValue == "skill" &&
            arguments.TryGetProperty("context", out JsonElement context))
        {
            var typed = new SkillBattlePreviewContext(
                context.GetProperty("encounterContentId").GetString()!,
                context.GetProperty("casterUnitInstanceId").GetString()!,
                context.TryGetProperty("targetUnitInstanceId", out JsonElement target) && target.ValueKind == JsonValueKind.String ? target.GetString() : null,
                new GridCellAuthoring(context.GetProperty("targetX").GetInt32(), context.GetProperty("targetY").GetInt32()),
                context.TryGetProperty("seed", out JsonElement contextSeed) ? contextSeed.GetUInt64() : (ulong)seedValue,
                context.TryGetProperty("casterUnitContentId", out JsonElement casterContent) ? casterContent.GetString() : null);
            evidence = Authoring.PreviewSkillBattle(stored.Document.ContentId, snapshotText, typed);
        }
        else if (validation.Succeeded) evidence = Authoring.Preview(stored.Entry.ResourceTypeIdValue, stored.Document.ContentId, snapshotText, seedValue);
        return new JsonObject { ["succeeded"] = validation.Succeeded, ["contentId"] = stored.Document.ContentId,
            ["revision"] = validation.PredictedRevision, ["previewKind"] = stored.Entry.ResourceTypeIdValue,
            ["seed"] = seedValue, ["available"] = validation.PreviewAvailable,
            ["diagnostics"] = DiagnosticsJson(validation.Diagnostics), ["evidence"] = PreviewEvidenceJson(evidence) };
    }
    private JsonObject ReferenceAudit(JsonElement arguments)
    {
        string? id = arguments.TryGetProperty("contentId", out JsonElement supplied) ? supplied.GetString() : null; AuthoringCatalogAuditRow[] rows = AuthoringCatalogAuditService.Audit(LoadCatalog()).Where(value => id is null || value.ContentId == id).ToArray();
        return new JsonObject { ["succeeded"] = true, ["rows"] = new JsonArray(rows.Select(value => (JsonNode)new JsonObject
        {
            ["contentId"] = value.ContentId, ["type"] = value.Type, ["path"] = value.Path, ["uid"] = value.Uid,
            ["revision"] = value.Revision, ["ownership"] = value.Ownership.ToString(),
            ["forwardReferences"] = StringArray(value.ForwardReferences), ["reverseReferences"] = StringArray(value.ReverseReferences),
            ["diagnostics"] = DiagnosticsJson(value.Diagnostics)
        }).ToArray()) };
    }

    public void ApplySerializedDocument(string kind, string contentId, string expectedRevision, string snapshot)
    {
        _ = Authoring.ApplySingle(kind, contentId, expectedRevision, snapshot);
    }

    public void ApplySerializedAuthoringBatch(string payload)
    {
        BridgeBatchPayload batch = DeserializeBatch(payload);
        _ = Authoring.ApplyBatch(BuildBatch(batch));
    }

    public void UndoSerializedLifecycleBatch(string changeId) => Authoring.UndoLifecycleBatch(changeId);

    private static AuthoringBatchChangeSet BuildBatch(BridgeBatchPayload payload) => new(
        payload.ChangeId,
        payload.Changes.Select(value => new AuthoringDocumentChange(ParseKind(value.Kind), value.ContentId,
            value.ExpectedRevision, value.Snapshot)),
        payload.Lifecycle.Select(value => new AuthoringAssetChange(ParseAssetKind(value.Operation), value.ContentId,
            value.SourceContentId, value.ResourceType, value.Path, value.ExpectedReferenceRevision)));

    private StoredAuthoringDocument LoadDocument(JsonElement arguments)
    {
        string kind = arguments.GetProperty("kind").GetString()!, contentId = arguments.GetProperty("contentId").GetString()!; return Authoring.Get(kind, contentId);
    }
    private GodotResourceCatalog LoadCatalog() => Authoring.LoadCatalog();
    private static AuthoringDocumentKind ParseKind(string value) => AuthoringResourceHandlerRegistry.Normalize(value) switch
    {
        "run-map" => AuthoringDocumentKind.Map,
        "event" => AuthoringDocumentKind.Event,
        "treasure" => AuthoringDocumentKind.Treasure,
        "encounter" => AuthoringDocumentKind.Encounter,
        "battle-layout" => AuthoringDocumentKind.BattleLayout,
        "ai" => AuthoringDocumentKind.Ai,
        "skill" => AuthoringDocumentKind.Skill,
        "presentation" => AuthoringDocumentKind.Presentation,
        _ => throw new InvalidOperationException($"Unsupported authoring kind '{value}'.")
    };
    private static AuthoringAssetChangeKind ParseAssetKind(string value) =>
        Enum.TryParse(value, true, out AuthoringAssetChangeKind parsed)
            ? parsed
            : throw new InvalidOperationException($"Unsupported lifecycle operation '{value}'.");
    private void WriteDescriptor(string state) { Directory.CreateDirectory(Path.GetDirectoryName(_descriptorPath)!); File.WriteAllText(_descriptorPath,
        new JsonObject { ["projectRoot"] = _projectRoot, ["transport"] = "named-pipe",
            ["endpointPath"] = string.Empty, ["pipeName"] = _pipeName,
            ["sessionToken"] = _token, ["processId"] = System.Environment.ProcessId, ["state"] = state }.ToJsonString()); }
    public void MarkReady()
    {
        if (Volatile.Read(ref _shutdown) != 0 || !IsInsideTree() || EditorInterface.Singleton.GetResourceFilesystem().IsScanning()) return;
        _ready = true; WriteDescriptor("ready"); GD.Print($"[Tactics Tooling] Authoring bridge ready on {_pipeName}.");
    }

    private void RemoveDeadDescriptors()
    {
        string directory = Path.GetDirectoryName(_descriptorPath)!;
        if (!Directory.Exists(directory)) return;
        foreach (string path in Directory.GetFiles(directory, "tactics-authoring-session-*.json"))
        {
            if (string.Equals(path, _descriptorPath, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using JsonDocument json = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = json.RootElement;
                string candidateRoot = Path.GetFullPath(root.GetProperty("projectRoot").GetString()!).TrimEnd(Path.DirectorySeparatorChar);
                int processId = root.GetProperty("processId").GetInt32();
                if (!string.Equals(candidateRoot, _projectRoot, StringComparison.OrdinalIgnoreCase) || IsProcessAlive(processId)) continue;
                File.Delete(path);
            }
            catch (JsonException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool IsProcessAlive(int processId)
    {
        try { using Process process = Process.GetProcessById(processId); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private static JsonObject ErrorJson(string message) => new() { ["succeeded"] = false, ["error"] = message };

    private static JsonArray StringArray(IEnumerable<string> values) =>
        new(values.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray());

    private static JsonObject ValidationJson(AuthoringValidationResult validation, string contentId) => new()
    {
        ["succeeded"] = validation.Succeeded,
        ["contentId"] = contentId,
        ["predictedRevision"] = validation.PredictedRevision,
        ["diagnostics"] = DiagnosticsJson(validation.Diagnostics),
        ["previewAvailable"] = validation.PreviewAvailable
    };

    private static JsonArray DiagnosticsJson(IEnumerable<AuthoringDiagnostic> diagnostics) =>
        new(diagnostics.Select(value => (JsonNode)new JsonObject
        {
            ["code"] = value.Code,
            ["severity"] = value.Severity.ToString(),
            ["message"] = value.Message,
            ["path"] = value.Path
        }).ToArray());

    private static JsonNode? PreviewEvidenceJson(object? evidence) => evidence switch
    {
        null => null,
        AuthoringPreviewEvidence value => new JsonObject
        {
            ["kind"] = value.Kind,
            ["summary"] = value.Summary,
            ["values"] = StringMap(value.Values)
        },
        SkillBattlePreviewResult value => new JsonObject
        {
            ["succeeded"] = value.Succeeded,
            ["rejectionReason"] = value.RejectionReason,
            ["beforeFingerprint"] = value.BeforeFingerprint,
            ["afterFingerprint"] = value.AfterFingerprint,
            ["events"] = StringArray(value.Events),
            ["values"] = StringMap(value.Values),
            ["sourceStateUnchanged"] = value.SourceStateUnchanged
        },
        _ => throw new InvalidOperationException($"Unsupported authoring preview evidence '{evidence.GetType().Name}'.")
    };

    private static JsonObject StringMap(IEnumerable<KeyValuePair<string, string>> values)
    {
        var result = new JsonObject();
        foreach ((string key, string value) in values) result[key] = value;
        return result;
    }

    private static string SerializeBatch(BridgeBatchPayload payload)
    {
        var changes = new JsonArray(payload.Changes.Select(value => (JsonNode)new JsonObject
        {
            ["kind"] = value.Kind, ["contentId"] = value.ContentId,
            ["expectedRevision"] = value.ExpectedRevision, ["snapshot"] = value.Snapshot
        }).ToArray());
        var lifecycle = new JsonArray(payload.Lifecycle.Select(value => (JsonNode)new JsonObject
        {
            ["operation"] = value.Operation, ["contentId"] = value.ContentId,
            ["sourceContentId"] = value.SourceContentId, ["resourceType"] = value.ResourceType,
            ["path"] = value.Path, ["expectedReferenceRevision"] = value.ExpectedReferenceRevision,
            ["initialSnapshot"] = value.InitialSnapshot
        }).ToArray());
        return new JsonObject { ["changeId"] = payload.ChangeId, ["changes"] = changes, ["lifecycle"] = lifecycle }.ToJsonString();
    }

    private static BridgeBatchPayload DeserializeBatch(string payload)
    {
        using JsonDocument parsed = JsonDocument.Parse(payload);
        JsonElement root = parsed.RootElement;
        BridgeDocumentChange[] changes = root.GetProperty("changes").EnumerateArray().Select(value => new BridgeDocumentChange(
            value.GetProperty("kind").GetString()!, value.GetProperty("contentId").GetString()!,
            value.GetProperty("expectedRevision").GetString()!, value.GetProperty("snapshot").GetString()!)).ToArray();
        BridgeAssetChange[] lifecycle = root.GetProperty("lifecycle").EnumerateArray().Select(value => new BridgeAssetChange(
            value.GetProperty("operation").GetString()!, value.GetProperty("contentId").GetString()!,
            OptionalString(value, "sourceContentId"), OptionalString(value, "resourceType"), OptionalString(value, "path"),
            OptionalString(value, "expectedReferenceRevision"), OptionalString(value, "initialSnapshot"))).ToArray();
        return new BridgeBatchPayload(root.GetProperty("changeId").GetString()!, changes, lifecycle);
    }

    private static string? OptionalString(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private sealed record BridgeDocumentChange(string Kind, string ContentId, string ExpectedRevision, string Snapshot);
    private sealed record BridgeAssetChange(string Operation, string ContentId, string? SourceContentId,
        string? ResourceType, string? Path, string? ExpectedReferenceRevision, string? InitialSnapshot);
    private sealed record BridgeBatchPayload(string ChangeId, BridgeDocumentChange[] Changes, BridgeAssetChange[] Lifecycle);
}
#endif
