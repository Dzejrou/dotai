using Godot;

using System;

// One of the four equippable bag slots on the hub Inventory/Equipment page.
// - Drop target: accepts an inventory-origin drag only when it carries a bag the controller
//   would accept (equip into empty / replace). Non-bag items are rejected.
// - Drag source: when occupied, produces a "bag" payload so an inventory slot can unequip it.
//   The DROP/TRASH utility slots reject "bag" payloads, so equipped bags cannot be tossed
//   directly; the player must unequip into inventory first.
[GlobalClass]
public partial class MenuHubBagSlotControl : PanelContainer
{
    public int BagIndex { get; set; }

    public InventoryController Inventory { get; set; }

    // Inventory drag dropped onto this bag slot. Args: (inventorySlotIndex, bagSlotIndex).
    public Action<int, int> InventoryDropReceived { get; set; }

    // Mouse enter/exit over an occupied slot drives the capacity-contribution highlight.
    public Action<int> HoverStarted { get; set; }
    public Action<int> HoverEnded { get; set; }

    // Raised when a drag that started from this slot ends, so the page can flush any
    // deferred capacity rebuild once the drag is no longer in progress.
    public Action DragEnded { get; set; }

    private bool _dragActive;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var bag = Inventory?.GetBag(BagIndex);
        if (bag == null)
            return default;

        _dragActive = true;

        var preview = new Control { CustomMinimumSize = Size };
        if (bag.Icon != null)
        {
            var iconRect = new TextureRect
            {
                Texture = bag.Icon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            preview.AddChild(iconRect);
        }

        SetDragPreview(preview);

        return new Godot.Collections.Dictionary
        {
            { "source", "bag" },
            { "slot", BagIndex },
        };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (!InventorySlotControl.TryReadInventoryPayload(data, out var inventorySlotIndex, out _))
            return false;

        return Inventory != null && Inventory.CanEquipBagFromInventory(BagIndex, inventorySlotIndex);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!InventorySlotControl.TryReadInventoryPayload(data, out var inventorySlotIndex, out _))
            return;

        InventoryDropReceived?.Invoke(inventorySlotIndex, BagIndex);
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        switch (what)
        {
            case (int)NotificationMouseEnter:
                HoverStarted?.Invoke(BagIndex);
                break;
            case (int)NotificationMouseExit:
                HoverEnded?.Invoke(BagIndex);
                break;
            case (int)NotificationDragEnd:
                if (_dragActive)
                    DragEnded?.Invoke();
                _dragActive = false;
                break;
        }
    }

    public override Control _MakeCustomTooltip(string forText)
    {
        var bag = Inventory?.GetBag(BagIndex);
        return bag == null ? null : TooltipFactory.Build(bag, 1);
    }

    public static bool TryReadBagPayload(Variant data, out int bagSlotIndex)
    {
        bagSlotIndex = -1;
        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        if (!dict.TryGetValue("source", out var source) || source.AsString() != "bag")
            return false;

        if (!dict.TryGetValue("slot", out var slotValue))
            return false;

        bagSlotIndex = slotValue.AsInt32();
        return true;
    }
}
