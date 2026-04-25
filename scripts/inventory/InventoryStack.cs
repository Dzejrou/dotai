using System;

public sealed class InventoryStack
{
    public InventoryStack(InventoryItemDefinition item, int quantity)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Quantity = Math.Max(1, quantity);
    }

    public InventoryItemDefinition Item { get; }

    public int Quantity { get; private set; }

    public int AvailableSpace => Math.Max(0, Item.MaxStackSize - Quantity);

    public int AddQuantity(int quantity)
    {
        var remaining = Math.Max(0, quantity);
        if (remaining <= 0 || AvailableSpace <= 0)
            return remaining;

        var amountToAdd = Math.Min(AvailableSpace, remaining);
        Quantity += amountToAdd;
        return remaining - amountToAdd;
    }
}
