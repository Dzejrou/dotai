using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class MerchantBuybackListPanel : VBoxContainer
{
    private InventoryController _inventory;
    private IReadOnlyList<MerchantBuybackEntry> _entries;
    private Action<int> _onBuybackPressed;
    private readonly List<BuybackRow> _rows = new();

    public override void _ExitTree()
    {
        Unbind();
    }

    public void Bind(
        InventoryController inventory,
        IReadOnlyList<MerchantBuybackEntry> entries,
        Action<int> onBuybackPressed)
    {
        _inventory = inventory;
        _entries = entries;
        _onBuybackPressed = onBuybackPressed;
    }

    public void Unbind()
    {
        ClearRows();
        _inventory = null;
        _entries = null;
        _onBuybackPressed = null;
    }

    public void Refresh()
    {
        ClearRows();

        if (_entries == null)
            return;

        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry == null)
                continue;

            var row = BuildRow(entry, i);
            AddChild(row.Root);
            _rows.Add(row);
        }
    }

    private void ClearRows()
    {
        foreach (var row in _rows)
        {
            if (GodotObject.IsInstanceValid(row.BuybackButton))
                row.BuybackButton.Pressed -= row.OnPressed;
            if (GodotObject.IsInstanceValid(row.Root))
                row.Root.QueueFree();
        }
        _rows.Clear();
    }

    private BuybackRow BuildRow(MerchantBuybackEntry entry, int entryIndex)
    {
        var root = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        root.AddThemeConstantOverride("separation", 8);

        AddIconAndNameGroup(root, entry);

        if (entry.Kind == MerchantBuybackEntryKind.Stack)
        {
            var quantityLabel = new Label
            {
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Text = $"x{entry.StackQuantity}",
                CustomMinimumSize = new Vector2(48, 0),
            };
            root.AddChild(quantityLabel);
        }
        else if (entry.Gear != null)
        {
            var levelLabel = new Label
            {
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Text = $"Lv {entry.Gear.Level}",
                CustomMinimumSize = new Vector2(48, 0),
            };
            root.AddChild(levelLabel);
        }

        var priceLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = $"{entry.Price}g",
        };
        root.AddChild(priceLabel);

        var buybackButton = new Button
        {
            Text = "Buy back",
            CustomMinimumSize = new Vector2(96, 0),
        };
        root.AddChild(buybackButton);

        var canAfford = _inventory != null &&
            GodotObject.IsInstanceValid(_inventory) &&
            _inventory.Gold >= entry.Price;
        var canAccept = CanInventoryAccept(entry);
        buybackButton.Disabled = !canAfford || !canAccept;

        var row = new BuybackRow
        {
            Root = root,
            BuybackButton = buybackButton,
            EntryIndex = entryIndex,
        };
        row.OnPressed = () => OnBuybackPressed(row);
        buybackButton.Pressed += row.OnPressed;

        return row;
    }

    private bool CanInventoryAccept(MerchantBuybackEntry entry)
    {
        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return false;

        return entry.Kind switch
        {
            MerchantBuybackEntryKind.Gear => entry.Gear != null && _inventory.CanAddGear(entry.Gear),
            MerchantBuybackEntryKind.Stack => entry.StackItem != null &&
                _inventory.CanAddItem(entry.StackItem, entry.StackQuantity),
            _ => false,
        };
    }

    private void OnBuybackPressed(BuybackRow row)
    {
        _onBuybackPressed?.Invoke(row.EntryIndex);
    }

    private static void AddIconAndNameGroup(HBoxContainer root, MerchantBuybackEntry entry)
    {
        var isGear = entry.Kind == MerchantBuybackEntryKind.Gear && entry.Gear != null;

        HBoxContainer group;
        if (isGear)
        {
            group = new MerchantGearOfferRow
            {
                Gear = entry.Gear,
                RevealedSubstatCount = int.MaxValue,
            };
        }
        else
        {
            group = new HBoxContainer
            {
                TooltipText = entry.StackItem?.DisplayName ?? string.Empty,
            };
        }
        group.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        group.MouseFilter = Control.MouseFilterEnum.Stop;
        group.AddThemeConstantOverride("separation", 8);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = isGear ? entry.Gear?.Definition?.Icon : entry.StackItem?.Icon,
        };
        group.AddChild(icon);

        var label = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = BuildLabel(entry),
        };
        if (isGear)
            label.SelfModulate = ItemQualityColors.GetColor(entry.Gear.Quality);
        else if (entry.StackItem != null)
            label.SelfModulate = ItemQualityColors.GetColor(entry.StackItem.Quality);
        group.AddChild(label);

        root.AddChild(group);
    }

    private static string BuildLabel(MerchantBuybackEntry entry)
    {
        return entry.Kind switch
        {
            MerchantBuybackEntryKind.Gear when entry.Gear != null => entry.Gear.Slot.ToString(),
            MerchantBuybackEntryKind.Stack when entry.StackItem != null => entry.StackItem.DisplayName,
            _ => string.Empty,
        };
    }

    private sealed class BuybackRow
    {
        public Control Root;
        public Button BuybackButton;
        public int EntryIndex;
        public Action OnPressed;
    }
}
