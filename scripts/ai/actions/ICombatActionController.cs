using Godot;

public interface ICombatActionController
{
    float MinimumRange { get; }
    float PreferredRange { get; }

    // True while this controller owns an in-flight operation (attack swing,
    // cast windup, cast release, ...). The actor suppresses behavior/movement
    // and a composite owner keeps routing animation-finished/cancel here until
    // it clears. This is the timing-independent ownership contract that replaces
    // hard-coding suppression to particular combat states.
    bool IsBusy { get; }

    void Update(Actor actor, double delta);
    bool CanStartAction(Actor actor, Node2D target);
    void StartAction(Actor actor, Node2D target);
    bool HandleAnimationFinished(Actor actor, StringName animationName);
    void Cancel(Actor actor);
}
