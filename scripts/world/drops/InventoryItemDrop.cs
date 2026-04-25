using Godot;

[GlobalClass]
public partial class InventoryItemDrop : Drop
{
    [Export]
    public InventoryItemDefinition ItemDefinition
    {
        get => _itemDefinition;
        set
        {
            _itemDefinition = value;
            WorldSprite = _itemDefinition?.Icon;
        }
    }

    [Export(PropertyHint.Range, "1,999,1")]
    public int Quantity { get; set; } = 1;

    private InventoryItemDefinition _itemDefinition;

    public override void _Ready()
    {
        WorldSprite = ItemDefinition?.Icon;
        base._Ready();
    }

    protected override bool TryApplyTo(Player player)
    {
        var inventory = player?.InventoryController;
        if (inventory == null || ItemDefinition == null)
            return false;

        var quantity = Mathf.Max(1, Quantity);
        if (!inventory.CanAddItem(ItemDefinition, quantity))
            return false;

        return inventory.AddItem(ItemDefinition, quantity) == 0;
    }
}
