using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class CharacterWindow : Control
{
    [Export(PropertyHint.Range, "32,128,1")]
    public int SlotSize { get; set; } = 70;

    [Export]
    public NodePath WindowPanelPath { get; set; } = new("Panel");

    [Export]
    public NodePath SlotsContainerPath { get; set; } = new("Panel/Margin/Slots");

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

    // Absolute positions inside the Slots Control. Top row: Head/Torso/Gloves.
    // Bottom row: Ring/Legs/Boots. Artifact sits to the right, centered between rows.
    private static readonly Dictionary<EquipmentSlot, Vector2> SlotPositions = new()
    {
        { EquipmentSlot.Head,     new Vector2(0,   0) },
        { EquipmentSlot.Torso,    new Vector2(90,  0) },
        { EquipmentSlot.Gloves,   new Vector2(180, 0) },
        { EquipmentSlot.Ring,     new Vector2(0,   90) },
        { EquipmentSlot.Legs,     new Vector2(90,  90) },
        { EquipmentSlot.Boots,    new Vector2(180, 90) },
        { EquipmentSlot.Artifact, new Vector2(270, 45) },
    };

    private readonly Dictionary<EquipmentSlot, EquipmentSlotView> _slotViews = new();
    private InventoryController _inventory;
    private EquipmentController _equipment;
    private Control _windowPanel;
    private Control _slotsContainer;
    private WindowDragger _windowDragger;
    private bool _panelPositioned;
    private bool _equipmentChangedBound;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _windowPanel = GetNodeOrNull<Control>(WindowPanelPath);
        _slotsContainer = GetNodeOrNull<Control>(SlotsContainerPath);

        if (_windowPanel != null)
            _windowDragger = new WindowDragger(this, _windowPanel);

        BuildSlots();
        Refresh();
        CallDeferred(MethodName.CenterPanelOnce);
    }

    public override void _ExitTree()
    {
        _windowDragger?.Detach();
        UnbindCurrentEquipment();
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

    public void Bind(InventoryController inventory, EquipmentController equipment)
    {
        _inventory = inventory;

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

        foreach (var slotView in _slotViews.Values)
        {
            slotView.Root.Inventory = _inventory;
            slotView.Root.Equipment = _equipment;
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
            Refresh();
        }
    }

    private void OnEquipmentChanged()
    {
        Refresh();
    }

    private void BuildSlots()
    {
        if (_slotsContainer == null)
            return;

        foreach (var child in _slotsContainer.GetChildren())
        {
            _slotsContainer.RemoveChild(child);
            child.QueueFree();
        }

        _slotViews.Clear();

        foreach (var slot in SlotOrder)
            _slotViews[slot] = CreateSlot(slot);
    }

    private EquipmentSlotView CreateSlot(EquipmentSlot slot)
    {
        var position = SlotPositions[slot];

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
        slotControl.OffsetRight = position.X + SlotSize;
        slotControl.OffsetBottom = position.Y + SlotSize;
        slotControl.CustomMinimumSize = new Vector2(SlotSize, SlotSize);

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

        var placeholderLabel = new Label
        {
            Name = "Placeholder",
            Text = slot.ToString().ToLowerInvariant(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.45f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        placeholderLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        slotControl.AddChild(placeholderLabel);

        _slotsContainer.AddChild(slotControl);
        return new EquipmentSlotView(slotControl, iconRect, placeholderLabel);
    }

    private void Refresh()
    {
        foreach (var slot in SlotOrder)
        {
            if (!_slotViews.TryGetValue(slot, out var view))
                continue;

            var gear = _equipment?.GetEquipped(slot);
            var hasGear = gear?.Definition != null;

            view.IconRect.Texture = hasGear ? gear.Definition.Icon : null;
            view.IconRect.Visible = hasGear && gear.Definition.Icon != null;
            view.Placeholder.Visible = !hasGear;
            view.Root.TooltipText = hasGear ? BuildTooltip(gear.Definition) : slot.ToString();
            view.Root.Modulate = hasGear ? Colors.White : new Color(1.0f, 1.0f, 1.0f, 1.0f);
        }
    }

    private static string BuildTooltip(GearDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        var parts = new List<string> { definition.DisplayName };
        if (definition.StatModifiers != null)
        {
            foreach (var modifier in definition.StatModifiers)
            {
                if (modifier == null || string.IsNullOrEmpty(modifier.StatId))
                    continue;

                var sign = modifier.Value >= 0 ? "+" : "";
                parts.Add($"{sign}{modifier.Value:0.##} {modifier.StatId}");
            }
        }

        return string.Join("\n", parts);
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
        {
            // Slot vanished between can-drop and drop. Nothing to roll back.
            return;
        }

        if (!_equipment.TryEquip(takenGear.Gear, equipmentSlot, out var displaced))
        {
            // Equip refused (mismatched slot etc.) — put it back where it came from.
            _inventory.TryPlaceGear(inventorySlotIndex, takenGear.Gear);
            return;
        }

        if (displaced != null && !_inventory.AddGear(displaced))
        {
            // Inventory full despite the just-freed slot — extremely unlikely. Restore and bail.
            _equipment.TryEquip(displaced, equipmentSlot, out _);
            _inventory.TryPlaceGear(inventorySlotIndex, takenGear.Gear);
        }
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
}
