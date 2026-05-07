using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class TEST_AoE_respawn : Spell
{
    public override bool ShouldFaceCastRequest => false;

    public override bool TryCast(ISpellCaster caster, SpellCastRequest request)
    {
        if (!CanCast(caster, request))
            return false;

        var world = FindWorld(caster?.SpellOrigin) ?? FindWorld(this);
        if (world?.ActiveRoom == null)
        {
            GD.PushWarning($"{GetPath()}: {nameof(TEST_AoE_respawn)} could not resolve an active room.");
            return false;
        }

        var contentNodes = FindContentNodes(world.ActiveRoom);
        if (contentNodes.Count == 0)
        {
            GD.PushWarning($"{GetPath()}: {nameof(TEST_AoE_respawn)} found no {nameof(Content)} nodes under active room '{world.ActiveRoom.Name}'.");
            return false;
        }

        if (!TrySpendCastMana(caster))
            return false;

        foreach (var content in contentNodes)
            content.Respawn();

        StartCooldown();
        return true;
    }

    private static List<Content> FindContentNodes(Node root)
    {
        var contentNodes = new List<Content>();
        if (root == null)
            return contentNodes;

        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current is Content content)
                contentNodes.Add(content);

            foreach (Node child in current.GetChildren())
                stack.Push(child);
        }

        return contentNodes;
    }

    private static World FindWorld(Node node)
    {
        var current = node;
        while (current != null)
        {
            if (current is World world)
                return world;

            current = current.GetParent();
        }

        return null;
    }
}
