using Godot;

using System.Collections.Generic;

// One scene-configurable phase transition for a boss: the HP threshold that arms it,
// the phase the boss ends up in, whether it is invulnerable while it runs, and an
// ordered list of child BossTransitionAction nodes that make up the transition.
//
// A boss declares one BossPhaseTransition per phase change under its
// BossPhaseController. Adding a later transition (e.g. a 25% phase) is just another
// configured node with its own threshold and actions - the controller discovers and
// runs them without any code change.
[GlobalClass]
public partial class BossPhaseTransition : Node
{
    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float HealthThreshold { get; set; } = 0.75f;

    // Phase the boss is in once this transition completes. Explicit so phases read
    // clearly and need not be strictly sequential.
    [Export]
    public int TargetPhase { get; set; } = 2;

    // Invulnerable for this transition's whole duration. Default on; a transition can
    // opt out if a future phase change should be interruptible by damage.
    [Export]
    public bool InvulnerableDuringTransition { get; set; } = true;

    // Combat-log line emitted on completion. Blank uses "<name> enters Phase <n>.".
    [Export]
    public string AnnouncementText { get; set; } = string.Empty;

    // True once this transition has started, so the controller never re-arms it.
    public bool HasTriggered { get; private set; }

    private readonly List<BossTransitionAction> _actions = new();
    private bool _actionsResolved;
    private bool _started;
    private int _activeActionIndex = -1;

    // Done once started and every ordered action has completed (or there are none).
    public bool IsComplete => _started && _activeActionIndex >= _actions.Count;

    public void Begin(Actor actor)
    {
        EnsureActionsResolved();
        HasTriggered = true;
        _started = true;
        _activeActionIndex = -1;
        BeginNextAction(actor);
    }

    public void Update(Actor actor, double delta)
    {
        var action = ActiveAction;
        if (action == null)
            return;

        action.Update(actor, delta);
        if (action.IsComplete)
            BeginNextAction(actor);
    }

    public ActorIntent BuildIntent(Actor actor)
    {
        var action = ActiveAction;
        return action != null
            ? action.BuildIntent(actor)
            : ActorIntent.Hold(CombatUnitState.Transitioning);
    }

    private BossTransitionAction ActiveAction =>
        _activeActionIndex >= 0 && _activeActionIndex < _actions.Count ? _actions[_activeActionIndex] : null;

    private void BeginNextAction(Actor actor)
    {
        _activeActionIndex++;
        ActiveAction?.Begin(actor);
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
}
