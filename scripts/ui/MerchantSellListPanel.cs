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
            if (entry is not InventoryGearEntry gearEntry)
                continue;

            var price = GearSellPricing.GetSellPrice(gearEntry.Gear, rules);
            if (price <= 0)
                continue;

            var row = BuildSellRow(gearEntry.Gear, slotIndex, price);
            AddChild(row.Root);
            _rows.Add(row);
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

    private SellRow BuildSellRow(GearInstance gear, int slotIndex, int price)
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
            Root = root,
            SellButton = sellButton,
            SlotIndex = slotIndex,
            Price = price,
        };
        row.OnPressed = () => OnSellPressed(row);
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
            Modulate = GearQualityColors.GetColor(gear.Quality),
        };
        group.AddChild(icon);

        var label = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = gear.Slot.ToString(),
            SelfModulate = GearQualityColors.GetColor(gear.Quality),
        };
        group.AddChild(label);

        root.AddChild(group);
    }

    private void OnSellPressed(SellRow row)
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

    private sealed class SellRow
    {
        public Control Root;
        public Button SellButton;
        public int SlotIndex;
        public int Price;
        public System.Action OnPressed;
    }
}
