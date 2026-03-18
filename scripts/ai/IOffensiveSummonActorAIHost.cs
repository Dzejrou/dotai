using Godot;

public interface IOffensiveSummonActorAIHost
{
    bool ShouldAttemptOffensiveSummonTargetAcquisition();
    Node2D GetCommandedOffensiveSummonTarget();
    Node2D SelectAutonomousOffensiveSummonTarget();
    void ApplyOffensiveSummonTarget(Node2D target);
    bool TryHandleOffensiveSummonNoTarget(double delta);
}
