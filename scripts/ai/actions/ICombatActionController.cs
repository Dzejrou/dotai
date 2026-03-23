using Godot;

public interface ICombatActionController
{
    float MinimumRange { get; }
    float PreferredRange { get; }
    void Update(Actor actor, double delta);
    bool CanStartAction(Actor actor, Node2D target);
    void StartAction(Actor actor, Node2D target);
    bool HandleAnimationFinished(Actor actor, StringName animationName);
    void Cancel(Actor actor);
}
