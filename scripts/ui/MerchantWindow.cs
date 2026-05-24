using Godot;

[GlobalClass]
public partial class MerchantWindow : Control
{
    private enum Mode
    {
        Buy,
        Sell,
    }

    [Export]
    public NodePath WindowPanelPath { get; set; } = new("Panel");

    [Export]
    public NodePath TitleLabelPath { get; set; } = new("Panel/Margin/VBox/Header/Title");

    [Export]
    public NodePath CloseButtonPath { get; set; } = new("Panel/Margin/VBox/Header/CloseButton");

    [Export]
    public NodePath GoldLabelPath { get; set; } = new("Panel/Margin/VBox/Summary/GoldLabel");

    [Export]
    public NodePath BuyTabButtonPath { get; set; } = new("Panel/Margin/VBox/ModeBar/BuyTabButton");

    [Export]
    public NodePath SellTabButtonPath { get; set; } = new("Panel/Margin/VBox/ModeBar/SellTabButton");

    [Export]
    public NodePath BuyListPanelPath { get; set; } = new("Panel/Margin/VBox/Offers");

    [Export]
    public NodePath SellListPanelPath { get; set; } = new("Panel/Margin/VBox/SellList");

    [Export]
    public NodePath RefreshButtonPath { get; set; } = new("Panel/Margin/VBox/Footer/RefreshButton");

    private InventoryController _inventory;
    private MerchantStock _stock;
    private Control _windowPanel;
    private Label _titleLabel;
    private Button _closeButton;
    private Label _goldLabel;
    private Button _buyTabButton;
    private Button _sellTabButton;
    private MerchantBuyListPanel _buyListPanel;
    private MerchantSellListPanel _sellListPanel;
    private Button _refreshButton;
    private WindowDragger _windowDragger;
    private bool _panelPositioned;
    private Mode _mode = Mode.Buy;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _windowPanel = GetNodeOrNull<Control>(WindowPanelPath);
        _titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
        _closeButton = GetNodeOrNull<Button>(CloseButtonPath);
        _goldLabel = GetNodeOrNull<Label>(GoldLabelPath);
        _buyTabButton = GetNodeOrNull<Button>(BuyTabButtonPath);
        _sellTabButton = GetNodeOrNull<Button>(SellTabButtonPath);
        _buyListPanel = GetNodeOrNull<MerchantBuyListPanel>(BuyListPanelPath);
        _sellListPanel = GetNodeOrNull<MerchantSellListPanel>(SellListPanelPath);
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

        if (_buyTabButton != null)
            _buyTabButton.Pressed += OnBuyTabPressed;

        if (_sellTabButton != null)
            _sellTabButton.Pressed += OnSellTabPressed;

        ApplyModeVisibility();
    }

    public override void _ExitTree()
    {
        if (_closeButton != null)
            _closeButton.Pressed -= CloseWindow;

        if (_refreshButton != null)
            _refreshButton.Pressed -= OnRefreshPressed;

        if (_buyTabButton != null)
            _buyTabButton.Pressed -= OnBuyTabPressed;

        if (_sellTabButton != null)
            _sellTabButton.Pressed -= OnSellTabPressed;

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

    private void OnBuyTabPressed()
    {
        SetMode(Mode.Buy);
    }

    private void OnSellTabPressed()
    {
        SetMode(Mode.Sell);
    }

    private void SetMode(Mode mode)
    {
        if (_mode == mode)
        {
            // Keep the active tab pinned even if the user re-clicks it.
            ApplyModeVisibility();
            return;
        }

        _mode = mode;
        ApplyModeVisibility();
        Refresh();
    }

    private void ApplyModeVisibility()
    {
        if (_buyTabButton != null)
            _buyTabButton.ButtonPressed = _mode == Mode.Buy;
        if (_sellTabButton != null)
            _sellTabButton.ButtonPressed = _mode == Mode.Sell;

        if (_buyListPanel != null)
            _buyListPanel.Visible = _mode == Mode.Buy;
        if (_sellListPanel != null)
            _sellListPanel.Visible = _mode == Mode.Sell;
        if (_refreshButton != null)
            _refreshButton.Visible = _mode == Mode.Buy;
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

        if (_buyListPanel != null)
        {
            _buyListPanel.Bind(_inventory, _stock);
            _buyListPanel.Refresh();
        }

        if (_sellListPanel != null)
        {
            _sellListPanel.Bind(_inventory);
            _sellListPanel.Refresh();
        }
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
}
