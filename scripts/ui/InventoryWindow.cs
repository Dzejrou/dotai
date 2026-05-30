using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class InventoryWindow : Control
{
    [Signal]
    public delegate void ItemDroppedToWorldEventHandler(int slotIndex, int amount);

    [Export]
    public string WindowTitle { get; set; } = "Inventory";

    [Export(PropertyHint.Range, "1,20,1")]
    public int Columns { get; set; } = 10;

    [Export(PropertyHint.Range, "1,20,1")]
    public int Rows { get; set; } = 5;

    [Export(PropertyHint.Range, "16,128,1")]
    public int CellSize { get; set; } = 32;

    [Export(PropertyHint.Range, "0,32,1")]
    public int SlotSpacing { get; set; } = 6;

    [Export]
    public NodePath WindowPanelPath { get; set; } = new("Panel");

    [Export]
    public NodePath TitleLabelPath { get; set; } = new("Panel/Margin/VBox/Header/Title");

    [Export]
    public NodePath SummaryLabelPath { get; set; } = new("Panel/Margin/VBox/Summary");

    [Export]
    public NodePath SlotGridPath { get; set; } = new("Panel/Margin/VBox/SlotGrid");

    private readonly List<InventorySlotView> _slotViews = new();
    private InventoryController _inventory;
    private EquipmentController _equipment;
    private Player _player;
    private Control _windowPanel;
    private Label _titleLabel;
    private Label _summaryLabel;
    private GridContainer _slotGrid;
    private SpinBox _amountSpinBox;
    private int _activeDragSlot = -1;
    private int _activeDragAmount = MaxAmountResolved;
    private bool _dragConsumed;
    private WindowDragger _windowDragger;
    private bool _panelPositioned;

    // Top step of the SpinBox renders as "MAX" and resolves to the full source
    // stack at drag time. Picked above any plausible MaxStackSize so the user can
    // still pick an explicit numeric amount on its own steps.
    private const int MaxAmountSentinel = 1000;

    // AmountProvider returns this when MAX is selected; the slot clamps it down
    // to the live source quantity per operation without mutating the SpinBox.
    private const int MaxAmountResolved = int.MaxValue;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _windowPanel = GetNodeOrNull<Control>(WindowPanelPath);
        _titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
        _summaryLabel = GetNodeOrNull<Label>(SummaryLabelPath);
        _slotGrid = GetNodeOrNull<GridContainer>(SlotGridPath);

        if (_windowPanel != null)
        {
            _windowDragger = new WindowDragger(this, _windowPanel)
            {
                BringToFront = FocusWindow,
            };
        }

        EnsureAmountControl();

        InventorySlotControl.DragConsumed += OnExternalDragConsumed;

        ApplyLayout();
        Refresh();
        CallDeferred(MethodName.CenterPanelOnce);
    }

    public override void _ExitTree()
    {
        InventorySlotControl.DragConsumed -= OnExternalDragConsumed;
        _windowDragger?.Detach();
        UnbindCurrentInventory();
    }

    private void OnExternalDragConsumed(int sourceSlotIndex)
    {
        if (_activeDragSlot == sourceSlotIndex)
            _dragConsumed = true;
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

    public void BindPlayer(Player player)
    {
        _player = player;
    }

    public void Bind(InventoryController inventory, EquipmentController equipment = null)
    {
        var inventoryChanged = !ReferenceEquals(_inventory, inventory);

        if (inventoryChanged)
        {
            UnbindCurrentInventory();
            _inventory = inventory;

            if (_inventory != null &&
                GodotObject.IsInstanceValid(_inventory))
            {
                if (!_inventory.IsConnected(InventoryController.SignalName.InventoryChanged, new Callable(this, nameof(OnInventoryChanged))))
                    _inventory.Connect(InventoryController.SignalName.InventoryChanged, new Callable(this, nameof(OnInventoryChanged)));

                if (!_inventory.IsConnected(InventoryController.SignalName.GoldChanged, new Callable(this, nameof(OnGoldChanged))))
                    _inventory.Connect(InventoryController.SignalName.GoldChanged, new Callable(this, nameof(OnGoldChanged)));
            }
        }

        _equipment = equipment;

        if (inventoryChanged)
        {
            ApplyLayout();
        }
        else
        {
            // Slot views already exist; just refresh their EquipmentController reference.
            foreach (var slotView in _slotViews)
                slotView.Root.Inventory = _inventory;
        }

        Refresh();
    }

    public void ToggleWindow()
    {
        SetWindowVisible(!Visible);
    }

    public void CloseWindow()
    {
        SetWindowVisible(false);
    }

    private void SetWindowVisible(bool visible)
    {
        Visible = visible;
        if (visible)
        {
            CenterPanelOnce();
            _windowDragger?.ClampToViewport();
            FocusWindow();
            ResetAmountToMax();
            Refresh();
        }
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
        RefreshSummary();
    }

    private void ApplyLayout()
    {
        if (_titleLabel != null)
            _titleLabel.Text = WindowTitle;

        if (_slotGrid != null)
        {
            _slotGrid.Columns = Math.Max(1, Columns);
            _slotGrid.AddThemeConstantOverride("h_separation", Math.Max(0, SlotSpacing));
            _slotGrid.AddThemeConstantOverride("v_separation", Math.Max(0, SlotSpacing));
        }

        ApplyWindowSize();
        RebuildSlots();

        var expectedSlotCount = GetExpectedSlotCount();
        if (_inventory != null &&
            GodotObject.IsInstanceValid(_inventory) &&
            _inventory.GetSlotCount() != expectedSlotCount)
        {
            GD.PushWarning(
                $"{nameof(InventoryWindow)} is configured for {expectedSlotCount} visible slots, but bound inventory has {_inventory.GetSlotCount()} slots.");
        }
    }

    private void ApplyWindowSize()
    {
        if (_windowPanel == null)
            return;

        const int outerPadding = 48;
        const int titleAreaHeight = 88;
        var clampedColumns = Math.Max(1, Columns);
        var clampedRows = Math.Max(1, Rows);
        var clampedCellSize = Math.Max(16, CellSize);
        var clampedSpacing = Math.Max(0, SlotSpacing);
        var gridWidth = (clampedColumns * clampedCellSize) + ((clampedColumns - 1) * clampedSpacing);
        var gridHeight = (clampedRows * clampedCellSize) + ((clampedRows - 1) * clampedSpacing);
        _windowPanel.CustomMinimumSize = new Vector2(gridWidth + outerPadding, gridHeight + titleAreaHeight);
    }

    private void RebuildSlots()
    {
        if (_slotGrid == null)
            return;

        if (_activeDragSlot >= 0)
            return;

        foreach (var slotView in _slotViews)
        {
            if (GodotObject.IsInstanceValid(slotView.Root))
                slotView.Root.QueueFree();
        }

        _slotViews.Clear();

        var slotCount = GetExpectedSlotCount();
        for (var i = 0; i < slotCount; i++)
        {
            var slotControl = new InventorySlotControl
            {
                SlotIndex = i,
                Inventory = _inventory,
                CustomMinimumSize = new Vector2(CellSize + 10.0f, CellSize + 10.0f),
                MouseFilter = Control.MouseFilterEnum.Stop,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };

            slotControl.AmountProvider = GetSelectedAmount;
            slotControl.DragStarted = (slot, amount) => OnSlotDragStarted(slot, amount);
            slotControl.DropReceived = (from, to, amount) => OnSlotDropReceived(from, to, amount);
            slotControl.DragEnded = (slot) => OnSlotDragEnded(slot);
            slotControl.EquipmentDropReceived = (equipmentSlot, to) => OnEquipmentDropReceived(equipmentSlot, to);
            slotControl.FocusRequested = FocusWindow;
            slotControl.UseRequested = OnSlotUseRequested;

            var margin = new MarginContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            margin.AddThemeConstantOverride("margin_left", 5);
            margin.AddThemeConstantOverride("margin_top", 5);
            margin.AddThemeConstantOverride("margin_right", 5);
            margin.AddThemeConstantOverride("margin_bottom", 5);
            slotControl.AddChild(margin);

            var overlay = new Control
            {
                CustomMinimumSize = new Vector2(CellSize, CellSize),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            margin.AddChild(overlay);

            var iconRect = new TextureRect
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Visible = false,
            };
            iconRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            overlay.AddChild(iconRect);

            var quantityLabel = new Label
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visible = false,
            };
            quantityLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            overlay.AddChild(quantityLabel);

            _slotGrid.AddChild(slotControl);
            _slotViews.Add(new InventorySlotView(slotControl, iconRect, quantityLabel));
        }
    }

    private void Refresh()
    {
        if (_titleLabel != null)
            _titleLabel.Text = WindowTitle;

        var occupiedSlotCount = 0;
        for (var i = 0; i < _slotViews.Count; i++)
        {
            var slotView = _slotViews[i];
            InventoryEntry entry = null;
            var hasEntry = _inventory != null &&
                GodotObject.IsInstanceValid(_inventory) &&
                _inventory.TryGetEntry(i, out entry) &&
                entry?.Definition != null;

            slotView.Root.TooltipText = "Empty";
            slotView.Root.Modulate = hasEntry ? Colors.White : new Color(0.68f, 0.68f, 0.68f, 1.0f);
            slotView.IconRect.Texture = hasEntry ? entry.Icon : null;
            slotView.IconRect.Visible = hasEntry && entry.Icon != null;
            slotView.IconRect.Modulate = Colors.White;
            slotView.QuantityLabel.Visible = hasEntry && entry.ShowQuantity;
            slotView.QuantityLabel.Text = hasEntry && entry.ShowQuantity ? entry.Quantity.ToString() : string.Empty;

            if (hasEntry)
            {
                occupiedSlotCount++;
                slotView.Root.TooltipText = entry.TooltipText;
            }
        }

        RefreshSummary(occupiedSlotCount);
    }

    private void RefreshSummary()
    {
        var occupiedSlotCount = 0;
        for (var i = 0; i < _slotViews.Count; i++)
        {
            if (_inventory != null &&
                GodotObject.IsInstanceValid(_inventory) &&
                _inventory.TryGetEntry(i, out var entry) &&
                entry?.Definition != null)
            {
                occupiedSlotCount++;
            }
        }

        RefreshSummary(occupiedSlotCount);
    }

    private void RefreshSummary(int occupiedSlotCount)
    {
        if (_summaryLabel == null)
            return;

        var gold = _inventory != null && GodotObject.IsInstanceValid(_inventory) ? _inventory.Gold : 0;
        _summaryLabel.Text = $"Gold: {gold}    {occupiedSlotCount}/{GetExpectedSlotCount()} slots occupied";
    }

    private void OnSlotUseRequested(int slotIndex)
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        _player.TryConsumeInventorySlot(slotIndex);
    }

    private void OnSlotDragStarted(int slotIndex, int amount)
    {
        _activeDragSlot = slotIndex;
        _activeDragAmount = Math.Max(1, amount);
        _dragConsumed = false;
        // Intentionally do NOT mutate _amountSpinBox here: the configured selector
        // value must survive drags from smaller stacks or gear/non-stackables.
    }

    private void OnSlotDropReceived(int fromSlot, int toSlot, int amount)
    {
        _dragConsumed = true;
        if (_inventory == null)
            return;

        if (!_inventory.TryGetEntry(fromSlot, out var fromEntry) || fromEntry == null)
            return;

        // Gear and non-stack entries ignore the amount — preserve full-stack swap/merge behavior.
        if (fromEntry is not InventoryStackEntry || amount >= fromEntry.Quantity)
        {
            _inventory.TryInteractSlots(fromSlot, toSlot);
            return;
        }

        _inventory.TryMovePartialStack(fromSlot, toSlot, amount);
    }

    private int GetSelectedAmount()
    {
        if (_amountSpinBox == null)
            return MaxAmountResolved;
        var value = (int)_amountSpinBox.Value;
        if (value >= MaxAmountSentinel)
            return MaxAmountResolved;
        return Math.Max(1, value);
    }

    private void EnsureAmountControl()
    {
        if (_amountSpinBox != null && GodotObject.IsInstanceValid(_amountSpinBox))
            return;

        var header = GetNodeOrNull<Container>("Panel/Margin/VBox/Header");
        if (header == null)
            return;

        var amountLabel = new Label
        {
            Text = "Amount:",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.AddChild(amountLabel);

        _amountSpinBox = new SpinBox
        {
            MinValue = 1,
            MaxValue = MaxAmountSentinel,
            Step = 1,
            Value = MaxAmountSentinel,
            CustomArrowStep = 1,
            TooltipText = "Stack amount for inventory drags. Top step is MAX (full source stack).",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
        };
        _amountSpinBox.ValueChanged += OnAmountSpinBoxValueChanged;
        header.AddChild(_amountSpinBox);
        UpdateAmountSpinBoxDisplay();
    }

    private void OnAmountSpinBoxValueChanged(double value)
    {
        UpdateAmountSpinBoxDisplay();
    }

    // Renders the sentinel step as "MAX" instead of the underlying number.
    // SpinBox writes the numeric text into its LineEdit on every value change,
    // so we override it after the value-changed signal has fired.
    private void UpdateAmountSpinBoxDisplay()
    {
        if (_amountSpinBox == null || !GodotObject.IsInstanceValid(_amountSpinBox))
            return;

        var lineEdit = _amountSpinBox.GetLineEdit();
        if (lineEdit == null)
            return;

        if ((int)_amountSpinBox.Value >= MaxAmountSentinel)
            lineEdit.Text = "MAX";
    }

    private void ResetAmountToMax()
    {
        if (_amountSpinBox == null || !GodotObject.IsInstanceValid(_amountSpinBox))
            return;

        _amountSpinBox.SetValueNoSignal(MaxAmountSentinel);
        UpdateAmountSpinBoxDisplay();
    }

    private void OnEquipmentDropReceived(int equipmentSlotInt, int inventorySlot)
    {
        _dragConsumed = true;
        if (_inventory == null || _equipment == null || !GodotObject.IsInstanceValid(_equipment))
            return;

        if (!_inventory.IsSlotEmpty(inventorySlot))
            return;

        var equipmentSlot = (EquipmentSlot)equipmentSlotInt;
        if (!_equipment.TryUnequip(equipmentSlot, out var gear))
            return;

        if (!_inventory.TryPlaceGear(inventorySlot, gear))
        {
            // Rollback: target slot vanished between can-drop and drop. Put the gear back.
            _equipment.TryEquip(gear, equipmentSlot, out _);
        }
    }

    private void OnSlotDragEnded(int slotIndex)
    {
        if (!_dragConsumed && _activeDragSlot == slotIndex)
            EmitSignal(SignalName.ItemDroppedToWorld, slotIndex, _activeDragAmount);

        _activeDragSlot = -1;
        _activeDragAmount = 1;
        _dragConsumed = false;
    }

    private int GetExpectedSlotCount()
    {
        return Math.Max(1, Columns) * Math.Max(1, Rows);
    }

    private void UnbindCurrentInventory()
    {
        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
        {
            _inventory = null;
            return;
        }

        var changedCallable = new Callable(this, nameof(OnInventoryChanged));
        if (_inventory.IsConnected(InventoryController.SignalName.InventoryChanged, changedCallable))
            _inventory.Disconnect(InventoryController.SignalName.InventoryChanged, changedCallable);

        var goldChangedCallable = new Callable(this, nameof(OnGoldChanged));
        if (_inventory.IsConnected(InventoryController.SignalName.GoldChanged, goldChangedCallable))
            _inventory.Disconnect(InventoryController.SignalName.GoldChanged, goldChangedCallable);

        _inventory = null;
    }

    private sealed class InventorySlotView
    {
        public InventorySlotView(InventorySlotControl root, TextureRect iconRect, Label quantityLabel)
        {
            Root = root;
            IconRect = iconRect;
            QuantityLabel = quantityLabel;
        }

        public InventorySlotControl Root { get; }

        public TextureRect IconRect { get; }

        public Label QuantityLabel { get; }
    }
}
