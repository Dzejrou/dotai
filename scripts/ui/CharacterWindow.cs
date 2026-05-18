using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class CharacterWindow : Control
{
    [Export]
    public string WindowTitle { get; set; } = "Character";

    [Export(PropertyHint.Range, "16,128,1")]
    public int CellSize { get; set; } = 40;

    [Export(PropertyHint.Range, "0,32,1")]
    public int SlotSpacing { get; set; } = 8;

    [Export]
    public NodePath WindowPanelPath { get; set; } = new("Center/Panel");

    [Export]
    public NodePath TitleLabelPath { get; set; } = new("Center/Panel/Margin/VBox/Header/Title");

    [Export]
    public NodePath SlotsContainerPath { get; set; } = new("Center/Panel/Margin/VBox/Slots");

    [Export]
    public NodePath SummaryLabelPath { get; set; } = new("Center/Panel/Margin/VBox/Summary");

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

    private readonly Dictionary<EquipmentSlot, EquipmentSlotView> _slotViews = new();
    private InventoryController _inventory;
    private EquipmentController _equipment;
    private Label _titleLabel;
    private Label _summaryLabel;
    private VBoxContainer _slotsContainer;
    private bool _equipmentChangedBound;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
        _summaryLabel = GetNodeOrNull<Label>(SummaryLabelPath);
        _slotsContainer = GetNodeOrNull<VBoxContainer>(SlotsContainerPath);

        ApplyLayout();
        Refresh();
    }

    public override void _ExitTree()
    {
        UnbindCurrentEquipment();
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
            Refresh();
    }

    private void OnEquipmentChanged()
    {
        Refresh();
    }

    private void ApplyLayout()
    {
        if (_titleLabel != null)
            _titleLabel.Text = WindowTitle;

        if (_slotsContainer == null)
            return;

        foreach (var child in _slotsContainer.GetChildren())
        {
            _slotsContainer.RemoveChild(child);
            child.QueueFree();
        }

        _slotViews.Clear();
        _slotsContainer.AddThemeConstantOverride("separation", Math.Max(0, SlotSpacing));

        foreach (var slot in SlotOrder)
            _slotViews[slot] = CreateSlotRow(slot);
    }

    private EquipmentSlotView CreateSlotRow(EquipmentSlot slot)
    {
        var row = new HBoxContainer
        {
            Name = $"{slot}_Row",
        };
        row.AddThemeConstantOverride("separation", 10);

        var nameLabel = new Label
        {
            Name = "SlotName",
            Text = slot.ToString(),
            CustomMinimumSize = new Vector2(80.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(nameLabel);

        var slotControl = new EquipmentSlotControl
        {
            Slot = slot,
            Inventory = _inventory,
            Equipment = _equipment,
            CustomMinimumSize = new Vector2(CellSize + 10.0f, CellSize + 10.0f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        slotControl.InventoryDropReceived = OnInventoryDropOnEquipmentSlot;

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

        row.AddChild(slotControl);

        var itemLabel = new Label
        {
            Name = "ItemName",
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(itemLabel);

        _slotsContainer.AddChild(row);
        return new EquipmentSlotView(slotControl, iconRect, itemLabel, nameLabel);
    }

    private void Refresh()
    {
        if (_titleLabel != null)
            _titleLabel.Text = WindowTitle;

        var equippedCount = 0;
        foreach (var slot in SlotOrder)
        {
            if (!_slotViews.TryGetValue(slot, out var view))
                continue;

            var gear = _equipment?.GetEquipped(slot);
            var hasGear = gear?.Definition != null;
            view.IconRect.Texture = hasGear ? gear.Definition.Icon : null;
            view.IconRect.Visible = hasGear && gear.Definition.Icon != null;
            view.ItemName.Text = hasGear ? gear.Definition.DisplayName : "(empty)";
            view.Root.TooltipText = hasGear ? BuildTooltip(gear.Definition) : slot.ToString();
            view.Root.Modulate = hasGear ? Colors.White : new Color(0.68f, 0.68f, 0.68f, 1.0f);
            if (hasGear)
                equippedCount++;
        }

        if (_summaryLabel != null)
            _summaryLabel.Text = $"{equippedCount}/{SlotOrder.Length} slots equipped";
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
        public EquipmentSlotView(EquipmentSlotControl root, TextureRect iconRect, Label itemName, Label slotName)
        {
            Root = root;
            IconRect = iconRect;
            ItemName = itemName;
            SlotName = slotName;
        }

        public EquipmentSlotControl Root { get; }
        public TextureRect IconRect { get; }
        public Label ItemName { get; }
        public Label SlotName { get; }
    }
}
