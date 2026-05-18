using Godot;

[GlobalClass]
public partial class GearSlotRules : Resource
{
    [Export]
    public EquipmentSlot Slot { get; set; } = EquipmentSlot.Head;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public Texture2D Icon { get; set; }

    [Export]
    public Godot.Collections.Array<string> MainStat1Pool { get; set; } = new();

    [Export]
    public Godot.Collections.Array<string> MainStat2Pool { get; set; } = new();
}
