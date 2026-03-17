using Godot;

using System;

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

        foreach (var targetGroup in CandidateFactionGroups)
        {
            foreach (var node in source.GetTree().GetNodesInGroup(targetGroup))
            {
                if (node == source || !IsValidTargetNode(node, source))
                    continue;

                var targetFaction = Factions.ResolveForNode(node);
                if (!sourceFaction.IsHostileTo(targetFaction))
                    continue;

                if (shouldConsiderTarget != null && !shouldConsiderTarget(node))
                    continue;

                var targetNode = (Node2D)node;
                var distance = (targetNode.GlobalPosition - source.GlobalPosition).Length();
                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                closest = targetNode;
            }
        }

        return closest;
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
