using Godot;

using System;

public enum MenuHubUtilityKind
{
    Drop,
    Trash,
}

// Two-purpose slot used on the hub Inventory/Equipment page:
// - Drop: accepts inventory or equipment payloads and forwards the request to the
//   owning page so the source can be removed and a world drop spawned.
// - Trash: a Terraria-style temporary buffer holding one entry. Accepts inventory
//   or equipment payloads; also acts as a drag source for restoring the buffered
//   item back to inventory. Buffer lifetime is "hub open".
[GlobalClass]
public partial class MenuHubUtilitySlot : PanelContainer
{
    public MenuHubUtilityKind Kind { get; set; } = MenuHubUtilityKind.Drop;

    // Inventory drag dropped onto this slot. Page decides the side effects.
    public Action<int, int> InventoryDropReceived { get; set; }

    // Equipment drag dropped onto this slot. Page decides the side effects.
    public Action<EquipmentSlot> EquipmentDropReceived { get; set; }

    // Page-driven check that returns true when the trash buffer currently holds
    // something to drag back into inventory. Only used in Trash mode.
    public Func<bool> TrashHasContents { get; set; }

    // Notifies the page that a trash drag has just started so it can paint feedback.
    // Not strictly required, but symmetric with the inventory slot drag-started hook.
    public Action TrashDragStarted { get; set; }

    private bool _trashDragActive;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        // Only trash can be a source. Drop is a one-way sink.
        if (Kind != MenuHubUtilityKind.Trash)
            return default;

        if (TrashHasContents == null || !TrashHasContents.Invoke())
            return default;

        _trashDragActive = true;
        TrashDragStarted?.Invoke();

        var preview = new Control { CustomMinimumSize = Size };
        SetDragPreview(preview);

        return new Godot.Collections.Dictionary
        {
            { "source", "trash" },
        };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        // Utility slots only accept fresh inventory/equipment payloads. Trash-origin
        // drags must be restored to an inventory slot, not re-routed through DROP
        // or back into TRASH itself.
        if (IsTrashPayload(data))
            return false;

        return InventorySlotControl.TryReadInventoryPayload(data, out _, out _) ||
               TryReadEquipmentPayload(data, out _);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (InventorySlotControl.TryReadInventoryPayload(data, out var inventorySlot, out var amount))
        {
            InventoryDropReceived?.Invoke(inventorySlot, amount);
            return;
        }

        if (TryReadEquipmentPayload(data, out var equipmentSlot))
            EquipmentDropReceived?.Invoke(equipmentSlot);
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationDragEnd)
            _trashDragActive = false;
    }

    public static bool IsTrashPayload(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        return dict.TryGetValue("source", out var source) && source.AsString() == "trash";
    }

    private static bool TryReadEquipmentPayload(Variant data, out EquipmentSlot slot)
    {
        slot = default;
        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        if (!dict.TryGetValue("source", out var source) || source.AsString() != "equipment")
            return false;

        if (!dict.TryGetValue("slot", out var slotValue))
            return false;

        slot = (EquipmentSlot)slotValue.AsInt32();
        return true;
    }
}
