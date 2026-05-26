using Godot;

// HBoxContainer that surfaces a custom TooltipFactory tooltip when the cursor hovers
// the row or any of its children. Set either Gear (for gear rows) or StackItem +
// optional StackQuantity (for stack item rows); Gear takes precedence if both are set.
public partial class TooltipRow : HBoxContainer
{
    public GearInstance Gear { get; set; }
    public int RevealedSubstatCount { get; set; } = int.MaxValue;
    public InventoryItemDefinition StackItem { get; set; }
    public int StackQuantity { get; set; } = 1;

    // Drive tooltip text from live state so the engine treats the row as tooltip-bearing
    // and routes hover through _MakeCustomTooltip below.
    public override string _GetTooltip(Vector2 atPosition)
    {
        if (Gear?.Definition != null)
            return Gear.Definition.DisplayName;
        if (StackItem != null)
            return StackItem.DisplayName;
        return string.Empty;
    }

    public override Control _MakeCustomTooltip(string forText)
    {
        if (Gear?.Definition != null)
            return TooltipFactory.Build(Gear, RevealedSubstatCount);
        if (StackItem != null)
            return TooltipFactory.Build(StackItem, StackQuantity);
        return null;
    }
}
