using Godot;

public static class GearTooltipFactory
{
    private static readonly Color PanelBackground = new(0.06f, 0.06f, 0.08f, 0.98f);
    private static readonly Color PanelBorder = new(0.25f, 0.25f, 0.30f, 1.0f);
    private const float TooltipMinWidth = 320.0f;

    public static Control Build(GearInstance gear)
    {
        if (gear == null)
            return null;

        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        panel.AddThemeStyleboxOverride("panel", BuildStyleBox());

        var vbox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(TooltipMinWidth, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        vbox.AddThemeConstantOverride("separation", 2);
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
        vbox.AddChild(nameLabel);

        AddLine(vbox, $"Quality: {gear.Quality}");
        AddLine(vbox, $"Slot: {gear.Slot}");
        AddLine(vbox, $"Level: {gear.Level}");

        if (gear.MainStats.Count > 0)
        {
            AddLine(vbox, "Main:");
            foreach (var modifier in gear.MainStats)
                AddLine(vbox, "  " + GearTooltipBuilder.FormatModifier(modifier));
        }

        if (gear.Substats.Count > 0)
        {
            AddLine(vbox, "Substats:");
            foreach (var modifier in gear.Substats)
                AddLine(vbox, "  " + GearTooltipBuilder.FormatModifier(modifier));
        }

        return panel;
    }

    private static StyleBoxFlat BuildStyleBox()
    {
        return new StyleBoxFlat
        {
            BgColor = PanelBackground,
            BorderColor = PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
    }

    private static void AddLine(VBoxContainer parent, string text)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddChild(label);
    }
}
