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

    // Runtime-only: when an existing gear instance is being dropped from inventory back to
    // the world, the caller assigns it here so pickup preserves identity instead of creating
    // a fresh GearInstance from the definition.
    public GearInstance GearInstance { get; set; }

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

        if (ItemDefinition is GearDefinition)
        {
            // All gear must arrive with a rolled GearInstance preassigned (debug spawn
            // or inventory-to-world toss). A bare GearDefinition without a runtime
            // instance is unsupported now that random rolls are the only gear path.
            if (GearInstance == null)
            {
                GD.PushWarning($"{nameof(InventoryItemDrop)}: refusing pickup — gear drop missing runtime GearInstance.");
                return false;
            }

            if (!inventory.CanAddGear(GearInstance))
                return false;

            return inventory.AddGear(GearInstance);
        }

        var quantity = Mathf.Max(1, Quantity);
        if (!inventory.CanAddItem(ItemDefinition, quantity))
            return false;

        return inventory.AddItem(ItemDefinition, quantity) == 0;
    }
}
