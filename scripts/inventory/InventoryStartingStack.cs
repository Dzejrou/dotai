using Godot;

using System;

[GlobalClass]
public partial class InventoryStartingStack : Resource
{
    [Export]
    public InventoryItemDefinition Item { get; set; }

    [Export(PropertyHint.Range, "1,999,1")]
    public int Quantity
    {
        get => _quantity;
        set => _quantity = Math.Max(1, value);
    }

    private int _quantity = 1;
}
