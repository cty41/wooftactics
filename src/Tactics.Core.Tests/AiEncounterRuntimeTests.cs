using NUnit.Framework;
using Tactics.Core.AI;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Skills;
using Tactics.Core.Battle;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

[TestFixture]
public sealed class AiEncounterRuntimeTests
{
    [Test]
    public void Resolver_BindsN1ToFrozenOpenSpawns()
    {
        var layout=new BattleLayoutDefinition(new ContentId("battle-layout.pure-run.open"),new[]{new GridPoint(1,4),new GridPoint(1,5),new GridPoint(2,4)},new[]{new GridPoint(6,4),new GridPoint(7,3),new GridPoint(7,5),new GridPoint(8,4)},Array.Empty<GridPoint>());
        var monster=new EncounterMonsterDefinition(new ContentId("unit.pure-run.charger"),new ContentId("ai.pure-run.charger"),new[]{new ContentId("skill.enemy.charge-strike.lv1")});
        var encounter=new EncounterDefinition(new ContentId("encounter.pure-run.n1"),layout.ContentId,new[]{monster,monster,monster});
        ResolvedEncounter resolved=new EncounterResolver().Resolve(encounter,layout);
        Assert.That(resolved.Enemies.Select(item=>item.Cell),Is.EqualTo(layout.EnemySpawns.Take(3)));
    }

    [Test]
    public void AiDefinition_PreservesElitePatternOrder()
    {
        var definition=new AiDefinition(new ContentId("ai.pure-run.elite-charger"),AiArchetype.EliteCharger,new AiProfileDefinition(1,1,0,0),new[]{new ContentId("skill.basic.melee"),new ContentId("skill.enemy.charge-strike.lv1")},new[]{new ContentId("skill.enemy.charge-strike.lv1"),new ContentId("skill.basic.melee")});
        Assert.That(definition.PatternSkillIds.Select(value=>value.Value),Is.EqualTo(new[]{"skill.enemy.charge-strike.lv1","skill.basic.melee"}));
    }

    [TestCase(SkillExecutionKind.RangedAttack)]
    [TestCase(SkillExecutionKind.ChargeStrike)]
    [TestCase(SkillExecutionKind.HeavyShot)]
    [TestCase(SkillExecutionKind.AreaBlast)]
    public void EnemySkillKinds_ArePartOfGenericSkillContract(SkillExecutionKind kind)=>Assert.That(Enum.IsDefined(kind),Is.True);

    [Test]
    public void Decision_GeneratesMoveAlongsideCurrentAttackAndCanSelectMoveThenSkill()
    {
        var cells=new Dictionary<GridPoint,CellState>();for(int x=0;x<5;x++)for(int y=0;y<3;y++)cells[new GridPoint(x,y)]=new CellState();
        var actorId=new UnitInstanceId("enemy");var targetId=new UnitInstanceId("party");
        var actor=new BattleUnitState(new UnitState(actorId,new ContentId("unit.enemy"),new GridPoint(0,1),3,5,1,0),20,20);
        var target=new BattleUnitState(new UnitState(targetId,new ContentId("unit.party"),new GridPoint(3,1),3,4,0,1),20,20);
        var state=new BattleState(new BoardSnapshot(cells),new[]{actor,target},new[]{actorId,targetId});
        var skill=new SkillDefinition(new ContentId("skill.basic.melee"),"melee",SkillRole.Any,SkillKind.Active,1,0,1,1,SkillExecutionKind.MeleeAttack,2,SkillDamageKind.Physical);
        var definition=new AiDefinition(new ContentId("ai.test"),AiArchetype.Charger,new AiProfileDefinition(1,1,0,0),new[]{skill.ContentId},Array.Empty<ContentId>());
        AiTurnPlan plan=new AiDecisionService().Decide(state,definition,new Dictionary<ContentId,SkillDefinition>{{skill.ContentId,skill}});
        Assert.Multiple(()=>
        {
            Assert.That(plan.Candidates.Any(item=>item.Intent==AiIntentKind.Engage),Is.True);
            Assert.That(plan.Candidates.Any(item=>item.SkillId==skill.ContentId&&item.MoveBeforeSkill),Is.True);
        });
    }

    [Test]
    public void BasicAttack_TargetHealthCurveCanSelectLowHealthSummon()
    {
        var cells=new Dictionary<GridPoint,CellState>();for(int x=0;x<4;x++)for(int y=0;y<4;y++)cells[new GridPoint(x,y)]=new CellState();
        var actorId=new UnitInstanceId("enemy");var heroId=new UnitInstanceId("party.hero");var skeletonId=new UnitInstanceId("party.skeleton.0");
        var actor=new BattleUnitState(new UnitState(actorId,new ContentId("unit.enemy"),new GridPoint(1,1),0,5,1,0),20,20);
        var hero=new BattleUnitState(new UnitState(heroId,new ContentId("unit.hero"),new GridPoint(2,1),0,4,0,1),20,20);
        var skeleton=new BattleUnitState(new UnitState(skeletonId,new ContentId("unit.pure-run.skeleton-warrior"),new GridPoint(1,2),0,4,0,2),12,2,summonOwnerId:heroId,canProduceCorpse:false);
        var state=new BattleState(new BoardSnapshot(cells),new[]{actor,hero,skeleton},new[]{actorId,heroId,skeletonId});
        var skill=new SkillDefinition(new ContentId("skill.basic.melee"),"melee",SkillRole.Any,SkillKind.Basic,1,0,1,1,SkillExecutionKind.MeleeAttack,2,SkillDamageKind.Physical);
        var graph=new AiDecisionGraphDefinition(
            new[]{new AiIntentDefinition("intent","BasicAttack",25,true)},Array.Empty<AiRuleDefinition>(),
            new[]{new AiScoreDefinition("health","TargetHealth",10,true,new[]{new AiCurveKey(0,1,0,-1),new AiCurveKey(1,0,-1,0)})},
            new[]{new AiDecisionEdge("intent","health")},"sha256:test");
        var definition=new AiDefinition(new ContentId("ai.test"),AiArchetype.Charger,new AiProfileDefinition(0,0,0,0),new[]{skill.ContentId},Array.Empty<ContentId>(),graph);
        AiTurnPlan plan=new AiDecisionService().Decide(state,definition,new Dictionary<ContentId,SkillDefinition>{{skill.ContentId,skill}});
        Assert.That(plan.Selected.TargetId,Is.EqualTo(skeletonId));
    }

    [Test]
    public void Decision_PriorityTargetOverridesOtherwiseEquivalentTarget()
    {
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++) cells[new GridPoint(x, y)] = new CellState();
        var actorId = new UnitInstanceId("enemy");
        var heroId = new UnitInstanceId("party.hero");
        var protectedNpcId = new UnitInstanceId("party.escort");
        var actor = new BattleUnitState(new UnitState(actorId, new ContentId("unit.enemy"),
            new GridPoint(1, 1), 0, 5, 1, 0), 20, 20);
        var hero = new BattleUnitState(new UnitState(heroId, new ContentId("unit.hero"),
            new GridPoint(2, 1), 0, 4, 0, 1), 20, 20);
        var protectedNpc = new BattleUnitState(new UnitState(protectedNpcId, new ContentId("unit.escort"),
            new GridPoint(1, 2), 0, 3, 0, 2), 20, 20);
        var state = new BattleState(new BoardSnapshot(cells), [actor, hero, protectedNpc],
            [actorId, heroId, protectedNpcId]);
        var skill = new SkillDefinition(new ContentId("skill.basic.melee"), "melee", SkillRole.Any,
            SkillKind.Basic, 1, 0, 1, 1, SkillExecutionKind.MeleeAttack, 2, SkillDamageKind.Physical);
        var definition = new AiDefinition(new ContentId("ai.test"), AiArchetype.Charger,
            new AiProfileDefinition(0, 0, 0, 0), [skill.ContentId], Array.Empty<ContentId>());

        AiTurnPlan plan = new AiDecisionService().Decide(state, definition,
            new Dictionary<ContentId, SkillDefinition> { [skill.ContentId] = skill },
            priorityTargetId: protectedNpcId);

        Assert.That(plan.Selected.TargetId, Is.EqualTo(protectedNpcId));
    }

    [Test]
    public void Decision_IncludesAndSelectsDemonicRegenerationAsSelfTargetWhenWounded()
    {
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) cells[new GridPoint(x, y)] = new CellState();
        var actorId = new UnitInstanceId("demonbound");
        var targetId = new UnitInstanceId("ally");
        var actor = new BattleUnitState(new UnitState(actorId, new ContentId("unit.pure-run.demonbound"),
            new GridPoint(1, 1), 0, 5, 0, 0), 20, 2, maxMana: 10, currentMana: 10,
            demonboundState: new DemonboundBattleState(10, 3, isPossessed: true));
        var target = new BattleUnitState(new UnitState(targetId, new ContentId("unit.pure-run.mage"),
            new GridPoint(2, 1), 0, 4, 0, 1), 20, 20);
        var state = new BattleState(new BoardSnapshot(cells), [actor, target], [actorId, targetId]);
        var regeneration = new SkillDefinition(new ContentId("skill.demonbound.regeneration.lv1"), "regen",
            SkillRole.Demonbound, SkillKind.Active, 1, 0, 0, 0, SkillExecutionKind.DemonicRegeneration,
            0, SkillDamageKind.None);
        var definition = new AiDefinition(new ContentId("ai.demonbound.possessed"), AiArchetype.Charger,
            new AiProfileDefinition(1, 1, 1, 1), [regeneration.ContentId], Array.Empty<ContentId>());

        AiTurnPlan plan = new AiDecisionService().Decide(state, definition,
            new Dictionary<ContentId, SkillDefinition> { [regeneration.ContentId] = regeneration },
            strategy: TargetRelationshipStrategy.UnifiedAll);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Selected.SkillId, Is.EqualTo(regeneration.ContentId));
            Assert.That(plan.Selected.TargetId, Is.EqualTo(actorId));
        });
    }

    [Test]
    public void Decision_UsesAdjacentDirectionCellForBaneInsteadOfSelfOrFarTargetCell()
    {
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 5; x++) for (int y = 0; y < 3; y++) cells[new GridPoint(x, y)] = new CellState();
        UnitInstanceId actorId = new("demonbound"), targetId = new("party");
        BattleUnitState actor = new(new UnitState(actorId, new ContentId("unit.pure-run.demonbound"),
            new GridPoint(1, 1), 0, 5, 1, 0), 20, 20, maxMana: 10, currentMana: 10,
            demonboundState: new DemonboundBattleState());
        BattleUnitState target = new(new UnitState(targetId, new ContentId("unit.party"),
            new GridPoint(3, 1), 0, 4, 0, 1), 20, 20);
        BattleState state = new(new BoardSnapshot(cells), [actor, target], [actorId, targetId]);
        SkillDefinition bane = new(new ContentId("skill.demonbound.bane.lv1"), "bane",
            SkillRole.Demonbound, SkillKind.Active, 1, 3, 1, 1, SkillExecutionKind.Bane, 5,
            SkillDamageKind.Magical, executionProfile: new SkillExecutionProfile(CorruptionCost: 3));
        AiDefinition definition = new(new ContentId("ai.demonbound"), AiArchetype.Charger,
            new AiProfileDefinition(1, 1, 1, 1), [bane.ContentId], Array.Empty<ContentId>());

        AiTurnPlan plan = new AiDecisionService().Decide(state, definition,
            new Dictionary<ContentId, SkillDefinition> { [bane.ContentId] = bane });

        AiIntentCandidate candidate = plan.Candidates.Single(value => value.SkillId == bane.ContentId &&
            value.Destination == actor.Unit.Position);
        Assert.That(candidate.TargetId, Is.EqualTo(targetId));
        Assert.That(candidate.TargetCell, Is.EqualTo(new GridPoint(2, 1)));
    }

    [Test]
    public void PredatoryDiver_SelectsKillableThenLowestHealthAndDoesNotRetreatWhenWounded()
    {
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 5; x++) for (int y = 0; y < 4; y++) cells[new GridPoint(x, y)] = new CellState();
        UnitInstanceId batId = new("enemy.bat"), killableId = new("party.killable"), healthyId = new("party.healthy");
        BattleUnitState bat = new(new UnitState(batId, new ContentId("unit.pure-run.maw-bat"),
            new GridPoint(0, 1), 5, 24, 1, 0, movementKind: UnitMovementKind.Air), 14, 3);
        BattleUnitState killable = new(new UnitState(killableId, new ContentId("unit.party.a"),
            new GridPoint(3, 1), 3, 10, 0, 1), 20, 4);
        BattleUnitState healthy = new(new UnitState(healthyId, new ContentId("unit.party.b"),
            new GridPoint(1, 2), 3, 9, 0, 2), 20, 12);
        BattleState state = new(new BoardSnapshot(cells), [bat, killable, healthy], [batId, killableId, healthyId]);
        SkillDefinition bite = new(new ContentId("skill.enemy.maw-bat-bite.lv1"), "maw_bat_bite",
            SkillRole.Any, SkillKind.Active, 1, 0, 1, 1, SkillExecutionKind.DirectAttack, 4,
            SkillDamageKind.Physical, maxUsesPerTurn: 1,
            executionProfile: new SkillExecutionProfile(LifeStealPercent: 50), canCrit: false);
        AiDefinition definition = new(new ContentId("ai.pure-run.maw-bat-predatory-diver"),
            AiArchetype.PredatoryDiver, new AiProfileDefinition(1, 1, 0, 0), [bite.ContentId], []);

        AiTurnPlan plan = new AiDecisionService().Decide(state, definition,
            new Dictionary<ContentId, SkillDefinition> { [bite.ContentId] = bite });

        Assert.Multiple(() =>
        {
            Assert.That(plan.Selected.TargetId, Is.EqualTo(killableId));
            Assert.That(plan.Selected.SkillId, Is.EqualTo(bite.ContentId));
            Assert.That(plan.Selected.Intent, Is.Not.EqualTo(AiIntentKind.Retreat));
        });
    }

    [Test]
    public void UnifiedStrategy_IncludesAllyOfSameFactionAsCandidateTarget()
    {
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 5; x++) for (int y = 0; y < 3; y++) cells[new GridPoint(x, y)] = new CellState();
        UnitInstanceId demonboundId = new("party-demonbound"), allyId = new("party-mage"), enemyId = new("enemy-goat");
        BattleUnitState demonbound = new(new UnitState(demonboundId, new ContentId("unit.pure-run.demonbound"),
            new GridPoint(1, 1), 0, 5, 0, 0), 20, 20, maxMana: 10, currentMana: 10,
            demonboundState: new DemonboundBattleState(10, 3, isPossessed: true));
        BattleUnitState ally = new(new UnitState(allyId, new ContentId("unit.pure-run.mage"),
            new GridPoint(2, 1), 0, 4, 0, 1), 20, 20);
        BattleUnitState enemy = new(new UnitState(enemyId, new ContentId("unit.pure-run.goat-charger"),
            new GridPoint(4, 1), 1, 2, 1, 2), 20, 20);
        BattleState state = new(new BoardSnapshot(cells), [demonbound, ally, enemy],
            [demonboundId, allyId, enemyId]);
        SkillDefinition attack = new(new ContentId("skill.test.direct"), "direct",
            SkillRole.Any, SkillKind.Active, 1, 0, 1, 5, SkillExecutionKind.DirectAttack, 5,
            SkillDamageKind.Magical, canCrit: false);
        AiDefinition definition = new(new ContentId("ai.demonbound"), AiArchetype.Charger,
            new AiProfileDefinition(1, 1, 1, 1), [attack.ContentId], Array.Empty<ContentId>());

        AiTurnPlan plan = new AiDecisionService().Decide(state, definition,
            new Dictionary<ContentId, SkillDefinition> { [attack.ContentId] = attack },
            strategy: TargetRelationshipStrategy.UnifiedAll);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Candidates.Any(candidate => candidate.TargetId == allyId), Is.True);
            Assert.That(plan.Candidates.Any(candidate => candidate.TargetId == enemyId), Is.True);
        });
    }

    [Test]
    public void UnifiedStrategy_ExcludesNonActingDecoysFromTheCandidatePool()
    {
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 5; x++) for (int y = 0; y < 3; y++) cells[new GridPoint(x, y)] = new CellState();
        UnitInstanceId demonboundId = new("party-demonbound"), decoyId = new("party-decoy");
        BattleUnitState demonbound = new(new UnitState(demonboundId, new ContentId("unit.pure-run.demonbound"),
            new GridPoint(1, 1), 0, 5, 0, 0), 20, 20, maxMana: 10, currentMana: 10,
            demonboundState: new DemonboundBattleState(10, 3, isPossessed: true));
        BattleUnitState decoy = new(new UnitState(decoyId, new ContentId("unit.pure-run.amazon-decoy"),
            new GridPoint(2, 1), 0, 4, 0, 1), 10, 10,
            summonOwnerId: new UnitInstanceId("party-amazon"), summonCategory: "Decoy");
        BattleState state = new(new BoardSnapshot(cells), [demonbound, decoy], [demonboundId, decoyId]);
        SkillDefinition bane = new(new ContentId("skill.demonbound.bane.lv1"), "bane",
            SkillRole.Demonbound, SkillKind.Active, 1, 3, 1, 1, SkillExecutionKind.Bane, 5,
            SkillDamageKind.Magical, executionProfile: new SkillExecutionProfile(CorruptionCost: 3));
        AiDefinition definition = new(new ContentId("ai.demonbound"), AiArchetype.Charger,
            new AiProfileDefinition(1, 1, 1, 1), [bane.ContentId], Array.Empty<ContentId>());

        AiTurnPlan plan = new AiDecisionService().Decide(state, definition,
            new Dictionary<ContentId, SkillDefinition> { [bane.ContentId] = bane },
            strategy: TargetRelationshipStrategy.UnifiedAll);

        Assert.That(plan.Candidates.Any(candidate => candidate.TargetId == decoyId), Is.False);
    }

    [Test]
    public void PoetChargeCandidate_DoesNotClaimARequestedTargetBehindTheFirstEnemy()
    {
        var cells = Enumerable.Range(0, 4).ToDictionary(x => new GridPoint(x, 0), _ => new CellState());
        UnitInstanceId actorId = new("enemy.poet"), frontId = new("party.front"), decoyId = new("party.decoy");
        BattleUnitState actor = new(new UnitState(actorId, new ContentId("unit.enemy.poet"),
            new GridPoint(0, 0), 0, 5, 1, 0), 20, 20, maxMana: 10, currentMana: 10);
        BattleUnitState front = new(new UnitState(frontId, new ContentId("unit.party.front"),
            new GridPoint(1, 0), 0, 4, 0, 1), 20, 20);
        BattleUnitState decoy = new(new UnitState(decoyId, new ContentId("unit.pure-run.poet-decoy"),
            new GridPoint(2, 0), 0, 3, 0, 2), 10, 10,
            summonOwnerId: frontId, canProduceCorpse: false, summonCategory: "Decoy");
        BattleState state = new(new BoardSnapshot(cells), [actor, front, decoy], [actorId, frontId, decoyId]);
        SkillDefinition charge = new(new ContentId("skill.enemy.poet-charge"), "charge", SkillRole.Poet,
            SkillKind.Active, 1, 0, 1, 4, SkillExecutionKind.PoetCharge, 2, SkillDamageKind.Physical,
            canCrit: false);
        AiDefinition definition = new(new ContentId("ai.enemy.poet"), AiArchetype.Charger,
            new AiProfileDefinition(1, 1, 0, 0), [charge.ContentId], []);

        AiTurnPlan plan = new AiDecisionService().Decide(state, definition,
            new Dictionary<ContentId, SkillDefinition> { [charge.ContentId] = charge },
            priorityTargetId: decoyId, requirePriorityTarget: true);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Candidates.Any(candidate => candidate.SkillId == charge.ContentId &&
                candidate.TargetId == decoyId), Is.False);
            Assert.That(plan.Selected.TargetId, Is.EqualTo(frontId));
        });
    }

    [Test]
    public void StandardStrategy_NeverIncludesOwnFactionCandidates()
    {
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 5; x++) for (int y = 0; y < 3; y++) cells[new GridPoint(x, y)] = new CellState();
        UnitInstanceId demonboundId = new("party-demonbound"), allyId = new("party-mage"), enemyId = new("enemy-goat");
        BattleUnitState demonbound = new(new UnitState(demonboundId, new ContentId("unit.pure-run.demonbound"),
            new GridPoint(1, 1), 0, 5, 0, 0), 20, 20, maxMana: 10, currentMana: 10,
            demonboundState: new DemonboundBattleState(7, 1));
        BattleUnitState ally = new(new UnitState(allyId, new ContentId("unit.pure-run.mage"),
            new GridPoint(4, 1), 0, 4, 0, 1), 20, 20);
        BattleUnitState enemy = new(new UnitState(enemyId, new ContentId("unit.pure-run.goat-charger"),
            new GridPoint(2, 1), 1, 2, 1, 2), 20, 20);
        BattleState state = new(new BoardSnapshot(cells), [demonbound, ally, enemy],
            [demonboundId, allyId, enemyId]);
        SkillDefinition bane = new(new ContentId("skill.demonbound.bane.lv1"), "bane",
            SkillRole.Demonbound, SkillKind.Active, 1, 3, 1, 1, SkillExecutionKind.Bane, 5,
            SkillDamageKind.Magical, executionProfile: new SkillExecutionProfile(CorruptionCost: 3));
        AiDefinition definition = new(new ContentId("ai.demonbound"), AiArchetype.Charger,
            new AiProfileDefinition(1, 1, 1, 1), [bane.ContentId], Array.Empty<ContentId>());

        AiTurnPlan plan = new AiDecisionService().Decide(state, definition,
            new Dictionary<ContentId, SkillDefinition> { [bane.ContentId] = bane });

        Assert.Multiple(() =>
        {
            Assert.That(plan.Candidates.Any(candidate => candidate.TargetId == allyId), Is.False);
            Assert.That(plan.Candidates.Any(candidate => candidate.TargetId == enemyId), Is.True);
        });
    }
}
