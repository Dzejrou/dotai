using Godot;

public interface ICombatActionController
{
    float MinimumRange { get; }
    float PreferredRange { get; }
    void Update(ActorBase actor, double delta);
    bool CanStartAction(ActorBase actor, Node2D target);
    void StartAction(ActorBase actor, Node2D target);
    bool HandleAnimationFinished(ActorBase actor, StringName animationName);
    void Cancel(ActorBase actor);
}
