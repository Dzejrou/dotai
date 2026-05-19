using Godot;

public static class GearTooltipFactory
{
    private const string StylePath = "res://resources/ui/gear_tooltip_style.tres";

    private static GearTooltipStyle _cachedStyle;
    private static bool _styleLookupAttempted;

    public static Control Build(GearInstance gear)
    {
        if (gear == null)
            return null;

        var style = ResolveStyle();

        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        panel.AddThemeStyleboxOverride("panel", BuildStyleBox(style));

        var vbox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(style.MinWidth, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        vbox.AddThemeConstantOverride("separation", style.LineSpacing);
        panel.AddChild(vbox);

        var displayName = gear.Definition?.DisplayName;
        if (string.IsNullOrEmpty(displayName))
            displayName = "Unknown";

        var nameLabel = new Label
        {
            Text = displayName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        nameLabel.AddThemeColorOverride("font_color", GearQualityColors.GetColor(gear.Quality));
        if (style.NameFontSize > 0)
            nameLabel.AddThemeFontSizeOverride("font_size", style.NameFontSize);
        vbox.AddChild(nameLabel);

        AddLine(vbox, $"Quality: {gear.Quality}", style);
        AddLine(vbox, $"Slot: {gear.Slot}", style);
        AddLine(vbox, $"Level: {gear.Level}", style);

        if (gear.MainStats.Count > 0)
        {
            AddLine(vbox, "Main:", style);
            foreach (var modifier in gear.MainStats)
                AddLine(vbox, "  " + GearTooltipBuilder.FormatModifier(modifier), style);
        }

        if (gear.Substats.Count > 0)
        {
            AddLine(vbox, "Substats:", style);
            foreach (var modifier in gear.Substats)
                AddLine(vbox, "  " + GearTooltipBuilder.FormatModifier(modifier), style);
        }

        return panel;
    }

    private static GearTooltipStyle ResolveStyle()
    {
        if (_cachedStyle != null)
            return _cachedStyle;

        if (!_styleLookupAttempted)
        {
            _styleLookupAttempted = true;
            if (ResourceLoader.Exists(StylePath))
                _cachedStyle = ResourceLoader.Load<GearTooltipStyle>(StylePath);
        }

        return _cachedStyle ?? new GearTooltipStyle();
    }

    private static StyleBoxFlat BuildStyleBox(GearTooltipStyle style)
    {
        return new StyleBoxFlat
        {
            BgColor = style.PanelBackground,
            BorderColor = style.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = style.PaddingLeft,
            ContentMarginRight = style.PaddingRight,
            ContentMarginTop = style.PaddingTop,
            ContentMarginBottom = style.PaddingBottom,
            CornerRadiusTopLeft = style.CornerRadius,
            CornerRadiusTopRight = style.CornerRadius,
            CornerRadiusBottomLeft = style.CornerRadius,
            CornerRadiusBottomRight = style.CornerRadius,
        };
    }

    private static void AddLine(VBoxContainer parent, string text, GearTooltipStyle style)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (style.BodyFontSize > 0)
            label.AddThemeFontSizeOverride("font_size", style.BodyFontSize);
        parent.AddChild(label);
    }
}
