using Godot;

using System;
using System.Collections.Generic;

public sealed class HealLowestHealthFriendlyBehavior : IActorBehavior
{
    private readonly float _acquisitionRange;

    public HealLowestHealthFriendlyBehavior(float acquisitionRange)
    {
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
            if (actor.Target != null)
            {
                intent = new ActorIntent
                {
                    ChangeTarget = true,
                    Target = null,
                    StopMovement = true,
                    State = CombatUnitState.Idle,
                };
                return true;
            }

            return false;
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
        Node2D bestTarget = null;
        var bestHealth = int.MaxValue;
        var bestDistance = float.MaxValue;

        foreach (var candidate in EnumerateSupportCandidates(actor))
        {
            if (!IsValidHealingTarget(actor, candidate, out var healable))
                continue;

            var currentHealth = healable.CurrentHealth;
            var distance = actor.GlobalPosition.DistanceTo(candidate.GlobalPosition);

            if (currentHealth > bestHealth)
                continue;

            if (currentHealth == bestHealth && distance > bestDistance)
                continue;

            if (currentHealth == bestHealth &&
                Math.Abs(distance - bestDistance) <= 0.0001f &&
                bestTarget != null &&
                candidate.GetInstanceId() >= bestTarget.GetInstanceId())
            {
                continue;
            }

            bestTarget = candidate;
            bestHealth = currentHealth;
            bestDistance = distance;
        }

        return bestTarget;
    }

    private IEnumerable<Node2D> EnumerateSupportCandidates(Actor actor)
    {
        if (actor != null)
            yield return actor;

        foreach (var node in TargetingHelper.EnumerateCandidateTargets(actor))
            yield return node;
    }

    private bool IsValidHealingTarget(Actor actor, Node2D target, out IHealable healable)
    {
        healable = null;

        if (!Actor.IsStructurallyValidTarget(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        if (target is not IFactionMember factionMember ||
            factionMember.Faction == null ||
            actor.Faction == null ||
            !actor.Faction.IsFriendlyTo(factionMember.Faction))
        {
            return false;
        }

        if (target is not IHealable targetHealable || !targetHealable.CanReceiveHealing)
            return false;

        if (actor.GlobalPosition.DistanceTo(target.GlobalPosition) > _acquisitionRange)
            return false;

        healable = targetHealable;
        return true;
    }
}
