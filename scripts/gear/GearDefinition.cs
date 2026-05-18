using Godot;

using System;

[GlobalClass]
public partial class GearDefinition : InventoryItemDefinition
{
    [Export]
    public EquipmentSlot Slot { get; set; } = EquipmentSlot.Head;

    [Export]
    public GearQuality Quality { get; set; } = GearQuality.Common;

    [Export(PropertyHint.Range, "1,20,1")]
    public int Level
    {
        get => _level;
        set => _level = Math.Clamp(value, 1, 20);
    }

    [Export]
    public Godot.Collections.Array<GearStatModifier> StatModifiers { get; set; } = new();

    private int _level = 1;
}
