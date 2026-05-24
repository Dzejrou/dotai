using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class MerchantSellListPanel : VBoxContainer
{
    private InventoryController _inventory;
    private readonly List<SellRow> _rows = new();

    public override void _ExitTree()
    {
        Unbind();
    }

    public void Bind(InventoryController inventory)
    {
        _inventory = inventory;
    }

    // Drops the inventory reference and tears down any rendered rows.
    // MerchantWindow calls this on close so the panel mirrors the parent's
    // unbind path and does not retain a stale inventory pointer.
    public void Unbind()
    {
        ClearRows();
        _inventory = null;
    }

    public void Refresh()
    {
        ClearRows();

        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return;

        var rules = _inventory.GearGenerationRules;
        var slotCount = _inventory.GetSlotCount();
        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            if (!_inventory.TryGetEntry(slotIndex, out var entry))
                continue;

            if (entry is InventoryGearEntry gearEntry)
            {
                var price = GearSellPricing.GetSellPrice(gearEntry.Gear, rules);
                if (price <= 0)
                    continue;

                var row = BuildGearSellRow(gearEntry.Gear, slotIndex, price);
                AddChild(row.Root);
                _rows.Add(row);
                continue;
            }

            if (entry is InventoryStackEntry stackEntry)
            {
                var item = stackEntry.Stack.Item;
                if (item == null || item.SellPrice <= 0)
                    continue;

                var row = BuildStackSellRow(stackEntry, slotIndex);
                AddChild(row.Root);
                _rows.Add(row);
            }
        }
    }

    private void ClearRows()
    {
        foreach (var row in _rows)
        {
            if (GodotObject.IsInstanceValid(row.SellButton))
                row.SellButton.Pressed -= row.OnPressed;
            if (GodotObject.IsInstanceValid(row.Root))
                row.Root.QueueFree();
        }
        _rows.Clear();
    }

    private SellRow BuildGearSellRow(GearInstance gear, int slotIndex, int price)
    {
        var root = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        root.AddThemeConstantOverride("separation", 8);

        AddSellIconAndNameGroup(root, gear);

        var levelLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = $"Lv {gear.Level}",
            CustomMinimumSize = new Vector2(48, 0),
        };
        root.AddChild(levelLabel);

        var priceLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = $"{price}g",
        };
        root.AddChild(priceLabel);

        var sellButton = new Button
        {
            Text = "Sell",
            CustomMinimumSize = new Vector2(72, 0),
        };
        root.AddChild(sellButton);

        var row = new SellRow
        {
            Kind = SellRowKind.Gear,
            Root = root,
            SellButton = sellButton,
            SlotIndex = slotIndex,
            Price = price,
        };
        row.OnPressed = () => OnGearSellPressed(row);
        sellButton.Pressed += row.OnPressed;

        return row;
    }

    private SellRow BuildStackSellRow(InventoryStackEntry stackEntry, int slotIndex)
    {
        var item = stackEntry.Stack.Item;
        var price = item.SellPrice;

        var root = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        root.AddThemeConstantOverride("separation", 8);

        var iconAndName = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        iconAndName.AddThemeConstantOverride("separation", 8);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = item.Icon,
        };
        iconAndName.AddChild(icon);

        var nameLabel = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = item.DisplayName,
        };
        iconAndName.AddChild(nameLabel);

        root.AddChild(iconAndName);

        var quantityLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = $"x{stackEntry.Stack.Quantity}",
            CustomMinimumSize = new Vector2(48, 0),
        };
        root.AddChild(quantityLabel);

        var priceLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = $"{price}g ea",
        };
        root.AddChild(priceLabel);

        var sellButton = new Button
        {
            Text = "Sell",
            CustomMinimumSize = new Vector2(72, 0),
        };
        root.AddChild(sellButton);

        var row = new SellRow
        {
            Kind = SellRowKind.Stack,
            Root = root,
            SellButton = sellButton,
            SlotIndex = slotIndex,
            Price = price,
            StackItemId = item.Id,
        };
        row.OnPressed = () => OnStackSellPressed(row);
        sellButton.Pressed += row.OnPressed;

        return row;
    }

    private static void AddSellIconAndNameGroup(HBoxContainer root, GearInstance gear)
    {
        // Owned gear: reveal all substats in the tooltip.
        var group = new MerchantGearOfferRow
        {
            Gear = gear,
            RevealedSubstatCount = int.MaxValue,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        group.AddThemeConstantOverride("separation", 8);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = gear.Definition?.Icon,
            Modulate = ItemQualityColors.GetColor(gear.Quality),
        };
        group.AddChild(icon);

        var label = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = gear.Slot.ToString(),
            SelfModulate = ItemQualityColors.GetColor(gear.Quality),
        };
        group.AddChild(label);

        root.AddChild(group);
    }

    private void OnGearSellPressed(SellRow row)
    {
        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return;
        if (row == null)
            return;

        // Re-validate price against the live entry so a stale row cannot pay an outdated amount.
        if (!_inventory.TryGetEntry(row.SlotIndex, out var entry))
            return;
        if (entry is not InventoryGearEntry gearEntry)
            return;

        var rules = _inventory.GearGenerationRules;
        var price = GearSellPricing.GetSellPrice(gearEntry.Gear, rules);
        if (price <= 0)
            return;

        var taken = _inventory.TakeEntry(row.SlotIndex);
        if (taken is not InventoryGearEntry)
        {
            // Defensive: if something else was at this slot, drop it back in to keep state sane.
            if (taken != null)
                GD.PushWarning($"{nameof(MerchantSellListPanel)}: sell aborted; entry at slot {row.SlotIndex} was not a gear entry.");
            return;
        }

        _inventory.AddGold(price);
    }

    private void OnStackSellPressed(SellRow row)
    {
        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return;
        if (row == null)
            return;

        // Re-validate the live slot so a stale row cannot pay gold for a changed entry.
        if (!_inventory.TryGetEntry(row.SlotIndex, out var entry))
            return;
        if (entry is not InventoryStackEntry stackEntry)
            return;

        var item = stackEntry.Stack.Item;
        if (item == null || item.SellPrice <= 0)
            return;
        if (!string.Equals(item.Id, row.StackItemId, System.StringComparison.Ordinal))
            return;

        var consumed = _inventory.TryConsumeFromStackSlot(row.SlotIndex, item.Id, 1);
        if (consumed <= 0)
            return;

        _inventory.AddGold(item.SellPrice * consumed);
    }

    private enum SellRowKind
    {
        Gear,
        Stack,
    }

    private sealed class SellRow
    {
        public SellRowKind Kind;
        public Control Root;
        public Button SellButton;
        public int SlotIndex;
        public int Price;
        public string StackItemId;
        public System.Action OnPressed;
    }
}
