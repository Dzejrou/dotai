using System;

public sealed class InventoryGearEntry : InventoryEntry
{
    public InventoryGearEntry(GearInstance gear)
    {
        Gear = gear ?? throw new ArgumentNullException(nameof(gear));
    }

    public GearInstance Gear { get; }

    public override InventoryItemDefinition Definition => Gear.Definition;

    public override int Quantity => 1;

    public override bool ShowQuantity => false;

    public override bool CanAcceptMergeFrom(InventoryEntry other) => false;
}
