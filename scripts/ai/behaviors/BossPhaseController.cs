using Godot;

using System.Collections.Generic;

// Reusable boss phase/transition foundation. Attach it under a boss actor at
// "Behaviors/Tier05_Phase/<this>" so the actor collects it as the highest-priority
// behavior (see Actor.ConfigureBehaviors): it then outranks targeting and combat
// while a transition is in flight.
//
// The controller owns phase state, invulnerability and threshold evaluation, but it
// is not hardcoded to any particular phases or sequence. Each phase change is a child
// BossPhaseTransition node carrying its own HP threshold, destination phase,
// invulnerability setting and ordered BossTransitionAction children. The controller
// arms the highest crossed, not-yet-triggered transition, runs its actions, then
// advances to that transition's destination phase. Adding a later transition (e.g. a
// 25% phase with a different sequence) is purely a scene change.
[GlobalClass]
public partial class BossPhaseController : Node, IActorBehavior, IActorTickBehavior, IActorDamageInterceptor, IActorPhaseState
{
    // Fired exactly once when a configured transition completes, after CurrentPhase has
    // already advanced to the destination phase. Never fired for the initial phase 1 on
    // spawn. Room content (e.g. a future encounter controller) can listen to drive
    // phase-entry effects without the boss scene referencing them.
    [Signal]
    public delegate void PhaseEnteredEventHandler(int phase);

    private readonly List<BossPhaseTransition> _transitions = new();
    private bool _transitionsResolved;
    private BossPhaseTransition _active;
    private int _currentPhase = 1;

    public bool IsTransitioning => _active != null;

    // Invulnerable for the whole active transition when that transition asks for it.
    public bool IsInvulnerable => _active != null && _active.InvulnerableDuringTransition;

    public int CurrentPhase => _currentPhase;

    // Highest-priority behavior: while a transition runs, it takes the actor over so
    // normal combat AI/action selection is suppressed.
    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;
        if (actor == null || _active == null)
            return false;

        intent = _active.BuildIntent(actor);
        return true;
    }

    public void Update(Actor actor, double delta)
    {
        if (actor == null)
            return;

        if (_active == null)
        {
            // Detect a crossing promptly even if the player stops attacking right at
            // the threshold.
            var next = SelectTransition(actor);
            if (next != null)
                BeginTransition(actor, next);
            return;
        }

        _active.Update(actor, delta);
        if (_active.IsComplete)
            CompleteTransition(actor);
    }

    public bool TryHandleIncomingDamage(Actor actor, Damage damageInfo, out IncomingDamageDecision decision)
    {
        decision = default;
        if (actor == null)
            return false;

        if (_active == null)
        {
            // The hit that first crosses a threshold still lands normally; this only
            // fires once a prior hit has already dropped the boss to/below it. Arming
            // the transition synchronously here means invulnerability is in effect
            // before this hit (and any further hits in the current action) land.
            var next = SelectTransition(actor);
            if (next == null)
                return false;

            BeginTransition(actor, next);
        }

        if (_active == null || !_active.InvulnerableDuringTransition)
            return false;

        decision = IncomingDamageDecision.Absorb(damageInfo?.Amount ?? 0);
        return true;
    }

    // Picks the highest crossed, not-yet-triggered transition so earlier phases (e.g.
    // 75%) always run before later ones (e.g. 25%), even if a single hit crosses both.
    private BossPhaseTransition SelectTransition(Actor actor)
    {
        EnsureTransitionsResolved();
        if (actor.IsDead)
            return null;

        var max = actor.MaxHealthValue;
        if (max <= 0)
            return null;

        var fraction = (float)actor.CurrentHealth / max;
        BossPhaseTransition best = null;
        foreach (var transition in _transitions)
        {
            if (transition.HasTriggered || fraction > transition.HealthThreshold)
                continue;

            if (best == null || transition.HealthThreshold > best.HealthThreshold)
                best = transition;
        }

        return best;
    }

    private void BeginTransition(Actor actor, BossPhaseTransition transition)
    {
        _active = transition;

        // Cancel any in-flight swing/cast through the existing action-controller API
        // and drop out of the action-owned state so behavior resolution (the active
        // transition's intent) resumes.
        actor.PrimaryActionController?.Cancel(actor);
        actor.SetState(CombatUnitState.Transitioning);

        transition.Begin(actor);

        // A transition with no actions just changes phase.
        if (transition.IsComplete)
            CompleteTransition(actor);
    }

    private void CompleteTransition(Actor actor)
    {
        var transition = _active;
        if (transition == null)
            return;

        _active = null;
        _currentPhase = transition.TargetPhase;

        // Clearing the active transition (and IsInvulnerable) lets normal combat
        // resume from the next behavior resolution.
        actor.SetState(actor.Target != null ? CombatUnitState.PursuingTarget : CombatUnitState.Idle);

        CombatLog.System(ResolveAnnouncement(actor, transition));

        // Emit after CurrentPhase is updated so listeners observe the new phase.
        EmitSignal(SignalName.PhaseEntered, _currentPhase);
    }

    private void EnsureTransitionsResolved()
    {
        if (_transitionsResolved)
            return;

        _transitionsResolved = true;
        foreach (var child in GetChildren())
        {
            if (child is BossPhaseTransition transition)
                _transitions.Add(transition);
        }
    }

    private static string ResolveAnnouncement(Actor actor, BossPhaseTransition transition)
    {
        if (!string.IsNullOrWhiteSpace(transition.AnnouncementText))
            return transition.AnnouncementText;

        var name = CombatLog.ResolveName(actor);
        return string.IsNullOrWhiteSpace(name)
            ? $"Boss enters Phase {transition.TargetPhase}."
            : $"{name} enters Phase {transition.TargetPhase}.";
    }
}
