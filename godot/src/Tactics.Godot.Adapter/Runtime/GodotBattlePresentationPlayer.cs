using Godot;
using Tactics.Application.Presentation;
using Tactics.Core.Board;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record PresentationFrameCompletion(string Stage, bool Recovered, string? Reason);

/// <summary>Consumes read-only cues and owns every transient Tween created for a battle page.</summary>
public partial class GodotBattlePresentationPlayer : Node
{
    private readonly List<Tween> _activeTweens = new();
    private readonly List<Node> _transientNodes = new();
    private readonly HashSet<GodotUnitActor> _animatedActors = new();
    private readonly Dictionary<GodotUnitActor, Vector2> _rootBaselines = new();
    private StandardUnitPresentationResource _profile = new();
    private float _speed = 1f;
    private readonly Dictionary<string, SkillPresentationResource> _skillProfiles = new(StringComparer.Ordinal);
    private string? _pendingStage;
    private bool _completionRaised;
    public bool HasPendingFrame => _pendingStage is not null;
    public int ActiveTransientCount => _transientNodes.Count(node => GodotObject.IsInstanceValid(node));
    public bool IsPlaying => _activeTweens.Any(tween => GodotObject.IsInstanceValid(tween) && tween.IsRunning());
    public int ActiveTweenCount => _activeTweens.Count(tween => GodotObject.IsInstanceValid(tween));
    public int TransientNodeCount => _transientNodes.Count(node => GodotObject.IsInstanceValid(node));
    public event Action<PresentationFrameCompletion>? FrameCompleted;
    public event Action<BattlePresentationNumber>? NumberRequested;

    public void Configure(StandardUnitPresentationResource profile) => _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    public void ConfigureSkills(IEnumerable<SkillPresentationResource> profiles)
    {
        _skillProfiles.Clear();
        foreach(SkillPresentationResource profile in profiles)_skillProfiles[profile.SkillBranch]=profile;
    }
    public static bool IsSupportedSpeed(float speed) => speed is .5f or 1f or 2f or 4f;

    public void SetSpeed(float speed)
    {
        if (!IsSupportedSpeed(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
        _speed = speed;
        foreach (Tween tween in _activeTweens.Where(GodotObject.IsInstanceValid)) tween.SetSpeedScale(speed);
    }

    public void Play(BattlePresentationFrame frame, IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Clear();
        _pendingStage = frame.Stage;
        _completionRaised = false;
        Tween sequence=CreateTween().SetSpeedScale(_speed);_activeTweens.Add(sequence);
        var pendingNumbers = new List<BattlePresentationNumber>(frame.Numbers.OrderBy(value => value.Sequence));
        for (int index = 0; index < frame.Cues.Count; index++)
        {
            BattlePresentationCue cue = frame.Cues[index];
            if (cue.Kind is PresentationCueKind.Melee or PresentationCueKind.Ranged or PresentationCueKind.Cast)
            {
                bool segmentedBane = IsBaneCue(cue);
                PlayCue(sequence, cue, actors, recoverAction: false, playSkillFx: !segmentedBane);
                var actionHits = new List<BattlePresentationCue>();
                while (index + 1 < frame.Cues.Count && frame.Cues[index + 1].Kind == PresentationCueKind.Hit)
                {
                    BattlePresentationCue hit = frame.Cues[++index];
                    actionHits.Add(hit);
                    if (!segmentedBane)
                    {
                        PlayCue(sequence, hit, actors);
                        QueueNextNumber(sequence, pendingNumbers, hit.ActorId);
                    }
                }
                if (segmentedBane) PlayBaneSequence(sequence, cue, actionHits, actors, pendingNumbers);
                RecoverAction(sequence, cue, actors);
            }
            else
            {
                PlayCue(sequence, cue, actors);
                if (cue.Kind is PresentationCueKind.Hit or PresentationCueKind.StatusTick)
                    QueueNextNumber(sequence, pendingNumbers, cue.ActorId);
            }
        }
        foreach (BattlePresentationNumber number in pendingNumbers)
            sequence.TweenCallback(Callable.From(() => NumberRequested?.Invoke(number)));
        // Decision and EndTurn frames intentionally contain no visual cue. A
        // no-op callback keeps the Tween valid so Finished still advances the
        // authoritative automatic-frame queue.
        sequence.TweenCallback(Callable.From(() => { }));
        sequence.Finished += () =>
        {
            _activeTweens.Remove(sequence);
            foreach (GodotUnitActor actor in _animatedActors.Where(GodotObject.IsInstanceValid))
            {
                actor.SetAirMoveOverlay(false);
                actor.RestoreTransientBodyPose();
                actor.SetActionPose(null);
            }
            _animatedActors.Clear();
            _rootBaselines.Clear();
            CompleteFrame(false, null);
        };
    }

    public bool TryRecoverStalledFrame(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!HasPendingFrame || IsPlaying || _completionRaised) return false;
        string stage = _pendingStage!;
        Clear();
        _pendingStage = stage;
        CompleteFrame(true, reason);
        return true;
    }

    private void CompleteFrame(bool recovered, string? reason)
    {
        if (_completionRaised || _pendingStage is null) return;
        _completionRaised = true;
        string stage = _pendingStage;
        _pendingStage = null;
        FrameCompleted?.Invoke(new PresentationFrameCompletion(stage, recovered, reason));
    }

    private void QueueNextNumber(Tween sequence, List<BattlePresentationNumber> pending, UnitInstanceId targetId)
    {
        int index = pending.FindIndex(value => value.TargetId == targetId);
        if (index < 0) return;
        BattlePresentationNumber number = pending[index];
        pending.RemoveAt(index);
        sequence.TweenCallback(Callable.From(() => NumberRequested?.Invoke(number)));
    }

    public static double EstimateMoveDuration(int segmentCount, double segmentDuration, double settleDuration) =>
        segmentCount * segmentDuration + settleDuration;

    public void SetPaused(bool paused){foreach(Tween tween in _activeTweens.Where(GodotObject.IsInstanceValid)){if(paused)tween.Pause();else tween.Play();}}

    public void Clear()
    {
        foreach (Tween tween in _activeTweens)
            if (GodotObject.IsInstanceValid(tween)) tween.Kill();
        _activeTweens.Clear();
        foreach (GodotUnitActor actor in _animatedActors.Where(GodotObject.IsInstanceValid))
        {
            actor.RestoreTransientBodyPose();
            actor.SetActionPose(null);
        }
        foreach ((GodotUnitActor actor, Vector2 baseline) in _rootBaselines)
            if (GodotObject.IsInstanceValid(actor)) actor.Position = baseline;
        _animatedActors.Clear();
        _rootBaselines.Clear();
        foreach(Node node in _transientNodes)
            if(GodotObject.IsInstanceValid(node))node.QueueFree();
        _transientNodes.Clear();
        _pendingStage = null;
        _completionRaised = false;
    }

    public override void _ExitTree() => Clear();

    private void PlayCue(Tween tween,BattlePresentationCue cue, IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors,
        bool recoverAction = true, bool playSkillFx = true)
    {
        if (!actors.TryGetValue(cue.ActorId, out GodotUnitActor? actor) || !GodotObject.IsInstanceValid(actor)) return;
        switch (cue.Kind)
        {
            case PresentationCueKind.Move:
                TrackActor(actor);
                actor.Position = IsometricBattleBoardLayout.GridToScreen(cue.Origin);
                actor.RestoreTransientBodyPose();
                _animatedActors.Add(actor);
                actor.SetAirMoveOverlay(true);
                GridPoint stepOrigin=cue.Origin;
                foreach (var cell in cue.Path)
                {
                    GridPoint from=stepOrigin,to=cell;
                    tween.TweenCallback(Callable.From(()=>actor.SetFacing(GodotPresentationFacingResolver.Resolve(from,to,actor.PresentationFacing))));
                    PlayMoveSegment(tween, actor, IsometricBattleBoardLayout.GridToScreen(from),
                        IsometricBattleBoardLayout.GridToScreen(to));
                    stepOrigin=cell;
                }
                PlayBodySettle(tween, actor);
                break;
            case PresentationCueKind.Melee:
                TrackActor(actor);
                _animatedActors.Add(actor);
                tween.TweenCallback(Callable.From(()=>actor.SetFacing(GodotPresentationFacingResolver.Resolve(cue.Origin,cue.Destination,actor.PresentationFacing))));
                tween.TweenCallback(Callable.From(() => actor.SetActionPose(GodotUnitActionPose.Melee)));
                PlayRelease(tween, actor, cue, _profile.MeleeWindupDuration, _profile.MeleeLungeDuration, _profile.MeleeImpactHold, 18f);
                if(playSkillFx && cue.SkillId is not null)PlaySkillFx(tween,cue,actors);
                if (recoverAction) PlayRecover(tween,actor,cue,_profile.MeleeRecoverDuration);
                break;
            case PresentationCueKind.Ranged:
                TrackActor(actor);
                _animatedActors.Add(actor);
                tween.TweenCallback(Callable.From(()=>actor.SetFacing(GodotPresentationFacingResolver.Resolve(cue.Origin,cue.Destination,actor.PresentationFacing))));
                tween.TweenCallback(Callable.From(() => actor.SetActionPose(GodotUnitActionPose.Ranged)));
                PlayRelease(tween, actor, cue, _profile.RangedAimDuration, _profile.RangedReleaseDuration, 0f, -8f,
                    clearPoseAtRelease: true);
                if(playSkillFx && cue.SkillId is not null)PlaySkillFx(tween,cue,actors);
                if (recoverAction) PlayRecover(tween,actor,cue,_profile.RangedRecoverDuration);
                break;
            case PresentationCueKind.Cast:
                TrackActor(actor);
                actor.RestoreTransientBodyPose();
                _animatedActors.Add(actor);
                tween.TweenCallback(Callable.From(()=>actor.SetFacing(GodotPresentationFacingResolver.Resolve(cue.Origin,cue.Destination,actor.PresentationFacing))));
                tween.TweenCallback(Callable.From(() => actor.SetActionPose(GodotUnitActionPose.Cast)));
                if (actor.Body is null) break;
                tween.TweenProperty(actor.Body, "scale", Vector2.One * 1.12f, _profile.CastChargeDuration).SetTrans(Tween.TransitionType.Sine);
                tween.TweenInterval(_profile.CastReleaseHold);
                if(playSkillFx && cue.SkillId is not null)PlaySkillFx(tween,cue,actors);
                if (recoverAction) tween.TweenProperty(actor.Body, "scale", Vector2.One, _profile.CastRecoverDuration).SetTrans(Tween.TransitionType.Sine);
                break;
            case PresentationCueKind.Hit:
                PlayHitReaction(tween, actor, cue, actors);
                break;
            case PresentationCueKind.StatusTick:
                tween.TweenCallback(Callable.From(() => { }));
                break;
            case PresentationCueKind.Defeat:
                PlayCorpseLanding(tween, actor);
                break;
            case PresentationCueKind.CorpseRemoved:
                tween.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(actor)) actor.QueueFree(); }));
                break;
        }
    }

    private void TrackActor(GodotUnitActor actor)
    {
        _animatedActors.Add(actor);
        _rootBaselines.TryAdd(actor, actor.Position);
    }

    private void PlayMoveSegment(Tween tween, GodotUnitActor actor, Vector2 origin, Vector2 destination) =>
        GodotUnitMoveTweenBuilder.AppendSegment(tween, actor, origin, destination, _profile);

    private void PlayBodySettle(Tween tween, GodotUnitActor actor) =>
        GodotUnitMoveTweenBuilder.AppendSettle(tween, actor, _profile);

    private void PlayHitReaction(Tween tween, GodotUnitActor actor, BattlePresentationCue cue,
        IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors)
    {
        if (actor.Body is null) return;
        actor.RestoreTransientBodyPose();
        _animatedActors.Add(actor);
        Vector2 incoming = Vector2.Right;
        if (cue.InstigatorId is UnitInstanceId sourceId && actors.TryGetValue(sourceId, out GodotUnitActor? source) &&
            GodotObject.IsInstanceValid(source))
            incoming = source.Position.DirectionTo(actor.Position);
        Vector2 recoil = incoming * _profile.HitRecoilPixels;
        float sign = incoming.X >= 0f ? -1f : 1f;
        float rotation = Mathf.DegToRad(sign * _profile.HitRotationDegrees);
        Color baseColor = actor.Body.Modulate;

        tween.TweenCallback(Callable.From(() => actor.SetActionPose(GodotUnitActionPose.Hit)));
        tween.TweenProperty(actor.Body, "position", recoil, _profile.HitRecoilDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(actor.Body, "rotation", rotation, _profile.HitRecoilDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(actor.Body, "scale", new Vector2(1.06f, .92f), _profile.HitRecoilDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(actor.Body, "modulate", new Color(1f, .4f, .4f, baseColor.A),
            _profile.HitRecoilDuration);
        tween.TweenProperty(actor.Body, "rotation", -rotation * .45f, _profile.HitShakeDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
        tween.TweenCallback(Callable.From(() => actor.SetActionPose(null)));
        tween.TweenProperty(actor.Body, "position", Vector2.Zero, _profile.HitRecoverDuration)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(actor.Body, "rotation", 0f, _profile.HitRecoverDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(actor.Body, "scale", Vector2.One, _profile.HitRecoverDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(actor.Body, "modulate", baseColor, _profile.HitRecoverDuration);
    }

    private void PlayCorpseLanding(Tween tween, GodotUnitActor actor)
    {
        if (actor.Body is null) return;
        _animatedActors.Add(actor);
        tween.TweenProperty(actor.Body, "scale", _profile.LethalCollapseScale, _profile.LethalCollapseDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(actor.Body, "rotation", 0f, _profile.LethalCollapseDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(actor) || actor.Body is null) return;
            actor.SetDeathVisual(true);
            actor.Body.Position = Vector2.Up * _profile.CorpseStartHeightPixels;
            actor.Body.Scale = Vector2.One * .85f;
            actor.Body.Rotation = 0f;
        }));
        tween.TweenProperty(actor.Body, "position", Vector2.Zero, _profile.CorpseDropDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(actor.Body, "scale", Vector2.One, _profile.CorpseDropDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenProperty(actor.Body, "scale", new Vector2(1.08f, .88f), _profile.CorpseImpactDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(actor.Body, "scale", Vector2.One, _profile.CorpseSettleDuration)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void RecoverAction(Tween tween, BattlePresentationCue cue,
        IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors)
    {
        if (!actors.TryGetValue(cue.ActorId, out GodotUnitActor? actor) || !GodotObject.IsInstanceValid(actor)) return;
        if (cue.Kind == PresentationCueKind.Cast)
        {
            tween.TweenCallback(Callable.From(() => actor.SetActionPose(null)));
            if (actor.Body is not null)
                tween.TweenProperty(actor.Body, "scale", Vector2.One, _profile.CastRecoverDuration).SetTrans(Tween.TransitionType.Sine);
        }
        else
        {
            tween.TweenCallback(Callable.From(() => actor.SetActionPose(null)));
            PlayRecover(tween, actor, cue, cue.Kind == PresentationCueKind.Melee
                ? _profile.MeleeRecoverDuration : _profile.RangedRecoverDuration);
        }
    }

    public static Vector2 VerticalLightningStart(Vector2 targetHead, float visibleBoardTop) =>
        new(targetHead.X, visibleBoardTop - 32f);

    private void PlaySkillFx(Tween tween, BattlePresentationCue cue, IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors)
    {
        SkillPresentationResource? profile = ResolveSkillProfile(cue);
        if(profile is null)return;
        string branch=cue.SkillId!.Value.Value;
        GridPoint end=cue.Effects?.FirstOrDefault(effect=>effect.Kind==BattlePresentationEffectKind.SpearDropped)?.Cell??cue.Destination;
        Vector2[] impacts=(cue.Effects??Array.Empty<BattlePresentationEffect>()).Where(effect=>effect.TargetId is not null)
            .Select(effect=>cue.AffectedUnitIds.Contains(effect.TargetId!.Value)?effect.TargetId:null).Where(id=>id is not null)
            .Select(id=>cue.TargetId==id?cue.Destination:cue.Destination).Distinct().Select(IsometricBattleBoardLayout.GridToScreen).ToArray();
        if(impacts.Length==0)impacts=cue.AffectedUnitIds.Count>0?[IsometricBattleBoardLayout.GridToScreen(cue.Destination)]:[];
        Vector2 effectEnd = IsometricBattleBoardLayout.GridToScreen(end);
        if (profile.ProgrammaticKind == "lightning" && cue.TargetId is UnitInstanceId targetId &&
            actors.TryGetValue(targetId, out GodotUnitActor? targetActor) && GodotObject.IsInstanceValid(targetActor))
            effectEnd = targetActor.HeadAnchorInParent();
        Vector2 effectStart = profile.ProgrammaticKind == "lightning"
            ? VerticalLightningStart(effectEnd, IsometricBattleBoardLayout.TopCenter.Y)
            : IsometricBattleBoardLayout.GridToScreen(cue.Origin);
        var fx=new GodotProgrammaticSkillFx{Kind=profile.ProgrammaticKind,Start=effectStart,End=effectEnd,Impacts=impacts,Primary=profile.PrimaryColor,Secondary=profile.SecondaryColor,ZIndex=900,Visible=false};
        _transientNodes.Add(fx);
        GetParent().AddChild(fx);
        tween.TweenCallback(Callable.From(()=>{if(GodotObject.IsInstanceValid(fx))fx.Visible=true;}));
        tween.TweenProperty(fx,"Progress",1f,profile.TravelDuration).SetTrans(Tween.TransitionType.Quad);
        tween.TweenInterval(profile.ImpactDuration);
        tween.TweenCallback(Callable.From(() => ReleaseFx(fx)));
    }

    private SkillPresentationResource? ResolveSkillProfile(BattlePresentationCue cue)
    {
        string branch=cue.SkillId!.Value.Value;
        return branch.Contains("fireball",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("mage.fireball"):
            branch.Contains("bone-spear",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("necromancer.bone-spear"):
            branch.Contains("ice-bolt",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("mage.ice-bolt"):
            branch.Contains("lightning",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("mage.lightning"):
            branch.Contains("poison-spear",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("amazon.poison-spear"):
            branch.Contains("amplify-damage",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("necromancer.amplify-damage"):
            branch.Contains("thrust",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("amazon.thrust"):
            branch.Contains("demonbound.bane",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("demonbound.bane"):null;
    }

    private static bool IsBaneCue(BattlePresentationCue cue) =>
        cue.SkillId?.Value.Contains("demonbound.bane", StringComparison.Ordinal) == true;

    private void PlayBaneSequence(Tween tween, BattlePresentationCue cue, IReadOnlyList<BattlePresentationCue> hits,
        IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors, List<BattlePresentationNumber> pendingNumbers)
    {
        SkillPresentationResource? profile = ResolveSkillProfile(cue);
        if (profile is null) return;
        var fx=new GodotProgrammaticSkillFx{Kind=profile.ProgrammaticKind,
            Start=IsometricBattleBoardLayout.GridToScreen(cue.Origin),
            End=IsometricBattleBoardLayout.GridToScreen(cue.Destination),
            Primary=profile.PrimaryColor,Secondary=profile.SecondaryColor,ZIndex=900,Visible=false};
        _transientNodes.Add(fx);
        GetParent().AddChild(fx);
        tween.TweenCallback(Callable.From(()=>{if(GodotObject.IsInstanceValid(fx))fx.Visible=true;}));
        int segments = Math.Max(1, cue.Path.Count);
        for (int index = 0; index < segments; index++)
        {
            float progress = (index + 1f) / segments;
            tween.TweenProperty(fx,"Progress",progress,profile.TravelDuration / segments).SetTrans(Tween.TransitionType.Linear);
            GridPoint cell = cue.Path.Count > index ? cue.Path[index] : cue.Destination;
            foreach (BattlePresentationCue hit in hits.Where(value => value.Origin == cell))
            {
                PlayCue(tween, hit, actors);
                QueueNextNumber(tween, pendingNumbers, hit.ActorId);
            }
        }
        tween.TweenInterval(profile.ImpactDuration);
        tween.TweenCallback(Callable.From(() => ReleaseFx(fx)));
    }

    private void ReleaseFx(Node fx)
    {
        _transientNodes.Remove(fx);
        if (GodotObject.IsInstanceValid(fx)) fx.QueueFree();
    }

    private static void PlayRelease(Tween tween, GodotUnitActor actor, BattlePresentationCue cue,
        float windup, float release, float hold, float distance, bool clearPoseAtRelease = false)
    {
        Vector2 origin = IsometricBattleBoardLayout.GridToScreen(cue.Origin);
        Vector2 target = IsometricBattleBoardLayout.GridToScreen(cue.Destination);
        Vector2 direction = origin.DirectionTo(target);
        tween.TweenInterval(windup);
        if (clearPoseAtRelease)
            tween.TweenCallback(Callable.From(() => actor.SetActionPose(null)));
        tween.TweenProperty(actor, "position", origin + direction * distance, release).SetTrans(Tween.TransitionType.Quad);
        if (hold > 0f) tween.TweenInterval(hold);
    }

    private static void PlayRecover(Tween tween,GodotUnitActor actor,BattlePresentationCue cue,float recover)=>
        tween.TweenProperty(actor,"position",IsometricBattleBoardLayout.GridToScreen(cue.Origin),recover).SetTrans(Tween.TransitionType.Sine);
}
