public enum MerchantBuybackEntryKind
{
    Gear,
    Stack,
}

// Session-local record of an item the player just sold to the current merchant.
// Held by MerchantWindow for the lifetime of one merchant interaction and discarded
// when the window closes or rebinds to a different stock. Not persisted to save data.
public sealed class MerchantBuybackEntry
{
    public MerchantBuybackEntryKind Kind { get; init; }

    // Total gold the buyback costs, equal to what the player received on sale.
    public int Price { get; init; }

    public GearInstance Gear { get; init; }

    public InventoryItemDefinition StackItem { get; init; }

    public int StackQuantity { get; init; }

    public static MerchantBuybackEntry ForGear(GearInstance gear, int price)
    {
        return new MerchantBuybackEntry
        {
            Kind = MerchantBuybackEntryKind.Gear,
            Gear = gear,
            Price = price,
        };
    }

    public static MerchantBuybackEntry ForStack(InventoryItemDefinition item, int quantity, int totalPrice)
    {
        return new MerchantBuybackEntry
        {
            Kind = MerchantBuybackEntryKind.Stack,
            StackItem = item,
            StackQuantity = quantity,
            Price = totalPrice,
        };
    }
}
