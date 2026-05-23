using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class MerchantWindow : Control
{
    [Export]
    public NodePath WindowPanelPath { get; set; } = new("Panel");

    [Export]
    public NodePath TitleLabelPath { get; set; } = new("Panel/Margin/VBox/Header/Title");

    [Export]
    public NodePath CloseButtonPath { get; set; } = new("Panel/Margin/VBox/Header/CloseButton");

    [Export]
    public NodePath GoldLabelPath { get; set; } = new("Panel/Margin/VBox/Summary/GoldLabel");

    [Export]
    public NodePath OffersContainerPath { get; set; } = new("Panel/Margin/VBox/Offers");

    [Export]
    public NodePath RefreshButtonPath { get; set; } = new("Panel/Margin/VBox/Footer/RefreshButton");

    private InventoryController _inventory;
    private MerchantStock _stock;
    private Control _windowPanel;
    private Label _titleLabel;
    private Button _closeButton;
    private Label _goldLabel;
    private VBoxContainer _offersContainer;
    private Button _refreshButton;
    private WindowDragger _windowDragger;
    private bool _panelPositioned;
    private readonly List<OfferRow> _rows = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _windowPanel = GetNodeOrNull<Control>(WindowPanelPath);
        _titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
        _closeButton = GetNodeOrNull<Button>(CloseButtonPath);
        _goldLabel = GetNodeOrNull<Label>(GoldLabelPath);
        _offersContainer = GetNodeOrNull<VBoxContainer>(OffersContainerPath);
        _refreshButton = GetNodeOrNull<Button>(RefreshButtonPath);

        if (_windowPanel != null)
        {
            _windowDragger = new WindowDragger(this, _windowPanel)
            {
                BringToFront = FocusWindow,
            };
        }

        if (_closeButton != null)
            _closeButton.Pressed += CloseWindow;

        if (_refreshButton != null)
            _refreshButton.Pressed += OnRefreshPressed;
    }

    public override void _ExitTree()
    {
        if (_closeButton != null)
            _closeButton.Pressed -= CloseWindow;

        if (_refreshButton != null)
            _refreshButton.Pressed -= OnRefreshPressed;

        _windowDragger?.Detach();
        UnbindInventory();
        UnbindStock();
    }

    public void Open(InventoryController inventory, MerchantStock stock)
    {
        if (inventory == null || !GodotObject.IsInstanceValid(inventory))
            return;
        if (stock == null || !GodotObject.IsInstanceValid(stock))
            return;

        BindInventory(inventory);
        BindStock(stock);

        Visible = true;
        CenterPanelOnce();
        _windowDragger?.ClampToViewport();
        FocusWindow();

        _stock?.EnsureStockBuilt();
        Refresh();
    }

    public void CloseWindow()
    {
        Visible = false;

        // Release room-local references so the HUD-level window does not keep
        // a stale pointer at a MerchantStock that may be freed with the room.
        // Reopening goes through Open() which rebinds via the idempotent
        // Bind* helpers.
        UnbindInventory();
        UnbindStock();
    }

    public void FocusWindow()
    {
        MoveToFront();
    }

    private void OnInventoryChanged()
    {
        Refresh();
    }

    private void OnGoldChanged(int totalGold)
    {
        Refresh();
    }

    private void OnStockChanged()
    {
        Refresh();
    }

    private void OnRefreshPressed()
    {
        if (_stock == null || _inventory == null)
            return;

        _stock.TryRefresh(_inventory);
    }

    private void Refresh()
    {
        if (_titleLabel != null)
            _titleLabel.Text = _stock?.DisplayName ?? "Merchant";

        var gold = _inventory != null && GodotObject.IsInstanceValid(_inventory) ? _inventory.Gold : 0;
        if (_goldLabel != null)
            _goldLabel.Text = $"Gold: {gold}";

        if (_refreshButton != null)
        {
            var cost = _stock?.RefreshCost ?? 0;
            _refreshButton.Text = cost > 0 ? $"Refresh ({cost}g)" : "Refresh";
            _refreshButton.Disabled = _stock == null || _inventory == null || gold < cost;
        }

        RebuildOfferRows();
    }

    private void RebuildOfferRows()
    {
        if (_offersContainer == null)
            return;

        // Tear down any stale rows.
        foreach (var row in _rows)
        {
            if (GodotObject.IsInstanceValid(row.BuyButton))
                row.BuyButton.Pressed -= row.OnPressed;
            if (GodotObject.IsInstanceValid(row.Root))
                row.Root.QueueFree();
        }
        _rows.Clear();

        if (_stock == null)
            return;

        var offers = _stock.Offers;
        for (var i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            if (offer == null)
                continue;

            var row = BuildOfferRow(offer, i);
            _offersContainer.AddChild(row.Root);
            _rows.Add(row);
        }
    }

    private OfferRow BuildOfferRow(MerchantOffer offer, int offerIndex)
    {
        var root = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        root.AddThemeConstantOverride("separation", 8);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = offer.Icon,
        };
        if (offer.Kind == MerchantOfferKind.GeneratedGear && offer.Gear != null)
            icon.Modulate = GearQualityColors.GetColor(offer.Gear.Quality);
        root.AddChild(icon);

        var label = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Pass,
            Text = BuildOfferLabel(offer),
            TooltipText = BuildOfferTooltip(offer),
        };
        root.AddChild(label);

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
                $"{offer.Gear.Quality} {offer.Gear.Slot}",
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

    private void CenterPanelOnce()
    {
        if (_panelPositioned || _windowPanel == null || !GodotObject.IsInstanceValid(_windowPanel))
            return;

        var size = _windowPanel.Size;
        if (size == Vector2.Zero)
            size = _windowPanel.GetCombinedMinimumSize();

        var viewportSize = GetViewportRect().Size;
        _windowPanel.GlobalPosition = (viewportSize - size) * 0.5f;
        _panelPositioned = true;
    }

    private void BindInventory(InventoryController inventory)
    {
        if (ReferenceEquals(_inventory, inventory))
            return;

        UnbindInventory();
        _inventory = inventory;

        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return;

        if (!_inventory.IsConnected(InventoryController.SignalName.InventoryChanged, new Callable(this, nameof(OnInventoryChanged))))
            _inventory.Connect(InventoryController.SignalName.InventoryChanged, new Callable(this, nameof(OnInventoryChanged)));

        if (!_inventory.IsConnected(InventoryController.SignalName.GoldChanged, new Callable(this, nameof(OnGoldChanged))))
            _inventory.Connect(InventoryController.SignalName.GoldChanged, new Callable(this, nameof(OnGoldChanged)));
    }

    private void UnbindInventory()
    {
        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
        {
            _inventory = null;
            return;
        }

        var changedCallable = new Callable(this, nameof(OnInventoryChanged));
        if (_inventory.IsConnected(InventoryController.SignalName.InventoryChanged, changedCallable))
            _inventory.Disconnect(InventoryController.SignalName.InventoryChanged, changedCallable);

        var goldCallable = new Callable(this, nameof(OnGoldChanged));
        if (_inventory.IsConnected(InventoryController.SignalName.GoldChanged, goldCallable))
            _inventory.Disconnect(InventoryController.SignalName.GoldChanged, goldCallable);

        _inventory = null;
    }

    private void BindStock(MerchantStock stock)
    {
        if (ReferenceEquals(_stock, stock))
            return;

        UnbindStock();
        _stock = stock;

        if (_stock == null || !GodotObject.IsInstanceValid(_stock))
            return;

        if (!_stock.IsConnected(MerchantStock.SignalName.StockChanged, new Callable(this, nameof(OnStockChanged))))
            _stock.Connect(MerchantStock.SignalName.StockChanged, new Callable(this, nameof(OnStockChanged)));
    }

    private void UnbindStock()
    {
        if (_stock == null || !GodotObject.IsInstanceValid(_stock))
        {
            _stock = null;
            return;
        }

        var callable = new Callable(this, nameof(OnStockChanged));
        if (_stock.IsConnected(MerchantStock.SignalName.StockChanged, callable))
            _stock.Disconnect(MerchantStock.SignalName.StockChanged, callable);

        _stock = null;
    }

    private sealed class OfferRow
    {
        public Control Root;
        public Button BuyButton;
        public int OfferIndex;
        public System.Action OnPressed;
    }
}
