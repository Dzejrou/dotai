using Godot;

[GlobalClass]
public partial class GearMainStatScaleEntry : Resource
{
    [Export]
    public string StatId { get; set; } = string.Empty;

    [Export]
    public GearQuality Quality { get; set; } = GearQuality.Common;

    [Export]
    public float MaxValue { get; set; }
}
