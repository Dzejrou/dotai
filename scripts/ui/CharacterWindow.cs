using Godot;

using System.Collections.Generic;
using System.Globalization;

[GlobalClass]
public partial class CharacterWindow : Control
{
    [Export(PropertyHint.Range, "32,128,1")]
    public int SlotSize { get; set; } = 70;

    [Export]
    public NodePath WindowPanelPath { get; set; } = new("Panel");

    [Export]
    public NodePath SlotsContainerPath { get; set; } = new("Panel/Margin/VBox/Slots");

    [Export]
    public NodePath StatsTogglePath { get; set; } = new("Panel/Margin/VBox/ToggleRow/StatsToggle");

    [Export]
    public NodePath LevelLabelPath { get; set; } = new("Panel/Margin/VBox/ToggleRow/LevelLabel");

    [Export]
    public NodePath StatsContainerPath { get; set; } = new("Panel/Margin/VBox/StatsContainer");

    [Export]
    public NodePath LevelingTogglePath { get; set; } = new("Panel/Margin/VBox/ToggleRow/LevelingToggle");

    [Export]
    public NodePath LevelingPanelPath { get; set; } = new("Panel/Margin/VBox/LevelingContainer");

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
    private CombatCharacter _statsOwner;
    private Player _playerOwner;
    private Control _windowPanel;
    private Control _slotsContainer;
    private Button _statsToggle;
    private Button _levelingToggle;
    private Label _levelLabel;
    private VBoxContainer _statsContainer;
    private GearLevelingPanel _levelingPanel;
    private WindowDragger _windowDragger;
    private bool _panelPositioned;
    private bool _equipmentChangedBound;
    private bool _playerLevelBound;
    private bool _statsExpanded;
    private bool _levelingExpanded;

    private Label _statMaxHealth;
    private Label _statMaxMana;
    private Label _statPower;
    private Label _statMP5;
    private Label _statCritRate;
    private Label _statCritDamage;
    private Label _statHaste;
    private Label _statMoveSpeed;
    private Label _statDamageBonus;
    private Label _statElementalDmg;
    private Label _statElementalResist;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _windowPanel = GetNodeOrNull<Control>(WindowPanelPath);
        _slotsContainer = GetNodeOrNull<Control>(SlotsContainerPath);
        _statsToggle = GetNodeOrNull<Button>(StatsTogglePath);
        _levelLabel = GetNodeOrNull<Label>(LevelLabelPath);
        _statsContainer = GetNodeOrNull<VBoxContainer>(StatsContainerPath);
        _levelingToggle = GetNodeOrNull<Button>(LevelingTogglePath);
        _levelingPanel = GetNodeOrNull<GearLevelingPanel>(LevelingPanelPath);

        if (_windowPanel != null)
        {
            _windowDragger = new WindowDragger(this, _windowPanel)
            {
                BringToFront = FocusWindow,
            };
        }

        BuildSlots();
        BuildStatsRows();

        if (_levelingPanel != null)
            _levelingPanel.FocusRequested = FocusWindow;

        if (_statsToggle != null)
        {
            _statsToggle.ButtonPressed = _statsExpanded;
            _statsToggle.Toggled += OnStatsToggled;
        }

        if (_levelingToggle != null)
        {
            _levelingToggle.ButtonPressed = _levelingExpanded;
            _levelingToggle.Toggled += OnLevelingToggled;
        }

        ApplyStatsExpansion();
        ApplyLevelingExpansion();
        Refresh();
        RefreshLevelLabel();
        CallDeferred(MethodName.CenterPanelOnce);
    }

    public override void _ExitTree()
    {
        _windowDragger?.Detach();
        UnbindCurrentEquipment();
        UnbindCurrentPlayer();

        if (_statsToggle != null)
            _statsToggle.Toggled -= OnStatsToggled;

        if (_levelingToggle != null)
            _levelingToggle.Toggled -= OnLevelingToggled;

        if (_levelingPanel != null)
            _levelingPanel.FocusRequested = null;
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

        _levelingPanel?.Bind(_inventory, _equipment);

        Refresh();
    }

    public void BindStatsOwner(CombatCharacter character)
    {
        _statsOwner = character;
        BindPlayer(character as Player);
        RefreshStats();
    }

    private void BindPlayer(Player player)
    {
        if (ReferenceEquals(_playerOwner, player))
        {
            RefreshLevelLabel();
            return;
        }

        UnbindCurrentPlayer();
        _playerOwner = player;

        if (_playerOwner != null && GodotObject.IsInstanceValid(_playerOwner))
        {
            var callable = new Callable(this, nameof(OnPlayerLevelChanged));
            if (!_playerOwner.IsConnected(Player.SignalName.LevelChanged, callable))
                _playerOwner.Connect(Player.SignalName.LevelChanged, callable);

            _playerLevelBound = true;
        }

        RefreshLevelLabel();
    }

    private void OnPlayerLevelChanged(int newLevel)
    {
        RefreshLevelLabel();
    }

    private void RefreshLevelLabel()
    {
        if (_levelLabel == null)
            return;

        var level = _playerOwner != null && GodotObject.IsInstanceValid(_playerOwner) ? _playerOwner.Level : 1;
        _levelLabel.Text = $"Lv {level}";
    }

    private void UnbindCurrentPlayer()
    {
        if (!_playerLevelBound || _playerOwner == null || !GodotObject.IsInstanceValid(_playerOwner))
        {
            _playerLevelBound = false;
            _playerOwner = null;
            return;
        }

        var callable = new Callable(this, nameof(OnPlayerLevelChanged));
        if (_playerOwner.IsConnected(Player.SignalName.LevelChanged, callable))
            _playerOwner.Disconnect(Player.SignalName.LevelChanged, callable);

        _playerLevelBound = false;
        _playerOwner = null;
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
            Refresh();
            RefreshLevelLabel();
        }
    }

    public void FocusWindow()
    {
        MoveToFront();
    }

    private void OnEquipmentChanged()
    {
        Refresh();
    }

    private void OnStatsToggled(bool pressed)
    {
        _statsExpanded = pressed;
        ApplyStatsExpansion();
        RefreshStats();
    }

    private void ApplyStatsExpansion()
    {
        if (_statsContainer != null)
            _statsContainer.Visible = _statsExpanded;

        if (_windowPanel == null)
            return;

        // Force the panel to shrink to its content's minimum when collapsing.
        _windowPanel.Size = _windowPanel.GetCombinedMinimumSize();
        if (_panelPositioned && Visible)
            _windowDragger?.ClampToViewport();
    }

    private void OnLevelingToggled(bool pressed)
    {
        _levelingExpanded = pressed;
        ApplyLevelingExpansion();
        _levelingPanel?.RefreshPanel();
    }

    private void ApplyLevelingExpansion()
    {
        if (_levelingPanel != null)
            _levelingPanel.Visible = _levelingExpanded;

        if (_windowPanel == null)
            return;

        _windowPanel.Size = _windowPanel.GetCombinedMinimumSize();
        if (_panelPositioned && Visible)
            _windowDragger?.ClampToViewport();
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
        slotControl.FocusRequested = FocusWindow;

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
        RefreshSlots();
        RefreshStats();
        _levelingPanel?.RefreshPanel();
    }

    private void RefreshSlots()
    {
        foreach (var slot in SlotOrder)
        {
            if (!_slotViews.TryGetValue(slot, out var view))
                continue;

            var gear = _equipment?.GetEquipped(slot);
            var hasGear = gear?.Definition != null;

            view.IconRect.Texture = hasGear ? gear.Definition.Icon : null;
            view.IconRect.Visible = hasGear && gear.Definition.Icon != null;
            view.IconRect.Modulate = hasGear ? GearQualityColors.GetColor(gear.Quality) : Colors.White;
            view.Placeholder.Visible = !hasGear;
            view.Root.TooltipText = hasGear ? GearTooltipBuilder.Build(gear) : slot.ToString();
            view.Root.Modulate = Colors.White;
        }
    }

    private void BuildStatsRows()
    {
        if (_statsContainer == null)
            return;

        foreach (var child in _statsContainer.GetChildren())
        {
            _statsContainer.RemoveChild(child);
            child.QueueFree();
        }

        _statMaxHealth = CreateStatLabel();
        _statMaxMana = CreateStatLabel();
        AddStatRow(_statMaxHealth, _statMaxMana);

        _statPower = CreateStatLabel();
        _statMP5 = CreateStatLabel();
        AddStatRow(_statPower, _statMP5);

        _statCritRate = CreateStatLabel();
        _statCritDamage = CreateStatLabel();
        AddStatRow(_statCritRate, _statCritDamage);

        _statHaste = CreateStatLabel();
        _statMoveSpeed = CreateStatLabel();
        AddStatRow(_statHaste, _statMoveSpeed);

        _statDamageBonus = CreateStatLabel();
        AddStatRow(_statDamageBonus, null);

        _statElementalDmg = CreateStatLabel();
        _statsContainer.AddChild(_statElementalDmg);

        _statElementalResist = CreateStatLabel();
        _statsContainer.AddChild(_statElementalResist);
    }

    private Label CreateStatLabel()
    {
        return new Label
        {
            Text = "-",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
    }

    private void AddStatRow(Label left, Label right)
    {
        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(left);

        if (right != null)
            row.AddChild(right);

        _statsContainer.AddChild(row);
    }

    private void RefreshStats()
    {
        if (_statsContainer == null || !_statsExpanded)
            return;

        if (_statsOwner == null || !GodotObject.IsInstanceValid(_statsOwner))
        {
            SetMissingStats();
            return;
        }

        SetTripleInt(_statMaxHealth, "Max Health", _statsOwner.ResolvedMaxHealth, _statsOwner.BaseMaxHealth);
        SetTripleInt(_statMaxMana, "Max Mana", _statsOwner.ResolvedMaxMana, _statsOwner.BaseMaxMana);
        SetTripleInt(_statPower, "Power", (int)System.Math.Round(_statsOwner.ResolvedPower), (int)System.Math.Round(_statsOwner.BasePower));
        SetTotalInt(_statMP5, "MP5", _statsOwner.ResolvedMP5);
        SetTriplePercent(_statCritRate, "Crit Rate", _statsOwner.ResolvedCritRate, _statsOwner.BaseCritRate);
        SetTriplePercent(_statCritDamage, "Crit Dmg", _statsOwner.ResolvedCritDamage, _statsOwner.BaseCritDamage);
        SetTotalInt(_statHaste, "Haste", _statsOwner.ResolvedHaste);
        SetTriplePercent(_statMoveSpeed, "Speed", _statsOwner.MovementSpeedMultiplier, _statsOwner.BaseMovementSpeedMultiplier);
        SetTotalPercent(_statDamageBonus, "Damage Bonus", _statsOwner.ResolvedGenericDamageBonus);

        _statElementalDmg.Text = "DMG Ph/F/I/Po/A " + FormatElementalLine(
            _statsOwner.ResolveDamageBonus(DamageSchool.Physical),
            _statsOwner.ResolveDamageBonus(DamageSchool.Fire),
            _statsOwner.ResolveDamageBonus(DamageSchool.Ice),
            _statsOwner.ResolveDamageBonus(DamageSchool.Poison),
            _statsOwner.ResolveDamageBonus(DamageSchool.Arcane));

        _statElementalResist.Text = "Resist Ph/F/I/Po/A " + FormatElementalLine(
            _statsOwner.ResolveResistance(DamageSchool.Physical),
            _statsOwner.ResolveResistance(DamageSchool.Fire),
            _statsOwner.ResolveResistance(DamageSchool.Ice),
            _statsOwner.ResolveResistance(DamageSchool.Poison),
            _statsOwner.ResolveResistance(DamageSchool.Arcane));
    }

    private void SetMissingStats()
    {
        foreach (var label in new[]
        {
            _statMaxHealth, _statMaxMana, _statPower, _statMP5,
            _statCritRate, _statCritDamage, _statHaste, _statMoveSpeed,
            _statDamageBonus, _statElementalDmg, _statElementalResist,
        })
        {
            if (label != null)
                label.Text = "-";
        }
    }

    private static void SetTripleInt(Label label, string name, int total, int baseValue)
    {
        if (label == null)
            return;
        var bonus = total - baseValue;
        label.Text = $"{name} {total} ({baseValue} + {bonus})";
    }

    private static void SetTotalInt(Label label, string name, int total)
    {
        if (label == null)
            return;
        label.Text = $"{name} {total}";
    }

    private static void SetTriplePercent(Label label, string name, float total, float baseValue)
    {
        if (label == null)
            return;
        var bonus = total - baseValue;
        label.Text = $"{name} {FormatPercent(total)} ({FormatPercent(baseValue)} + {FormatPercent(bonus)})";
    }

    private static void SetTotalPercent(Label label, string name, float total)
    {
        if (label == null)
            return;
        label.Text = $"{name} {FormatPercent(total)}";
    }

    private static string FormatPercent(float value)
    {
        return ((int)System.Math.Round(value * 100.0f)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    private static string FormatElementalLine(float physical, float fire, float ice, float poison, float arcane)
    {
        return string.Concat(
            FormatPercent(physical), "/",
            FormatPercent(fire), "/",
            FormatPercent(ice), "/",
            FormatPercent(poison), "/",
            FormatPercent(arcane));
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
