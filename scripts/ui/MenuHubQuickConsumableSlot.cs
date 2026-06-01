using Godot;

using System;

// Assignment-only quick slot on the hub Inventory/Equipment page. Accepts a valid
// inventory stack drop to record its item-definition id; the inventory stack itself
// is never mutated. Right-click clears the assignment. The slot is not a drag source.
//
// Rejected payloads (no assignment occurs): wrong ConsumableKind, non-consumables,
// gear entries, equipment-origin drags, trash-origin drags, and malformed payloads.
[GlobalClass]
public partial class MenuHubQuickConsumableSlot : PanelContainer
{
    public ConsumableKind Kind { get; set; } = ConsumableKind.Food;

    public InventoryController Inventory { get; set; }

    // Invoked with the source inventory slot index when a valid stack is dropped.
    public Action<int> AssignRequested { get; set; }

    // Invoked on right-click to clear the current assignment.
    public Action ClearRequested { get; set; }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
            return;

        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            ClearRequested?.Invoke();
            AcceptEvent();
        }
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return TryResolveValidPayload(data, out _);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (TryResolveValidPayload(data, out var sourceSlot))
            AssignRequested?.Invoke(sourceSlot);
    }

    private bool TryResolveValidPayload(Variant data, out int sourceSlot)
    {
        sourceSlot = -1;

        // Trash-origin drags must be restored to inventory, never re-routed here.
        if (MenuHubUtilitySlot.IsTrashPayload(data))
            return false;

        // Only inventory-origin payloads are eligible (equipment-origin drags are rejected).
        if (!InventorySlotControl.TryReadInventoryPayload(data, out var slot, out _))
            return false;

        if (Inventory == null || !GodotObject.IsInstanceValid(Inventory))
            return false;

        // Stacks only: gear entries are rejected.
        if (!Inventory.TryGetEntry(slot, out var entry) || entry is not InventoryStackEntry stackEntry)
            return false;

        var definition = stackEntry.Stack.Item;
        if (definition == null || definition.ConsumableKind != Kind)
            return false;

        sourceSlot = slot;
        return true;
    }
}
