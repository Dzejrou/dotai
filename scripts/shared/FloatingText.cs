using Godot;

public static class FloatingText
{
    private const string CentralLayerName = "FloatingTextLayer";
    private const float DefaultVerticalOffset = -16.0f;
    private const float DefaultRiseDistance = 18.0f;
    private const float DefaultDuration = 0.6f;
    private const int DefaultFontSize = 20;
    private const int DefaultOutlineSize = 2;
    private const int DefaultZIndex = 4;

    private static readonly Color GoodColor = new Color(0.0f, 1.0f, 0.0f, 1.0f);
    private static readonly Color BadColor = new Color(1.0f, 0.0f, 0.0f, 1.0f);
    private static readonly Color NeutralColor = new Color(1.0f, 1.0f, 0.0f, 1.0f);

    public static void ShowGood(string text, Node2D origin, Node attachTo = null)
    {
        Show(text, origin, GoodColor, attachTo);
    }

    public static void ShowBad(string text, Node2D origin, Node attachTo = null)
    {
        Show(text, origin, BadColor, attachTo);
    }

    public static void ShowNeutral(string text, Node2D origin, Node attachTo = null)
    {
        Show(text, origin, NeutralColor, attachTo);
    }

    public static void ShowCustom(string text, Node2D origin, Color color, Node attachTo = null)
    {
        Show(text, origin, color, attachTo);
    }

    internal static void Show(
        string text,
        Node2D origin,
        Color color,
        Node attachTo = null,
        float riseDistance = DefaultRiseDistance,
        float duration = DefaultDuration,
        int fontSize = DefaultFontSize)
    {
        if (origin == null ||
            !GodotObject.IsInstanceValid(origin) ||
            !origin.IsInsideTree() ||
            string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var tree = origin.GetTree();
        if (tree == null)
            return;

        var popup = new Node2D
        {
            Name = "FloatingTextPopup",
            ZIndex = DefaultZIndex
        };

        var label = new Label
        {
            Name = "Label",
            Text = text,
            ZIndex = DefaultZIndex
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", DefaultOutlineSize);
        popup.AddChild(label);

        var worldPosition = origin.GlobalPosition + new Vector2(0.0f, DefaultVerticalOffset);
        if (!TryResolveParent(origin, attachTo, out var parent, out var localSpaceNode))
            return;

        parent.AddChild(popup);

        if (localSpaceNode != null)
            popup.Position = localSpaceNode.ToLocal(worldPosition);
        else
            popup.GlobalPosition = worldPosition;

        var tween = tree.CreateTween();
        if (localSpaceNode != null)
        {
            tween.TweenProperty(popup, "position", popup.Position + new Vector2(0.0f, -riseDistance), duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        }
        else
        {
            tween.TweenProperty(popup, "global_position", worldPosition + new Vector2(0.0f, -riseDistance), duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        }

        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, duration);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(popup))
                popup.QueueFree();
        };
    }

    private static bool TryResolveParent(Node2D origin, Node attachTo, out Node2D parent, out Node2D localSpaceNode)
    {
        if (attachTo is Node2D attachTarget && GodotObject.IsInstanceValid(attachTarget) && attachTarget.IsInsideTree())
        {
            parent = attachTarget;
            localSpaceNode = attachTarget;
            return true;
        }

        if (TryResolveCentralLayer(origin, out parent))
        {
            localSpaceNode = null;
            return true;
        }

        if (origin.GetParent() is Node2D originParent && GodotObject.IsInstanceValid(originParent))
        {
            parent = originParent;
            localSpaceNode = originParent;
            return true;
        }

        parent = null;
        localSpaceNode = null;
        return false;
    }

    private static bool TryResolveCentralLayer(Node2D origin, out Node2D layer)
    {
        layer = null;

        var tree = origin.GetTree();
        var scene = tree?.CurrentScene;
        if (scene == null || !GodotObject.IsInstanceValid(scene))
            return false;

        layer = scene.GetNodeOrNull<Node2D>(CentralLayerName);
        if (layer != null && GodotObject.IsInstanceValid(layer))
            return true;

        if (scene is not Node2D sceneRoot)
            return false;

        layer = new Node2D
        {
            Name = CentralLayerName,
            ZIndex = DefaultZIndex
        };
        sceneRoot.AddChild(layer);
        return true;
    }
}
