using Godot;

using System;

[GlobalClass]
public partial class RoomContentOption : Resource
{
    [Export]
    public StringName Id { get; set; } = default;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public PackedScene ContentScene { get; set; }

    [Export(PropertyHint.Range, "0,100,0.1,or_greater")]
    public float Weight { get; set; } = 1.0f;

    // Completion points this specific content awards to a run's base score when its room is first
    // marked cleared. Authored per actual content rather than per room kind, so non-combat content
    // (rest/shop/pre-boss) authors 0. Clamped non-negative; editable directly on the room
    // definition resources.
    [Export(PropertyHint.Range, "0,10000,1,or_greater")]
    public int CompletionPoints
    {
        get => _completionPoints;
        set => _completionPoints = Math.Max(0, value);
    }

    private int _completionPoints;

    // Has a usable scene: the option can be placed/selected explicitly (e.g. the guaranteed
    // Pre-Boss slot, or the Debug HUB content selector) regardless of its weight.
    public bool IsConfigured => ContentScene != null;

    // Eligible for random weighted selection: configured and with a finite, positive weight.
    // A zero-weight option (e.g. Pre-Boss) stays configured but is never randomly drawn.
    public bool IsRandomlySelectable => IsConfigured && float.IsFinite(Weight) && Weight > 0.0f;
}
