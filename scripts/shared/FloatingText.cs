using Godot;

public static class FloatingText
{
    private const float FallbackVerticalOffset = -16.0f;
    private const float FallbackRiseDistance = 18.0f;
    private const float FallbackDuration = 0.6f;
    private const int FallbackFontSize = 20;
    private const int FallbackOutlineSize = 2;
    private const int FallbackZIndex = 4;

    private static readonly Color FallbackGoodColor = new Color(0.0f, 1.0f, 0.0f, 1.0f);
    private static readonly Color FallbackBadColor = new Color(1.0f, 0.0f, 0.0f, 1.0f);
    private static readonly Color FallbackNeutralColor = new Color(1.0f, 1.0f, 0.0f, 1.0f);
    private static FloatingTextLayer _registeredLayer;
    private static bool _didWarnMissingLayer;

    public static void ShowGood(string text, Node2D origin, Node attachTo = null)
    {
        var layer = ResolveLayer();
        if (layer != null)
        {
            layer.ShowGood(text, origin, attachTo);
            return;
        }

        ShowFallback(text, origin, FallbackGoodColor, attachTo);
    }

    public static void ShowBad(string text, Node2D origin, Node attachTo = null)
    {
        var layer = ResolveLayer();
        if (layer != null)
        {
            layer.ShowBad(text, origin, attachTo);
            return;
        }

        ShowFallback(text, origin, FallbackBadColor, attachTo);
    }

    public static void ShowNeutral(string text, Node2D origin, Node attachTo = null)
    {
        var layer = ResolveLayer();
        if (layer != null)
        {
            layer.ShowNeutral(text, origin, attachTo);
            return;
        }

        ShowFallback(text, origin, FallbackNeutralColor, attachTo);
    }

    public static void ShowCustom(string text, Node2D origin, Color color, Node attachTo = null)
    {
        Show(text, origin, color, attachTo);
    }

    internal static void RegisterLayer(FloatingTextLayer layer)
    {
        if (layer == null || !GodotObject.IsInstanceValid(layer))
            return;

        _registeredLayer = layer;
        _didWarnMissingLayer = false;
    }

    internal static void UnregisterLayer(FloatingTextLayer layer)
    {
        if (_registeredLayer == null)
            return;

        if (!IsLayerValid(_registeredLayer) || ReferenceEquals(_registeredLayer, layer))
            _registeredLayer = null;
    }

    internal static void Show(
        string text,
        Node2D origin,
        Color color,
        Node attachTo = null,
        float riseDistance = FallbackRiseDistance,
        float duration = FallbackDuration,
        int fontSize = FallbackFontSize)
    {
        var layer = ResolveLayer();
        if (layer != null)
        {
            layer.ShowCustom(text, origin, color, attachTo, riseDistance, duration, fontSize);
            return;
        }

        ShowFallback(text, origin, color, attachTo, riseDistance, duration, fontSize);
    }

    private static FloatingTextLayer ResolveLayer()
    {
        if (!IsLayerValid(_registeredLayer))
        {
            _registeredLayer = null;
            return null;
        }

        return _registeredLayer;
    }

    private static bool IsLayerValid(FloatingTextLayer layer)
    {
        return layer != null && GodotObject.IsInstanceValid(layer) && layer.IsInsideTree();
    }

    private static void ShowFallback(
        string text,
        Node2D origin,
        Color color,
        Node attachTo = null,
        float riseDistance = FallbackRiseDistance,
        float duration = FallbackDuration,
        int fontSize = FallbackFontSize)
    {
        if (origin == null ||
            !GodotObject.IsInstanceValid(origin) ||
            !origin.IsInsideTree() ||
            string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        WarnMissingLayerOnce();

        var tree = origin.GetTree();
        if (tree == null)
            return;

        var popup = new Node2D
        {
            Name = "FloatingTextPopup",
            ZIndex = FallbackZIndex
        };

        var label = new Label
        {
            Name = "Label",
            Text = text,
            ZIndex = FallbackZIndex
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", FallbackOutlineSize);
        popup.AddChild(label);

        var worldPosition = origin.GlobalPosition + new Vector2(0.0f, FallbackVerticalOffset);
        if (!TryResolveFallbackParent(origin, attachTo, out var parent, out var localSpaceNode))
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

    private static void WarnMissingLayerOnce()
    {
        if (_didWarnMissingLayer)
            return;

        _didWarnMissingLayer = true;
        GD.PushWarning($"{nameof(FloatingTextLayer)} is not registered. Using compatibility fallback for floating text.");
    }

    private static bool TryResolveFallbackParent(Node2D origin, Node attachTo, out Node parent, out Node2D localSpaceNode)
    {
        if (attachTo != null && GodotObject.IsInstanceValid(attachTo) && attachTo.IsInsideTree())
        {
            parent = attachTo;
            localSpaceNode = attachTo as Node2D;
            return true;
        }

        var fallbackParent = origin.GetTree()?.CurrentScene ?? origin.GetParent();
        if (fallbackParent != null && GodotObject.IsInstanceValid(fallbackParent))
        {
            parent = fallbackParent;
            localSpaceNode = fallbackParent as Node2D;
            return true;
        }

        parent = null;
        localSpaceNode = null;
        return false;
    }
}
