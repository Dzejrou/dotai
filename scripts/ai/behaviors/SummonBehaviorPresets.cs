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
        FollowSummonerBehavior followSummonerBehavior,
        Func<ActorBase, bool> stuckCondition = null,
        params IActorBehavior[] extraBehaviors)
    {
        if (followSummonerBehavior == null)
            throw new ArgumentNullException(nameof(followSummonerBehavior));

        var behaviors = new List<IActorBehavior>();
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

        if (extraBehaviors != null)
        {
            foreach (var behavior in extraBehaviors)
            {
                if (behavior != null)
                    behaviors.Add(behavior);
            }
        }

        return new SummonBehaviorPreset(followSummonerBehavior, behaviors.ToArray());
    }

    public static Node2D GetOwnerCombatAssistTarget(
        ActorBase actor,
        Func<ActorBase, Node2D, bool> targetValidator)
    {
        if (actor == null || targetValidator == null)
            return null;

        if (!ReferenceEquals(actor.Faction, Factions.Allies))
            return null;

        var summonState = SummonState.ResolveFor(actor);
        var ownerCombat = CombatState.ResolveFor(summonState?.SummonerNode);
        if (ownerCombat == null)
            return null;
        if (!ownerCombat.InCombat)
            return null;

        var ownerCombatTarget = ownerCombat.Target;
        return targetValidator(actor, ownerCombatTarget) ? ownerCombatTarget : null;
    }

    public static Vector2 GetFormationAnchor(
        ActorBase actor,
        float horizontalOffset,
        float verticalOffset,
        int maxSlots = 4)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));

        var summonState = SummonState.ResolveFor(actor);
        if (summonState == null)
            return actor.GlobalPosition;

        var summonerNode = summonState.SummonerNode;
        if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode))
            return actor.GlobalPosition;

        var summonSlot = GetSummonSlotIndex(actor, summonState, maxSlots);
        return summonerNode.GlobalPosition + GetFormationOffset(summonSlot, horizontalOffset, verticalOffset);
    }

    private static int GetSummonSlotIndex(ActorBase actor, SummonState summonState, int maxSlots)
    {
        var summonerNode = summonState.SummonerNode;
        if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode))
            return 0;

        var parent = actor.GetParent();
        if (parent == null)
            return 0;

        var clampedMaxSlots = Math.Max(1, maxSlots);
        var slot = 0;
        foreach (var node in parent.GetChildren())
        {
            if (node is not ActorBase siblingActor)
                continue;

            var siblingSummonState = SummonState.ResolveFor(siblingActor);
            if (siblingSummonState == null || !ReferenceEquals(siblingSummonState.Summoner, summonState.Summoner))
                continue;

            if (node == actor)
                return Math.Min(slot, clampedMaxSlots - 1);

            slot++;
            if (slot >= clampedMaxSlots)
                return clampedMaxSlots - 1;
        }

        return 0;
    }

    private static Vector2 GetFormationOffset(int slot, float horizontalOffset, float verticalOffset)
    {
        return slot switch
        {
            0 => new Vector2(-horizontalOffset, -verticalOffset),
            1 => new Vector2(horizontalOffset, -verticalOffset),
            2 => new Vector2(-horizontalOffset, verticalOffset),
            3 => new Vector2(horizontalOffset, verticalOffset),
            _ => Vector2.Zero,
        };
    }
}
