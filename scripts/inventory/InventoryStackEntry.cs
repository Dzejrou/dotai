using System;

public sealed class InventoryStackEntry : InventoryEntry
{
    public InventoryStackEntry(InventoryStack stack)
    {
        Stack = stack ?? throw new ArgumentNullException(nameof(stack));
    }

    public InventoryStack Stack { get; }

    public override InventoryItemDefinition Definition => Stack.Item;

    public override int Quantity => Stack.Quantity;

    public override bool ShowQuantity => Stack.Quantity > 1;

    public override bool CanAcceptMergeFrom(InventoryEntry other)
    {
        if (other is not InventoryStackEntry otherStack)
            return false;

        if (Stack.AvailableSpace <= 0)
            return false;

        return DefinitionsMatch(Stack.Item, otherStack.Stack.Item);
    }

    public static bool DefinitionsMatch(InventoryItemDefinition a, InventoryItemDefinition b)
    {
        if (a == null || b == null)
            return false;

        // Bags are always unique, non-stackable entries; never merge them even if a
        // misconfigured definition left MaxStackSize above 1.
        if (a is BagItemDefinition || b is BagItemDefinition)
            return false;

        if (ReferenceEquals(a, b))
            return true;

        if (!string.IsNullOrEmpty(a.Id) && a.Id == b.Id)
            return true;

        return !string.IsNullOrEmpty(a.ResourcePath) && a.ResourcePath == b.ResourcePath;
    }
}
