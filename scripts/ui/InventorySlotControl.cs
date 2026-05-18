using Godot;

using System;

[GlobalClass]
public partial class InventorySlotControl : PanelContainer
{
    public int SlotIndex { get; set; }

    public InventoryController Inventory { get; set; }

    public Action<int> DragStarted { get; set; }

    public Action<int, int> DropReceived { get; set; }

    public Action<int> DragEnded { get; set; }

    // Invoked when an equipment-origin drag is dropped onto this inventory slot.
    public Action<int, int> EquipmentDropReceived { get; set; }

    // Invoked on any left mouse press over this slot so the owning window can move to front.
    public Action FocusRequested { get; set; }

    private bool _dragActive;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            mouseButton.Pressed)
        {
            FocusRequested?.Invoke();
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Inventory == null || !Inventory.TryGetEntry(SlotIndex, out var entry) || entry?.Definition == null)
            return default;

        DragStarted?.Invoke(SlotIndex);
        _dragActive = true;

        var preview = new Control { CustomMinimumSize = Size };

        if (entry.Icon != null)
        {
            var icon = new TextureRect
            {
                Texture = entry.Icon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            preview.AddChild(icon);
        }

        if (entry.ShowQuantity)
        {
            var qty = new Label
            {
                Text = entry.Quantity.ToString(),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            qty.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            preview.AddChild(qty);
        }

        SetDragPreview(preview);
        return Variant.From(SlotIndex);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType == Variant.Type.Int)
            return data.AsInt32() != SlotIndex;

        if (TryReadEquipmentPayload(data, out _))
            return Inventory != null && Inventory.IsSlotEmpty(SlotIndex);

        return false;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType == Variant.Type.Int)
        {
            DropReceived?.Invoke(data.AsInt32(), SlotIndex);
            return;
        }

        if (TryReadEquipmentPayload(data, out var equipmentSlot))
            EquipmentDropReceived?.Invoke(equipmentSlot, SlotIndex);
    }

    private static bool TryReadEquipmentPayload(Variant data, out int equipmentSlot)
    {
        equipmentSlot = -1;
        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        if (!dict.TryGetValue("source", out var source) || source.AsString() != "equipment")
            return false;

        if (!dict.TryGetValue("slot", out var slotValue))
            return false;

        equipmentSlot = slotValue.AsInt32();
        return true;
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what != NotificationDragEnd)
            return;

        if (_dragActive)
            DragEnded?.Invoke(SlotIndex);

        _dragActive = false;
    }

    public override Control _MakeCustomTooltip(string forText)
    {
        if (Inventory == null || !Inventory.TryGetEntry(SlotIndex, out var entry))
            return null;

        if (entry is not InventoryGearEntry gearEntry || gearEntry.Gear?.Definition == null)
            return null;

        return GearTooltipFactory.Build(gearEntry.Gear);
    }
}
