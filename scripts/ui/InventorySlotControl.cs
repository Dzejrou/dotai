using Godot;

using System;

[GlobalClass]
public partial class InventorySlotControl : PanelContainer
{
    // Raised by custom drop targets (e.g. the gear leveling reference slot) that
    // accept an inventory-origin drag without going through InventoryWindow's
    // OnSlotDropReceived / OnEquipmentDropReceived paths. The owning InventoryWindow
    // listens so it can flip _dragConsumed and avoid spawning a world drop.
    public static event Action<int> DragConsumed;

    public static void NotifyDragConsumed(int sourceSlotIndex)
    {
        DragConsumed?.Invoke(sourceSlotIndex);
    }

    public int SlotIndex { get; set; }

    public InventoryController Inventory { get; set; }

    // Supplies the currently-selected amount for stack drags (e.g. from a SpinBox).
    // The slot still clamps the result to [1, source quantity] at drag time.
    public Func<int> AmountProvider { get; set; }

    public Action<int, int> DragStarted { get; set; }

    public Action<int, int, int> DropReceived { get; set; }

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

        var sourceQuantity = Math.Max(1, entry.Quantity);
        var requested = AmountProvider?.Invoke() ?? sourceQuantity;
        var amount = entry is InventoryStackEntry
            ? Math.Clamp(requested, 1, sourceQuantity)
            : 1;

        DragStarted?.Invoke(SlotIndex, amount);
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

        var showAmount = entry is InventoryStackEntry ? amount > 1 : entry.ShowQuantity;
        if (showAmount)
        {
            var qty = new Label
            {
                Text = amount.ToString(),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            qty.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            preview.AddChild(qty);
        }

        SetDragPreview(preview);
        return BuildInventoryPayload(SlotIndex, amount);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (TryReadInventoryPayload(data, out var sourceSlot, out _))
        {
            // Inventory-origin drags are always "accepted" by another inventory slot so
            // that releasing over an incompatible target counts as a no-op (consumed)
            // rather than falling through to the world-drop branch in InventoryWindow.
            // The controller-level move/split decides whether the drop actually mutates.
            return sourceSlot != SlotIndex;
        }

        if (TryReadEquipmentPayload(data, out _))
            return Inventory != null && Inventory.IsSlotEmpty(SlotIndex);

        return false;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (TryReadInventoryPayload(data, out var sourceSlot, out var amount))
        {
            DropReceived?.Invoke(sourceSlot, SlotIndex, amount);
            return;
        }

        if (TryReadEquipmentPayload(data, out var equipmentSlot))
            EquipmentDropReceived?.Invoke(equipmentSlot, SlotIndex);
    }

    public static Variant BuildInventoryPayload(int sourceSlot, int amount)
    {
        return new Godot.Collections.Dictionary
        {
            { "source", "inventory" },
            { "slot", sourceSlot },
            { "amount", Math.Max(1, amount) },
        };
    }

    // Reads an inventory-origin drag payload. Backwards-compatible with raw int
    // payloads is intentionally not provided; all in-tree producers now use the
    // dictionary shape.
    public static bool TryReadInventoryPayload(Variant data, out int sourceSlot, out int amount)
    {
        sourceSlot = -1;
        amount = 0;
        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        if (!dict.TryGetValue("source", out var source) || source.AsString() != "inventory")
            return false;
        if (!dict.TryGetValue("slot", out var slotValue))
            return false;

        sourceSlot = slotValue.AsInt32();
        amount = dict.TryGetValue("amount", out var amountValue) ? amountValue.AsInt32() : 1;
        if (amount < 1)
            amount = 1;
        return true;
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

        if (entry is InventoryGearEntry gearEntry && gearEntry.Gear?.Definition != null)
            return TooltipFactory.Build(gearEntry.Gear);

        if (entry is InventoryStackEntry stackEntry && stackEntry.Stack.Item != null)
            return TooltipFactory.Build(stackEntry.Stack.Item, stackEntry.Stack.Quantity);

        return null;
    }
}
