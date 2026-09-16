using NUnit.Framework;
using Tactics.Application.Battle;
using Tactics.Application.Runs;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Items;
using Tactics.Core.Pathfinding;
using Tactics.Core.Skills;
using Tactics.Core.Statuses;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PlayableBattleSessionServiceTests
{
    [Test]
    public void ExplicitClassDerivedStatsPreserveDemonboundMoveAndInitiativeInBattleProjection()
    {
        var attributes = new UnitAttributes(5, 5, 5, 5, 6, 5);
        var definition = new UnitDefinition(new ContentId("unit.pure-run.demonbound"), "godot.demonbound",
            "Demonbound", "player", "demonbound", attributes, 4,
            new UnitDerivedStats(20, 18, 6, 4, 8), 1, 1, 1, UnitMovementKind.Land, true,
            UnitDerivedStatMode.Explicit);
        EquipmentStatProjection projection = EquipmentStatProjector.Project(attributes, definition.Speed, []);

        UnitDerivedStats result = PlayableBattleSessionFactory.ResolvePartyDerivedStats(definition, projection);

        Assert.Multiple(() =>
        {
            Assert.That(result.MoveRange, Is.EqualTo(4));
            Assert.That(result.Initiative, Is.EqualTo(8));
            Assert.That(projection.DerivedStats.MoveRange, Is.EqualTo(4));
        });
    }

    [Test]
    public void PrimaryAttributeDamageBonusUsesProjectedRoleAttributeAboveFive()
    {
        UnitAttributes attributes = new(8, 7, 5, 9, 11, 5);

        Assert.Multiple(() =>
        {
            Assert.That(PlayableBattleSessionFactory.CalculatePrimaryAttributeDamageBonus(attributes, SkillRole.Mage), Is.EqualTo(4));
            Assert.That(PlayableBattleSessionFactory.CalculatePrimaryAttributeDamageBonus(attributes, SkillRole.Necromancer), Is.Zero);
            Assert.That(PlayableBattleSessionFactory.CalculatePrimaryAttributeDamageBonus(attributes, SkillRole.Demonbound), Is.EqualTo(6));
            Assert.That(PlayableBattleSessionFactory.CalculatePrimaryAttributeDamageBonus(attributes, SkillRole.Amazon), Is.EqualTo(2));
            Assert.That(PlayableBattleSessionFactory.CalculatePrimaryAttributeDamageBonus(attributes, SkillRole.Poet), Is.EqualTo(3));
            Assert.That(PlayableBattleSessionFactory.CalculatePrimaryAttributeDamageBonus(attributes, SkillRole.Any), Is.Zero);
        });
    }

    [Test]
    public void PlayableEnemySpeedProfile_PreservesPlayerSpeedAndOverridesEnemyArchetypes()
    {
        var profile = new PlayableEnemySpeedProfile(new Dictionary<ContentId, float>
        {
            [new ContentId("unit.pure-run.goat-ranged")] = 6f,
            [new ContentId("unit.pure-run.goat-charger")] = 6f,
            [new ContentId("unit.pure-run.goat-support")] = 5f,
            [new ContentId("unit.pure-run.goat-aoe")] = 5f,
            [new ContentId("unit.pure-run.goat-elite-charger")] = 7f,
            [new ContentId("unit.pure-run.goat-elite-poison-caster")] = 6f
        });

        Assert.Multiple(() =>
        {
            Assert.That(profile.Speed(new ContentId("unit.pure-run.mage"), 5f), Is.EqualTo(5f));
            Assert.That(profile.Speed(new ContentId("unit.pure-run.goat-ranged"), 12f), Is.EqualTo(6f));
            Assert.That(profile.Speed(new ContentId("unit.pure-run.goat-charger"), 8f), Is.EqualTo(6f));
            Assert.That(profile.Speed(new ContentId("unit.pure-run.goat-support"), 8f), Is.EqualTo(5f));
            Assert.That(profile.Speed(new ContentId("unit.pure-run.goat-aoe"), 6f), Is.EqualTo(5f));
            Assert.That(profile.Speed(new ContentId("unit.pure-run.goat-elite-charger"), 8f), Is.EqualTo(7f));
            Assert.That(profile.Speed(new ContentId("unit.pure-run.goat-elite-poison-caster"), 6f), Is.EqualTo(6f));
        });
    }

    [Test]
    public void PlayableBalanceProfile_PreservesNonCriticalSkillContract()
    {
        var id = new ContentId("skill.summon.fire-demon-attack");
        var source = new SkillDefinition(id, "unity.fire-demon", SkillRole.Any, SkillKind.Basic, 1, 0, 1, 3,
            SkillExecutionKind.FireDemonAttack, 4, SkillDamageKind.Magical, new ContentId("buff.ignite"), 1,
            isBasicAbility: true, canCrit: false);
        var profile = new PlayableBattleBalanceProfile(
            new Dictionary<ContentId, (int Mana, int Damage)> { [id] = (0, 5) },
            new Dictionary<ContentId, (int Physical, int Magical)>());

        SkillDefinition result = profile.Apply(source);

        Assert.That(result.CanCrit, Is.False);
        Assert.That(result.Damage, Is.EqualTo(5));
    }

    [Test]
    public void DynamicSummonsReceiveTheirExplicitBasicAttackBindings()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlayableBattleSessionService.DynamicSummonBasicSkill(new ContentId("unit.pure-run.skeleton-warrior")),
                Is.EqualTo(new ContentId("skill.basic.melee")));
            Assert.That(PlayableBattleSessionService.DynamicSummonBasicSkill(new ContentId("unit.pure-run.fire-demon")),
                Is.EqualTo(new ContentId("skill.summon.fire-demon-attack")));
            Assert.That(PlayableBattleSessionService.DynamicSummonBasicSkill(new ContentId("unit.pure-run.decoy")), Is.Null);
        });
    }

    [Test]
    public void Snapshot_ProjectsCommittedFacingAndCurrentRemainingInitiativeQueue()
    {
        PlayableBattleSessionService service = CreateService(out _, out _);

        BattleUiSnapshot snapshot = service.CaptureSnapshot();
        IReadOnlyList<BattleUiInitiativeEntry> queue = snapshot.InitiativeQueue!;

        Assert.Multiple(() =>
        {
            Assert.That(queue, Is.Not.Null.And.Not.Empty);
            Assert.That(queue.First().UnitId, Is.EqualTo(snapshot.ActiveUnitId));
            Assert.That(queue.Count(item => item.IsCurrent), Is.EqualTo(1));
            Assert.That(snapshot.Units.Single(item => item.PlayerNumber == 0).Facing, Is.EqualTo(UnitFacing.East));
            Assert.That(snapshot.Units.Single(item => item.PlayerNumber == 1).Facing, Is.EqualTo(UnitFacing.West));
        });
    }

    [Test]
    public void InsufficientMana_DisablesSkillAndRejectsTargetingWithoutMutation()
    {
        PlayableBattleSessionService service = CreateService(out SkillDefinition skill, out _, playerMana: 2, skillMana: 3);
        BattleState before = service.State;

        BattleUiSnapshot snapshot = service.CaptureSnapshot();
        BattleUiIntentResult result = service.Submit(new SelectSkillIntent(skill.ContentId));

        Assert.Multiple(() =>
        {
            BattleUiSkillAvailability availability = snapshot.SkillAvailability!.Single(value => value.SkillId == skill.ContentId);
            Assert.That(availability.IsAvailable, Is.False);
            Assert.That(availability.FailureCode, Is.EqualTo("insufficient_mana"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo("insufficient_mana"));
            Assert.That(result.Snapshot.TargetingMode, Is.EqualTo(BattleTargetingMode.None));
            Assert.That(service.State, Is.SameAs(before));
        });
    }

    [Test]
    public void DistantDroppedSpear_DisablesPickupBeforeTargeting()
    {
        PlayableBattleSessionService service = CreateService(out SkillDefinition skill, out _,
            executionKind: SkillExecutionKind.PickupSpear, droppedSpear: new GridPoint(4, 4));

        BattleUiSnapshot snapshot = service.CaptureSnapshot();
        BattleUiIntentResult result = service.Submit(new SelectSkillIntent(skill.ContentId));

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.SkillAvailability!.Single().FailureCode, Is.EqualTo("spear_not_adjacent"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo("spear_not_adjacent"));
            Assert.That(result.Snapshot.TargetingMode, Is.EqualTo(BattleTargetingMode.None));
        });
    }

    [Test]
    public void TargetingCancelAndIllegalCell_DoNotMutateBattleState()
    {
        PlayableBattleSessionService service = CreateService(out _, out _);
        BattleState original = service.State;

        Assert.That(service.Submit(new BeginMoveIntent()).Snapshot.TargetingMode, Is.EqualTo(BattleTargetingMode.Move));
        Assert.That(service.Submit(new CancelTargetingIntent()).Snapshot.TargetingMode, Is.EqualTo(BattleTargetingMode.None));
        service.Submit(new BeginMoveIntent());
        BattleUiIntentResult rejected = service.Submit(new ConfirmCellIntent(new GridPoint(9, 9)));

        Assert.Multiple(() =>
        {
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.FailureCode, Is.Not.Empty);
            Assert.That(service.State, Is.SameAs(original));
        });
    }

    [Test]
    public void SuccessfulMove_DisablesMoveTargetingUntilTheUnitsNextTurn()
    {
        PlayableBattleSessionService service = CreateService(out _, out _);
        GridPoint destination = service.CaptureSnapshot().LegalMoveCells.First();

        Assert.That(service.Submit(new BeginMoveIntent()).Succeeded, Is.True);
        Assert.That(service.Submit(new ConfirmCellIntent(destination)).Succeeded, Is.True);
        BattleUiSnapshot moved = service.CaptureSnapshot();
        BattleUiIntentResult repeated = service.Submit(new BeginMoveIntent());

        Assert.Multiple(() =>
        {
            Assert.That(moved.MoveAvailability.IsAvailable, Is.False);
            Assert.That(moved.MoveAvailability.FailureCode, Is.EqualTo("move_already_used"));
            Assert.That(repeated.Succeeded, Is.False);
            Assert.That(repeated.FailureCode, Is.EqualTo("move_already_used"));
            Assert.That(repeated.Snapshot.TargetingMode, Is.EqualTo(BattleTargetingMode.None));
        });
    }

    [Test]
    public void SkillIntent_UsesCanonicalTransitionAndCreatesSingleRunResult()
    {
        PlayableBattleSessionService service = CreateService(out SkillDefinition playerSkill, out UnitInstanceId enemyId);
        GridPoint targetCell = service.State.Units[enemyId].Unit.Position;

        Assert.That(service.Submit(new SelectSkillIntent(playerSkill.ContentId)).Succeeded, Is.True);
        BattleUiIntentResult result = service.Submit(new ConfirmCellIntent(targetCell));
        BattleUiIntentResult duplicate = service.Submit(new ConfirmCellIntent(targetCell));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.Phase, Is.EqualTo(PlayableBattlePhase.Victory));
            Assert.That(result.BattleResult, Is.Not.Null);
            Assert.That(result.BattleResult!.PlayerVictory, Is.True);
            Assert.That(result.Events.OfType<DamageAppliedEvent>().Count(), Is.EqualTo(1));
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(service.BattleResult, Is.SameAs(result.BattleResult));
        });
    }

    [Test]
    public void SkillPreview_SeparatesGeometricRangeFromLegalTargetsWithoutMutatingState()
    {
        PlayableBattleSessionService service = CreateService(out SkillDefinition playerSkill, out UnitInstanceId enemyId);
        BattleState original = service.State;
        GridPoint emptyInRange = new(1, 2);
        GridPoint enemyCell = service.State.Units[enemyId].Unit.Position;

        BattleUiIntentResult selected = service.Submit(new SelectSkillIntent(playerSkill.ContentId));
        BattleUiSkillPreview? skillPreview = selected.Snapshot.SkillPreview;
        BattleUiImpactPreview? emptyPreview = service.PreviewSkillTarget(emptyInRange);
        BattleUiImpactPreview? enemyPreview = service.PreviewSkillTarget(enemyCell);

        Assert.Multiple(() =>
        {
            Assert.That(skillPreview, Is.Not.Null);
            Assert.That(skillPreview!.RangeCells, Does.Contain(emptyInRange));
            Assert.That(skillPreview.LegalTargets.Select(target => target.Cell), Does.Not.Contain(emptyInRange));
            Assert.That(skillPreview.LegalTargets.Select(target => target.Cell), Does.Contain(enemyCell));
            Assert.That(emptyPreview!.IsInRange, Is.True);
            Assert.That(emptyPreview.IsLegal, Is.False);
            Assert.That(emptyPreview.FailureCode, Is.EqualTo("no_valid_target"));
            Assert.That(enemyPreview!.IsLegal, Is.True);
            Assert.That(enemyPreview.PrimaryImpactUnitId, Is.EqualTo(enemyId));
            Assert.That(enemyPreview.ImpactCells, Does.Contain(enemyCell));
            Assert.That(service.State, Is.SameAs(original));
            Assert.That(service.State.RandomState, Is.EqualTo(original.RandomState));
        });
    }

    [Test]
    public void SkillPreview_ShowsRangeWhenNoLegalTargetExists()
    {
        PlayableBattleSessionService service = CreateService(out SkillDefinition playerSkill, out _, enemyCell: new GridPoint(9, 9));

        BattleUiSkillPreview? preview = service.Submit(new SelectSkillIntent(playerSkill.ContentId)).Snapshot.SkillPreview;

        Assert.Multiple(() =>
        {
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview!.RangeCells, Is.Not.Empty);
            Assert.That(preview.LegalTargets, Is.Empty);
        });
    }

    [Test]
    public void SkillPreview_ReportsTheLivingUnitThatBlocksLineOfSight()
    {
        PlayableBattleSessionService service = CreateService(out SkillDefinition skill, out UnitInstanceId enemyId,
            enemyCell: new GridPoint(3, 1), livingBlockerCell: new GridPoint(2, 1));
        GridPoint target = service.State.Units[enemyId].Unit.Position;

        BattleUiIntentResult selection = service.Submit(new SelectSkillIntent(skill.ContentId));
        Assert.That(selection.Succeeded, Is.True, selection.FailureCode);
        BattleUiImpactPreview preview = service.PreviewSkillTarget(target)!;

        Assert.Multiple(() =>
        {
            Assert.That(preview.IsLegal, Is.False);
            Assert.That(preview.FailureCode, Is.EqualTo("line_of_sight_blocked"));
            Assert.That(preview.LineOfSight!.BlockingCell, Is.EqualTo(new GridPoint(2, 1)));
            Assert.That(preview.LineOfSight.BlockingKind, Is.EqualTo(LineOfSightBlockingKind.LivingUnit));
            Assert.That(preview.LineOfSight.BlockingUnitId!.Value.Value, Is.EqualTo("party-blocker"));
        });
    }

    [Test]
    public void IceBolt_AllowsTheTargetWhenAnAllyOnlyTouchesTheDiagonalRayCorner()
    {
        PlayableBattleSessionService service = CreateService(out SkillDefinition skill, out UnitInstanceId enemyId,
            enemyCell: new GridPoint(3, 3), livingBlockerCell: new GridPoint(2, 1),
            executionKind: SkillExecutionKind.IceBolt);
        GridPoint target = service.State.Units[enemyId].Unit.Position;

        Assert.That(service.Submit(new SelectSkillIntent(skill.ContentId)).Succeeded, Is.True);
        BattleUiImpactPreview preview = service.PreviewSkillTarget(target)!;
        BattleUiIntentResult result = service.Submit(new ConfirmCellIntent(target));

        Assert.Multiple(() =>
        {
            Assert.That(preview.IsLegal, Is.True, preview.FailureCode);
            Assert.That(preview.LineOfSight!.BlockingCell, Is.Null);
            Assert.That(result.Succeeded, Is.True, result.FailureCode);
            Assert.That(service.State.Units[enemyId].IsAlive, Is.False);
        });
    }

    [Test]
    public void ThrustPreview_OnlyIncludesAxialCells()
    {
        PlayableBattleSessionService service = CreateService(
            out SkillDefinition playerSkill,
            out _,
            executionKind: SkillExecutionKind.Thrust);

        BattleUiSkillPreview? preview = service.Submit(new SelectSkillIntent(playerSkill.ContentId)).Snapshot.SkillPreview;

        Assert.Multiple(() =>
        {
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview!.RangeCells, Does.Contain(new GridPoint(1, 2)));
            Assert.That(preview.RangeCells, Does.Contain(new GridPoint(2, 1)));
            Assert.That(preview.RangeCells, Does.Not.Contain(new GridPoint(2, 2)));
        });
    }

    [Test]
    public void PoetChargePreview_OnlyIncludesAxialCellsAndProjectsLinePath()
    {
        PlayableBattleSessionService service = CreateService(
            out SkillDefinition playerSkill,
            out _,
            executionKind: SkillExecutionKind.PoetCharge);

        BattleUiSkillPreview? preview = service.Submit(new SelectSkillIntent(playerSkill.ContentId)).Snapshot.SkillPreview;
        BattleUiImpactPreview impact = service.PreviewSkillTarget(new GridPoint(2, 1))!;

        Assert.Multiple(() =>
        {
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview!.RangeCells, Does.Contain(new GridPoint(1, 2)));
            Assert.That(preview.RangeCells, Does.Contain(new GridPoint(2, 1)));
            Assert.That(preview.RangeCells, Does.Not.Contain(new GridPoint(2, 2)));
            Assert.That(impact.PathCells, Is.EqualTo(new[] { new GridPoint(2, 1) }));
        });
    }

    [Test]
    public void EndTurn_AutomaticallyExecutesEnemyAndReturnsToPlayer()
    {
        PlayableBattleSessionService service = CreateService(out _, out _);

        BattleUiIntentResult result = service.Submit(new EndTurnIntent());

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.Phase, Is.EqualTo(PlayableBattlePhase.PlayerTurn));
            Assert.That(result.Snapshot.Round, Is.EqualTo(2));
            Assert.That(result.Snapshot.RecentEvents.OfType<SkillUsedEvent>(), Is.Not.Empty);
            Assert.That(service.HasPendingAutomaticFrames,Is.True);
        });
        var frames=new List<BattleUiFrame>();while(service.DequeueAutomaticFrame() is { } frame)frames.Add(frame);
        Assert.That(frames.Select(frame=>frame.Stage),Does.Contain("Decision").And.Contain("Skill").And.Contain("EndTurn"));
    }

    [Test]
    public void FriendlySummonWithController_AutomaticallyActsBeforeReturningToHero()
    {
        UnitInstanceId ownerId = new("party-necromancer");
        UnitInstanceId summonId = new("summon-skeleton");
        UnitInstanceId enemyId = new("enemy-goat");
        SkillDefinition attack = Skill("skill.summon.skeleton-attack.lv1", 2);
        BattleUnitState owner = Unit(ownerId, "unit.pure-run.necromancer", new GridPoint(0, 0), 0, 0, 20, 20);
        BattleUnitState summon = new(new UnitState(summonId, new ContentId("unit.pure-run.skeleton-warrior"),
            new GridPoint(1, 1), 3, 20, 0, 1), 12, 12, physicalAttack: 2, summonOwnerId: ownerId,
            canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: "Skeleton");
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(2, 1), 1, 2, 20, 20);
        BattleState state = State([owner, summon, enemy], [summonId, ownerId, enemyId]);
        AiDefinition ai = BasicAi("ai.summon.basic-melee", attack.ContentId);
        var context = new PlayableBattleSessionContext(state, 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>> { [ownerId] = Array.Empty<SkillDefinition>() },
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = BasicAi("ai.enemy", attack.ContentId) },
            new Dictionary<ContentId, SkillDefinition> { [attack.ContentId] = attack },
            SummonControllers: new Dictionary<ContentId, SummonControllerDefinition>
            {
                [new ContentId("unit.pure-run.skeleton-warrior")] = new(ai,
                    new Dictionary<int, SkillDefinition> { [1] = attack }, SkillExecutionKind.SummonSkeleton)
            });

        var service = new PlayableBattleSessionService(context);

        Assert.Multiple(() =>
        {
            Assert.That(service.CaptureSnapshot().ActiveUnitId, Is.EqualTo(ownerId));
            Assert.That(service.State.Units[enemyId].CurrentHealth, Is.EqualTo(17));
            Assert.That(service.CaptureSnapshot().Phase, Is.EqualTo(PlayableBattlePhase.PlayerTurn));
            Assert.That(service.CaptureSnapshot().RecentEvents.OfType<SkillUsedEvent>().Any(value => value.ActorId == summonId), Is.True);
        });
    }

    [Test]
    public void DeadHeroesWithLivingSummon_ContinueThroughItsAiTurnUntilTheLastFactionEntityFalls()
    {
        UnitInstanceId ownerId = new("party-necromancer");
        UnitInstanceId summonId = new("summon-skeleton");
        UnitInstanceId enemyId = new("enemy-goat");
        SkillDefinition summonAttack = Skill("skill.summon.skeleton-attack.lv1", 2);
        SkillDefinition enemyAttack = Skill("skill.enemy.finisher", 20);
        BattleUnitState owner = Unit(ownerId, "unit.pure-run.necromancer", new GridPoint(0, 0), 0, 0, 20, 0);
        BattleUnitState summon = new(new UnitState(summonId, new ContentId("unit.pure-run.skeleton-warrior"),
            new GridPoint(1, 1), 3, 20, 0, 1), 4, 4, physicalAttack: 2, summonOwnerId: ownerId,
            canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: "Skeleton");
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(2, 1), 1, 2, 2, 2);
        AiDefinition summonAi = BasicAi("ai.summon.basic-melee", summonAttack.ContentId);
        var skills = new Dictionary<ContentId, SkillDefinition>
        {
            [summonAttack.ContentId] = summonAttack,
            [enemyAttack.ContentId] = enemyAttack
        };
        var controllers = new Dictionary<ContentId, SummonControllerDefinition>
        {
            [new ContentId("unit.pure-run.skeleton-warrior")] = new(summonAi,
                new Dictionary<int, SkillDefinition> { [1] = summonAttack }, SkillExecutionKind.SummonSkeleton)
        };
        PlayableBattleSessionContext Context(BattleUnitState currentSummon, BattleUnitState currentEnemy) => new(
            State([owner, currentSummon, currentEnemy], [summonId, enemyId, ownerId]), 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>(),
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = BasicAi("ai.enemy", enemyAttack.ContentId) },
            skills,
            new EncounterRequest("run-summon-terminal", 2, new ContentId("encounter.pure-run.n1"),
                Array.Empty<RunCharacterState>()),
            new Dictionary<UnitInstanceId, string> { [ownerId] = "pure_run_necromancer" },
            SummonControllers: controllers);

        var service = new PlayableBattleSessionService(Context(summon, enemy));
        var defeated = new PlayableBattleSessionService(Context(summon.WithHealth(0), enemy.WithHealth(2)));

        Assert.Multiple(() =>
        {
            Assert.That(service.CaptureSnapshot().RecentEvents.OfType<SkillUsedEvent>()
                .Any(value => value.ActorId == summonId), Is.True,
                "The living summon must receive its automatic turn before defeat is evaluated.");
            Assert.That(service.BattleResult, Is.Not.Null);
            Assert.That(service.BattleResult!.PlayerVictory, Is.True,
                "A living summon can win after every persistent party character is dead.");
            Assert.That(defeated.BattleResult, Is.Not.Null);
            Assert.That(defeated.BattleResult!.PlayerVictory, Is.False);
            Assert.That(defeated.CaptureSnapshot().Phase, Is.EqualTo(PlayableBattlePhase.Defeat));
        });
    }

    [Test]
    public void AllPlayerFactionUnitsDefeated_ProducesOneDefeatResult()
    {
        UnitInstanceId heroId = new("party-mage");
        UnitInstanceId enemyId = new("enemy-goat");
        BattleUnitState hero = Unit(heroId, "unit.pure-run.mage", new GridPoint(1, 1), 0, 0, 20, 0);
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(2, 1), 1, 1, 20, 20);
        SkillDefinition attack = Skill("skill.basic.melee", 2);
        var service = new PlayableBattleSessionService(new PlayableBattleSessionContext(
            State([hero, enemy], [heroId, enemyId]), 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>(),
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = BasicAi("ai.enemy", attack.ContentId) },
            new Dictionary<ContentId, SkillDefinition> { [attack.ContentId] = attack },
            new EncounterRequest("run-test", 2, new ContentId("encounter.pure-run.e1"), Array.Empty<RunCharacterState>())));

        Assert.Multiple(() =>
        {
            Assert.That(service.BattleResult, Is.Not.Null);
            Assert.That(service.BattleResult!.PlayerVictory, Is.False);
            Assert.That(service.CaptureSnapshot().Phase, Is.EqualTo(PlayableBattlePhase.Defeat));
        });
    }

    [Test]
    public void ProtectedNpc_FleesAutomaticallyAndCannotTurnPartyWipeIntoVictory()
    {
        UnitInstanceId heroId = new("party-mage");
        UnitInstanceId protectedNpcId = new("escort-villager");
        UnitInstanceId enemyId = new("enemy-goat");
        BattleUnitState hero = Unit(heroId, "unit.pure-run.mage", new GridPoint(1, 1), 0, 0, 20, 20);
        BattleUnitState protectedNpc = Unit(protectedNpcId, "unit.escort.lost-villager",
            new GridPoint(2, 1), 0, 1, 12, 12);
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(3, 1), 1, 2, 20, 20);
        SkillDefinition attack = Skill("skill.basic.melee", 2);
        PlayableBattleSessionContext Context(BattleUnitState currentHero) => new(
            State([currentHero, protectedNpc, enemy], [heroId, protectedNpcId, enemyId]), 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>(),
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = BasicAi("ai.enemy", attack.ContentId) },
            new Dictionary<ContentId, SkillDefinition> { [attack.ContentId] = attack },
            new EncounterRequest("run-escort", 2, new ContentId("encounter.pure-run.n1"), Array.Empty<RunCharacterState>()),
            new Dictionary<UnitInstanceId, string> { [heroId] = "pure_run_mage" },
            ProtectedNpcUnitId: protectedNpcId);
        var service = new PlayableBattleSessionService(Context(hero));
        var partyWiped = new PlayableBattleSessionService(Context(hero.WithHealth(0)));
        BattleUiIntentResult ended = service.Submit(new EndTurnIntent());
        var stages = new List<string>();
        while (service.DequeueAutomaticFrame() is { } frame) stages.Add(frame.Stage);

        Assert.Multiple(() =>
        {
            Assert.That(ended.Succeeded, Is.True, ended.FailureCode);
            Assert.That(stages, Does.Contain("EscortFlee"));
            Assert.That(partyWiped.BattleResult, Is.Not.Null);
            Assert.That(partyWiped.BattleResult!.PlayerVictory, Is.False);
            Assert.That(partyWiped.CaptureSnapshot().Phase, Is.EqualTo(PlayableBattlePhase.Defeat));
        });
    }

    [Test]
    public void PossessedDemonbound_AutomaticallyTargetsItsOwnFactionWithoutChangingFaction()
    {
        UnitInstanceId demonboundId = new("party-demonbound");
        UnitInstanceId allyId = new("party-mage");
        UnitInstanceId enemyId = new("enemy-goat");
        BattleUnitState demonbound = Unit(demonboundId, "unit.pure-run.demonbound",
                new GridPoint(1, 1), 0, 0, 20, 20)
            .WithDemonboundState(new DemonboundBattleState(10, 3, isPossessed: true));
        BattleUnitState ally = Unit(allyId, "unit.pure-run.mage", new GridPoint(2, 1), 0, 1, 20, 20);
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(8, 8), 1, 2, 20, 20);
        SkillDefinition attack = Skill("skill.basic.melee", 3);

        var service = new PlayableBattleSessionService(new PlayableBattleSessionContext(
            State([demonbound, ally, enemy], [demonboundId, allyId, enemyId]), 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>> { [demonboundId] = [attack] },
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = BasicAi("ai.enemy", attack.ContentId) },
            new Dictionary<ContentId, SkillDefinition> { [attack.ContentId] = attack }));

        Assert.Multiple(() =>
        {
            Assert.That(service.State.Units[demonboundId].Unit.PlayerNumber, Is.Zero);
            Assert.That(service.State.Units[allyId].CurrentHealth, Is.LessThan(20));
            Assert.That(service.CaptureSnapshot().RecentEvents.OfType<SkillUsedEvent>()
                .Any(value => value.ActorId == demonboundId), Is.True);
        });
    }

    [Test]
    public void PossessedDemonboundAsOnlySurvivor_WithEnemiesDefeated_IsPlayerVictory()
    {
        UnitInstanceId demonboundId = new("party-demonbound");
        UnitInstanceId allyId = new("party-mage");
        UnitInstanceId enemyId = new("enemy-goat");
        BattleUnitState demonbound = Unit(demonboundId, "unit.pure-run.demonbound",
                new GridPoint(1, 1), 0, 0, 20, 20)
            .WithDemonboundState(new DemonboundBattleState(10, 3, isPossessed: true));
        BattleUnitState ally = Unit(allyId, "unit.pure-run.mage", new GridPoint(2, 1), 0, 1, 20, 0);
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(8, 8), 1, 2, 20, 0);
        var request = new EncounterRequest("run-possessed-victory", 2,
            new ContentId("encounter.pure-run.n1"), Array.Empty<RunCharacterState>());

        var service = new PlayableBattleSessionService(new PlayableBattleSessionContext(
            State([demonbound, ally, enemy], [demonboundId, allyId, enemyId]), 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>(),
            new Dictionary<UnitInstanceId, AiDefinition>(),
            new Dictionary<ContentId, SkillDefinition>(), request,
            new Dictionary<UnitInstanceId, string>
            {
                [demonboundId] = "pure_run_demonbound",
                [allyId] = "pure_run_mage"
            }));

        Assert.Multiple(() =>
        {
            Assert.That(service.BattleResult, Is.Not.Null);
            Assert.That(service.BattleResult!.PlayerVictory, Is.True);
            Assert.That(service.CaptureSnapshot().Phase, Is.EqualTo(PlayableBattlePhase.Victory));
        });
    }

    [Test]
    public void PossessedDemonboundTerminalResult_RestoresBattleOnlyResourcesToRunScale()
    {
        UnitInstanceId demonboundId = new("party-demonbound");
        UnitInstanceId enemyId = new("enemy-goat");
        var attributes = new UnitAttributes(6, 4, 6, 6, 6, 2);
        BattleUnitState demonbound = new(
            new UnitState(demonboundId, new ContentId("unit.pure-run.demonbound"),
                new GridPoint(1, 1), 5, 18, 0, 0, effectiveAttributes: attributes),
            44, 44, maxMana: 33, currentMana: 22,
            demonboundState: new DemonboundBattleState(10, 1, isPossessed: true)
                .WithPossessedBoostApplied());
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger",
            new GridPoint(8, 8), 1, 1, 20, 0);
        var checkpointMember = new RunCharacterState("pure_run_demonbound",
            new ContentId("unit.pure-run.demonbound"), 1, attributes,
            24, 24, 6, 18, false, Array.Empty<ContentId>());
        var request = new EncounterRequest("run-possessed-normalization", 2,
            new ContentId("encounter.pure-run.n1"), [checkpointMember]);

        var service = new PlayableBattleSessionService(new PlayableBattleSessionContext(
            State([demonbound, enemy], [demonboundId, enemyId]), 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>(),
            new Dictionary<UnitInstanceId, AiDefinition>(),
            new Dictionary<ContentId, SkillDefinition>(), request,
            new Dictionary<UnitInstanceId, string> { [demonboundId] = checkpointMember.CharacterId }));

        BattlePartyResult result = service.BattleResult!.Party.Single();
        Assert.Multiple(() =>
        {
            Assert.That(service.BattleResult.PlayerVictory, Is.True);
            Assert.That(result.CurrentHealth, Is.EqualTo(24));
            Assert.That(result.CurrentMana, Is.EqualTo(12));
            Assert.That(result.IsDead, Is.False);
        });
    }

    [Test]
    public void PossessedDemonboundFallsBackToEnemyTargetsAfterAlliesAreDown()
    {
        UnitInstanceId demonboundId = new("party-demonbound");
        UnitInstanceId allyId = new("party-mage");
        UnitInstanceId enemyId = new("enemy-goat");
        BattleUnitState demonbound = Unit(demonboundId, "unit.pure-run.demonbound",
                new GridPoint(1, 1), 0, 0, 20, 20)
            .WithDemonboundState(new DemonboundBattleState(10, 3, isPossessed: true));
        BattleUnitState ally = Unit(allyId, "unit.pure-run.mage", new GridPoint(0, 1), 0, 1, 20, 0);
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(2, 1), 1, 2, 20, 20);
        SkillDefinition attack = Skill("skill.basic.melee", 3);

        var service = new PlayableBattleSessionService(new PlayableBattleSessionContext(
            State([demonbound, ally, enemy], [demonboundId, allyId, enemyId]), 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>> { [demonboundId] = [attack] },
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = BasicAi("ai.enemy", attack.ContentId) },
            new Dictionary<ContentId, SkillDefinition> { [attack.ContentId] = attack }));

        Assert.Multiple(() =>
        {
            Assert.That(service.State.Units[enemyId].CurrentHealth, Is.LessThan(20));
            Assert.That(service.CaptureSnapshot().RecentEvents.OfType<SkillUsedEvent>()
                .Any(value => value.ActorId == demonboundId), Is.True);
        });
    }

    [Test]
    public void AutomaticFinalKill_CachesTerminalResultUntilEveryCommittedFrameIsDequeued()
    {
        UnitInstanceId heroId = new("party-mage");
        UnitInstanceId enemyId = new("enemy-boss");
        BattleUnitState hero = Unit(heroId, "unit.pure-run.mage", new GridPoint(1, 1), 0, 1, 20, 1);
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-elite-poison-caster", new GridPoint(2, 1), 1, 0, 20, 20);
        SkillDefinition attack = Skill("skill.basic.melee", 2);
        var service = new PlayableBattleSessionService(new PlayableBattleSessionContext(
            State([hero, enemy], [enemyId, heroId]), 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>> { [heroId] = Array.Empty<SkillDefinition>() },
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = BasicAi("ai.boss", attack.ContentId) },
            new Dictionary<ContentId, SkillDefinition> { [attack.ContentId] = attack },
            new EncounterRequest("run-boss", 148, new ContentId("encounter.pure-run.special"),
                Array.Empty<RunCharacterState>()),
            new Dictionary<UnitInstanceId, string> { [heroId] = "pure_run_mage" }));

        PureRunBattleResult? terminal = service.BattleResult;
        BattleTerminalDiagnostics diagnostics = service.TerminalDiagnostics;
        BattleUiIntentResult rejected = service.Submit(new EndTurnIntent());
        var stages = new List<string>();
        while (service.DequeueAutomaticFrame() is { } frame) stages.Add(frame.Stage);

        Assert.Multiple(() =>
        {
            Assert.That(terminal, Is.Not.Null);
            Assert.That(terminal!.PlayerVictory, Is.False);
            Assert.That(diagnostics.TerminalResultGenerated, Is.True);
            Assert.That(diagnostics.PendingAutomaticFrameCount, Is.GreaterThan(0));
            Assert.That(diagnostics.NextAutomaticStage, Is.EqualTo("Decision"));
            Assert.That(diagnostics.LivingPlayerUnits, Is.Empty);
            Assert.That(diagnostics.LivingEnemyUnits.Select(value => value.UnitId), Does.Contain(enemyId));
            Assert.That(service.CaptureSnapshot().TerminalPending, Is.True);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.FailureCode, Is.EqualTo("battle.already_finished"));
            Assert.That(stages, Does.Contain("Decision").And.Contain("Skill"));
            Assert.That(service.BattleResult, Is.SameAs(terminal));
            Assert.That(service.TerminalDiagnostics.PendingAutomaticFrameCount, Is.Zero);
        });
    }

    [Test]
    public void FriendlyDecoy_AutomaticallySkipsWithoutBecomingPlayerInput()
    {
        UnitInstanceId ownerId = new("party-amazon");
        UnitInstanceId decoyId = new("summon-decoy");
        UnitInstanceId enemyId = new("enemy-goat");
        BattleUnitState owner = Unit(ownerId, "unit.pure-run.amazon", new GridPoint(0, 0), 0, 0, 20, 20);
        BattleUnitState decoy = new(new UnitState(decoyId, new ContentId("unit.pure-run.amazon-decoy"),
            new GridPoint(1, 1), 3, 20, 0, 1), 10, 10, summonOwnerId: ownerId,
            canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: "Decoy");
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(3, 1), 1, 2, 20, 20);
        SkillDefinition attack = Skill("skill.basic.melee", 2);
        var service = new PlayableBattleSessionService(new PlayableBattleSessionContext(
            State([owner, decoy, enemy], [decoyId, ownerId, enemyId]), 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>> { [ownerId] = Array.Empty<SkillDefinition>() },
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = BasicAi("ai.enemy", attack.ContentId) },
            new Dictionary<ContentId, SkillDefinition> { [attack.ContentId] = attack }));

        Assert.Multiple(() =>
        {
            Assert.That(service.CaptureSnapshot().ActiveUnitId, Is.EqualTo(ownerId));
            Assert.That(service.CaptureSnapshot().Phase, Is.EqualTo(PlayableBattlePhase.PlayerTurn));
            Assert.That(service.CaptureSnapshot().RecentEvents.OfType<TurnAdvancedEvent>(), Is.Not.Empty);
        });
    }

    [Test]
    public void PlayerPresentationAfter_DoesNotContainFutureAiTurnResults()
    {
        PlayableBattleSessionService service = CreateService(out _, out UnitInstanceId enemyId);
        UnitInstanceId playerId = service.State.Units.Values.Single(unit => unit.Unit.PlayerNumber == 0).Unit.InstanceId;
        int playerHealthBefore = service.State.Units[playerId].CurrentHealth;

        BattleUiIntentResult result = service.Submit(new EndTurnIntent());

        Assert.Multiple(() =>
        {
            Assert.That(result.Presentation, Is.Not.Null);
            Assert.That(result.Presentation!.After.ActiveUnitId, Is.EqualTo(enemyId));
            Assert.That(result.Presentation.After.Units.Single(unit => unit.UnitId == playerId).CurrentHealth,
                Is.EqualTo(playerHealthBefore));
            Assert.That(result.Snapshot.ActiveUnitId, Is.EqualTo(playerId),
                "The authoritative session may already have drained AI turns, but the player frame must remain chronological.");
        });
    }

    [Test]
    public void DefeatedActiveUnit_IsSkippedThroughCanonicalEndTurn()
    {
        PlayableBattleSessionService service = CreateService(out _, out _, defeatedLeader: true);

        Assert.Multiple(() =>
        {
            Assert.That(service.CaptureSnapshot().Phase, Is.EqualTo(PlayableBattlePhase.PlayerTurn));
            Assert.That(service.CaptureSnapshot().RecentEvents.OfType<TurnAdvancedEvent>().Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public void PossessedDemonboundSnapshot_ProjectsLearnedSkillsToTheirHighestBranchLevel()
    {
        UnitInstanceId demonboundId = new("party-demonbound");
        UnitInstanceId enemyId = new("enemy-goat");
        SkillDefinition baneLv1 = new(new ContentId("skill.demonbound.bane.lv1"), "test.bane.lv1",
            SkillRole.Demonbound, SkillKind.Active, 1, 3, 1, 2, SkillExecutionKind.Bane, 5,
            SkillDamageKind.Magical, branchId: "skill.demonbound.bane",
            executionProfile: new SkillExecutionProfile(CorruptionCost: 3));
        SkillDefinition baneLv3 = new(new ContentId("skill.demonbound.bane.lv3"), "test.bane.lv3",
            SkillRole.Demonbound, SkillKind.Active, 3, 3, 1, 2, SkillExecutionKind.Bane, 7,
            SkillDamageKind.Magical, branchId: "skill.demonbound.bane",
            executionProfile: new SkillExecutionProfile(CorruptionCost: 3));
        BattleUnitState demonbound = Unit(demonboundId, "unit.pure-run.demonbound",
                new GridPoint(1, 1), 0, 0, 20, 20)
            .WithDemonboundState(new DemonboundBattleState(10, 3, isPossessed: true));
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(8, 8), 1, 1, 20, 20);

        var service = new PlayableBattleSessionService(new PlayableBattleSessionContext(
            State([demonbound, enemy], [demonboundId, enemyId]), 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>> { [demonboundId] = [baneLv1] },
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = BasicAi("ai.enemy", baneLv1.ContentId) },
            new Dictionary<ContentId, SkillDefinition> { [baneLv1.ContentId] = baneLv1, [baneLv3.ContentId] = baneLv3 }));

        SkillDefinition? projected = service.CaptureSnapshot().ActiveSkills
            .SingleOrDefault(skill => skill.BranchId == "skill.demonbound.bane");

        Assert.Multiple(() =>
        {
            Assert.That(projected, Is.Not.Null);
            Assert.That(projected!.ContentId, Is.EqualTo(baneLv3.ContentId));
            Assert.That(projected.Level, Is.EqualTo(3));
        });
    }

    [Test]
    public void IncapacitatedCurrentUnit_AutoEndsAndConsumesStatusBeforeNextPlayer()
    {
        var stunnedId = new UnitInstanceId("party-stunned");
        var nextId = new UnitInstanceId("party-next");
        var enemyId = new UnitInstanceId("enemy-waiting");
        ContentId stunId = new("buff.test-stun");
        BattleUnitState stunnedBase = Unit(stunnedId, "unit.pure-run.poet", new GridPoint(1, 1), 0, 0, 20, 20);
        var stun = new BattleStatusState(stunId, enemyId, 1, 0, canAct: false,
            effectKind: StatusEffectKind.Stun, refreshStrategy: StatusRefreshStrategy.RefreshDuration);
        var stunned = new BattleUnitState(stunnedBase.Unit, 20, 20,
            statuses: new Dictionary<ContentId, BattleStatusState> { [stunId] = stun }, maxMana: 10, currentMana: 10);
        BattleUnitState next = Unit(nextId, "unit.pure-run.mage", new GridPoint(2, 1), 0, 1, 20, 20);
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(8, 8), 1, 2, 20, 20);
        BattleState state = State([stunned, next, enemy], [stunnedId, nextId, enemyId]);
        var context = new PlayableBattleSessionContext(state, 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>(),
            new Dictionary<UnitInstanceId, AiDefinition>(),
            new Dictionary<ContentId, SkillDefinition>());

        var service = new PlayableBattleSessionService(context);
        BattleUiFrame? frame = service.DequeueAutomaticFrame();

        Assert.Multiple(() =>
        {
            Assert.That(service.State.ActiveUnitId, Is.EqualTo(nextId));
            Assert.That(service.State.Round, Is.EqualTo(1));
            Assert.That(service.State.Units[stunnedId].Statuses, Does.Not.ContainKey(stunId));
            Assert.That(service.CaptureSnapshot().RecentEvents.OfType<StatusExpiredEvent>()
                .Any(status => status.TargetId == stunnedId && status.StatusId == stunId), Is.True);
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame!.Stage, Is.EqualTo("IncapacitatedEnd"));
            Assert.That(frame.Snapshot.ActiveUnitId, Is.EqualTo(nextId));
            Assert.That(frame.Snapshot.Units.Single(unit => unit.UnitId == stunnedId).Statuses,
                Does.Not.Contain(stunId));
            Assert.That(frame.Events.OfType<StatusExpiredEvent>()
                .Any(status => status.TargetId == stunnedId && status.StatusId == stunId), Is.True);
        });
    }

    [TestCase(2)]
    [TestCase(3)]
    public void EnemyAi_PrioritizesPoetDecoyAfterCompleteLegalMoveThenDirectAttack(int distance)
    {
        PlayableBattleSessionService service = CreatePoetDecoyPriorityService(distance, blockRoute: false,
            out UnitInstanceId decoyId, out UnitInstanceId protectedId);

        Assert.Multiple(() =>
        {
            Assert.That(service.State.Units[decoyId].CurrentHealth, Is.Zero);
            Assert.That(service.State.Units[protectedId].CurrentHealth, Is.EqualTo(20));
        });
    }

    [Test]
    public void EnemyAi_DoesNotHardPrioritizePoetDecoyWhenNoLegalMoveAttackRouteExists()
    {
        PlayableBattleSessionService service = CreatePoetDecoyPriorityService(3, blockRoute: true,
            out UnitInstanceId decoyId, out UnitInstanceId protectedId);

        Assert.Multiple(() =>
        {
            Assert.That(service.State.Units[decoyId].CurrentHealth, Is.EqualTo(1));
            Assert.That(service.State.Units[protectedId].CurrentHealth, Is.EqualTo(19));
        });
    }

    [TestCase(AiArchetype.Charger)]
    [TestCase(AiArchetype.PredatoryDiver)]
    public void EnemyAi_PrioritizesNearbyPoetDecoyWhenDirectAttackIsLegal(AiArchetype archetype)
    {
        var enemyId = new UnitInstanceId("enemy-poet-test");
        var decoyId = new UnitInstanceId("poet-decoy-test");
        var playerId = new UnitInstanceId("party-poet-test");
        var protectedId = new UnitInstanceId("protected-poet-test");
        SkillDefinition melee = Skill("skill.basic.melee", 5, SkillExecutionKind.MeleeAttack);
        SkillDefinition patternArea = Skill("skill.enemy.pattern-area", 9, SkillExecutionKind.AreaBlast);
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(0, 1), 1, 0, 20, 20);
        var decoyFacts = new UnitState(decoyId, new ContentId("unit.pure-run.poet-decoy"),
            new GridPoint(1, 1), 0, 0, 0, 1);
        var decoy = new BattleUnitState(decoyFacts, 1, 1, summonOwnerId: playerId,
            canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: "Decoy");
        BattleUnitState player = Unit(playerId, "unit.pure-run.poet", new GridPoint(9, 9), 0, 1, 20, 20);
        BattleUnitState protectedNpc = Unit(protectedId, "unit.pure-run.amazon", new GridPoint(2, 1), 0, 2, 20, 20);
        BattleState state = State([enemy, decoy, player, protectedNpc], [enemyId, playerId, decoyId, protectedId]);
        var ai = new AiDefinition(new ContentId("ai.poet-decoy-priority"), archetype,
            new AiProfileDefinition(1, 1, 0, 0), [melee.ContentId, patternArea.ContentId], [patternArea.ContentId]);
        var context = new PlayableBattleSessionContext(state, 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>(),
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = ai },
            new Dictionary<ContentId, SkillDefinition>
            {
                [melee.ContentId] = melee,
                [patternArea.ContentId] = patternArea
            },
            ProtectedNpcUnitId: protectedId);

        var service = new PlayableBattleSessionService(context);

        Assert.Multiple(() =>
        {
            Assert.That(service.State.Units[decoyId].CurrentHealth, Is.Zero);
            Assert.That(service.State.Units[protectedId].CurrentHealth, Is.EqualTo(20));
        });
    }

    [Test]
    public void EnemyAi_DoesNotPrioritizePoetDecoyBehindPoetChargeFirstEnemy()
    {
        UnitInstanceId enemyId = new("enemy-poet-charge"), frontId = new("party-front"),
            decoyId = new("party-poet-decoy");
        BattleUnitState enemy = Unit(enemyId, "unit.enemy.poet", new GridPoint(0, 0), 1, 0, 20, 20);
        BattleUnitState front = Unit(frontId, "unit.pure-run.amazon", new GridPoint(1, 0), 0, 1, 20, 20);
        var decoyFacts = new UnitState(decoyId, new ContentId("unit.pure-run.poet-decoy"),
            new GridPoint(2, 0), 0, 8, 0, 2);
        var decoy = new BattleUnitState(decoyFacts, 1, 1, summonOwnerId: frontId,
            canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: "Decoy");
        var cells = Enumerable.Range(0, 4).ToDictionary(x => new GridPoint(x, 0), _ => new CellState());
        BattleState state = new(new BoardSnapshot(cells), [enemy, front, decoy], [enemyId, frontId, decoyId],
            randomState: 7);
        SkillDefinition charge = new(new ContentId("skill.enemy.poet-charge"), "poet-charge",
            SkillRole.Poet, SkillKind.Active, 1, 0, 1, 4, SkillExecutionKind.PoetCharge, 5,
            SkillDamageKind.Physical, canCrit: false);
        AiDefinition ai = new(new ContentId("ai.enemy.poet-charge"), AiArchetype.Charger,
            new AiProfileDefinition(1, 1, 0, 0), [charge.ContentId], []);

        var service = new PlayableBattleSessionService(new PlayableBattleSessionContext(state, 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>(),
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = ai },
            new Dictionary<ContentId, SkillDefinition> { [charge.ContentId] = charge }));

        Assert.Multiple(() =>
        {
            Assert.That(service.State.Units[frontId].CurrentHealth, Is.LessThan(20));
            Assert.That(service.State.Units[decoyId].CurrentHealth, Is.EqualTo(1));
        });
    }

    private static PlayableBattleSessionService CreatePoetDecoyPriorityService(int distance, bool blockRoute,
        out UnitInstanceId decoyId, out UnitInstanceId protectedId)
    {
        var enemyId = new UnitInstanceId("enemy-poet-route-test");
        decoyId = new UnitInstanceId("poet-decoy-route-test");
        var playerId = new UnitInstanceId("party-poet-route-test");
        protectedId = new UnitInstanceId("protected-poet-route-test");
        var melee = new SkillDefinition(new ContentId("skill.route-test.melee"), "route-test.melee",
            SkillRole.Any, SkillKind.Active, 1, 0, 1, 1, SkillExecutionKind.MeleeAttack, 5,
            SkillDamageKind.Physical, canCrit: false);
        BattleUnitState enemy = Unit(enemyId, "unit.pure-run.goat-charger", new GridPoint(0, 1), 1, 0, 20, 20);
        var decoyFacts = new UnitState(decoyId, new ContentId("unit.pure-run.poet-decoy"),
            new GridPoint(distance, 1), 0, 0, 0, 1);
        var decoy = new BattleUnitState(decoyFacts, 1, 1, summonOwnerId: playerId,
            canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: "Decoy");
        BattleUnitState player = Unit(playerId, "unit.pure-run.poet", new GridPoint(9, 9), 0, 1, 20, 20);
        BattleUnitState protectedNpc = Unit(protectedId, "unit.pure-run.amazon", new GridPoint(0, 2), 0, 2, 20, 20);
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 10; x++)
        for (int y = 0; y < 10; y++)
            cells[new GridPoint(x, y)] = blockRoute && x == 1
                ? new CellState(obstacle: MovementObstacleKind.Absolute)
                : new CellState();
        var state = new BattleState(new BoardSnapshot(cells), [enemy, decoy, player, protectedNpc],
            [enemyId, playerId, decoyId, protectedId], randomState: 7);
        var ai = new AiDefinition(new ContentId("ai.poet-decoy-route-priority"), AiArchetype.Charger,
            new AiProfileDefinition(1, 1, 0, 0), [melee.ContentId], Array.Empty<ContentId>());
        return new PlayableBattleSessionService(new PlayableBattleSessionContext(state, 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>(),
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = ai },
            new Dictionary<ContentId, SkillDefinition> { [melee.ContentId] = melee },
            ProtectedNpcUnitId: protectedId));
    }

    private static PlayableBattleSessionService CreateService(
        out SkillDefinition playerSkill,
        out UnitInstanceId enemyId,
        bool defeatedLeader = false,
        GridPoint? enemyCell = null,
        SkillExecutionKind executionKind = SkillExecutionKind.Fireball,
        int playerMana = 10,
        int skillMana = 0,
        GridPoint? droppedSpear = null,
        GridPoint? livingBlockerCell = null)
    {
        var playerId = new UnitInstanceId("party-mage");
        enemyId = new UnitInstanceId("enemy-goat");
        var leaderId = new UnitInstanceId("party-leader");
        playerSkill = Skill("skill.mage.fireball.lv1", 50, executionKind, skillMana);
        SkillDefinition enemySkill = Skill("skill.basic.melee", 1);
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 10; x++)
        for (int y = 0; y < 10; y++)
            cells[new GridPoint(x, y)] = new CellState();
        var player = Unit(playerId, "unit.pure-run.mage", new GridPoint(1, 1), 0, 0, 20, 20, playerMana);
        var enemy = Unit(enemyId, "unit.pure-run.goat-charger", enemyCell ?? new GridPoint(2, 1), 1, 1, 20, 20);
        var units = new List<BattleUnitState>();
        var order = new List<UnitInstanceId>();
        if (defeatedLeader)
        {
            units.Add(Unit(leaderId, "unit.pure-run.amazon", new GridPoint(0, 1), 0, 0, 20, 0));
            order.Add(leaderId);
        }
        units.Add(player);
        order.Add(playerId);
        if (livingBlockerCell is GridPoint blockerCell)
        {
            var blockerId = new UnitInstanceId("party-blocker");
            units.Add(Unit(blockerId, "unit.pure-run.amazon", blockerCell, 0, 2, 20, 20));
            order.Add(blockerId);
        }
        units.Add(enemy);
        order.Add(enemyId);
        var state = new BattleState(new BoardSnapshot(cells), units, order, randomState: 7,
            droppedSpears: droppedSpear is GridPoint spear
                ? new Dictionary<UnitInstanceId, GridPoint> { [playerId] = spear }
                : null);
        var ai = new AiDefinition(
            new ContentId("ai.pure-run.charger"), AiArchetype.Charger,
            new AiProfileDefinition(1, 1, 0, 0), new[] { enemySkill.ContentId }, Array.Empty<ContentId>());
        var request = new EncounterRequest("run-test", 2, new ContentId("encounter.pure-run.n1"), Array.Empty<RunCharacterState>());
        var context = new PlayableBattleSessionContext(
            state, 0,
            new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>> { [playerId] = new[] { playerSkill } },
            new Dictionary<UnitInstanceId, AiDefinition> { [enemyId] = ai },
            new Dictionary<ContentId, SkillDefinition> { [playerSkill.ContentId] = playerSkill, [enemySkill.ContentId] = enemySkill },
            request,
            new Dictionary<UnitInstanceId, string> { [playerId] = "pure_run_mage" });
        return new PlayableBattleSessionService(context);
    }

    private static SkillDefinition Skill(string id, int damage, SkillExecutionKind? executionKind = null, int manaCost = 0) => new(
        new ContentId(id), id, SkillRole.Any, SkillKind.Active, 1, manaCost, 1, 4,
        executionKind ?? (id.Contains("fireball", StringComparison.Ordinal) ? SkillExecutionKind.Fireball : SkillExecutionKind.MeleeAttack),
        damage, SkillDamageKind.Magical);

    private static AiDefinition BasicAi(string id, ContentId skillId) => new(new ContentId(id), AiArchetype.Charger,
        new AiProfileDefinition(1, 2, 0, 0), [skillId], Array.Empty<ContentId>());

    private static BattleState State(IEnumerable<BattleUnitState> units, IReadOnlyList<UnitInstanceId> order)
    {
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 10; x++) for (int y = 0; y < 10; y++) cells[new GridPoint(x, y)] = new CellState();
        return new BattleState(new BoardSnapshot(cells), units, order, randomState: 7);
    }

    private static BattleUnitState Unit(
        UnitInstanceId id, string definitionId, GridPoint cell, int player, int ordinal, int maxHealth, int health, int currentMana = 10) =>
        new(new UnitState(id, new ContentId(definitionId), cell, 3, 10 - ordinal, player, ordinal),
            maxHealth, health, maxMana: 10, currentMana: currentMana, physicalAttack: 1, magicalAttack: 1);
}
