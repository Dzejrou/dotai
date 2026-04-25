using Godot;

[GlobalClass]
public partial class InventoryItemDropDefinition : DropDefinition
{
    [Export]
    public InventoryItemDefinition ItemDefinition { get; set; }

    public override void ConfigureDrop(Drop drop, int amount)
    {
        base.ConfigureDrop(drop, amount);

        if (drop is InventoryItemDrop inventoryItemDrop)
        {
            inventoryItemDrop.ItemDefinition = ItemDefinition;
            inventoryItemDrop.Quantity = Mathf.Max(1, amount);
        }
    }
}
