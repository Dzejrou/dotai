using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class MenuHubInventoryPage : Control
{
    [Export(PropertyHint.Range, "1,20,1")]
    public int InventoryColumns { get; set; } = 10;

    [Export(PropertyHint.Range, "16,128,1")]
    public int InventoryCellSize { get; set; } = 32;

    [Export(PropertyHint.Range, "0,32,1")]
    public int InventorySlotSpacing { get; set; } = 6;

    [Export(PropertyHint.Range, "32,128,1")]
    public int EquipmentSlotSize { get; set; } = 70;

    [Export(PropertyHint.Range, "32,128,1")]
    public int UtilitySlotSize { get; set; } = 72;

    [Export]
    public NodePath EquipmentSlotsContainerPath { get; set; } = new("Margin/VBox/TopRow/EquipmentArea/Slots");

    [Export]
    public NodePath LevelingAreaPath { get; set; } = new("Margin/VBox/TopRow/LevelingArea");

    [Export]
    public NodePath AmountSpinBoxParentPath { get; set; } = new("Margin/VBox/BottomRow/InventoryColumn/AmountRow");

    [Export]
    public NodePath SlotGridPath { get; set; } = new("Margin/VBox/BottomRow/InventoryColumn/SlotGrid");

    [Export]
    public NodePath UtilityColumnPath { get; set; } = new("Margin/VBox/BottomRow/UtilityColumn");

    [Export]
    public NodePath SummaryLabelPath { get; set; } = new("Margin/VBox/BottomRow/InventoryColumn/Summary");

    // Forwarded by Main: drop the entry at slotIndex with up to `amount` to the world.
    public Action<int, int> InventoryDropToWorldRequested { get; set; }

    // Forwarded by Main: drop the loose gear instance to the world (for equipment → DROP).
    public Action<GearInstance> GearDropToWorldRequested { get; set; }

    private const int MaxAmountSentinel = 1000;
    private const int MaxAmountResolved = int.MaxValue;

    private readonly List<InventorySlotControl> _slotControls = new();
    private readonly List<TextureRect> _slotIcons = new();
    private readonly List<Label> _slotQuantityLabels = new();
    private readonly Dictionary<EquipmentSlot, EquipmentSlotView> _equipmentSlotViews = new();

    private static readonly EquipmentSlot[] SlotOrder =
    {
        EquipmentSlot.Head,
        EquipmentSlot.Torso,
        EquipmentSlot.Gloves,
        EquipmentSlot.Legs,
        EquipmentSlot.Boots,
        EquipmentSlot.Ring,
        EquipmentSlot.Artifact,
    };

    private static readonly Dictionary<EquipmentSlot, Vector2> EquipmentSlotPositions = new()
    {
        { EquipmentSlot.Head,     new Vector2(0,   0) },
        { EquipmentSlot.Torso,    new Vector2(90,  0) },
        { EquipmentSlot.Gloves,   new Vector2(180, 0) },
        { EquipmentSlot.Ring,     new Vector2(0,   90) },
        { EquipmentSlot.Legs,     new Vector2(90,  90) },
        { EquipmentSlot.Boots,    new Vector2(180, 90) },
        { EquipmentSlot.Artifact, new Vector2(270, 45) },
    };

    private InventoryController _inventory;
    private EquipmentController _equipment;
    private Player _player;
    private bool _inventoryChangedBound;
    private bool _equipmentChangedBound;

    private Control _equipmentSlotsContainer;
    private Control _levelingArea;
    private Container _amountRow;
    private GridContainer _slotGrid;
    private VBoxContainer _utilityColumn;
    private Label _summaryLabel;
    private SpinBox _amountSpinBox;

    private GearLevelingPanel _levelingPanel;
    private MenuHubUtilitySlot _dropSlot;
    private MenuHubUtilitySlot _trashSlot;
    private TextureRect _trashIcon;
    private Label _trashQuantityLabel;
    private Label _trashPlaceholder;

    private readonly Dictionary<ConsumableKind, QuickConsumableSlotView> _quickConsumableSlots = new();
    private QuickConsumableLoadout _quickLoadout;
    private bool _quickLoadoutBound;

    private int _activeDragSlot = -1;
    private int _activeDragAmount = MaxAmountResolved;
    private bool _dragConsumed;

    private InventoryEntry _trashBuffer;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _equipmentSlotsContainer = GetNodeOrNull<Control>(EquipmentSlotsContainerPath);
        _levelingArea = GetNodeOrNull<Control>(LevelingAreaPath);
        _amountRow = GetNodeOrNull<Container>(AmountSpinBoxParentPath);
        _slotGrid = GetNodeOrNull<GridContainer>(SlotGridPath);
        _utilityColumn = GetNodeOrNull<VBoxContainer>(UtilityColumnPath);
        _summaryLabel = GetNodeOrNull<Label>(SummaryLabelPath);

        BuildEquipmentSlots();
        BuildLevelingPanel();
        BuildAmountControl();
        BuildSlotGrid();
        BuildUtilityColumn();

        InventorySlotControl.DragConsumed += OnExternalDragConsumed;

        Refresh();
    }

    public override void _ExitTree()
    {
        InventorySlotControl.DragConsumed -= OnExternalDragConsumed;
        UnbindCurrentInventory();
        UnbindCurrentEquipment();
        UnbindQuickConsumableLoadout();
    }

    public void BindPlayer(Player player)
    {
        _player = player;
    }

    public void Bind(InventoryController inventory, EquipmentController equipment)
    {
        if (!ReferenceEquals(_inventory, inventory))
        {
            UnbindCurrentInventory();
            _inventory = inventory;

            if (_inventory != null && GodotObject.IsInstanceValid(_inventory))
            {
                var callable = new Callable(this, nameof(OnInventoryChanged));
                if (!_inventory.IsConnected(InventoryController.SignalName.InventoryChanged, callable))
                    _inventory.Connect(InventoryController.SignalName.InventoryChanged, callable);

                _inventoryChangedBound = true;
            }
        }

        if (!ReferenceEquals(_equipment, equipment))
        {
            UnbindCurrentEquipment();
            _equipment = equipment;

            if (_equipment != null && GodotObject.IsInstanceValid(_equipment))
            {
                var callable = new Callable(this, nameof(OnEquipmentChanged));
                if (!_equipment.IsConnected(EquipmentController.SignalName.Changed, callable))
                    _equipment.Connect(EquipmentController.SignalName.Changed, callable);

                _equipmentChangedBound = true;
            }
        }

        foreach (var slotControl in _slotControls)
            slotControl.Inventory = _inventory;

        foreach (var view in _equipmentSlotViews.Values)
        {
            view.Root.Inventory = _inventory;
            view.Root.Equipment = _equipment;
        }

        foreach (var view in _quickConsumableSlots.Values)
            view.Root.Inventory = _inventory;

        _levelingPanel?.Bind(_inventory, _equipment);

        BindQuickConsumableLoadout();

        RebuildSlotsForCapacity();
        Refresh();
    }

    private void BindQuickConsumableLoadout()
    {
        var loadout = _player != null && GodotObject.IsInstanceValid(_player)
            ? _player.QuickConsumableLoadoutNode
            : null;

        if (ReferenceEquals(_quickLoadout, loadout))
            return;

        UnbindQuickConsumableLoadout();
        _quickLoadout = loadout;

        if (_quickLoadout == null || !GodotObject.IsInstanceValid(_quickLoadout))
            return;

        var callable = new Callable(this, nameof(OnQuickConsumablesChanged));
        if (!_quickLoadout.IsConnected(QuickConsumableLoadout.SignalName.QuickConsumablesChanged, callable))
            _quickLoadout.Connect(QuickConsumableLoadout.SignalName.QuickConsumablesChanged, callable);

        _quickLoadoutBound = true;
    }

    private void UnbindQuickConsumableLoadout()
    {
        if (!_quickLoadoutBound || _quickLoadout == null || !GodotObject.IsInstanceValid(_quickLoadout))
        {
            _quickLoadoutBound = false;
            _quickLoadout = null;
            return;
        }

        var callable = new Callable(this, nameof(OnQuickConsumablesChanged));
        if (_quickLoadout.IsConnected(QuickConsumableLoadout.SignalName.QuickConsumablesChanged, callable))
            _quickLoadout.Disconnect(QuickConsumableLoadout.SignalName.QuickConsumablesChanged, callable);

        _quickLoadoutBound = false;
        _quickLoadout = null;
    }

    // Called by MenuHub when the hub is closing so we can drop the temporary
    // trash buffer (Terraria-style — trash is never persisted).
    public void OnHubClosed()
    {
        _trashBuffer = null;
        RefreshTrashSlot();
        ResetAmountToMax();
    }

    // Called by MenuHub when this page becomes the active one. Keeps the slot
    // count synced if inventory capacity changed while another page was visible
    // and resets the amount selector to MAX, matching the old InventoryWindow.
    public void OnPageEntered()
    {
        RebuildSlotsForCapacity();
        ResetAmountToMax();
        Refresh();
    }

    private void BuildEquipmentSlots()
    {
        if (_equipmentSlotsContainer == null)
            return;

        foreach (var child in _equipmentSlotsContainer.GetChildren())
        {
            _equipmentSlotsContainer.RemoveChild(child);
            child.QueueFree();
        }

        _equipmentSlotViews.Clear();

        foreach (var slot in SlotOrder)
        {
            var position = EquipmentSlotPositions[slot];

            var slotControl = new EquipmentSlotControl
            {
                Name = $"{slot}_Slot",
                Slot = slot,
                Inventory = _inventory,
                Equipment = _equipment,
                MouseFilter = MouseFilterEnum.Stop,
            };
            slotControl.InventoryDropReceived = OnInventoryDropOnEquipmentSlot;

            slotControl.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            slotControl.OffsetLeft = position.X;
            slotControl.OffsetTop = position.Y;
            slotControl.OffsetRight = position.X + EquipmentSlotSize;
            slotControl.OffsetBottom = position.Y + EquipmentSlotSize;
            slotControl.CustomMinimumSize = new Vector2(EquipmentSlotSize, EquipmentSlotSize);

            var iconRect = new TextureRect
            {
                Name = "Icon",
                MouseFilter = MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Visible = false,
            };
            iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            iconRect.OffsetLeft = 4;
            iconRect.OffsetTop = 4;
            iconRect.OffsetRight = -4;
            iconRect.OffsetBottom = -4;
            slotControl.AddChild(iconRect);

            var placeholder = new Label
            {
                Name = "Placeholder",
                Text = slot.ToString().ToLowerInvariant(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = new Color(1.0f, 1.0f, 1.0f, 0.45f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            placeholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            slotControl.AddChild(placeholder);

            _equipmentSlotsContainer.AddChild(slotControl);
            _equipmentSlotViews[slot] = new EquipmentSlotView(slotControl, iconRect, placeholder);
        }
    }

    private void BuildLevelingPanel()
    {
        if (_levelingArea == null)
            return;

        _levelingPanel = new GearLevelingPanel
        {
            Name = "GearLeveling",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _levelingArea.AddChild(_levelingPanel);
    }

    private void BuildAmountControl()
    {
        if (_amountRow == null)
            return;

        var label = new Label
        {
            Text = "Amount:",
            MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _amountRow.AddChild(label);

        _amountSpinBox = new SpinBox
        {
            MinValue = 1,
            MaxValue = MaxAmountSentinel,
            Step = 1,
            Value = MaxAmountSentinel,
            CustomArrowStep = 1,
            TooltipText = "Stack amount for inventory drags. Top step is MAX (full source stack).",
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
        };
        _amountSpinBox.ValueChanged += OnAmountSpinBoxValueChanged;
        _amountRow.AddChild(_amountSpinBox);
        UpdateAmountSpinBoxDisplay();
    }

    private void BuildSlotGrid()
    {
        if (_slotGrid == null)
            return;

        _slotGrid.Columns = Math.Max(1, InventoryColumns);
        _slotGrid.AddThemeConstantOverride("h_separation", Math.Max(0, InventorySlotSpacing));
        _slotGrid.AddThemeConstantOverride("v_separation", Math.Max(0, InventorySlotSpacing));

        RebuildSlotsForCapacity();
    }

    private void RebuildSlotsForCapacity()
    {
        if (_slotGrid == null)
            return;

        if (_activeDragSlot >= 0)
            return;

        foreach (var control in _slotControls)
        {
            if (GodotObject.IsInstanceValid(control))
                control.QueueFree();
        }

        _slotControls.Clear();
        _slotIcons.Clear();
        _slotQuantityLabels.Clear();

        var slotCount = _inventory != null && GodotObject.IsInstanceValid(_inventory)
            ? _inventory.GetSlotCount()
            : 0;

        for (var i = 0; i < slotCount; i++)
        {
            var slotControl = new InventorySlotControl
            {
                SlotIndex = i,
                Inventory = _inventory,
                CustomMinimumSize = new Vector2(InventoryCellSize + 10.0f, InventoryCellSize + 10.0f),
                MouseFilter = MouseFilterEnum.Stop,
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };

            slotControl.AmountProvider = GetSelectedAmount;
            slotControl.DragStarted = (slot, amount) => OnSlotDragStarted(slot, amount);
            slotControl.DropReceived = (from, to, amount) => OnSlotDropReceived(from, to, amount);
            slotControl.DragEnded = OnSlotDragEnded;
            slotControl.EquipmentDropReceived = (equipmentSlot, to) => OnEquipmentDropReceivedOnInventorySlot(equipmentSlot, to);
            slotControl.UseRequested = OnSlotUseRequested;
            slotControl.TrashDropReceived = OnTrashDropReceivedOnInventorySlot;

            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            margin.AddThemeConstantOverride("margin_left", 5);
            margin.AddThemeConstantOverride("margin_top", 5);
            margin.AddThemeConstantOverride("margin_right", 5);
            margin.AddThemeConstantOverride("margin_bottom", 5);
            slotControl.AddChild(margin);

            var overlay = new Control
            {
                CustomMinimumSize = new Vector2(InventoryCellSize, InventoryCellSize),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            margin.AddChild(overlay);

            var iconRect = new TextureRect
            {
                MouseFilter = MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Visible = false,
            };
            iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            overlay.AddChild(iconRect);

            var quantityLabel = new Label
            {
                MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visible = false,
            };
            quantityLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            overlay.AddChild(quantityLabel);

            _slotGrid.AddChild(slotControl);
            _slotControls.Add(slotControl);
            _slotIcons.Add(iconRect);
            _slotQuantityLabels.Add(quantityLabel);
        }
    }

    private void BuildUtilityColumn()
    {
        if (_utilityColumn == null)
            return;

        _dropSlot = BuildUtilitySlot("DROP", MenuHubUtilityKind.Drop);
        _dropSlot.InventoryDropReceived = OnDropUtilityInventory;
        _dropSlot.EquipmentDropReceived = OnDropUtilityEquipment;
        _utilityColumn.AddChild(_dropSlot);

        _trashSlot = BuildUtilitySlot("TRASH", MenuHubUtilityKind.Trash);
        _trashSlot.InventoryDropReceived = OnTrashUtilityInventory;
        _trashSlot.EquipmentDropReceived = OnTrashUtilityEquipment;
        _trashSlot.TrashHasContents = () => _trashBuffer != null;

        var trashOverlay = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        trashOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _trashSlot.AddChild(trashOverlay);

        _trashIcon = new TextureRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Visible = false,
        };
        _trashIcon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        trashOverlay.AddChild(_trashIcon);

        _trashQuantityLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Visible = false,
        };
        _trashQuantityLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        trashOverlay.AddChild(_trashQuantityLabel);

        _trashPlaceholder = new Label
        {
            Text = "TRASH",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.45f),
        };
        _trashPlaceholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        trashOverlay.AddChild(_trashPlaceholder);

        _utilityColumn.AddChild(_trashSlot);

        BuildQuickConsumableSlot(ConsumableKind.Food, "FOOD");
        BuildQuickConsumableSlot(ConsumableKind.Drink, "DRINK");
    }

    private void BuildQuickConsumableSlot(ConsumableKind kind, string placeholderText)
    {
        if (_utilityColumn == null)
            return;

        var slot = new MenuHubQuickConsumableSlot
        {
            Name = $"{kind}QuickSlot",
            Kind = kind,
            Inventory = _inventory,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(UtilitySlotSize, UtilitySlotSize),
        };
        slot.AssignRequested = sourceSlot => OnQuickConsumableAssign(kind, sourceSlot);
        slot.ClearRequested = () => OnQuickConsumableClear(kind);

        var overlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        slot.AddChild(overlay);

        var icon = new TextureRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Visible = false,
        };
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        icon.OffsetLeft = 4;
        icon.OffsetTop = 4;
        icon.OffsetRight = -4;
        icon.OffsetBottom = -4;
        overlay.AddChild(icon);

        var placeholder = new Label
        {
            Text = placeholderText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.55f),
        };
        placeholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(placeholder);

        _utilityColumn.AddChild(slot);
        _quickConsumableSlots[kind] = new QuickConsumableSlotView(slot, icon, placeholder);
    }

    private MenuHubUtilitySlot BuildUtilitySlot(string title, MenuHubUtilityKind kind)
    {
        var slot = new MenuHubUtilitySlot
        {
            Name = $"{kind}Slot",
            Kind = kind,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(UtilitySlotSize, UtilitySlotSize),
            TooltipText = kind == MenuHubUtilityKind.Drop
                ? "Drag items here to drop them on the ground."
                : "Drag items here to trash them. Trash clears when the hub closes.",
        };

        if (kind == MenuHubUtilityKind.Drop)
        {
            var label = new Label
            {
                Text = title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = new Color(1.0f, 1.0f, 1.0f, 0.55f),
            };
            label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            slot.AddChild(label);
        }

        return slot;
    }

    private void OnInventoryChanged()
    {
        Refresh();
    }

    private void OnEquipmentChanged()
    {
        RefreshEquipmentSlots();
        _levelingPanel?.RefreshPanel();
    }

    private void Refresh()
    {
        RefreshSlots();
        RefreshEquipmentSlots();
        RefreshTrashSlot();
        RefreshQuickConsumableSlots();
        _levelingPanel?.RefreshPanel();
    }

    private void RefreshSlots()
    {
        var occupied = 0;
        for (var i = 0; i < _slotControls.Count; i++)
        {
            var control = _slotControls[i];
            var iconRect = _slotIcons[i];
            var quantityLabel = _slotQuantityLabels[i];

            InventoryEntry entry = null;
            var hasEntry = _inventory != null &&
                GodotObject.IsInstanceValid(_inventory) &&
                _inventory.TryGetEntry(i, out entry) &&
                entry?.Definition != null;

            control.TooltipText = hasEntry ? entry.TooltipText : "Empty";
            control.Modulate = hasEntry ? Colors.White : new Color(0.68f, 0.68f, 0.68f, 1.0f);
            iconRect.Texture = hasEntry ? entry.Icon : null;
            iconRect.Visible = hasEntry && entry.Icon != null;
            iconRect.Modulate = Colors.White;
            quantityLabel.Visible = hasEntry && entry.ShowQuantity;
            quantityLabel.Text = hasEntry && entry.ShowQuantity ? entry.Quantity.ToString() : string.Empty;

            if (hasEntry)
                occupied++;
        }

        RefreshSummary(occupied);
    }

    private void RefreshSummary(int occupied)
    {
        if (_summaryLabel == null)
            return;

        var gold = _inventory != null && GodotObject.IsInstanceValid(_inventory) ? _inventory.Gold : 0;
        var capacity = _inventory != null && GodotObject.IsInstanceValid(_inventory) ? _inventory.GetSlotCount() : 0;
        _summaryLabel.Text = $"Gold: {gold}    {occupied}/{capacity} slots occupied";
    }

    private void RefreshEquipmentSlots()
    {
        foreach (var slot in SlotOrder)
        {
            if (!_equipmentSlotViews.TryGetValue(slot, out var view))
                continue;

            var gear = _equipment?.GetEquipped(slot);
            var hasGear = gear?.Definition != null;

            view.IconRect.Texture = hasGear ? gear.Definition.Icon : null;
            view.IconRect.Visible = hasGear && gear.Definition.Icon != null;
            view.IconRect.Modulate = Colors.White;
            view.Placeholder.Visible = !hasGear;
            view.Root.TooltipText = hasGear ? GearTooltipBuilder.Build(gear) : slot.ToString();
            view.Root.Modulate = Colors.White;
        }
    }

    private void RefreshTrashSlot()
    {
        if (_trashIcon == null || _trashQuantityLabel == null || _trashPlaceholder == null)
            return;

        if (_trashBuffer == null)
        {
            _trashIcon.Texture = null;
            _trashIcon.Visible = false;
            _trashQuantityLabel.Visible = false;
            _trashQuantityLabel.Text = string.Empty;
            _trashPlaceholder.Visible = true;
            if (_trashSlot != null)
                _trashSlot.TooltipText = "Drag items here to trash them. Trash clears when the hub closes.";
            return;
        }

        _trashPlaceholder.Visible = false;
        _trashIcon.Texture = _trashBuffer.Icon;
        _trashIcon.Visible = _trashBuffer.Icon != null;
        _trashQuantityLabel.Visible = _trashBuffer.ShowQuantity;
        _trashQuantityLabel.Text = _trashBuffer.ShowQuantity ? _trashBuffer.Quantity.ToString() : string.Empty;
        if (_trashSlot != null)
            _trashSlot.TooltipText = _trashBuffer.TooltipText;
    }

    private void OnQuickConsumablesChanged()
    {
        RefreshQuickConsumableSlots();
    }

    private void OnQuickConsumableAssign(ConsumableKind kind, int sourceSlotIndex)
    {
        if (_quickLoadout == null || !GodotObject.IsInstanceValid(_quickLoadout))
            return;

        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return;

        // Assignment records the item-definition id only; the inventory stack is left intact.
        if (!_inventory.TryGetEntry(sourceSlotIndex, out var entry) || entry is not InventoryStackEntry stackEntry)
            return;

        _quickLoadout.TryAssign(kind, stackEntry.Stack.Item);
    }

    private void OnQuickConsumableClear(ConsumableKind kind)
    {
        if (_quickLoadout == null || !GodotObject.IsInstanceValid(_quickLoadout))
            return;

        _quickLoadout.Clear(kind);
    }

    private void RefreshQuickConsumableSlots()
    {
        foreach (var pair in _quickConsumableSlots)
        {
            var kind = pair.Key;
            var view = pair.Value;

            var assignedId = _quickLoadout != null && GodotObject.IsInstanceValid(_quickLoadout)
                ? _quickLoadout.GetAssignedItemId(kind)
                : string.Empty;

            var definition = ResolveQuickAssignmentDefinition(assignedId);
            if (definition == null)
            {
                view.Icon.Texture = null;
                view.Icon.Visible = false;
                view.Placeholder.Visible = true;
                view.Root.TooltipText = kind == ConsumableKind.Food
                    ? "Drag a food item here to assign your quick food. Right-click to clear."
                    : "Drag a drink item here to assign your quick drink. Right-click to clear.";
                continue;
            }

            var quantity = _inventory != null && GodotObject.IsInstanceValid(_inventory)
                ? _inventory.GetQuantityByItemId(assignedId)
                : 0;
            var displayName = string.IsNullOrEmpty(definition.DisplayName) ? definition.Id : definition.DisplayName;

            view.Icon.Texture = definition.Icon;
            view.Icon.Visible = definition.Icon != null;
            view.Placeholder.Visible = definition.Icon == null;
            view.Root.TooltipText = $"{displayName}\nIn inventory: {quantity}";
        }
    }

    private InventoryItemDefinition ResolveQuickAssignmentDefinition(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || _inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return null;

        return _inventory.ItemCatalog?.Resolve(itemId, null);
    }

    private void OnExternalDragConsumed(int sourceSlotIndex)
    {
        if (_activeDragSlot == sourceSlotIndex)
            _dragConsumed = true;
    }

    private void OnSlotDragStarted(int slotIndex, int amount)
    {
        _activeDragSlot = slotIndex;
        _activeDragAmount = Math.Max(1, amount);
        _dragConsumed = false;
    }

    private void OnSlotDropReceived(int fromSlot, int toSlot, int amount)
    {
        _dragConsumed = true;
        if (_inventory == null)
            return;

        if (!_inventory.TryGetEntry(fromSlot, out var fromEntry) || fromEntry == null)
            return;

        if (fromEntry is not InventoryStackEntry || amount >= fromEntry.Quantity)
        {
            _inventory.TryInteractSlots(fromSlot, toSlot);
            return;
        }

        _inventory.TryMovePartialStack(fromSlot, toSlot, amount);
    }

    private void OnSlotDragEnded(int slotIndex)
    {
        // The hub replaces the InventoryWindow's drag-out-of-window world-drop
        // behavior with the explicit DROP utility slot, so unconsumed drags here
        // are a no-op rather than spawning a world drop.
        _activeDragSlot = -1;
        _activeDragAmount = MaxAmountResolved;
        _dragConsumed = false;
    }

    private void OnSlotUseRequested(int slotIndex)
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        _player.TryConsumeInventorySlot(slotIndex);
    }

    private void OnEquipmentDropReceivedOnInventorySlot(int equipmentSlotInt, int inventorySlot)
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
            // Rollback: target slot vanished between can-drop and drop.
            _equipment.TryEquip(gear, equipmentSlot, out _);
        }
    }

    private void OnInventoryDropOnEquipmentSlot(int inventorySlotIndex, EquipmentSlot equipmentSlot)
    {
        if (_inventory == null || _equipment == null || !GodotObject.IsInstanceValid(_equipment))
            return;

        if (!_inventory.TryGetEntry(inventorySlotIndex, out var entry) || entry is not InventoryGearEntry gearEntry)
            return;

        if (gearEntry.Gear?.Definition?.Slot != equipmentSlot)
            return;

        var taken = _inventory.TakeEntry(inventorySlotIndex);
        if (taken is not InventoryGearEntry takenGear)
            return;

        if (!_equipment.TryEquip(takenGear.Gear, equipmentSlot, out var displaced))
        {
            _inventory.TryPlaceGear(inventorySlotIndex, takenGear.Gear);
            return;
        }

        if (displaced != null && !_inventory.AddGear(displaced))
        {
            _equipment.TryEquip(displaced, equipmentSlot, out _);
            _inventory.TryPlaceGear(inventorySlotIndex, takenGear.Gear);
        }
    }

    private void OnDropUtilityInventory(int inventorySlotIndex, int amount)
    {
        _dragConsumed = true;
        InventoryDropToWorldRequested?.Invoke(inventorySlotIndex, amount);
    }

    private void OnDropUtilityEquipment(EquipmentSlot equipmentSlot)
    {
        _dragConsumed = true;
        if (_equipment == null || !GodotObject.IsInstanceValid(_equipment))
            return;

        if (!_equipment.TryUnequip(equipmentSlot, out var gear) || gear == null)
            return;

        if (GearDropToWorldRequested == null)
        {
            // No drop handler hooked up — roll back the unequip to avoid losing the gear.
            _equipment.TryEquip(gear, equipmentSlot, out _);
            return;
        }

        GearDropToWorldRequested.Invoke(gear);
    }

    private void OnTrashUtilityInventory(int inventorySlotIndex, int amount)
    {
        _dragConsumed = true;
        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return;

        if (!_inventory.TryGetEntry(inventorySlotIndex, out var entry) || entry == null)
            return;

        if (entry is InventoryGearEntry)
        {
            var taken = _inventory.TakeEntry(inventorySlotIndex);
            if (taken == null)
                return;

            _trashBuffer = taken;
            RefreshTrashSlot();
            return;
        }

        if (entry is InventoryStackEntry stackEntry && stackEntry.Stack?.Item != null)
        {
            var requested = Math.Max(1, amount);
            if (!_inventory.TryTakePartialStack(inventorySlotIndex, requested, out var takenStack) || takenStack == null)
                return;

            _trashBuffer = new InventoryStackEntry(takenStack);
            RefreshTrashSlot();
        }
    }

    private void OnTrashUtilityEquipment(EquipmentSlot equipmentSlot)
    {
        _dragConsumed = true;
        if (_equipment == null || !GodotObject.IsInstanceValid(_equipment))
            return;

        if (!_equipment.TryUnequip(equipmentSlot, out var gear) || gear == null)
            return;

        _trashBuffer = new InventoryGearEntry(gear);
        RefreshTrashSlot();
    }

    private void OnTrashDropReceivedOnInventorySlot(int targetSlot)
    {
        if (_trashBuffer == null || _inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return;

        if (_trashBuffer is InventoryGearEntry gearEntry && gearEntry.Gear != null)
        {
            if (_inventory.TryPlaceGear(targetSlot, gearEntry.Gear))
            {
                _trashBuffer = null;
                RefreshTrashSlot();
            }
            return;
        }

        if (_trashBuffer is InventoryStackEntry stackEntry && stackEntry.Stack?.Item != null)
        {
            if (!_inventory.TryPlaceStackAtSlot(targetSlot, stackEntry.Stack, out var remainder))
                return;

            _trashBuffer = remainder != null ? new InventoryStackEntry(remainder) : null;
            RefreshTrashSlot();
        }
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

    private void OnAmountSpinBoxValueChanged(double value)
    {
        UpdateAmountSpinBoxDisplay();
    }

    // Renders the sentinel step as "MAX" instead of the underlying number.
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

    private void UnbindCurrentInventory()
    {
        if (!_inventoryChangedBound || _inventory == null || !GodotObject.IsInstanceValid(_inventory))
        {
            _inventoryChangedBound = false;
            _inventory = null;
            return;
        }

        var callable = new Callable(this, nameof(OnInventoryChanged));
        if (_inventory.IsConnected(InventoryController.SignalName.InventoryChanged, callable))
            _inventory.Disconnect(InventoryController.SignalName.InventoryChanged, callable);

        _inventoryChangedBound = false;
        _inventory = null;
    }

    private void UnbindCurrentEquipment()
    {
        if (!_equipmentChangedBound || _equipment == null || !GodotObject.IsInstanceValid(_equipment))
        {
            _equipmentChangedBound = false;
            _equipment = null;
            return;
        }

        var callable = new Callable(this, nameof(OnEquipmentChanged));
        if (_equipment.IsConnected(EquipmentController.SignalName.Changed, callable))
            _equipment.Disconnect(EquipmentController.SignalName.Changed, callable);

        _equipmentChangedBound = false;
        _equipment = null;
    }

    private sealed class EquipmentSlotView
    {
        public EquipmentSlotView(EquipmentSlotControl root, TextureRect iconRect, Label placeholder)
        {
            Root = root;
            IconRect = iconRect;
            Placeholder = placeholder;
        }

        public EquipmentSlotControl Root { get; }
        public TextureRect IconRect { get; }
        public Label Placeholder { get; }
    }

    private sealed class QuickConsumableSlotView
    {
        public QuickConsumableSlotView(MenuHubQuickConsumableSlot root, TextureRect icon, Label placeholder)
        {
            Root = root;
            Icon = icon;
            Placeholder = placeholder;
        }

        public MenuHubQuickConsumableSlot Root { get; }
        public TextureRect Icon { get; }
        public Label Placeholder { get; }
    }
}
