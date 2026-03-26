using Godot;

using System;
using System.Collections.Generic;

public static class TargetingHelper
{
    public static Node2D FindClosestHostileTarget(Node2D source, Faction sourceFaction, Func<Node, bool> shouldConsiderTarget = null)
    {
        if (source == null || !source.IsInsideTree() || source.GetTree() == null || sourceFaction == null)
            return null;

        Node2D closest = null;
        var closestDistance = float.MaxValue;

        foreach (var targetNode in EnumerateCandidateTargets(source))
        {
            var targetFaction = targetNode is IFactionMember factionMember ? factionMember.Faction : null;
            if (!sourceFaction.IsHostileTo(targetFaction))
                continue;

            if (shouldConsiderTarget != null && !shouldConsiderTarget(targetNode))
                continue;

            var distance = (targetNode.GlobalPosition - source.GlobalPosition).Length();
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = targetNode;
        }

        return closest;
    }

    public static IEnumerable<Node2D> EnumerateCandidateTargets(Node2D source)
    {
        if (source == null || !source.IsInsideTree() || source.GetTree() == null)
            yield break;

        foreach (var node in source.GetTree().GetNodesInGroup(CombatGroups.Actors))
        {
            if (node == source || !IsValidTargetNode(node))
                continue;

            yield return (Node2D)node;
        }
    }

    public static bool CanBeExplicitlyTargetedByFaction(Faction sourceFaction, Node target)
    {
        if (sourceFaction == null || target == null)
            return false;

        var targetFaction = target is IFactionMember factionMember ? factionMember.Faction : null;
        if (targetFaction == null)
            return false;

        return !ReferenceEquals(sourceFaction, targetFaction);
    }

    public static bool CanProjectileHitTarget(Node source, Node2D target)
    {
        if (target == null ||
            !GodotObject.IsInstanceValid(target) ||
            !target.IsInsideTree() ||
            source is not IFactionMember sourceFactionMember ||
            sourceFactionMember.Faction == null ||
            target is not IFactionMember targetFactionMember ||
            targetFactionMember.Faction == null ||
            target is not IAttackable ||
            target is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            return false;
        }

        var targetFactionState = FactionState.ResolveFor(target);
        if (targetFactionState != null)
            return targetFactionState.CanBeDamagedBy(sourceFactionMember.Faction);

        if (ReferenceEquals(sourceFactionMember.Faction, targetFactionMember.Faction))
            return false;

        if (ReferenceEquals(targetFactionMember.Faction, Factions.Neutral))
            return true;

        return sourceFactionMember.Faction.IsHostileTo(targetFactionMember.Faction) ||
               targetFactionMember.Faction.IsHostileTo(sourceFactionMember.Faction);
    }

    private static bool IsValidTargetNode(Node node)
    {
        return node != null &&
               GodotObject.IsInstanceValid(node) &&
               node.IsInsideTree() &&
               node.GetParent() != null &&
               node is Node2D targetNode &&
               node is IFactionMember &&
               node is IAttackable &&
               node is ITargetable targetable &&
               targetable.CanBeTargeted &&
               targetNode.IsInsideTree();
    }
}
