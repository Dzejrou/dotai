using Godot;

public interface IAggressiveCombatActorAIHost
{
    bool ShouldAttemptAggressiveTargetAcquisition();
    Node2D SelectAggressiveTargetCandidate();
    void ApplyAggressiveTargetCandidate(Node2D target);
}
