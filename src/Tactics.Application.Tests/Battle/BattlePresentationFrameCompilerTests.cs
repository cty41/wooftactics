using NUnit.Framework;
using Tactics.Application.Battle;
using Tactics.Application.Presentation;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Application.Tests.Battle;

public sealed class BattlePresentationFrameCompilerTests
{
    [Test]
    public void CompilesOrderedMoveActionHitAndDefeatWithoutChangingSnapshots()
    {
        UnitInstanceId actor = new("actor"); UnitInstanceId target = new("target"); ContentId skillId = new("skill.basic.melee");
        BattleUiSnapshot before = Snapshot(actor,target,new GridPoint(1,1),new GridPoint(3,1),true);
        BattleUiSnapshot after = Snapshot(actor,target,new GridPoint(2,1),new GridPoint(3,1),false);
        SkillDefinition skill = new(skillId,"basic",SkillRole.Any,SkillKind.Basic,1,0,1,1,SkillExecutionKind.MeleeAttack,5,SkillDamageKind.Physical);
        BattleEvent[] events =
        [
            new UnitMovedEvent(actor,new GridPoint(1,1),new GridPoint(2,1),[new GridPoint(2,1)]),
            new SkillUsedEvent(actor,target,skillId),
            new DamageAppliedEvent(actor,target,skillId,5,0),
            new UnitDefeatedEvent(target)
        ];
        BattlePresentationFrame frame=BattlePresentationFrameCompiler.Compile("test",before,after,events,new Dictionary<ContentId,SkillDefinition>{{skillId,skill}});
        Assert.That(frame.Cues.Select(cue=>cue.Kind),Is.EqualTo(new[]{PresentationCueKind.Move,PresentationCueKind.Melee,PresentationCueKind.Hit,PresentationCueKind.Defeat}));
        Assert.That(frame.Cues[0].Path,Is.EqualTo(new[]{new GridPoint(2,1)}));
        Assert.That(frame.Cues[2].ActorId,Is.EqualTo(target));
        Assert.That(frame.Cues[2].InstigatorId,Is.EqualTo(actor));
        Assert.That(frame.Cues.All(cue=>cue.Markers.Count==5),Is.True);
        Assert.That(before.Units.Single(value=>value.UnitId==actor).Cell,Is.EqualTo(new GridPoint(1,1)));
    }

    [Test]
    public void FireballPathAndAffectedUnitsComeOnlyFromCommittedEvents()
    {
        UnitInstanceId actor=new("mage"),primary=new("enemy.primary"),secondary=new("enemy.secondary");ContentId skillId=new("skill.mage.fireball.lv1");
        BattleUiSnapshot before=Snapshot(actor,primary,new GridPoint(1,1),new GridPoint(4,1),true);
        BattleUiSnapshot after=before with{RecentEvents=[]};
        SkillDefinition skill=new(skillId,"mage.fireball",SkillRole.Mage,SkillKind.Active,1,5,1,5,SkillExecutionKind.Fireball,6,SkillDamageKind.Magical);
        BattleEvent[] events=[new SkillUsedEvent(actor,primary,skillId),new DamageAppliedEvent(actor,primary,skillId,6,4)];
        BattlePresentationFrame frame=BattlePresentationFrameCompiler.Compile("fireball",before,after,events,new Dictionary<ContentId,SkillDefinition>{{skillId,skill}});
        BattlePresentationCue cue=frame.Cues.Single(value=>value.Kind==PresentationCueKind.Cast);
        Assert.That(cue.Path,Is.EqualTo(new[]{new GridPoint(2,1),new GridPoint(3,1),new GridPoint(4,1)}));
        Assert.That(cue.AffectedUnitIds,Is.EqualTo(new[]{primary}));
        Assert.That(cue.AffectedUnitIds,Does.Not.Contain(secondary));
    }

    [Test]
    public void BaneCompilesAsMeleeWithTwoCellPathAndNearToFarImpacts()
    {
        UnitInstanceId actor = new("demonbound"), near = new("enemy.near"), far = new("enemy.far");
        ContentId skillId = new("skill.demonbound.bane.lv2");
        BattleUiSnapshot snapshot = Snapshot(actor, near, new GridPoint(1, 1), new GridPoint(2, 1), true);
        BattleUiUnitSnapshot farUnit = new(far, new ContentId("unit.test"), new GridPoint(3, 1), 1, true,
            10, 10, 5, 5, false, [], new Dictionary<ContentId, int>());
        snapshot = snapshot with { Units = snapshot.Units.Append(farUnit).ToArray() };
        SkillDefinition skill = new(skillId, "godot.bane", SkillRole.Demonbound, SkillKind.Active, 2, 3, 1, 1,
            SkillExecutionKind.Bane, 6, SkillDamageKind.Magical,
            new ContentId("buff.demonbound.bane-debuff"), 1);
        BattleEvent[] events =
        [
            new SkillUsedEvent(actor, near, skillId),
            new DamageAppliedEvent(actor, near, skillId, 7, 3),
            new DamageAppliedEvent(actor, far, skillId, 7, 3)
        ];

        BattlePresentationFrame frame = BattlePresentationFrameCompiler.Compile("bane", snapshot, snapshot,
            events, new Dictionary<ContentId, SkillDefinition> { [skillId] = skill });
        BattlePresentationCue action = frame.Cues[0];

        Assert.Multiple(() =>
        {
            Assert.That(action.Kind, Is.EqualTo(PresentationCueKind.Melee));
            Assert.That(action.Destination, Is.EqualTo(new GridPoint(3, 1)));
            Assert.That(action.Path, Is.EqualTo(new[] { new GridPoint(2, 1), new GridPoint(3, 1) }));
            Assert.That(action.AffectedUnitIds, Is.EqualTo(new[] { near, far }));
            Assert.That(frame.Cues.Skip(1).Select(value => value.ActorId), Is.EqualTo(new[] { near, far }));
        });
    }

    [Test]
    public void StatusAndSpearEffectsComeOnlyFromCommittedEvents()
    {
        UnitInstanceId actor=new("amazon"),target=new("enemy");ContentId skillId=new("skill.poison-spear.lv1"),poison=new("buff.poison");
        BattleUiSnapshot snapshot=Snapshot(actor,target,new GridPoint(1,1),new GridPoint(4,1),true);
        SkillDefinition skill=new(skillId,"poison",SkillRole.Amazon,SkillKind.Active,1,5,1,5,SkillExecutionKind.PoisonSpear,9,SkillDamageKind.Physical);
        GridPoint drop=new(5,1);BattleEvent[] events=[new SkillUsedEvent(actor,target,skillId),new StatusAppliedEvent(actor,target,poison,2),new SpearDroppedEvent(actor,drop)];
        BattlePresentationCue cue=BattlePresentationFrameCompiler.Compile("poison",snapshot,snapshot,events,new Dictionary<ContentId,SkillDefinition>{{skillId,skill}}).Cues.Single();
        Assert.That(cue.Effects!.Any(value=>value.Kind==BattlePresentationEffectKind.StatusApplied&&value.ContentId==poison),Is.True);
        Assert.That(cue.Effects!.Single(value=>value.Kind==BattlePresentationEffectKind.SpearDropped).Cell,Is.EqualTo(drop));
    }

    [Test]
    public void SummonCastFacesTheCommittedSummonCellInsteadOfTheCaster()
    {
        UnitInstanceId actor=new("necromancer"),summon=new("skeleton");
        ContentId skillId=new("skill.necromancer.summon-skeleton.lv1");
        BattleUiSnapshot before=Snapshot(actor,new UnitInstanceId("corpse"),new GridPoint(1,4),new GridPoint(3,4),false);
        BattleUiSnapshot after=before;
        SkillDefinition skill=new(skillId,"summon",SkillRole.Necromancer,SkillKind.Active,1,3,1,5,
            SkillExecutionKind.SummonSkeleton,0,SkillDamageKind.None);
        GridPoint summonedCell=new(3,4);
        BattleEvent[] events=
        [
            new SkillUsedEvent(actor,actor,skillId),
            new UnitSummonedEvent(actor,summon,new ContentId("unit.pure-run.skeleton-warrior"),summonedCell)
        ];

        BattlePresentationCue cue=BattlePresentationFrameCompiler.Compile("summon",before,after,events,
            new Dictionary<ContentId,SkillDefinition>{{skillId,skill}}).Cues.Single();

        Assert.That(cue.Destination,Is.EqualTo(summonedCell));
    }

    [Test]
    public void CompilesFloatingNumbersOnlyFromCommittedCombatFacts()
    {
        UnitInstanceId actor=new("mage"),target=new("enemy");
        ContentId skillId=new("skill.mage.lightning.lv1"),itemId=new("item.consumable.mana-potion");
        BattleUiSnapshot snapshot=Snapshot(actor,target,new GridPoint(1,1),new GridPoint(3,1),true);
        SkillDefinition skill=new(skillId,"lightning",SkillRole.Mage,SkillKind.Active,1,5,1,5,
            SkillExecutionKind.Lightning,10,SkillDamageKind.Magical);
        BattleEvent[] events=
        [
            new CombatRollResolvedEvent(actor,target,skillId,99,0,"critical",1),
            new DamageAppliedEvent(actor,target,skillId,10,0),
            new HealthRestoredEvent(actor,actor,new ContentId("item.consumable.life-potion"),3,8),
            new ManaRestoredEvent(actor,actor,itemId,4,9)
        ];

        BattlePresentationFrame frame=BattlePresentationFrameCompiler.Compile("numbers",snapshot,snapshot,events,
            new Dictionary<ContentId,SkillDefinition>{{skillId,skill}});

        Assert.That(frame.Numbers.Select(value=>value.Kind),Is.EqualTo(new[]
            { BattlePresentationNumberKind.Critical, BattlePresentationNumberKind.Heal, BattlePresentationNumberKind.Mana }));
        Assert.That(frame.Numbers.Select(value=>value.Text),Is.EqualTo(new[]{"-10","+3","+4 MP"}));
        Assert.That(frame.Numbers.All(value=>value.Marker==PresentationMarkerKind.Impact),Is.True);
    }

    [Test]
    public void PoisonTickCreatesAnExplicitTargetMarkerAndDamageNumber()
    {
        UnitInstanceId source = new("amazon"), target = new("enemy");
        ContentId poison = new("buff.poison");
        BattleUiSnapshot snapshot = Snapshot(source, target, new GridPoint(1, 1), new GridPoint(3, 1), true);

        BattlePresentationFrame frame = BattlePresentationFrameCompiler.Compile("turn-start", snapshot, snapshot,
            [new StatusTickedEvent(source, target, poison, 2, 8)],
            new Dictionary<ContentId, SkillDefinition>());

        Assert.Multiple(() =>
        {
            Assert.That(frame.Cues.Single().Kind, Is.EqualTo(PresentationCueKind.StatusTick));
            Assert.That(frame.Cues.Single().ActorId, Is.EqualTo(target));
            Assert.That(frame.Numbers.Single().Text, Is.EqualTo("-2"));
            Assert.That(frame.Numbers.Single().Marker, Is.EqualTo(PresentationMarkerKind.Impact));
        });
    }

    [Test]
    public void HealingStatusTickCreatesHealEffectNumberAndStatusCue()
    {
        UnitInstanceId source = new("poet"), target = new("ally");
        ContentId statusId = new("buff.poet.wine-healing");
        BattleUiSnapshot snapshot = Snapshot(source, target, new GridPoint(1, 1), new GridPoint(2, 1), true);

        BattlePresentationFrame frame = BattlePresentationFrameCompiler.Compile("turn-start", snapshot, snapshot,
            [new StatusHealingTickedEvent(source, target, statusId, 3, 8)],
            new Dictionary<ContentId, SkillDefinition>());

        BattlePresentationCue cue = frame.Cues.Single();
        BattlePresentationEffect effect = cue.Effects!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(cue.Kind, Is.EqualTo(PresentationCueKind.StatusTick));
            Assert.That(cue.ActorId, Is.EqualTo(target));
            Assert.That(cue.InstigatorId, Is.EqualTo(source));
            Assert.That(effect.Kind, Is.EqualTo(BattlePresentationEffectKind.StatusTicked));
            Assert.That(effect.ContentId, Is.EqualTo(statusId));
            Assert.That(effect.Amount, Is.EqualTo(3));
            Assert.That(frame.Numbers.Single().Kind, Is.EqualTo(BattlePresentationNumberKind.Heal));
            Assert.That(frame.Numbers.Single().Text, Is.EqualTo("+3"));
            Assert.That(frame.Numbers.Single().TargetId, Is.EqualTo(target));
        });
    }

    [Test]
    public void MultiHitRollsAreCorrelatedInEventOrderWithoutDuplicateKeyFailure()
    {
        UnitInstanceId actor=new("amazon"),target=new("enemy");
        ContentId skillId=new("skill.amazon.multi-stab.lv1");
        BattleUiSnapshot snapshot=Snapshot(actor,target,new GridPoint(1,1),new GridPoint(2,1),true);
        SkillDefinition skill=new(skillId,"multi-stab",SkillRole.Amazon,SkillKind.Active,1,3,1,2,
            SkillExecutionKind.MultiStab,4,SkillDamageKind.Physical);
        BattleEvent[] events=
        [
            new CombatRollResolvedEvent(actor,target,skillId,99,0,"critical",1),
            new DamageAppliedEvent(actor,target,skillId,4,6),
            new CombatRollResolvedEvent(actor,target,skillId,30,0,"hit",2),
            new DamageAppliedEvent(actor,target,skillId,4,2)
        ];

        BattlePresentationFrame frame=BattlePresentationFrameCompiler.Compile("multi-hit",snapshot,snapshot,events,
            new Dictionary<ContentId,SkillDefinition>{{skillId,skill}});

        Assert.That(frame.Numbers.Select(value=>value.Kind),Is.EqualTo(new[]
            { BattlePresentationNumberKind.Critical, BattlePresentationNumberKind.Normal }));
        Assert.That(frame.Numbers.Select(value=>value.Sequence),Is.EqualTo(new[]{0,1}));
        Assert.That(frame.Numbers.Select(value=>value.Text),Is.EqualTo(new[]{"-4","-4"}));
    }

    private static BattleUiSnapshot Snapshot(UnitInstanceId actor,UnitInstanceId target,GridPoint actorCell,GridPoint targetCell,bool targetAlive)
    {
        BattleUiUnitSnapshot Unit(UnitInstanceId id,GridPoint cell,bool alive)=>new(id,new ContentId("unit.test"),cell,id==actor?0:1,alive,alive?10:0,10,5,5,false,[],new Dictionary<ContentId,int>());
        return new BattleUiSnapshot(PlayableBattlePhase.PlayerTurn,1,actor,BattleTargetingMode.None,null,[Unit(actor,actorCell,true),Unit(target,targetCell,targetAlive)],[],[],[],null,[],new Dictionary<UnitInstanceId,GridPoint>(),[],[actor,target],0,null,[]);
    }
}
