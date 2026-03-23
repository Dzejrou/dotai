using Godot;

using System;
using System.Collections.Generic;

public static class TargetingHelper
{
    private static readonly string[] CandidateFactionGroups =
    {
        CombatGroups.Allies,
        CombatGroups.Enemies,
    };

    public static Node2D FindClosestHostileTarget(Node2D source, Faction sourceFaction, Func<Node, bool> shouldConsiderTarget = null)
    {
        if (source == null || !source.IsInsideTree() || source.GetTree() == null || sourceFaction == null)
            return null;

        Node2D closest = null;
        var closestDistance = float.MaxValue;

        foreach (var targetNode in EnumerateCandidateTargets(source))
        {
            var targetFaction = Factions.ResolveForNode(targetNode);
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

        var seenInstanceIds = new HashSet<ulong>();
        foreach (var targetGroup in CandidateFactionGroups)
        {
            foreach (var node in source.GetTree().GetNodesInGroup(targetGroup))
            {
                if (node == source || !IsValidTargetNode(node, source))
                    continue;

                var targetNode = (Node2D)node;
                if (!seenInstanceIds.Add(targetNode.GetInstanceId()))
                    continue;

                yield return targetNode;
            }
        }
    }

    public static bool CanBeExplicitlyTargetedByFaction(Faction sourceFaction, Node target)
    {
        if (sourceFaction == null || target == null)
            return false;

        var targetFaction = Factions.ResolveForNode(target);
        if (targetFaction == null)
            return false;

        return !ReferenceEquals(sourceFaction, targetFaction);
    }

    public static bool CanProjectileHitTarget(Node source, Node2D target, StringName compatibilityTargetGroup = default)
    {
        if (target == null ||
            !GodotObject.IsInstanceValid(target) ||
            !target.IsInsideTree() ||
            target is not IAttackable ||
            target is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            return false;
        }

        var sourceFaction = Factions.ResolveForNode(source);
        var targetFaction = Factions.ResolveForNode(target);
        if (sourceFaction != null && targetFaction != null)
        {
            if (source is Player)
                return !ReferenceEquals(sourceFaction, targetFaction);

            return sourceFaction.IsHostileTo(targetFaction);
        }

        return !compatibilityTargetGroup.IsEmpty && target.IsInGroup(compatibilityTargetGroup);
    }

    public static Node2D FindClosestTarget(Node2D source, string targetGroup, Func<Node, bool> shouldConsiderTarget = null)
    {
        if (source == null || !source.IsInsideTree() || source.GetTree() == null || string.IsNullOrWhiteSpace(targetGroup))
            return null;

        Node2D closest = null;
        var closestDistance = float.MaxValue;

        foreach (var node in source.GetTree().GetNodesInGroup(targetGroup))
        {
            if (node == source || !IsValidTargetNode(node, source))
                continue;

            if (shouldConsiderTarget != null && !shouldConsiderTarget(node))
                continue;

            var enemyNode = (Node2D)node;
            var distance = (enemyNode.GlobalPosition - source.GlobalPosition).Length();
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = enemyNode;
        }

        return closest;
    }

    private static bool IsValidTargetNode(Node node, Node2D source)
    {
        if (node == null || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree())
            return false;

        if (node.GetParent() == null || node is not Node2D targetNode)
            return false;

        return targetNode.IsInsideTree();
    }
}
