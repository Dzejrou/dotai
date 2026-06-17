using Godot;

using System;

// Reusable boss phase/transition foundation. Attach it under a boss actor at
// "Behaviors/Tier05_Phase/<this>" so the actor collects it as the highest-priority
// behavior (see Actor.ConfigureBehaviors): it then outranks targeting and combat
// while a transition is in flight.
//
// Lifecycle for this slice: the boss is damaged to/below the phase-2 HP threshold,
// becomes invulnerable, navigates back to a transition anchor, then resumes combat
// in phase 2 (which is a placeholder identical to phase 1 for now). Later slices
// specialize phase behavior - phase-2 ranged AI, minion spawners, enrage - by
// overriding OnTransitionStarted / OnPhaseActivated without touching this control
// flow, or by raising additional thresholds.
[GlobalClass]
public partial class BossPhaseController : Node, IActorBehavior, IActorTickBehavior, IActorDamageInterceptor
{
    private enum Phase
    {
        Phase1,
        Transitioning,
        Phase2,
    }

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float Phase2HealthThreshold { get; set; } = 0.75f;

    // Optional fixed world marker the boss walks back to during the transition,
    // resolved relative to the owning actor. When unset, the boss returns to its
    // initial spawn position (Actor.HomePosition).
    [Export]
    public NodePath TransitionAnchorPath { get; set; } = new NodePath();

    [Export]
    public float TransitionSpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public float AnchorArrivalTolerance { get; set; } = 8.0f;

    // Combat-log line emitted once phase 2 begins. Blank uses "<name> enters Phase 2.".
    [Export]
    public string Phase2AnnouncementText { get; set; } = string.Empty;

    private Phase _phase = Phase.Phase1;
    private Node2D _resolvedAnchor;
    private bool _anchorResolved;

    public bool IsTransitioning => _phase == Phase.Transitioning;

    // Invulnerable for the whole transition: from the threshold crossing until the
    // boss reaches the anchor and phase 2 begins.
    public bool IsInvulnerable => _phase == Phase.Transitioning;

    public int CurrentPhase => _phase == Phase.Phase2 ? 2 : 1;

    // Highest-priority behavior: while transitioning, take the actor over with a
    // move-to-anchor intent so normal combat AI/action selection is suppressed.
    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;
        if (actor == null || _phase != Phase.Transitioning)
            return false;

        var anchor = ResolveAnchorPosition(actor);
        if (HasReachedAnchor(actor, anchor))
        {
            // Arrival completes in Update (which runs first each frame); hold here as
            // a one-frame safety so the boss never drifts past the anchor.
            intent = ActorIntent.Hold(CombatUnitState.Transitioning);
            return true;
        }

        intent = ActorIntent.MoveTo(anchor, CombatUnitState.Transitioning, Math.Max(0.0f, TransitionSpeedMultiplier));
        return true;
    }

    public void Update(Actor actor, double delta)
    {
        if (actor == null)
            return;

        switch (_phase)
        {
            case Phase.Phase1:
                // Detect the crossing promptly even if the player stops attacking
                // right at the threshold.
                if (ShouldEnterTransition(actor))
                    BeginTransition(actor);
                break;
            case Phase.Transitioning:
                if (HasReachedAnchor(actor, ResolveAnchorPosition(actor)))
                    CompleteTransition(actor);
                break;
        }
    }

    public bool TryHandleIncomingDamage(Actor actor, Damage damageInfo, out IncomingDamageDecision decision)
    {
        decision = default;
        if (actor == null || _phase == Phase.Phase2)
            return false;

        if (_phase == Phase.Phase1)
        {
            // The hit that first crosses the threshold still lands normally; this
            // interceptor only fires once a prior hit has already dropped the boss
            // to/below the threshold. Begin the transition synchronously here so
            // invulnerability is in effect before this hit (and any further hits in
            // the boss's current action) can push damage through.
            if (!ShouldEnterTransition(actor))
                return false;

            BeginTransition(actor);
        }

        decision = IncomingDamageDecision.Absorb(damageInfo?.Amount ?? 0);
        return true;
    }

    // Extension seams for later slices (kept intentionally minimal for now).
    protected virtual void OnTransitionStarted(Actor actor) { }

    protected virtual void OnPhaseActivated(Actor actor, int phase) { }

    private bool ShouldEnterTransition(Actor actor)
    {
        if (_phase != Phase.Phase1 || actor.IsDead)
            return false;

        var max = actor.MaxHealthValue;
        if (max <= 0)
            return false;

        return (float)actor.CurrentHealth / max <= Phase2HealthThreshold;
    }

    private void BeginTransition(Actor actor)
    {
        if (_phase != Phase.Phase1)
            return;

        _phase = Phase.Transitioning;

        // Cancel any in-flight swing/cast through the existing action-controller API
        // and drop out of the action-owned state so behavior resolution (this
        // controller's move intent) resumes and the boss turns to walk back.
        actor.PrimaryActionController?.Cancel(actor);
        actor.SetState(CombatUnitState.Transitioning);

        OnTransitionStarted(actor);
    }

    private void CompleteTransition(Actor actor)
    {
        if (_phase != Phase.Transitioning)
            return;

        _phase = Phase.Phase2;

        // Clearing the transition state (and IsInvulnerable) lets normal combat
        // resume from the next behavior resolution.
        actor.SetState(actor.Target != null ? CombatUnitState.PursuingTarget : CombatUnitState.Idle);

        CombatLog.System(ResolveAnnouncement(actor));
        OnPhaseActivated(actor, 2);
    }

    private string ResolveAnnouncement(Actor actor)
    {
        if (!string.IsNullOrWhiteSpace(Phase2AnnouncementText))
            return Phase2AnnouncementText;

        var name = CombatLog.ResolveName(actor);
        return string.IsNullOrWhiteSpace(name) ? "Boss enters Phase 2." : $"{name} enters Phase 2.";
    }

    private bool HasReachedAnchor(Actor actor, Vector2 anchor)
    {
        return actor.GlobalPosition.DistanceTo(anchor) <= Math.Max(0.0f, AnchorArrivalTolerance);
    }

    private Vector2 ResolveAnchorPosition(Actor actor)
    {
        var anchor = ResolveAnchorNode(actor);
        return anchor != null && GodotObject.IsInstanceValid(anchor)
            ? anchor.GlobalPosition
            : actor.HomePosition;
    }

    private Node2D ResolveAnchorNode(Actor actor)
    {
        if (_anchorResolved)
            return _resolvedAnchor;

        _anchorResolved = true;
        if (TransitionAnchorPath != null && !TransitionAnchorPath.IsEmpty && actor.HasNode(TransitionAnchorPath))
            _resolvedAnchor = actor.GetNodeOrNull<Node2D>(TransitionAnchorPath);

        return _resolvedAnchor;
    }
}
