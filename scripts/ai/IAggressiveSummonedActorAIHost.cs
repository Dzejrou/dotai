using Godot;

public interface IAggressiveSummonedActorAIHost
{
    bool ShouldAttemptAggressiveSummonedTargetAcquisition();
    Node2D SelectAggressiveSummonedTarget();
    void ApplyAggressiveSummonedTarget(Node2D target);
    bool TryHandleAggressiveSummonedNoTarget(double delta);
}
