using Godot;

[GlobalClass]
public partial class RoomRegistryEntry : Resource
{
    [Export]
    public StringName ScreenId { get; set; } = default;

    [Export]
    public PackedScene RoomScene { get; set; }
}
