using Godot;

using System.Collections.Generic;

// Reusable boss phase/transition foundation. Attach it under a boss actor at
// "Behaviors/Tier05_Phase/<this>" so the actor collects it as the highest-priority
// behavior (see Actor.ConfigureBehaviors): it then outranks targeting and combat
// while a transition is in flight.
//
// This controller owns threshold detection, phase state, invulnerability and
// sequencing. The transition itself is described by ordered child
// BossTransitionAction nodes (move to anchor, channel a spell, ...): the controller
// runs them in order and only activates phase 2 once every action has completed.
// Adding phases or different action sequences is a scene-level change; the control
// flow here does not need to change.
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

    // Invulnerable for the entire transition sequence (movement and channel). On by
    // default so the Demon boss cannot be damaged through any part of its transition.
    [Export]
    public bool InvulnerableDuringTransition { get; set; } = true;

    // Combat-log line emitted once phase 2 begins. Blank uses "<name> enters Phase 2.".
    [Export]
    public string Phase2AnnouncementText { get; set; } = string.Empty;

    private Phase _phase = Phase.Phase1;
    private readonly List<BossTransitionAction> _actions = new();
    private bool _actionsResolved;
    private int _activeActionIndex = -1;

    public bool IsTransitioning => _phase == Phase.Transitioning;

    // Invulnerable for the whole transition: from the threshold crossing until the
    // final action completes and phase 2 begins.
    public bool IsInvulnerable => _phase == Phase.Transitioning && InvulnerableDuringTransition;

    public int CurrentPhase => _phase == Phase.Phase2 ? 2 : 1;

    // Highest-priority behavior: while transitioning, the active action takes the
    // actor over so normal combat AI/action selection is suppressed.
    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;
        if (actor == null || _phase != Phase.Transitioning)
            return false;

        var action = ActiveAction;
        if (action == null)
            return false;

        intent = action.BuildIntent(actor);
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
                AdvanceTransition(actor, delta);
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

        if (!InvulnerableDuringTransition)
            return false;

        decision = IncomingDamageDecision.Absorb(damageInfo?.Amount ?? 0);
        return true;
    }

    // Extension seams for later slices (kept intentionally minimal for now).
    protected virtual void OnTransitionStarted(Actor actor) { }

    protected virtual void OnPhaseActivated(Actor actor, int phase) { }

    private BossTransitionAction ActiveAction =>
        _activeActionIndex >= 0 && _activeActionIndex < _actions.Count ? _actions[_activeActionIndex] : null;

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
        EnsureActionsResolved();

        // Cancel any in-flight swing/cast through the existing action-controller API
        // and drop out of the action-owned state so behavior resolution (the active
        // transition action's intent) resumes.
        actor.PrimaryActionController?.Cancel(actor);
        actor.SetState(CombatUnitState.Transitioning);

        OnTransitionStarted(actor);

        _activeActionIndex = -1;
        if (!TryBeginNextAction(actor))
            CompleteTransition(actor); // no actions configured: go straight to phase 2
    }

    private void AdvanceTransition(Actor actor, double delta)
    {
        var action = ActiveAction;
        if (action == null)
        {
            CompleteTransition(actor);
            return;
        }

        action.Update(actor, delta);
        if (action.IsComplete && !TryBeginNextAction(actor))
            CompleteTransition(actor);
    }

    private bool TryBeginNextAction(Actor actor)
    {
        _activeActionIndex++;
        var action = ActiveAction;
        if (action == null)
            return false;

        action.Begin(actor);
        return true;
    }

    private void CompleteTransition(Actor actor)
    {
        if (_phase != Phase.Transitioning)
            return;

        _phase = Phase.Phase2;
        _activeActionIndex = -1;

        // Clearing the transition state (and IsInvulnerable) lets normal combat
        // resume from the next behavior resolution.
        actor.SetState(actor.Target != null ? CombatUnitState.PursuingTarget : CombatUnitState.Idle);

        CombatLog.System(ResolveAnnouncement(actor));
        OnPhaseActivated(actor, 2);
    }

    private void EnsureActionsResolved()
    {
        if (_actionsResolved)
            return;

        _actionsResolved = true;
        foreach (var child in GetChildren())
        {
            if (child is BossTransitionAction action)
                _actions.Add(action);
        }
    }

    private string ResolveAnnouncement(Actor actor)
    {
        if (!string.IsNullOrWhiteSpace(Phase2AnnouncementText))
            return Phase2AnnouncementText;

        var name = CombatLog.ResolveName(actor);
        return string.IsNullOrWhiteSpace(name) ? "Boss enters Phase 2." : $"{name} enters Phase 2.";
    }
}
