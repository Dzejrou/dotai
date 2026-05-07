using Godot;

using System.Collections.Generic;

public sealed class SpellCastResult
{
    private readonly List<Node> _channelOwnedNodes = new();

    public IReadOnlyList<Node> ChannelOwnedNodes => _channelOwnedNodes;

    public void AddChannelOwnedNode(Node node)
    {
        if (node == null || !GodotObject.IsInstanceValid(node) || _channelOwnedNodes.Contains(node))
            return;

        _channelOwnedNodes.Add(node);
    }
}
