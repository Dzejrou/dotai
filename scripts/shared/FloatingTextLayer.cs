using Godot;

using System;

[GlobalClass]
public partial class FloatingTextLayer : Node2D
{
    [Export]
    public bool Enabled { get; set; } = true;

    [Export]
    public Color GoodColor { get; set; } = new Color(0.0f, 1.0f, 0.0f, 1.0f);

    [Export]
    public Color BadColor { get; set; } = new Color(1.0f, 0.0f, 0.0f, 1.0f);

    [Export]
    public Color NeutralColor { get; set; } = new Color(1.0f, 1.0f, 0.0f, 1.0f);

    [Export]
    public Color OutlineColor { get; set; } = Colors.Black;

    [Export(PropertyHint.Range, "0,8,1")]
    public int OutlineSize { get; set; } = 2;

    [Export(PropertyHint.Range, "-128,128,0.5")]
    public float VerticalOffset { get; set; } = -16.0f;

    [Export(PropertyHint.Range, "0,128,0.5")]
    public float RiseDistance { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "0,10,0.05")]
    public float Duration { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "1,128,1")]
    public int FontSize { get; set; } = 20;

    public override void _EnterTree()
    {
        FloatingText.RegisterLayer(this);
    }

    public override void _ExitTree()
    {
        FloatingText.UnregisterLayer(this);
    }

    public void ShowGood(string text, Node2D origin, Node attachTo = null)
    {
        ShowText(text, origin, GoodColor, attachTo);
    }

    public void ShowBad(string text, Node2D origin, Node attachTo = null)
    {
        ShowText(text, origin, BadColor, attachTo);
    }

    public void ShowNeutral(string text, Node2D origin, Node attachTo = null)
    {
        ShowText(text, origin, NeutralColor, attachTo);
    }

    public void ShowCustom(
        string text,
        Node2D origin,
        Color color,
        Node attachTo = null,
        float riseDistance = -1.0f,
        float duration = -1.0f,
        int fontSize = -1)
    {
        ShowText(text, origin, color, attachTo, riseDistance, duration, fontSize);
    }

    private void ShowText(
        string text,
        Node2D origin,
        Color color,
        Node attachTo = null,
        float riseDistance = -1.0f,
        float duration = -1.0f,
        int fontSize = -1)
    {
        if (!Enabled ||
            origin == null ||
            !GodotObject.IsInstanceValid(origin) ||
            !origin.IsInsideTree() ||
            string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var tree = origin.GetTree();
        if (tree == null)
            return;

        if (!TryResolveParent(attachTo, out var parent, out var localSpaceNode))
            return;

        var resolvedRiseDistance = riseDistance >= 0.0f ? riseDistance : RiseDistance;
        var resolvedDuration = duration >= 0.0f ? duration : Duration;
        var resolvedFontSize = fontSize > 0 ? fontSize : FontSize;
        var worldPosition = origin.GlobalPosition + new Vector2(0.0f, VerticalOffset);

        var popup = new Node2D
        {
            Name = "FloatingTextPopup",
            ZIndex = ZIndex
        };

        var label = new Label
        {
            Name = "Label",
            Text = text,
            ZIndex = ZIndex
        };
        label.AddThemeFontSizeOverride("font_size", resolvedFontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", OutlineColor);
        label.AddThemeConstantOverride("outline_size", Math.Max(0, OutlineSize));
        popup.AddChild(label);

        parent.AddChild(popup);

        if (localSpaceNode != null)
            popup.Position = localSpaceNode.ToLocal(worldPosition);
        else
            popup.GlobalPosition = worldPosition;

        var tween = tree.CreateTween();
        if (localSpaceNode != null)
        {
            tween.TweenProperty(popup, "position", popup.Position + new Vector2(0.0f, -resolvedRiseDistance), resolvedDuration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        }
        else
        {
            tween.TweenProperty(popup, "global_position", worldPosition + new Vector2(0.0f, -resolvedRiseDistance), resolvedDuration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        }

        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, resolvedDuration);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(popup))
                popup.QueueFree();
        };
    }

    private bool TryResolveParent(Node attachTo, out Node parent, out Node2D localSpaceNode)
    {
        if (attachTo != null && GodotObject.IsInstanceValid(attachTo) && attachTo.IsInsideTree())
        {
            parent = attachTo;
            localSpaceNode = attachTo as Node2D;
            return true;
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
        {
            parent = null;
            localSpaceNode = null;
            return false;
        }

        parent = this;
        localSpaceNode = null;
        return true;
    }
}
