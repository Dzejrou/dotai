using Godot;

public sealed class SpellCastRequest
{
    public static SpellCastRequest Empty { get; } = new();

    public Vector2? TargetPosition { get; set; }
    public Node2D TargetNode { get; set; }
    public Vector2? Direction { get; set; }
    public bool OwnRuntimeNodesForChannel { get; set; }

    public SpellCastRequest Clone()
    {
        return new SpellCastRequest
        {
            TargetPosition = TargetPosition,
            TargetNode = TargetNode,
            Direction = Direction,
            OwnRuntimeNodesForChannel = OwnRuntimeNodesForChannel,
        };
    }

    public bool TryResolveTargetNode(out Node2D targetNode)
    {
        if (TargetNode != null &&
            GodotObject.IsInstanceValid(TargetNode) &&
            TargetNode.IsInsideTree())
        {
            targetNode = TargetNode;
            return true;
        }

        targetNode = null;
        return false;
    }

    public bool TryResolveTargetPosition(out Vector2 targetPosition)
    {
        if (TargetPosition.HasValue)
        {
            targetPosition = TargetPosition.Value;
            return true;
        }

        if (TryResolveTargetNode(out var targetNode))
        {
            targetPosition = targetNode.GlobalPosition;
            return true;
        }

        targetPosition = default;
        return false;
    }

    public bool TryResolveDirection(Node2D origin, out Vector2 direction)
    {
        if (origin != null &&
            GodotObject.IsInstanceValid(origin) &&
            TryResolveTargetPosition(out var targetPosition))
        {
            var toTarget = targetPosition - origin.GlobalPosition;
            if (toTarget != Vector2.Zero)
            {
                direction = toTarget.Normalized();
                return true;
            }
        }

        if (Direction.HasValue && Direction.Value != Vector2.Zero)
        {
            direction = Direction.Value.Normalized();
            return true;
        }

        direction = Vector2.Zero;
        return false;
    }
}
