using Godot;

using System;

[GlobalClass]
public partial class RoomClearUnlocker : Node
{
    [Export]
    public NodePath[] TargetNodePaths { get; set; } = Array.Empty<NodePath>();

    public void UnlockTargets()
    {
        foreach (var targetNodePath in TargetNodePaths)
        {
            if (targetNodePath.IsEmpty)
                continue;

            var targetNode = ResolveTargetNode(targetNodePath);
            if (targetNode == null)
            {
                GD.PushWarning($"{nameof(RoomClearUnlocker)} '{Name}' could not resolve target '{targetNodePath}'.");
                continue;
            }

            if (targetNode is not ILockable lockable)
            {
                GD.PushWarning($"{nameof(RoomClearUnlocker)} '{Name}' target '{targetNodePath}' does not implement {nameof(ILockable)}.");
                continue;
            }

            lockable.UnlockExternal();
        }
    }

    private Node ResolveTargetNode(NodePath targetNodePath)
    {
        var targetNode = GetNodeOrNull<Node>(targetNodePath);
        if (targetNode != null)
            return targetNode;

        if (Owner is Node owner && owner != this)
        {
            targetNode = owner.GetNodeOrNull<Node>(targetNodePath);
            if (targetNode != null)
                return targetNode;
        }

        return GetParent()?.GetNodeOrNull<Node>(targetNodePath);
    }
}
