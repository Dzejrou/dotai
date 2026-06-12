using Godot;

// Shift-held comparison section appended to owned-gear tooltips. Hidden while
// Shift is up so the normal tooltip is unchanged; while either Shift is held it
// shows stat deltas against the gear equipped in the hovered item's slot and
// resizes the tooltip popup to fit. Lives for the lifetime of one tooltip popup.
public partial class GearComparisonTooltipSection : VBoxContainer
{
    private static readonly Color GainColor = new(0.35f, 0.85f, 0.35f);
    private static readonly Color LossColor = new(0.85f, 0.25f, 0.25f);

    public GearInstance Gear { get; set; }
    public EquipmentController Equipment { get; set; }
    public GearTooltipStyle Style { get; set; }

    private bool _shiftHeld;
    private bool _dirty = true;
    private bool _hasComparison;

    public override void _Ready()
    {
        // Tooltips can show while the tree is paused (menu hub), so keep polling.
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        if (Style != null)
            AddThemeConstantOverride("separation", Style.LineSpacing);
    }

    public override void _EnterTree()
    {
        if (Equipment != null && GodotObject.IsInstanceValid(Equipment))
            Equipment.Changed += OnEquipmentChanged;
    }

    public override void _ExitTree()
    {
        if (Equipment != null && GodotObject.IsInstanceValid(Equipment))
            Equipment.Changed -= OnEquipmentChanged;
    }

    private void OnEquipmentChanged()
    {
        // Rebuilt lazily from _Process so we never free children mid-signal.
        _dirty = true;
    }

    public override void _Process(double delta)
    {
        var held = Input.IsKeyPressed(Key.Shift);
        var needsRebuild = held && _dirty;
        if (held == _shiftHeld && !needsRebuild)
            return;

        _shiftHeld = held;
        if (needsRebuild)
        {
            RebuildContent();
            _dirty = false;
        }

        var showSection = held && _hasComparison;
        var visibilityChanged = Visible != showSection;
        Visible = showSection;
        if (visibilityChanged || (needsRebuild && showSection))
            FitTooltipWindow();
    }

    private void RebuildContent()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.Free();
        }

        _hasComparison = false;

        if (Gear == null || Equipment == null || !GodotObject.IsInstanceValid(Equipment))
            return;

        var equipped = Equipment.GetEquipped(Gear.Slot);
        if (equipped == null || ReferenceEquals(equipped, Gear))
            return;

        var equippedName = equipped.Definition?.DisplayName;
        AddLine($"Compared to: {(string.IsNullOrEmpty(equippedName) ? "Unknown" : equippedName)}", null);

        foreach (var statDelta in GearStatComparison.ComputeDeltas(Gear, equipped))
        {
            AddLine(
                GearTooltipBuilder.FormatStatValue(statDelta.StatId, statDelta.Difference),
                statDelta.Difference > 0.0f ? GainColor : LossColor);
        }

        _hasComparison = true;
    }

    private void AddLine(string text, Color? color)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        if (Style != null && Style.BodyFontSize > 0)
            label.AddThemeFontSizeOverride("font_size", Style.BodyFontSize);
        if (color.HasValue)
            label.AddThemeColorOverride("font_color", color.Value);
        AddChild(label);
    }

    // The tooltip lives in its own popup window sized once at show time; grow or
    // shrink it back to content size when the comparison section toggles.
    private void FitTooltipWindow()
    {
        var window = GetWindow();
        if (window == null || window == GetTree()?.Root)
            return;

        window.WrapControls = true;
        window.ResetSize();
    }
}
