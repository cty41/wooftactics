using System.Text.Json;
using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

public sealed record PureRunEncounterRequestValidationResult(bool Succeeded, string? ErrorCode);

/// <summary>Read-only launch fence. Legacy V11 requests remain explicit; bound requests must prove their scene binding.</summary>
public static class PureRunEncounterRequestValidator
{
    public static PureRunEncounterRequestValidationResult Validate(PureRunSaveSnapshot snapshot,
        EncounterRequest request, PureRunMapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(map);
        if (snapshot.PendingRunSetup is not null) return Fail("encounter.setup_pending");
        if (snapshot.ActiveRun is not { Phase: PureRunPhase.PendingBattle } run || run.Checkpoint is not { } checkpoint)
            return Fail("encounter.pending_checkpoint_required");
        if (snapshot.Revision != run.Revision || checkpoint.Revision <= 0 || checkpoint.Revision > run.Revision)
            return Fail("encounter.revision_conflict");
        if (request.RunId != run.RunId || request.CheckpointRevision != checkpoint.Revision ||
            request.EncounterContentId != checkpoint.EncounterContentId || run.EncounterContentId != checkpoint.EncounterContentId)
            return Fail("encounter.checkpoint_conflict");
        long adventureRevision = run.AdventureState?.Revision ?? 0;
        if (adventureRevision < 0 || request.AdventureRevision != adventureRevision)
            return Fail("encounter.adventure_revision_conflict");
        if (request.Party is null || checkpoint.Party is null || request.Party.Any(member => member is null) ||
            checkpoint.Party.Any(member => member is null) || request.Party.Count != run.Party.Count ||
            checkpoint.Party.Count != run.Party.Count ||
            checkpoint.Party.Select(member => member.CharacterId).Distinct(StringComparer.Ordinal).Count() != checkpoint.Party.Count ||
            JsonSerializer.Serialize(request.Party) != JsonSerializer.Serialize(checkpoint.Party))
            return Fail("encounter.party_conflict");

        RunNodeSceneIdentity? current = run.AdventureState?.SceneIdentity;
        RunNodeSceneIdentity? saved = checkpoint.SceneIdentity;
        if (current is null && saved is null && request.SceneIdentity is null)
            return new(true, null); // Explicit transition path: do not reinterpret historical UI event contexts.
        if (current is null || saved is null || request.SceneIdentity is null || current != saved || saved != request.SceneIdentity)
            return Fail("encounter.scene_identity_conflict");
        PureRunNodeSceneProjectionResult projection;
        try
        {
            projection = PureRunNodeSceneProjector.ResolvePendingBattle(run, PureRunPendingSceneRecovery.ResolveBlueprint(run, map));
        }
        catch (ArgumentException) { return Fail("encounter.scene_definition_invalid"); }
        catch (NotSupportedException) { return Fail("encounter.scene_definition_invalid"); }
        if (!projection.Succeeded || projection.RequiresMigration || projection.Identity != request.SceneIdentity)
            return Fail(projection.ErrorCode ?? "encounter.scene_binding_invalid");
        return new(true, null);
    }

    private static PureRunEncounterRequestValidationResult Fail(string error) => new(false, error);
}
