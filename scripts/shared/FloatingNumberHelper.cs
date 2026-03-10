using Godot;

public static class FloatingNumberHelper
{
    public static void ShowFloatingNumber(Node2D owner, string text, Color color, float riseDistance = 18.0f, float duration = 0.6f, int fontSize = 20)
    {
        if (owner == null || string.IsNullOrWhiteSpace(text))
            return;

        var popup = new Node2D
        {
            GlobalPosition = owner.GlobalPosition + new Vector2(0, -16.0f)
        };

        var label = new Label
        {
            Text = text,
            Modulate = color,
            ZIndex = 4
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        popup.AddChild(label);

        var scene = owner.GetTree().CurrentScene;
        var parent = scene ?? owner.GetParent();
        if (parent == null)
            return;

        parent.AddChild(popup);

        var tween = owner.GetTree().CreateTween();
        var targetY = popup.GlobalPosition + new Vector2(0.0f, -riseDistance);
        tween.TweenProperty(popup, "global_position", targetY, duration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, duration);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(popup))
                popup.QueueFree();
        };
    }
}
