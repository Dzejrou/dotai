using Godot;

using System;
using System.Collections.Generic;

public readonly struct ActorBehaviorPreset
{
    public ActorBehaviorPreset(LeashBehavior leashBehavior, IActorBehavior[] behaviors)
    {
        LeashBehavior = leashBehavior;
        Behaviors = behaviors ?? Array.Empty<IActorBehavior>();
    }

    public LeashBehavior LeashBehavior { get; }
    public IActorBehavior[] Behaviors { get; }
}

public static class ActorBehaviorPresets
{
    public const float StandardHostileReturnHomeRegenerationFractionPerSecond = 0.1f;
    public const float StandardHostileIdleRegenerationFractionPerSecond = 0.01f;
    public const float StandardHostileIdleRegenerationIntervalSeconds = 5.0f;

    public static ActorBehaviorPreset CreateHostileMeleePreset(
        float aggroAcquisitionRange,
        NodePath initialTargetPath,
        string actorName,
        float aggroLossRange,
        bool evadeOnAggroLoss,
        bool ignoreDamageWhileEvading,
        Func<ActorBase, bool> canAttemptAcquisition = null,
        Func<ActorBase, Node2D, bool> additionalTargetFilter = null,
        Action<ActorBase> onPursuitStuck = null,
        params IActorBehavior[] extraBehaviors)
    {
        return CreateHostileCombatPreset(
            aggroAcquisitionRange,
            initialTargetPath,
            actorName,
            aggroLossRange,
            evadeOnAggroLoss,
            ignoreDamageWhileEvading,
            canAttemptAcquisition,
            additionalTargetFilter,
            onPursuitStuck,
            extraBehaviors);
    }

    public static ActorBehaviorPreset CreateHostileRangedPreset(
        float aggroAcquisitionRange,
        NodePath initialTargetPath,
        string actorName,
        float aggroLossRange,
        bool evadeOnAggroLoss,
        bool ignoreDamageWhileEvading,
        Func<ActorBase, bool> canAttemptAcquisition = null,
        Func<ActorBase, Node2D, bool> additionalTargetFilter = null,
        Action<ActorBase> onPursuitStuck = null,
        params IActorBehavior[] extraBehaviors)
    {
        return CreateHostileCombatPreset(
            aggroAcquisitionRange,
            initialTargetPath,
            actorName,
            aggroLossRange,
            evadeOnAggroLoss,
            ignoreDamageWhileEvading,
            canAttemptAcquisition,
            additionalTargetFilter,
            onPursuitStuck,
            extraBehaviors);
    }

    private static ActorBehaviorPreset CreateHostileCombatPreset(
        float aggroAcquisitionRange,
        NodePath initialTargetPath,
        string actorName,
        float aggroLossRange,
        bool evadeOnAggroLoss,
        bool ignoreDamageWhileEvading,
        Func<ActorBase, bool> canAttemptAcquisition,
        Func<ActorBase, Node2D, bool> additionalTargetFilter,
        Action<ActorBase> onPursuitStuck,
        params IActorBehavior[] extraBehaviors)
    {
        var leashBehavior = new LeashBehavior(
            aggroLossRange,
            evadeOnAggroLoss,
            ignoreDamageWhileEvading,
            actor => actor.HomePosition,
            actor => actor.IsAtHome());

        var pursuitStuckCallback = onPursuitStuck ?? (actor => leashBehavior.BeginReturnHome(actor, true));
        var behaviors = new List<IActorBehavior>
        {
            leashBehavior,
            new PursuitStuckRecoveryBehavior(
                1.0f,
                0.6f,
                8.0f,
                actor => actor.CurrentState == CombatUnitState.PursuingTarget && actor.CurrentTarget != null,
                pursuitStuckCallback),
            new AcquireHostileTargetBehavior(
                aggroAcquisitionRange,
                initialTargetPath,
                actorName,
                canAttemptAcquisition ?? (actor => !leashBehavior.IsReturningHome),
                additionalTargetFilter),
            new TargetCombatBehavior(),
        };

        if (extraBehaviors != null)
        {
            foreach (var behavior in extraBehaviors)
            {
                if (behavior != null)
                    behaviors.Add(behavior);
            }
        }

        behaviors.Add(new ReturnHomeBehavior(actor => actor.HomePosition, actor => actor.IsAtHome()));
        behaviors.Add(CreateStandardHostileReturnHomeRegenerationBehavior());
        behaviors.Add(CreateStandardHostileIdleRegenerationBehavior());

        return new ActorBehaviorPreset(leashBehavior, behaviors.ToArray());
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
