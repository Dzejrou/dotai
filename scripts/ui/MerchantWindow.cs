using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class MerchantWindow : Control
{
    // Raised when the player asks to leave the page (its close button). Pause ownership lives in
    // Main, so the page never unpauses itself; Main listens and runs the close+unpause sequence.
    [Signal]
    public delegate void CloseRequestedEventHandler();

    private enum Mode
    {
        Buy,
        Sell,
        Buyback,
    }

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
    public NodePath BuybackTabButtonPath { get; set; } = new("Panel/Margin/VBox/ModeBar/BuybackTabButton");

    [Export]
    public NodePath ListScrollPath { get; set; } = new("Panel/Margin/VBox/ListScroll");

    [Export]
    public NodePath BuyListPanelPath { get; set; } = new("Panel/Margin/VBox/ListScroll/ListHost/Offers");

    [Export]
    public NodePath SellListPanelPath { get; set; } = new("Panel/Margin/VBox/ListScroll/ListHost/SellList");

    [Export]
    public NodePath BuybackListPanelPath { get; set; } = new("Panel/Margin/VBox/ListScroll/ListHost/BuybackList");

    [Export]
    public NodePath RefreshButtonPath { get; set; } = new("Panel/Margin/VBox/Footer/RefreshButton");

    [Export]
    public NodePath SellModeButtonPath { get; set; } = new("Panel/Margin/VBox/Footer/SellModeButton");

    private InventoryController _inventory;
    private ICurrencyWallet _buyWallet;
    private ICurrencyWallet _buyWalletOverride;
    private MerchantStock _stock;
    private Label _titleLabel;
    private Button _closeButton;
    private Label _goldLabel;
    private Button _buyTabButton;
    private Button _sellTabButton;
    private Button _buybackTabButton;
    private MerchantBuyListPanel _buyListPanel;
    private MerchantSellListPanel _sellListPanel;
    private MerchantBuybackListPanel _buybackListPanel;
    private ScrollContainer _listScroll;
    private Button _refreshButton;
    private Button _sellModeButton;
    private Mode _mode = Mode.Buy;
    private MerchantSellQuantityMode _sellQuantityMode = MerchantSellQuantityMode.One;
    private readonly List<MerchantBuybackEntry> _buybackEntries = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
        _closeButton = GetNodeOrNull<Button>(CloseButtonPath);
        _goldLabel = GetNodeOrNull<Label>(GoldLabelPath);
        _buyTabButton = GetNodeOrNull<Button>(BuyTabButtonPath);
        _sellTabButton = GetNodeOrNull<Button>(SellTabButtonPath);
        _buybackTabButton = GetNodeOrNull<Button>(BuybackTabButtonPath);
        _buyListPanel = GetNodeOrNull<MerchantBuyListPanel>(BuyListPanelPath);
        _sellListPanel = GetNodeOrNull<MerchantSellListPanel>(SellListPanelPath);
        _buybackListPanel = GetNodeOrNull<MerchantBuybackListPanel>(BuybackListPanelPath);
        _listScroll = GetNodeOrNull<ScrollContainer>(ListScrollPath);
        _refreshButton = GetNodeOrNull<Button>(RefreshButtonPath);
        _sellModeButton = GetNodeOrNull<Button>(SellModeButtonPath);

        if (_sellListPanel != null)
            _sellListPanel.OnItemSold = OnItemSold;

        if (_closeButton != null)
            _closeButton.Pressed += OnCloseButtonPressed;

        if (_refreshButton != null)
            _refreshButton.Pressed += OnRefreshPressed;

        if (_sellModeButton != null)
            _sellModeButton.Pressed += OnSellModePressed;

        if (_buyTabButton != null)
            _buyTabButton.Pressed += OnBuyTabPressed;

        if (_sellTabButton != null)
            _sellTabButton.Pressed += OnSellTabPressed;

        if (_buybackTabButton != null)
            _buybackTabButton.Pressed += OnBuybackTabPressed;

        ApplyModeVisibility();
    }

    public override void _ExitTree()
    {
        if (_closeButton != null)
            _closeButton.Pressed -= OnCloseButtonPressed;

        if (_refreshButton != null)
            _refreshButton.Pressed -= OnRefreshPressed;

        if (_sellModeButton != null)
            _sellModeButton.Pressed -= OnSellModePressed;

        if (_buyTabButton != null)
            _buyTabButton.Pressed -= OnBuyTabPressed;

        if (_sellTabButton != null)
            _sellTabButton.Pressed -= OnSellTabPressed;

        if (_buybackTabButton != null)
            _buybackTabButton.Pressed -= OnBuybackTabPressed;

        if (_sellListPanel != null)
            _sellListPanel.OnItemSold = null;

        _buyListPanel?.Unbind();
        _sellListPanel?.Unbind();
        _buybackListPanel?.Unbind();
        _buybackEntries.Clear();
        UnbindInventory();
        UnbindStock();
    }

    // buyWallet selects the Buy currency: null falls back to Gold (ordinary merchants), or an
    // injected wallet such as Dungeon Points for the Dungeon Shop. Sell and Buyback always use Gold
    // through the InventoryController regardless of this wallet.
    public void Open(InventoryController inventory, MerchantStock stock, ICurrencyWallet buyWallet = null)
    {
        if (inventory == null || !GodotObject.IsInstanceValid(inventory))
            return;
        if (stock == null || !GodotObject.IsInstanceValid(stock))
            return;

        BindInventory(inventory);
        BindStock(stock);
        _buyWalletOverride = buyWallet;

        Visible = true;

        _stock?.EnsureStockBuilt();
        // Re-evaluate per-mode + stock-driven visibility (e.g. the Refresh button) now that the
        // stock is bound, then push the data into the panels.
        ApplyModeVisibility();
        Refresh();
    }

    public void CloseWindow()
    {
        Visible = false;

        // Release room-local references so the HUD-level window does not keep
        // a stale pointer at a MerchantStock that may be freed with the room.
        // Child panels mirror this so they do not retain the same stale stock
        // one layer deeper. Reopening goes through Open() which rebinds via
        // the idempotent Bind* helpers and Refresh() re-pushes into the panels.
        _buyListPanel?.Unbind();
        _sellListPanel?.Unbind();
        _buybackListPanel?.Unbind();
        // Buyback is session-local to one merchant interaction. Wipe so that reopening
        // (the same or a different merchant) starts with an empty buyback list.
        _buybackEntries.Clear();
        // Drop any injected Buy wallet so a reopen as an ordinary merchant defaults back to Gold.
        _buyWalletOverride = null;
        _mode = Mode.Buy;
        ApplyModeVisibility();
        UnbindInventory();
        UnbindStock();
    }

    private void OnCloseButtonPressed()
    {
        EmitSignal(SignalName.CloseRequested);
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

    private void OnSellModePressed()
    {
        _sellQuantityMode = _sellQuantityMode.Next();
        Refresh();
    }

    private void OnBuyTabPressed()
    {
        SetMode(Mode.Buy);
    }

    private void OnSellTabPressed()
    {
        SetMode(Mode.Sell);
    }

    private void OnBuybackTabPressed()
    {
        SetMode(Mode.Buyback);
    }

    private void OnItemSold(MerchantBuybackEntry entry)
    {
        if (entry == null)
            return;

        // Newest sales at the top so the most-recent mistake is the easiest to undo.
        _buybackEntries.Insert(0, entry);
        Refresh();
    }

    private void OnBuybackPressed(int entryIndex)
    {
        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return;
        if (entryIndex < 0 || entryIndex >= _buybackEntries.Count)
            return;

        var entry = _buybackEntries[entryIndex];
        if (entry == null)
            return;

        // Re-check capacity and gold against live state so a stale row cannot trigger
        // a partial transaction. The list panel disables the button when it can, but
        // the inventory may have filled up between the panel's last refresh and the
        // click landing here.
        if (!CanInventoryAcceptBuyback(entry))
            return;
        if (_inventory.Gold < entry.Price)
            return;

        if (!_inventory.TrySpendGold(entry.Price))
            return;

        var added = AddBuybackToInventory(entry);
        if (!added)
        {
            // Capacity check passed but add failed (e.g. slot vacated between check and add).
            // Refund defensively so the player isn't out of gold for nothing.
            _inventory.AddGold(entry.Price);
            return;
        }

        _buybackEntries.RemoveAt(entryIndex);
        Refresh();
    }

    private bool CanInventoryAcceptBuyback(MerchantBuybackEntry entry)
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

    private bool AddBuybackToInventory(MerchantBuybackEntry entry)
    {
        switch (entry.Kind)
        {
            case MerchantBuybackEntryKind.Gear:
                return _inventory.AddGear(entry.Gear);
            case MerchantBuybackEntryKind.Stack:
                var remaining = _inventory.AddItem(entry.StackItem, entry.StackQuantity);
                return remaining == 0;
        }

        return false;
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

        // The three lists share one ScrollContainer, so start the newly shown tab at the top
        // instead of inheriting a stale scroll offset from the previous (possibly longer) list.
        if (_listScroll != null)
            _listScroll.ScrollVertical = 0;
    }

    private void ApplyModeVisibility()
    {
        if (_buyTabButton != null)
            _buyTabButton.ButtonPressed = _mode == Mode.Buy;
        if (_sellTabButton != null)
            _sellTabButton.ButtonPressed = _mode == Mode.Sell;
        if (_buybackTabButton != null)
            _buybackTabButton.ButtonPressed = _mode == Mode.Buyback;

        if (_buyListPanel != null)
            _buyListPanel.Visible = _mode == Mode.Buy;
        if (_sellListPanel != null)
            _sellListPanel.Visible = _mode == Mode.Sell;
        if (_buybackListPanel != null)
            _buybackListPanel.Visible = _mode == Mode.Buyback;
        // Refresh is conditional on the bound stock, not the shop type: shown only for stock that
        // can change on refresh (limited/dynamic offers). Unlimited-only stock (the Dungeon Shop)
        // hides it. Applied here so it tracks both the active tab and the bound stock.
        if (_refreshButton != null)
            _refreshButton.Visible = _mode == Mode.Buy && (_stock?.SupportsRefresh ?? false);
        if (_sellModeButton != null)
            _sellModeButton.Visible = _mode == Mode.Sell;
    }

    private void Refresh()
    {
        if (_titleLabel != null)
            _titleLabel.Text = _stock?.DisplayName ?? "Merchant";

        var buyWallet = ResolveBuyWallet();
        var gold = _inventory != null && GodotObject.IsInstanceValid(_inventory) ? _inventory.Gold : 0;
        if (_goldLabel != null)
        {
            // Show the currency relevant to the active tab: the Buy wallet on Buy (Gold for ordinary
            // merchants, DP for the Dungeon Shop), Gold on Sell/Buyback which are always Gold-based.
            _goldLabel.Text = _mode == Mode.Buy && buyWallet != null
                ? $"{buyWallet.Label}: {buyWallet.Balance}"
                : $"Gold: {gold}";
        }

        if (_refreshButton != null)
        {
            // Visibility is owned by ApplyModeVisibility (stock-aware); here we only keep the
            // cost/disabled state current. Refresh always costs Gold.
            var cost = _stock?.RefreshCost ?? 0;
            _refreshButton.Text = cost > 0 ? $"Refresh ({cost}g)" : "Refresh";
            _refreshButton.Disabled = _stock == null || _inventory == null || gold < cost;
        }

        if (_sellModeButton != null)
            _sellModeButton.Text = _sellQuantityMode.GetButtonLabel();

        if (_buyListPanel != null)
        {
            // The Buy panel uses the Buy wallet (Gold or DP). Sell/Buyback below stay Gold-based
            // through the InventoryController directly and are never handed this wallet.
            _buyListPanel.Bind(_inventory, _stock, buyWallet);
            _buyListPanel.Refresh();
        }

        if (_sellListPanel != null)
        {
            _sellListPanel.Bind(_inventory);
            _sellListPanel.SetSellQuantityMode(_sellQuantityMode);
            _sellListPanel.Refresh();
        }

        if (_buybackListPanel != null)
        {
            _buybackListPanel.Bind(_inventory, _buybackEntries, OnBuybackPressed);
            _buybackListPanel.Refresh();
        }
    }

    // Lazily builds the Gold buy wallet over the bound inventory, cached until the inventory
    // unbinds. Returns null when no valid inventory is bound so the buy panel renders disabled.
    private ICurrencyWallet ResolveBuyWallet()
    {
        // An injected wallet (e.g. Dungeon Points) wins; otherwise fall back to the cached Gold
        // wallet over the bound inventory (ordinary merchants).
        if (_buyWalletOverride != null)
            return _buyWalletOverride;

        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return null;

        return _buyWallet ??= new GoldWallet(_inventory);
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
        // Drop the cached buy wallet so a rebind to a different inventory builds a fresh one.
        _buyWallet = null;

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
        // Buyback is local to one merchant interaction. Switching to a different
        // stock counts as a new interaction even if the window stays open.
        _buybackEntries.Clear();
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
