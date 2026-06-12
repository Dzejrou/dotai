using Godot;

public static class TooltipFactory
{
    private const string StylePath = "res://resources/ui/gear_tooltip_style.tres";
    private const string PlaceholderText = "  ???";
    private static readonly Color PlaceholderColor = new(0.85f, 0.25f, 0.25f);

    private static GearTooltipStyle _cachedStyle;
    private static bool _styleLookupAttempted;

    public static Control Build(GearInstance gear) => Build(gear, int.MaxValue);

    // equipment: when non-null, appends a Shift-held comparison section against the
    // item equipped in the hovered gear's slot. The section stays hidden until Shift
    // is pressed, so the normal tooltip is unchanged.
    public static Control Build(GearInstance gear, EquipmentController equipment) =>
        BuildGear(gear, int.MaxValue, equipment);

    // revealedSubstatCount: how many substats to show with their real values; the
    // remainder render as red "???" placeholder lines. Pass int.MaxValue to reveal all.
    // Used by merchant tooltips to support hidden substats before purchase.
    public static Control Build(GearInstance gear, int revealedSubstatCount) =>
        BuildGear(gear, revealedSubstatCount, null);

    private static Control BuildGear(GearInstance gear, int revealedSubstatCount, EquipmentController equipment)
    {
        if (gear == null)
            return null;

        var style = ResolveStyle();
        var (panel, vbox) = BuildShell(style, gear.Definition?.DisplayName, ItemQualityColors.GetColor(gear.Quality));

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
            var revealed = System.Math.Clamp(revealedSubstatCount, 0, gear.Substats.Count);
            for (var i = 0; i < revealed; i++)
                AddLine(vbox, "  " + GearTooltipBuilder.FormatModifier(gear.Substats[i]), style);
            for (var i = revealed; i < gear.Substats.Count; i++)
                AddLine(vbox, PlaceholderText, style, PlaceholderColor);
        }

        if (equipment != null)
        {
            vbox.AddChild(new GearComparisonTooltipSection
            {
                Gear = gear,
                Equipment = equipment,
                Style = style,
            });
        }

        return panel;
    }

    public static Control Build(InventoryItemDefinition item, int quantity)
    {
        return Build(item, quantity, alwaysShowQuantity: false);
    }

    // alwaysShowQuantity: quick-slot tooltips always show the held quantity (even x1
    // or x0); inventory stack tooltips keep omitting the redundant x1 row.
    public static Control Build(InventoryItemDefinition item, int quantity, bool alwaysShowQuantity)
    {
        if (item == null)
            return null;

        var style = ResolveStyle();
        var (panel, vbox) = BuildShell(style, item.DisplayName, ItemQualityColors.GetColor(item.Quality));

        AddLine(vbox, $"Quality: {item.Quality}", style);
        if (alwaysShowQuantity || quantity > 1)
            AddLine(vbox, $"x{System.Math.Max(0, quantity)}", style);

        return panel;
    }

    public static Control Build(Spell spell)
    {
        if (spell == null)
            return null;

        var style = ResolveStyle();
        var (panel, vbox) = BuildShell(style, spell.DisplayLabel, SpellSchoolColors.GetColor(spell.DisplaySchool));

        if (!string.IsNullOrWhiteSpace(spell.Description))
            AddWrappedLine(vbox, spell.Description, style);

        if (spell.DisplayManaCost > 0)
            AddLine(vbox, $"Mana: {spell.DisplayManaCost}", style);

        if (spell.CooldownDuration > 0.0f)
            AddLine(vbox, $"Cooldown: {FormatSeconds(spell.CooldownDuration)}", style);

        if (spell.CastTimeDuration > 0.0f)
            AddLine(vbox, $"Cast time: {FormatSeconds(spell.CastTimeDuration)}", style);

        if (spell.ChannelDuration > 0.0f)
            AddLine(vbox, $"Channel: {FormatSeconds(spell.ChannelDuration)}", style);

        return panel;
    }

    private static string FormatSeconds(float seconds)
    {
        return $"{seconds:0.#}s";
    }

    private static (PanelContainer Panel, VBoxContainer VBox) BuildShell(
        GearTooltipStyle style,
        string displayName,
        Color nameColor)
    {
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

        var nameLabel = new Label
        {
            Text = string.IsNullOrEmpty(displayName) ? "Unknown" : displayName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        nameLabel.AddThemeColorOverride("font_color", nameColor);
        if (style.NameFontSize > 0)
            nameLabel.AddThemeFontSizeOverride("font_size", style.NameFontSize);
        vbox.AddChild(nameLabel);

        return (panel, vbox);
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
        AddLine(parent, text, style, null);
    }

    // Word-wrapped variant for free-form prose (spell descriptions) so long text
    // wraps at the tooltip's minimum width instead of widening the panel.
    private static void AddWrappedLine(VBoxContainer parent, string text, GearTooltipStyle style)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(style.MinWidth, 0.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (style.BodyFontSize > 0)
            label.AddThemeFontSizeOverride("font_size", style.BodyFontSize);
        parent.AddChild(label);
    }

    private static void AddLine(VBoxContainer parent, string text, GearTooltipStyle style, Color? color)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (style.BodyFontSize > 0)
            label.AddThemeFontSizeOverride("font_size", style.BodyFontSize);
        if (color.HasValue)
            label.AddThemeColorOverride("font_color", color.Value);
        parent.AddChild(label);
    }
}
