using Godot;

public sealed class PlayerTargetingState
{
    public Node2D SoftTarget { get; private set; }
    public Node2D TabTarget { get; private set; }
    public Node2D ActiveTarget { get; private set; }

    public void SetSoftTarget(Node2D target)
    {
        SoftTarget = target;
        RefreshActiveTarget();
    }

    public void ClearSoftTarget()
    {
        SetSoftTarget(null);
    }

    public void SetTabTarget(Node2D target)
    {
        TabTarget = target;
        RefreshActiveTarget();
    }

    public void ClearTabTarget()
    {
        SetTabTarget(null);
    }

    public void ClearAllTargets()
    {
        SoftTarget = null;
        TabTarget = null;
        RefreshActiveTarget();
    }

    public void RefreshActiveTarget()
    {
        if (TabTarget != null)
        {
            ActiveTarget = TabTarget;
            return;
        }

        if (SoftTarget != null)
        {
            ActiveTarget = SoftTarget;
            return;
        }

        ActiveTarget = null;
    }
}
