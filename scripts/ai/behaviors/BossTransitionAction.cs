using Godot;

// One ordered step in a boss phase transition. BossPhaseController runs its child
// actions in sequence: each is begun, updated every frame for its side effects, and
// asked for the actor intent to execute, until it reports completion. This contract
// lets a boss describe its transition as editor-configurable steps (move to anchor,
// channel a spell, ...) and lets later slices add new steps or sequences without
// changing the controller.
[GlobalClass]
public abstract partial class BossTransitionAction : Node
{
    // Set false by Begin; subclasses flip it from Update once their step finishes.
    public bool IsComplete { get; protected set; }

    public void Begin(Actor actor)
    {
        IsComplete = false;
        OnBegin(actor);
    }

    public void Cancel(Actor actor)
    {
        IsComplete = false;
        OnCancel(actor);
    }

    // Per-frame side effects (timers, spell ticks, animation, completion check). Runs
    // from the controller's tick while this action is the active step.
    public virtual void Update(Actor actor, double delta) { }

    // Actor control for this frame (movement, hold, facing). Pure: no side effects, so
    // it can be queried during behavior resolution after Update has already run.
    public abstract ActorIntent BuildIntent(Actor actor);

    protected abstract void OnBegin(Actor actor);

    protected virtual void OnCancel(Actor actor) { }
}
