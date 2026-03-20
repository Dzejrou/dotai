using Godot;

using System;
using System.Collections.Generic;

public readonly struct SummonBehaviorPreset
{
    public SummonBehaviorPreset(FollowSummonerBehavior followSummonerBehavior, IActorBehavior[] behaviors)
    {
        FollowSummonerBehavior = followSummonerBehavior;
        Behaviors = behaviors ?? Array.Empty<IActorBehavior>();
    }

    public FollowSummonerBehavior FollowSummonerBehavior { get; }
    public IActorBehavior[] Behaviors { get; }
}

public static class SummonBehaviorPresets
{
    public static SummonBehaviorPreset CreateSummonedMeleePreset(
        Func<ActorBase, Node2D> summonerGetter,
        Func<ActorBase, Vector2> anchorGetter,
        float leashDistance,
        float leashReturnDistance,
        float idleAnchorTolerance,
        float leashCatchupSpeedMultiplier,
        bool followWhenIdle,
        Func<ActorBase, Node2D> commandedTargetGetter = null,
        Func<ActorBase, bool> canAttemptAcquisition = null,
        Func<ActorBase, Node2D, bool> additionalTargetFilter = null,
        Func<ActorBase, Node2D, bool> shouldDropTarget = null,
        Func<ActorBase, Vector2> teleportDestinationGetter = null,
        float teleportRecoveryTimeout = 0.0f,
        Func<ActorBase, bool> stuckCondition = null,
        params IActorBehavior[] extraBehaviors)
    {
        var followSummonerBehavior = new FollowSummonerBehavior(
            summonerGetter,
            anchorGetter,
            leashDistance,
            leashReturnDistance,
            idleAnchorTolerance,
            leashCatchupSpeedMultiplier,
            followWhenIdle,
            teleportDestinationGetter: teleportDestinationGetter,
            teleportRecoveryTimeout: teleportRecoveryTimeout);

        var behaviors = new List<IActorBehavior>();
        if (commandedTargetGetter != null)
            behaviors.Add(new CommandedTargetBehavior(commandedTargetGetter));

        behaviors.Add(new AcquireHostileTargetBehavior(
            float.MaxValue,
            canAttemptAcquisition: canAttemptAcquisition ?? (actor => !followSummonerBehavior.IsRecovering),
            additionalTargetFilter: additionalTargetFilter));
        behaviors.Add(new PursuitStuckRecoveryBehavior(
            1.0f,
            0.6f,
            8.0f,
            stuckCondition ?? (actor =>
                actor.CurrentState == CombatUnitState.PursuingTarget ||
                actor.CurrentState == CombatUnitState.FollowingOwner ||
                actor.CurrentState == CombatUnitState.Leashing),
            actor =>
            {
                actor.ClearTarget();
                followSummonerBehavior.BeginRecovery();
            }));
        behaviors.Add(new TargetCombatBehavior(shouldDropTarget));

        if (extraBehaviors != null)
        {
            foreach (var behavior in extraBehaviors)
            {
                if (behavior != null)
                    behaviors.Add(behavior);
            }
        }

        behaviors.Add(followSummonerBehavior);
        return new SummonBehaviorPreset(followSummonerBehavior, behaviors.ToArray());
    }
}
