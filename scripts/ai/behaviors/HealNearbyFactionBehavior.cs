using Godot;

using System;

public sealed class HealNearbyFactionBehavior : IActorBehavior
{
    private readonly Faction _healedFaction;
    private readonly float _acquisitionRange;

    public HealNearbyFactionBehavior(Faction healedFaction, float acquisitionRange)
    {
        _healedFaction = healedFaction ?? throw new ArgumentNullException(nameof(healedFaction));
        _acquisitionRange = Math.Max(0.0f, acquisitionRange);
    }

    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        var actionController = actor.PrimaryActionController;
        if (actionController == null)
            return false;

        var target = ResolveHealingTarget(actor);
        if (target == null)
        {
            intent = new ActorIntent
            {
                ChangeTarget = actor.Target != null,
                Target = null,
                StopMovement = true,
                State = CombatUnitState.Idle,
            };
            return true;
        }

        if (target != actor.Target)
        {
            intent = new ActorIntent
            {
                ChangeTarget = true,
                Target = target,
                StopMovement = true,
                State = CombatUnitState.Idle,
            };
            return true;
        }

        if (actionController.CanStartAction(actor, target))
        {
            intent = ActorIntent.UseAction();
            return true;
        }

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        intent = ActorIntent.Hold(CombatUnitState.Idle);
        return true;
    }

    private Node2D ResolveHealingTarget(Actor actor)
    {
        var currentTarget = actor.Target;
        if (IsValidHealingTarget(actor, currentTarget))
            return currentTarget;

        Node2D closest = null;
        var closestDistance = float.MaxValue;
        foreach (var candidate in TargetingHelper.EnumerateCandidateTargets(actor))
        {
            if (!IsValidHealingTarget(actor, candidate))
                continue;

            var distance = actor.GlobalPosition.DistanceTo(candidate.GlobalPosition);
            if (distance >= closestDistance)
                continue;

            closest = candidate;
            closestDistance = distance;
        }

        return closest;
    }

    private bool IsValidHealingTarget(Actor actor, Node2D target)
    {
        if (!Actor.IsStructurallyValidTarget(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        if (target is not IFactionMember factionMember || !ReferenceEquals(factionMember.Faction, _healedFaction))
            return false;

        if (target is not IHealable healable || !healable.CanReceiveHealing)
            return false;

        return actor.GlobalPosition.DistanceTo(target.GlobalPosition) <= _acquisitionRange;
    }
}
