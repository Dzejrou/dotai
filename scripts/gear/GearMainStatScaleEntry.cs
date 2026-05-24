using Godot;

[GlobalClass]
public partial class GearMainStatScaleEntry : Resource
{
    [Export]
    public string StatId { get; set; } = string.Empty;

    [Export]
    public ItemQuality Quality { get; set; } = ItemQuality.Common;

    [Export]
    public float MaxValue { get; set; }
}
