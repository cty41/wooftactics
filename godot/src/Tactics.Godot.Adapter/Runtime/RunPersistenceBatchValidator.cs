using Godot;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record RunPersistenceBatchValidation(int BatchCount, int GlobalCount, RunPersistenceFixtureResult Fixture);

public static class RunPersistenceBatchValidator
{
    public static RunPersistenceBatchValidation Validate(GodotResourceCatalog batch, GodotResourceCatalog global, PackedScene fixtureScene)
    {
        batch.Validate(); global.Validate();
        if (batch.Entries.Length != 1 || global.Entries.Length is not (74 or 101 or 108 or 114 or 115 or 116 or 119 or 123 or 124 or 125 or 131 or 132 or 141 or 142 or 143 or 160 or 161 or 162 or 166 or 185)) throw new InvalidOperationException("Pure Run catalog count is invalid.");
        if (!batch.TryGet("run.pure-run.three-encounter-v1", out Resource? resource) || resource is not PureRunDefinitionResource definition)
            throw new InvalidOperationException("Pure Run definition is missing.");
        definition.ToCoreDefinition();
        Node node = fixtureScene.Instantiate();
        if (node is not GodotRunPersistenceFixture fixture) { node.Free(); throw new InvalidOperationException("Pure Run fixture root is invalid."); }
        RunPersistenceFixtureResult result = fixture.RunAutomated(); node.Free();
        if (!result.DuplicateRejected || !result.BackupRecovered || !result.DoubleCorruptionIsolated) throw new InvalidOperationException("Pure Run persistence fixture failed.");
        return new RunPersistenceBatchValidation(1, global.Entries.Length, result);
    }
}
