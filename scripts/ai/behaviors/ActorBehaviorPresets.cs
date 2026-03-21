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

    public static ActorBehaviorPreset CreateSceneBackedHostileMeleePreset(
        Action<ActorBase> onPursuitStuck = null,
        params IActorBehavior[] extraBehaviors)
    {
        var pursuitStuckCallback = onPursuitStuck ?? (actor => LeashBehavior.ResolveFor(actor)?.BeginReturnHome(actor, true));
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

        return new ActorBehaviorPreset(null, behaviors.ToArray());
    }

    public static ActorBehaviorPreset CreateSceneBackedHostileRangedPreset(
        Action<ActorBase> onPursuitStuck = null,
        params IActorBehavior[] extraBehaviors)
    {
        return CreateSceneBackedHostileMeleePreset(onPursuitStuck, extraBehaviors);
    }

    public static ActorBehaviorPreset CreateHostileMeleePreset(
        float aggroAcquisitionRange,
        NodePath initialTargetPath,
        string actorName,
        float aggroLossRange,
        bool evadeOnAggroLoss,
        bool ignoreDamageWhileEvading,
        bool includeNodeMigratedBehaviors = true,
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
            includeNodeMigratedBehaviors,
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
        bool includeNodeMigratedBehaviors = true,
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
            includeNodeMigratedBehaviors,
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
        bool includeNodeMigratedBehaviors,
        Func<ActorBase, bool> canAttemptAcquisition,
        Func<ActorBase, Node2D, bool> additionalTargetFilter,
        Action<ActorBase> onPursuitStuck,
        params IActorBehavior[] extraBehaviors)
    {
        LeashBehavior leashBehavior = null;
        if (includeNodeMigratedBehaviors)
        {
            leashBehavior = new LeashBehavior(
                aggroLossRange,
                evadeOnAggroLoss,
                ignoreDamageWhileEvading,
                actor => actor.HomePosition,
                actor => actor.IsAtHome());
        }

        var pursuitStuckCallback = onPursuitStuck ?? (actor =>
        {
            if (leashBehavior != null)
            {
                leashBehavior.BeginReturnHome(actor, true);
                return;
            }

            LeashBehavior.ResolveFor(actor)?.BeginReturnHome(actor, true);
        });

        var behaviors = new List<IActorBehavior>
        {
            new PursuitStuckRecoveryBehavior(
                1.0f,
                0.6f,
                8.0f,
                actor => actor.CurrentState == CombatUnitState.PursuingTarget && actor.Target != null,
                pursuitStuckCallback),
        };

        if (leashBehavior != null)
            behaviors.Insert(0, leashBehavior);

        if (includeNodeMigratedBehaviors)
        {
            behaviors.Add(new AcquireHostileTargetBehavior(
                aggroAcquisitionRange,
                initialTargetPath,
                actorName,
                canAttemptAcquisition ?? (actor => !leashBehavior.IsReturningHome),
                additionalTargetFilter));
            behaviors.Add(new TargetCombatBehavior());
        }

        if (extraBehaviors != null)
        {
            foreach (var behavior in extraBehaviors)
            {
                if (behavior != null)
                    behaviors.Add(behavior);
            }
        }

        if (includeNodeMigratedBehaviors)
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
