using System;
using System.Collections.Generic;

public static class ActorBehaviorPresets
{
    public const float StandardHostileReturnHomeRegenerationFractionPerSecond = 0.1f;
    public const float StandardHostileIdleRegenerationFractionPerSecond = 0.01f;
    public const float StandardHostileIdleRegenerationIntervalSeconds = 5.0f;

    public static IActorBehavior[] CreateSceneBackedHostileMeleePreset(
        Action<Actor> onPursuitStuck = null,
        params IActorBehavior[] extraBehaviors)
    {
        var pursuitStuckCallback = onPursuitStuck ?? (actor => ReturnHomeBehavior.ResolveFor(actor)?.BeginReturnHome(actor));
        var behaviors = new List<IActorBehavior>
        {
            new PursuitStuckRecoveryBehavior(
                1.0f,
                0.6f,
                8.0f,
                actor => actor.CurrentState == CombatUnitState.PursuingTarget && actor.Target != null,
                pursuitStuckCallback),
        };

        if (extraBehaviors != null)
        {
            foreach (var behavior in extraBehaviors)
            {
                if (behavior != null)
                    behaviors.Add(behavior);
            }
        }

        behaviors.Add(CreateStandardHostileReturnHomeRegenerationBehavior());
        behaviors.Add(CreateStandardHostileIdleRegenerationBehavior());

        return behaviors.ToArray();
    }

    public static IActorBehavior[] CreateSceneBackedHostileRangedPreset(
        Action<Actor> onPursuitStuck = null,
        params IActorBehavior[] extraBehaviors)
    {
        return CreateSceneBackedHostileMeleePreset(onPursuitStuck, extraBehaviors);
    }

    public static ReturnHomeRegenerationBehavior CreateStandardHostileReturnHomeRegenerationBehavior()
    {
        return new ReturnHomeRegenerationBehavior(StandardHostileReturnHomeRegenerationFractionPerSecond);
    }

    public static IdleRegenerationBehavior CreateStandardHostileIdleRegenerationBehavior()
    {
        return new IdleRegenerationBehavior(
            StandardHostileIdleRegenerationFractionPerSecond,
            StandardHostileIdleRegenerationIntervalSeconds);
    }
}
