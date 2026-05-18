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

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Inventory == null || !Inventory.TryGetEntry(SlotIndex, out var entry) || entry?.Definition == null)
            return default;

        DragStarted?.Invoke(SlotIndex);

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
        return data.VariantType == Variant.Type.Int && data.AsInt32() != SlotIndex;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        DropReceived?.Invoke(data.AsInt32(), SlotIndex);
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationDragEnd)
            DragEnded?.Invoke(SlotIndex);
    }
}
