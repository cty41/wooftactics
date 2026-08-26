using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

internal static class RunSaveNormalizer
{
    public static PureRunSaveSnapshot Normalize(PureRunSaveSnapshot snapshot)
    {
        if (snapshot.Revision < 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot));
        PureRunState? active = snapshot.ActiveRun;
        if (active is not null && active.Revision != snapshot.Revision)
            throw new ArgumentException("Active run revision must equal envelope revision.", nameof(snapshot));
        PendingRunSetup? setup = snapshot.PendingRunSetup;
        if (setup is not null && (setup.CurrentCharacterIndex < 0 || setup.CurrentCharacterIndex > 3 ||
            setup.Choices.Count != setup.CurrentCharacterIndex || setup.SelectedCharacterIds.Count is not (0 or 3) ||
            setup.SelectedCharacterIds.Distinct(StringComparer.Ordinal).Count() != setup.SelectedCharacterIds.Count))
            throw new ArgumentException("Pending run setup progress is invalid.", nameof(snapshot));
        return snapshot with
        {
            ActiveRun = active is null ? null : Normalize(active),
            TerminalSummary = snapshot.TerminalSummary is null ? null : Normalize(snapshot.TerminalSummary),
            // Setup choices are an ordered transaction log (Mage, Necromancer, Amazon), not a set.
            PendingRunSetup = setup is null ? null : setup with
            {
                SelectedCharacterIds = setup.SelectedCharacterIds.ToArray(),
                Choices = setup.Choices.Select(value => value with
                {
                    SkillContentId = CanonicalSkillId(value.SkillContentId)
                }).ToArray()
            }
        };
    }

    private static PureRunState Normalize(PureRunState state) => new(
        state.RunId, state.Seed, state.Revision, state.Phase, state.EncounterIndex,
        state.EncounterContentId, state.Party.Select(Normalize).ToArray(), state.BackpackConsumables, state.BackpackEquipment,
        state.PendingProgression.OrderBy(value => value.TransactionKey, StringComparer.Ordinal).Select(value => value with
        {
            // V5 once persisted the in-progress UI draft. Unity only commits
            // growth after a skill is confirmed, so recovery retains the
            // entitlement but intentionally discards both transient choices.
            ProposedAttributes = null,
            SelectedSkillContentId = null
        }).ToArray(),
        state.AppliedTransactionKeys.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        state.Gold, state.BattlesCompleted, state.EnemiesDefeated,
        state.AcquiredItems.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray(),
        state.Checkpoint is null ? null : state.Checkpoint with
        {
            Party = state.Checkpoint.Party.Select(Normalize).ToArray()
        },
        Normalize(state.MapState), state.NodeTransaction, state.EscortState, Normalize(state.AdventureState));

    private static RunAdventureState? Normalize(RunAdventureState? state) => state is null ? null : state with
    {
        LeaderId = state.LeaderId.Trim(),
        ActorCells = state.ActorCells.OrderBy(value => value.ActorId, StringComparer.Ordinal).ToArray(),
        PendingEventNodeId = NullIfWhiteSpace(state.PendingEventNodeId),
        PendingEventObjectId = NullIfWhiteSpace(state.PendingEventObjectId)
    };

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RunCharacterState Normalize(RunCharacterState character) => new(
        character.CharacterId,
        character.UnitContentId,
        character.Level,
        character.Attributes,
        character.CurrentHealth,
        character.MaxHealth,
        character.CurrentMana,
        character.MaxMana,
        character.IsDead,
        character.LearnedSkills.Select(CanonicalSkillId).ToArray(),
        character.Equipment,
        character.CarriedConsumables,
        character.LearnedSkillStates.Select(value => value with
        {
            DefinitionId = CanonicalSkillId(value.DefinitionId)
        }).ToArray(),
        character.StartingSkillContentId is ContentId starting ? CanonicalSkillId(starting) : null);

    private static ContentId CanonicalSkillId(ContentId id) =>
        id.Value == "skill.amazon.poison-spear.lv1"
            ? new ContentId("skill.poison-spear.lv1")
            : id;

    private static PureRunMapState? Normalize(PureRunMapState? state) => state is null ? null : state with
    {
        ReachableNodeIds = state.ReachableNodeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        VisitedNodeIds = state.VisitedNodeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        MysteryEventAssignments = state.MysteryEventAssignments.OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal),
        MysteryAdjudicatorAssignments = state.MysteryAdjudicatorAssignments?
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal),
        StoreOffers = state.StoreOffers?.OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal).ToArray(),
        PendingStatuses = state.PendingStatuses?.OrderBy(value => value.CharacterId, StringComparer.Ordinal)
            .ThenBy(value => value.StatusId.Value, StringComparer.Ordinal).ToArray(),
        NodeIntelStates = state.NodeIntelStates?.OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal)
    };

    private static PureRunSummary Normalize(PureRunSummary summary) => summary with
    {
        AcquiredItems = summary.AcquiredItems.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray(),
        DeadCharacters = summary.DeadCharacters.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        AppliedTransactionKeys = summary.AppliedTransactionKeys.OrderBy(value => value, StringComparer.Ordinal).ToArray()
    };
}
