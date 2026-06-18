using Godot;

using System;

// One explicit selection entry for a CompositeActionController. The controller is
// referenced by NodePath (resolved relative to the composite) so configuration does
// not depend on fragile child ordering.
[GlobalClass]
public partial class CompositeActionEntry : Resource
{
    [Export]
    public NodePath Controller { get; set; }

    [Export]
    public int Priority { get; set; } = 0;

    [Export(PropertyHint.Range, "0,100,0.1,or_greater")]
    public float Weight { get; set; } = 1.0f;

    // Phases in which this entry may be selected. Empty means selectable in every
    // phase, so existing entries with no phase configuration are unaffected. An entry
    // that is inactive for the actor's current phase is simply skipped during
    // selection; its controller still ticks and advances its own cooldowns.
    [Export]
    public int[] ActivePhases { get; set; } = Array.Empty<int>();
}
