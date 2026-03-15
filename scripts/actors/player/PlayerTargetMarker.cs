using Godot;

public partial class PlayerTargetMarker : Node2D
{
    private static readonly Color MarkerColor = new Color(1.0f, 0.92f, 0.35f, 0.8f);
    private const float MarkerWidth = 2.0f;

    private Node2D _target;

    public override void _Ready()
    {
        TopLevel = true;
        ZIndex = 50;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_target == null || !GodotObject.IsInstanceValid(_target) || !_target.IsInsideTree())
        {
            ClearTarget();
            return;
        }

        GlobalPosition = _target.GlobalPosition + new Vector2(0.0f, -8.0f);
    }

    public override void _Draw()
    {
        var leftTop = new Vector2(-16.0f, -12.0f);
        var leftMid = new Vector2(-22.0f, -12.0f);
        var leftBottom = new Vector2(-22.0f, 12.0f);
        var leftEnd = new Vector2(-16.0f, 12.0f);

        var rightTop = new Vector2(16.0f, -12.0f);
        var rightMid = new Vector2(22.0f, -12.0f);
        var rightBottom = new Vector2(22.0f, 12.0f);
        var rightEnd = new Vector2(16.0f, 12.0f);

        DrawPolyline([leftTop, leftMid, leftBottom, leftEnd], MarkerColor, MarkerWidth);
        DrawPolyline([rightTop, rightMid, rightBottom, rightEnd], MarkerColor, MarkerWidth);
    }

    public void SetTarget(Node2D target)
    {
        _target = target;
        Visible = _target != null;

        if (Visible)
            GlobalPosition = _target.GlobalPosition + new Vector2(0.0f, -8.0f);
    }

    public void ClearTarget()
    {
        _target = null;
        Visible = false;
    }
}
