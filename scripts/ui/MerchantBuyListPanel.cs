using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class MerchantBuyListPanel : VBoxContainer
{
    private InventoryController _inventory;
    private MerchantStock _stock;
    private readonly List<OfferRow> _rows = new();

    public override void _ExitTree()
    {
        Unbind();
    }

    public void Bind(InventoryController inventory, MerchantStock stock)
    {
        _inventory = inventory;
        _stock = stock;
    }

    // Drops references to the inventory/stock and tears down any rendered rows.
    // MerchantWindow calls this on close so that closing the HUD-level window
    // does not keep a stale pointer at a room-local MerchantStock that may be
    // freed with the room.
    public void Unbind()
    {
        ClearRows();
        _inventory = null;
        _stock = null;
    }

    public void Refresh()
    {
        ClearRows();

        if (_stock == null || !GodotObject.IsInstanceValid(_stock))
            return;

        var offers = _stock.Offers;
        for (var i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            if (offer == null)
                continue;

            var row = BuildOfferRow(offer, i);
            AddChild(row.Root);
            _rows.Add(row);
        }
    }

    private void ClearRows()
    {
        foreach (var row in _rows)
        {
            if (GodotObject.IsInstanceValid(row.BuyButton))
                row.BuyButton.Pressed -= row.OnPressed;
            if (GodotObject.IsInstanceValid(row.Root))
                row.Root.QueueFree();
        }
        _rows.Clear();
    }

    private OfferRow BuildOfferRow(MerchantOffer offer, int offerIndex)
    {
        var root = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        root.AddThemeConstantOverride("separation", 8);

        AddIconAndNameGroup(root, offer);

        var priceLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = $"{offer.Price}g",
        };
        root.AddChild(priceLabel);

        var buyButton = new Button
        {
            Text = offer.Purchased ? "Sold" : "Buy",
            CustomMinimumSize = new Vector2(72, 0),
        };
        root.AddChild(buyButton);

        var canAfford = _inventory != null &&
            GodotObject.IsInstanceValid(_inventory) &&
            _inventory.Gold >= offer.Price;
        var canAccept = _stock != null && _stock.CanInventoryAccept(offer, _inventory);
        buyButton.Disabled = offer.Purchased || !canAfford || !canAccept;

        var row = new OfferRow
        {
            Root = root,
            BuyButton = buyButton,
            OfferIndex = offerIndex,
        };
        row.OnPressed = () => OnBuyPressed(row);
        buyButton.Pressed += row.OnPressed;

        return row;
    }

    // Builds the icon + name portion of an offer row. For generated gear we wrap both
    // controls in a MerchantGearOfferRow so hovering either one surfaces the same custom
    // GearTooltipFactory tooltip used by inventory and equipped gear. Other offers keep
    // a plain default tooltip on the name label.
    private static void AddIconAndNameGroup(HBoxContainer root, MerchantOffer offer)
    {
        var isGear = offer.Kind == MerchantOfferKind.GeneratedGear && offer.Gear != null;

        HBoxContainer group;
        if (isGear)
        {
            group = new MerchantGearOfferRow
            {
                Gear = offer.Gear,
                RevealedSubstatCount = offer.RevealedSubstatCount,
            };
        }
        else
        {
            group = new HBoxContainer { TooltipText = BuildOfferTooltip(offer) };
        }
        group.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        // Stop so the group owns the hover area and receives tooltip events even when
        // its children (icon, label) use MouseFilter.Ignore.
        group.MouseFilter = Control.MouseFilterEnum.Stop;
        group.AddThemeConstantOverride("separation", 8);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = offer.Icon,
        };
        group.AddChild(icon);

        var label = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = BuildOfferLabel(offer),
        };
        if (isGear)
            label.SelfModulate = ItemQualityColors.GetColor(offer.Gear.Quality);
        group.AddChild(label);

        root.AddChild(group);
    }

    private void OnBuyPressed(OfferRow row)
    {
        if (_stock == null || _inventory == null)
            return;
        _stock.TryPurchase(row.OfferIndex, _inventory);
    }

    private static string BuildOfferLabel(MerchantOffer offer)
    {
        return offer.Kind switch
        {
            MerchantOfferKind.StackItem => offer.StackQuantity > 1
                ? $"{offer.DisplayName} x{offer.StackQuantity}"
                : offer.DisplayName,
            MerchantOfferKind.GeneratedGear when offer.Gear != null =>
                offer.Gear.Slot.ToString(),
            _ => offer.DisplayName,
        };
    }

    private static string BuildOfferTooltip(MerchantOffer offer)
    {
        if (offer.Kind == MerchantOfferKind.GeneratedGear && offer.Gear != null)
            return GearTooltipBuilder.Build(offer.Gear);

        if (offer.Kind == MerchantOfferKind.StackItem && offer.StackItem != null)
            return offer.StackItem.DisplayName;

        return string.Empty;
    }

    private sealed class OfferRow
    {
        public Control Root;
        public Button BuyButton;
        public int OfferIndex;
        public System.Action OnPressed;
    }
}
