using Godot;

using System;

[GlobalClass]
public partial class EquipmentSlotControl : PanelContainer
{
    public EquipmentSlot Slot { get; set; }

    public InventoryController Inventory { get; set; }

    public EquipmentController Equipment { get; set; }

    // Invoked when an inventory-origin drag is dropped onto this equipment slot.
    public Action<int, EquipmentSlot> InventoryDropReceived { get; set; }

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
        var gear = Equipment?.GetEquipped(Slot);
        if (gear?.Definition == null)
            return default;

        _dragActive = true;

        var preview = new Control { CustomMinimumSize = Size };
        var icon = gear.Definition.Icon;
        if (icon != null)
        {
            var iconRect = new TextureRect
            {
                Texture = icon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            preview.AddChild(iconRect);
        }

        SetDragPreview(preview);

        var payload = new Godot.Collections.Dictionary
        {
            { "source", "equipment" },
            { "slot", (int)Slot },
        };
        return payload;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.Int)
            return false;

        if (Inventory == null)
            return false;

        var inventorySlotIndex = data.AsInt32();
        if (!Inventory.TryGetEntry(inventorySlotIndex, out var entry))
            return false;

        if (entry is not InventoryGearEntry gearEntry)
            return false;

        return gearEntry.Gear?.Definition?.Slot == Slot;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.Int)
            return;

        InventoryDropReceived?.Invoke(data.AsInt32(), Slot);
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationDragEnd)
            _dragActive = false;
    }

    public override Control _MakeCustomTooltip(string forText)
    {
        var gear = Equipment?.GetEquipped(Slot);
        return gear?.Definition == null ? null : GearTooltipFactory.Build(gear);
    }
}
