using Godot;

using System;

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

    // The popup's placement as computed by the engine at show time, captured
    // before we ever move the window; collapse restores it.
    private Vector2I? _basePosition;

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

    // The tooltip lives in its own popup window placed and clamped on screen only
    // once, at show time; grow or shrink it back to content size when the
    // comparison section toggles. While expanded, re-clamp the window to the
    // visible bounds ourselves (hugging the edge like the engine's show-time
    // clamp) so the comparison can't run off screen; collapse restores the
    // engine's original placement so the plain tooltip is unchanged.
    private void FitTooltipWindow()
    {
        var window = GetWindow();
        if (window == null || window == GetTree()?.Root)
            return;

        window.WrapControls = true;
        _basePosition ??= window.Position;
        window.ResetSize();

        if (!Visible)
        {
            window.Position = _basePosition.Value;
            return;
        }

        // Clamp from the base placement each time so the result is idempotent
        // across rebuilds while Shift stays held. The upper bound never drops
        // below the lower one, so a comparison taller than the screen degrades
        // to hugging the top edge.
        var bounds = GetTooltipBounds(window);
        var position = _basePosition.Value;
        position.X = Math.Clamp(
            position.X,
            bounds.Position.X,
            Math.Max(bounds.Position.X, bounds.End.X - window.Size.X));
        position.Y = Math.Clamp(
            position.Y,
            bounds.Position.Y,
            Math.Max(bounds.Position.Y, bounds.End.Y - window.Size.Y));
        window.Position = position;
    }

    private Rect2I GetTooltipBounds(Window window)
    {
        if (window.IsEmbedded())
        {
            // Embedded popup positions are in the embedding viewport's coordinates.
            var visible = window.GetParent()?.GetViewport()?.GetVisibleRect()
                ?? GetTree().Root.GetVisibleRect();
            return new Rect2I((Vector2I)visible.Position, (Vector2I)visible.Size);
        }

        return DisplayServer.ScreenGetUsableRect(window.CurrentScreen);
    }
}
