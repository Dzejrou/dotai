using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class MerchantSellListPanel : VBoxContainer
{
    private InventoryController _inventory;
    private MerchantSellQuantityMode _sellQuantityMode = MerchantSellQuantityMode.One;
    private readonly List<SellRow> _rows = new();

    // Owner (e.g. MerchantWindow) sets this to receive sold items for session-local buyback.
    // Fired after gold has been paid out, on successful sale only.
    public System.Action<MerchantBuybackEntry> OnItemSold { get; set; }

    public override void _ExitTree()
    {
        Unbind();
    }

    public void Bind(InventoryController inventory)
    {
        _inventory = inventory;
    }

    public void SetSellQuantityMode(MerchantSellQuantityMode mode)
    {
        _sellQuantityMode = mode;
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
            SelfModulate = ItemQualityColors.GetColor(item.Quality),
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

        var projected = _sellQuantityMode.ResolveSellQuantity(stackEntry.Stack.Quantity);
        var priceLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = $"{price}g ea / {price * projected}g",
        };
        root.AddChild(priceLabel);

        var sellButton = new Button
        {
            Text = "Sell",
            CustomMinimumSize = new Vector2(72, 0),
            Disabled = projected <= 0,
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
        if (taken is not InventoryGearEntry takenGearEntry)
        {
            // Defensive: if something else was at this slot, drop it back in to keep state sane.
            if (taken != null)
                GD.PushWarning($"{nameof(MerchantSellListPanel)}: sell aborted; entry at slot {row.SlotIndex} was not a gear entry.");
            return;
        }

        _inventory.AddGold(price);
        OnItemSold?.Invoke(MerchantBuybackEntry.ForGear(takenGearEntry.Gear, price));
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

        // Compute the request from the live current quantity, not stale row text. The mode
        // is a cap; partial stacks shorter than the cap sell whatever they have.
        var requested = _sellQuantityMode.ResolveSellQuantity(stackEntry.Stack.Quantity);
        if (requested <= 0)
            return;

        var consumed = _inventory.TryConsumeFromStackSlot(row.SlotIndex, item.Id, requested);
        if (consumed <= 0)
            return;

        // Only pay for what was actually consumed in case the live stack shrank under us.
        var totalPrice = item.SellPrice * consumed;
        _inventory.AddGold(totalPrice);
        OnItemSold?.Invoke(MerchantBuybackEntry.ForStack(item, consumed, totalPrice));
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
