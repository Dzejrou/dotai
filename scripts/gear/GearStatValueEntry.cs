using Godot;

[GlobalClass]
public partial class GearStatValueEntry : Resource
{
    [Export]
    public string StatId { get; set; } = string.Empty;

    [Export]
    public float Value { get; set; }
}
