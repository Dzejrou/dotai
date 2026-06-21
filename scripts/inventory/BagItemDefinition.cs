using Godot;

using System;

// An ordinary inventory item that can be equipped into a bag slot to grant
// additional inventory capacity. Bags are always non-stackable: the editor sets
// MaxStackSize = 1 and InventoryStackEntry.DefinitionsMatch refuses to merge any
// bag definition, so two bags always occupy distinct slots.
[GlobalClass]
public partial class BagItemDefinition : InventoryItemDefinition
{
    // Number of inventory slots this bag contributes to effective capacity while
    // equipped. Editor-editable so designers can tune bag sizes per item.
    [Export(PropertyHint.Range, "1,100,1")]
    public int AdditionalSlots
    {
        get => _additionalSlots;
        set => _additionalSlots = Math.Max(0, value);
    }

    private int _additionalSlots = 1;
}
