using Godot;

using System;

public sealed class AggressiveRangedActorAI : AggressiveCombatActorAI
{
    public override bool TryGetDesiredMovementTarget(Vector2 targetPosition, double delta, out Vector2 desiredMovementTarget)
    {
        desiredMovementTarget = Vector2.Zero;

        if (Actor is not IAggressiveRangedActorAIHost rangedHost)
            return false;

        var toTarget = targetPosition - Actor.GlobalPosition;
        var distance = toTarget.Length();
        var resolvedMinimumRange = Math.Max(0.0f, rangedHost.MinimumRange);
        var resolvedPreferredRange = Math.Max(resolvedMinimumRange, rangedHost.PreferredRange);

        if (toTarget == Vector2.Zero || (distance >= resolvedMinimumRange && distance <= resolvedPreferredRange))
        {
            desiredMovementTarget = Actor.GlobalPosition;
            return true;
        }

        if (distance > resolvedPreferredRange)
        {
            desiredMovementTarget = targetPosition;
            return true;
        }

        desiredMovementTarget = Actor.GlobalPosition + -toTarget.Normalized() * resolvedPreferredRange;
        return true;
    }
}
