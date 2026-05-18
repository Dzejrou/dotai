using Godot;

// In-memory display shell for a gear pickup: icon, name, slot, quality.
// Rolled stats live on GearInstance, not here. No .tres files exist for generated gear;
// instances of this resource are synthesized at runtime by GearGenerator.
[GlobalClass]
public partial class GearDefinition : InventoryItemDefinition
{
    [Export]
    public EquipmentSlot Slot { get; set; } = EquipmentSlot.Head;

    [Export]
    public GearQuality Quality { get; set; } = GearQuality.Common;
}
