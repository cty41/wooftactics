using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV12Tests
{
    [Test]
    public void PendingBattleRoundTripIsByteIdempotentAndPreservesCheckpointPartyAndRandomState()
    {
        PureRunSaveSnapshot source = Pending(Identity("layer_04_event"));
        string json = RunSaveDocumentV12.Encode(source);
        RunSaveDecodeResultV12 decoded = RunSaveDocumentV12.Decode(json);
        Assert.That(decoded.Succeeded, Is.True, decoded.ErrorCode);
        Assert.Multiple(() =>
        {
            Assert.That(JsonNode.Parse(json)!["schemaVersion"]!.GetValue<int>(), Is.EqualTo(12));
            Assert.That(decoded.MigratedFromSchema, Is.Zero);
            Assert.That(decoded.RequiresNodeMigration, Is.False);
            Assert.That(decoded.RequiresNewRun, Is.False);
            Assert.That(decoded.Snapshot!.ActiveRun!.AdventureState!.SceneIdentity, Is.EqualTo(source.ActiveRun!.AdventureState!.SceneIdentity));
            Assert.That(decoded.Snapshot.ActiveRun.Checkpoint!.SceneIdentity, Is.EqualTo(source.ActiveRun.Checkpoint!.SceneIdentity));
            Assert.That(RunSaveDocumentV12.Encode(decoded.Snapshot), Is.EqualTo(json));
            Assert.That(RunSaveDocumentV12.Encode(source), Is.EqualTo(json));
        });
        AssertPreserved(source, decoded.Snapshot!);
    }

    [Test]
    public void ResumeAfterV12RoundTripReissuesSameIdentityAndRandomStateWithoutSaving()
    {
        PureRunSaveSnapshot source = Pending(Identity("layer_04_event"));
        var store = new MemoryStore { Snapshot = RunSaveDocumentV12.Decode(RunSaveDocumentV12.Encode(source)).Snapshot! };
        RunSessionResult resumed = new PureRunSessionService(Definition(), store).ResumeRun();
        Assert.That(resumed.Succeeded, Is.True, resumed.ErrorCode);
        EncounterRequest request = resumed.EncounterRequest!;
        Assert.Multiple(() =>
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request.SceneIdentity, Is.EqualTo(source.ActiveRun!.Checkpoint!.SceneIdentity));
            Assert.That(request.RunId, Is.EqualTo(source.ActiveRun.RunId));
            Assert.That(request.CheckpointRevision, Is.EqualTo(source.ActiveRun.Checkpoint.Revision));
            Assert.That(request.AdventureRevision, Is.EqualTo(source.ActiveRun.AdventureState!.Revision));
            Assert.That(request.EffectiveRandomState, Is.EqualTo(RandomState(source.ActiveRun)));
            Assert.That(JsonSerializer.Serialize(request.Party), Is.EqualTo(JsonSerializer.Serialize(source.ActiveRun.Checkpoint.Party)));
            Assert.That(store.SaveCount, Is.Zero);
        });
    }

    [TestCase(11)]
    [TestCase(10)]
    public void LegacyDecodeOnlyMarksNodeMigrationAndDoesNotRewriteOrGuessIdentity(int schema)
    {
        PureRunSaveSnapshot source = Pending(null);
        string original = schema == 11 ? RunSaveDocumentV11.Encode(source) : RunSaveDocumentV10.Encode(source);
        byte[] bytesBefore = Encoding.UTF8.GetBytes(original);
        RunSaveDecodeResultV12 decoded = RunSaveDocumentV12.Decode(original);
        Assert.That(decoded.Succeeded, Is.True, decoded.ErrorCode);
        Assert.Multiple(() =>
        {
            Assert.That(decoded.MigratedFromSchema, Is.EqualTo(schema));
            Assert.That(decoded.RequiresNodeMigration, Is.True);
            Assert.That(decoded.RequiresNewRun, Is.False);
            Assert.That(decoded.Snapshot!.ActiveRun!.AdventureState!.SceneIdentity, Is.Null);
            Assert.That(decoded.Snapshot.ActiveRun.Checkpoint!.SceneIdentity, Is.Null);
            Assert.That(Encoding.UTF8.GetBytes(original), Is.EqualTo(bytesBefore));
            Assert.That(original, Does.Not.Contain("sceneIdentity").IgnoreCase);
            Assert.Throws<ArgumentException>(() => RunSaveDocumentV12.Encode(decoded.Snapshot));
        });
        AssertPreserved(source, decoded.Snapshot!);
        if (schema == 11)
        {
            RunSaveDecodeResultV11 legacy = RunSaveDocumentV11.Decode(original);
            Assert.That(legacy.Succeeded, Is.True, legacy.ErrorCode);
            Assert.That(legacy.Snapshot!.ActiveRun!.AdventureState!.SceneIdentity, Is.Null);
            Assert.That(legacy.Snapshot.ActiveRun.Checkpoint!.SceneIdentity, Is.Null);
            Assert.That(RunSaveDocumentV11.Encode(legacy.Snapshot), Is.EqualTo(original));
        }
    }

    [Test]
    public void ReusedEncounterDoesNotCollapseDistinctNodeIdentities()
    {
        PureRunSaveSnapshot fourth = Pending(Identity("layer_04_event"));
        PureRunSaveSnapshot sixth = Pending(Identity("layer_06_event"));
        string first = RunSaveDocumentV12.Encode(fourth), second = RunSaveDocumentV12.Encode(sixth);
        PureRunState a = RunSaveDocumentV12.Decode(first).Snapshot!.ActiveRun!;
        PureRunState b = RunSaveDocumentV12.Decode(second).Snapshot!.ActiveRun!;
        Assert.Multiple(() =>
        {
            Assert.That(a.Checkpoint!.EncounterContentId, Is.EqualTo(b.Checkpoint!.EncounterContentId));
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(a.AdventureState!.SceneIdentity!.NodeId, Is.EqualTo("layer_04_event"));
            Assert.That(b.AdventureState!.SceneIdentity!.NodeId, Is.EqualTo("layer_06_event"));
            Assert.That(a.Checkpoint.SceneIdentity, Is.EqualTo(a.AdventureState.SceneIdentity));
            Assert.That(b.Checkpoint.SceneIdentity, Is.EqualTo(b.AdventureState.SceneIdentity));
        });
    }

    [TestCase("actId", "act-02")]
    [TestCase("definitionVersion", "2")]
    [TestCase("nodeId", "layer_06_event")]
    [TestCase("templateId", "adventure-map-template.other")]
    public void CheckpointIdentityMustMatchEveryCurrentIdentityField(string field, string value)
    {
        PureRunSaveSnapshot source = Pending(Identity("layer_04_event"));
        RunNodeSceneIdentity current = source.ActiveRun!.AdventureState!.SceneIdentity!;
        var conflicting = new RunNodeSceneIdentity(
            field == "actId" ? new ContentId(value) : current.ActId,
            field == "definitionVersion" ? int.Parse(value) : current.DefinitionVersion,
            field == "nodeId" ? value : current.NodeId,
            field == "templateId" ? new ContentId(value) : current.TemplateId);
        PureRunState invalid = Copy(source.ActiveRun, source.ActiveRun.AdventureState,
            source.ActiveRun.Checkpoint! with { SceneIdentity = conflicting });
        Assert.Throws<ArgumentException>(() => RunSaveDocumentV12.Encode(source with { ActiveRun = invalid }));
        string malformed = Rewrite(source, active =>
            active["checkpoint"]!["sceneIdentity"]![field] = field == "definitionVersion"
                ? JsonValue.Create(int.Parse(value)) : JsonValue.Create(value));
        AssertRejected(malformed, "save.scene_identity_mismatch");
    }

    [TestCase("adventureState")]
    [TestCase("checkpoint")]
    public void IdentityIsMandatoryOnBothCurrentStateAndExistingCheckpoint(string owner)
    {
        PureRunSaveSnapshot source = Pending(Identity("layer_04_event"));
        PureRunState run = source.ActiveRun!;
        PureRunState invalid = Copy(run,
            owner == "adventureState" ? run.AdventureState! with { SceneIdentity = null } : run.AdventureState,
            owner == "checkpoint" ? run.Checkpoint! with { SceneIdentity = null } : run.Checkpoint);
        Assert.Throws<ArgumentException>(() => RunSaveDocumentV12.Encode(source with { ActiveRun = invalid }));
        AssertRejected(Rewrite(source, active => ((JsonObject)active[owner]!).Remove("sceneIdentity")), "save.scene_identity_missing");
    }

    [Test]
    public void ActiveRunWithoutAdventureIdentityIsRejected()
    {
        PureRunSaveSnapshot source = Pending(Identity("layer_04_event"));
        PureRunState invalid = Copy(source.ActiveRun!, null, source.ActiveRun!.Checkpoint);
        Assert.Throws<ArgumentException>(() => RunSaveDocumentV12.Encode(source with { ActiveRun = invalid }));
        AssertRejected(Rewrite(source, active => active["adventureState"] = null), "save.scene_identity_missing");
    }

    [Test]
    public void PendingBattleWithoutCheckpointIsRejectedByBothBoundaries()
    {
        PureRunSaveSnapshot source = Pending(Identity("layer_04_event"));
        PureRunState invalid = Copy(source.ActiveRun!, source.ActiveRun!.AdventureState, null);
        Assert.Throws<ArgumentException>(() => RunSaveDocumentV12.Encode(source with { ActiveRun = invalid }));
        AssertRejected(Rewrite(source, active => active["checkpoint"] = null), "save.invalid_payload");
    }

    [TestCase("definitionVersion", "-1")]
    [TestCase("definitionVersion", "0")]
    [TestCase("definitionVersion", "\"1\"")]
    [TestCase("definitionVersion", "null")]
    [TestCase("actId", "\"\"")]
    [TestCase("actId", "{}")]
    [TestCase("templateId", "\"\"")]
    [TestCase("templateId", "null")]
    [TestCase("nodeId", "\" \"")]
    [TestCase("nodeId", "null")]
    public void RehashedMalformedIdentityIsNotMistakenForAValidChecksum(string field, string jsonValue)
    {
        string json = Rewrite(Pending(Identity("layer_04_event")), active =>
            active["adventureState"]!["sceneIdentity"]![field] = JsonNode.Parse(jsonValue));
        AssertRejected(json, "save.invalid_payload");
    }

    [TestCase("actId")]
    [TestCase("definitionVersion")]
    [TestCase("nodeId")]
    [TestCase("templateId")]
    public void RehashedIdentityWithMissingRequiredFieldIsRejected(string field)
    {
        AssertRejected(Rewrite(Pending(Identity("layer_04_event")), active =>
            ((JsonObject)active["adventureState"]!["sceneIdentity"]!).Remove(field)), "save.invalid_payload");
    }

    [TestCase("[]")]
    [TestCase("\"node\"")]
    [TestCase("{}")]
    public void RehashedNonIdentityMetadataIsRejected(string metadata)
    {
        AssertRejected(Rewrite(Pending(Identity("layer_04_event")), active =>
            active["adventureState"]!["sceneIdentity"] = JsonNode.Parse(metadata)), "save.invalid_payload");
    }

    [Test]
    public void ChangedIdentityWithoutRehashIsRejectedBeforeMigration()
    {
        JsonObject root = (JsonObject)JsonNode.Parse(RunSaveDocumentV12.Encode(Pending(Identity("layer_04_event"))))!;
        root["payload"]!["activeRun"]!["adventureState"]!["sceneIdentity"]!["nodeId"] = "layer_06_event";
        AssertRejected(root.ToJsonString(), "save.payload_hash_mismatch");
    }

    [Test]
    public void IdentityConstructorRejectsDefaultIdsAndNegativeVersions()
    {
        Assert.Throws<ArgumentException>(() => new RunNodeSceneIdentity(default, 1, "node", new ContentId("template.test")));
        Assert.Throws<ArgumentException>(() => new RunNodeSceneIdentity(new ContentId("act-01"), 1, "node", default));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunNodeSceneIdentity(new ContentId("act-01"), -1, "node", new ContentId("template.test")));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NoActiveRunCanRoundTripWithoutRequiringNodeMigration(bool hasSummary)
    {
        PureRunSummary? summary = hasSummary ? new("old-run", 41, PureRunOutcome.Abandoned, 2, 5, 7,
            [new ContentId("item.test")], ["mage"], ["settlement:test"]) : null;
        var source = new PureRunSaveSnapshot(8, null, summary);
        string json = RunSaveDocumentV12.Encode(source);
        RunSaveDecodeResultV12 decoded = RunSaveDocumentV12.Decode(json);
        Assert.That(decoded.Succeeded, Is.True, decoded.ErrorCode);
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Snapshot!.ActiveRun, Is.Null);
            Assert.That(decoded.RequiresNodeMigration, Is.False);
            Assert.That(decoded.RequiresNewRun, Is.False);
            Assert.That(decoded.Snapshot.Revision, Is.EqualTo(8));
            Assert.That(JsonSerializer.Serialize(decoded.Snapshot.TerminalSummary), Is.EqualTo(JsonSerializer.Serialize(summary)));
            Assert.That(RunSaveDocumentV12.Encode(decoded.Snapshot), Is.EqualTo(json));
        });
        RunSaveDecodeResultV12 legacy = RunSaveDocumentV12.Decode(RunSaveDocumentV11.Encode(source));
        Assert.That(legacy.Succeeded, Is.True, legacy.ErrorCode);
        Assert.That(legacy.RequiresNodeMigration, Is.False);
    }

    [Test]
    public void IdentityAloneCannotPromoteLegacyInitialExplorationToV12()
    {
        PureRunSaveSnapshot source = Pending(Identity("layer_04_event"));
        PureRunState run = source.ActiveRun!;
        PureRunState invalid = Copy(run, run.AdventureState! with { Lifecycle = RunAdventureLifecycle.InitialExploration }, run.Checkpoint);
        Assert.Throws<ArgumentException>(() => RunSaveDocumentV12.Encode(source with { ActiveRun = invalid }));
        JsonObject wire = (JsonObject)JsonNode.Parse(RunSaveDocumentV11.Encode(Pending(null)))!;
        JsonNode? legacyLifecycle = wire["payload"]!["activeRun"]!["adventureState"]!["lifecycle"]?.DeepClone();
        Assert.That(legacyLifecycle, Is.Not.Null);
        AssertRejected(Rewrite(source, active => active["adventureState"]!["lifecycle"] = legacyLifecycle),
            "save.legacy_lifecycle_unmigrated");
    }

    [TestCase("both")]
    [TestCase("adventureState")]
    [TestCase("checkpoint")]
    public void V11RejectsImplicitIdentityDowngradeEvenWhenOnlyOneOwnerHasIdentity(string owner)
    {
        PureRunSaveSnapshot source = Pending(Identity("layer_04_event"));
        PureRunState run = source.ActiveRun!;
        PureRunState downgraded = Copy(run,
            owner == "checkpoint" ? run.AdventureState! with { SceneIdentity = null } : run.AdventureState,
            owner == "adventureState" ? run.Checkpoint! with { SceneIdentity = null } : run.Checkpoint);
        Assert.Throws<ArgumentException>(() => RunSaveDocumentV11.Encode(source with { ActiveRun = downgraded }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RehashedNullPartyEntryReturnsFailureInsteadOfThrowing(bool checkpoint)
    {
        string json = Rewrite(Pending(Identity("layer_04_event")), active =>
        {
            JsonObject owner = checkpoint ? (JsonObject)active["checkpoint"]! : active;
            ((JsonArray)owner["party"]!)[0] = null;
        });
        AssertRejected(json, "save.invalid_payload");
    }

    [TestCase(8, true)]
    [TestCase(9, true)]
    [TestCase(8, false)]
    [TestCase(9, false)]
    public void HistoricalRunDiscardCannotLoseTheRequiresNewRunFlag(int schema, bool active)
    {
        PureRunSaveSnapshot source = active ? Pending(null) : new(0, null,
            new PureRunSummary("old-run", 41, PureRunOutcome.Abandoned, 0, 0, 0, [], [], []));
        string json = schema == 8 ? RunSaveDocumentV8.Encode(source) : RunSaveDocumentV9.Encode(source);
        RunSaveDecodeResultV12 decoded = RunSaveDocumentV12.Decode(json);
        Assert.That(decoded.Succeeded, Is.True, decoded.ErrorCode);
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Snapshot!.ActiveRun, Is.Null);
            Assert.That(decoded.RequiresNewRun, Is.EqualTo(active));
            Assert.That(decoded.RequiresNodeMigration, Is.False);
            Assert.That(decoded.MigratedFromSchema, Is.EqualTo(schema));
            if (!active) Assert.That(decoded.Snapshot.TerminalSummary!.RunId, Is.EqualTo("old-run"));
        });
    }

    private static void AssertRejected(string json, string error)
    {
        RunSaveDecodeResultV12 decoded = RunSaveDocumentV12.Decode(json);
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.False);
            Assert.That(decoded.ErrorCode, Is.EqualTo(error));
            Assert.That(decoded.Snapshot, Is.Null);
            Assert.That(decoded.RequiresNodeMigration, Is.False);
        });
    }

    private static string Rewrite(PureRunSaveSnapshot snapshot, Action<JsonObject> mutate)
    {
        var root = (JsonObject)JsonNode.Parse(RunSaveDocumentV12.Encode(snapshot))!;
        var payload = (JsonObject)root["payload"]!;
        mutate((JsonObject)payload["activeRun"]!);
        root["payloadSha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false })))).ToLowerInvariant();
        return root.ToJsonString();
    }

    private static void AssertPreserved(PureRunSaveSnapshot expected, PureRunSaveSnapshot actual)
    {
        PureRunState before = expected.ActiveRun!, after = actual.ActiveRun!;
        Assert.Multiple(() =>
        {
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(after.RunId, Is.EqualTo(before.RunId));
            Assert.That(after.Seed, Is.EqualTo(before.Seed));
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.Phase, Is.EqualTo(PureRunPhase.PendingBattle));
            Assert.That(after.EncounterContentId, Is.EqualTo(before.EncounterContentId));
            Assert.That(after.Checkpoint!.Revision, Is.EqualTo(before.Checkpoint!.Revision));
            Assert.That(after.Checkpoint.EncounterIndex, Is.EqualTo(before.Checkpoint.EncounterIndex));
            Assert.That(after.Checkpoint.EncounterContentId, Is.EqualTo(before.Checkpoint.EncounterContentId));
            Assert.That(after.AdventureState!.Revision, Is.EqualTo(before.AdventureState!.Revision));
            Assert.That(RandomState(after), Is.EqualTo(RandomState(before)));
            // 比较序列内容，不把新反序列化的List引用当作record值相等。
            Assert.That(JsonSerializer.Serialize(after.Party), Is.EqualTo(JsonSerializer.Serialize(before.Party)));
            Assert.That(JsonSerializer.Serialize(after.Checkpoint.Party), Is.EqualTo(JsonSerializer.Serialize(before.Checkpoint.Party)));
            Assert.That(JsonSerializer.Serialize(after.Checkpoint.BackpackConsumables), Is.EqualTo(JsonSerializer.Serialize(before.Checkpoint.BackpackConsumables)));
            Assert.That(JsonSerializer.Serialize(after.Checkpoint.BackpackEquipment), Is.EqualTo(JsonSerializer.Serialize(before.Checkpoint.BackpackEquipment)));
        });
    }

    private static ulong RandomState(PureRunState run) => new EncounterRequest(run.RunId, run.Checkpoint!.Revision,
        run.Checkpoint.EncounterContentId, run.Checkpoint.Party, run.AdventureState!.Revision).EffectiveRandomState;

    private static RunNodeSceneIdentity Identity(string node) => new(new ContentId("act-01"), 1, node,
        new ContentId("adventure-map-template.pure-run.node." + node.Replace('_', '-')));

    private static PureRunSaveSnapshot Pending(RunNodeSceneIdentity? identity)
    {
        // 与Session测试相同的真实StartNewRun→BeginEncounter链，避免手造无效checkpoint。
        var store = new MemoryStore();
        var service = new PureRunSessionService(Definition(), store);
        Assert.That(service.StartNewRun(41).Succeeded, Is.True);
        RunSessionResult started = service.BeginEncounter();
        Assert.That(started.Succeeded, Is.True, started.ErrorCode);
        PureRunState run = started.Snapshot!.ActiveRun!;
        RunAdventureState adventure = run.AdventureState! with
        {
            Revision = 1, SceneIdentity = identity,
            Lifecycle = identity is null ? run.AdventureState.Lifecycle : RunAdventureLifecycle.MapActive,
            BoardContentId = identity is null ? run.AdventureState.BoardContentId
                : new ContentId("adventure-board.pure-run.node." + identity.NodeId.Replace('_', '-'))
        };
        return started.Snapshot with { ActiveRun = Copy(run, adventure, run.Checkpoint! with { SceneIdentity = identity }) };
    }

    private static PureRunState Copy(PureRunState run, RunAdventureState? adventure, RunEncounterCheckpoint? checkpoint) => new(
        run.RunId, run.Seed, run.Revision, run.Phase, run.EncounterIndex, run.EncounterContentId, run.Party,
        run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression, run.AppliedTransactionKeys,
        run.Gold, run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems, checkpoint, run.MapState,
        run.NodeTransaction, run.EscortState, adventure);

    private static PureRunDefinition Definition() => new(new ContentId("run.pure-run.three-encounter-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }.Select(id => new ContentId(id)),
        [new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5, 5, 5, 6, 5, 5)),
         new PureRunPartyTemplate("necro", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5, 5, 5, 5, 6, 5)),
         new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5, 6, 5, 5, 5, 5))]);

    private sealed class MemoryStore : IRunSaveStore
    {
        public PureRunSaveSnapshot Snapshot { get; set; } = new(0, null, null);
        public int SaveCount { get; private set; }
        public RunStoreResult Load() => new(true, null, Snapshot);
        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
        {
            SaveCount++;
            if (Snapshot.Revision != expectedRevision) return new(false, "save.revision_conflict", Snapshot);
            Snapshot = snapshot;
            return new(true, null, Snapshot);
        }
    }
}
