using Godot;

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
}
