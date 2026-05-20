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
    public NodePath LevelingContainerPath { get; set; } = new("Panel/Margin/VBox/LevelingContainer");

    [Export(PropertyHint.Range, "48,128,1")]
    public int LevelingSlotSize { get; set; } = 64;

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
    private VBoxContainer _levelingContainer;
    private WindowDragger _windowDragger;
    private bool _panelPositioned;
    private bool _equipmentChangedBound;
    private bool _inventoryChangedBound;
    private bool _playerLevelBound;
    private bool _statsExpanded;
    private bool _levelingExpanded;

    private string _lastEnhanceSubstatMessage = string.Empty;
    private string _lastEnhanceBankMessage = string.Empty;

    private GearLevelingReferenceSlot _levelingTargetSlot;
    private TextureRect _levelingTargetIcon;
    private Label _levelingTargetPlaceholder;
    private GearLevelingReferenceSlot _levelingMaterialSlot;
    private TextureRect _levelingMaterialIcon;
    private Label _levelingMaterialPlaceholder;
    private Label _levelingMaterialQuantity;
    private Label _levelingLevelLabel;
    private Label _levelingXpLabel;
    private Label _levelingMessageLabel;
    private Label _levelingSubstatLabel;
    private Label _levelingBankLabel;
    private Button _levelingEnhanceButton;

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
        _levelingContainer = GetNodeOrNull<VBoxContainer>(LevelingContainerPath);

        if (_windowPanel != null)
        {
            _windowDragger = new WindowDragger(this, _windowPanel)
            {
                BringToFront = FocusWindow,
            };
        }

        BuildSlots();
        BuildStatsRows();
        BuildLevelingPanel();

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
        UnbindCurrentInventory();
        UnbindCurrentPlayer();

        if (_statsToggle != null)
            _statsToggle.Toggled -= OnStatsToggled;

        if (_levelingToggle != null)
            _levelingToggle.Toggled -= OnLevelingToggled;

        if (_levelingEnhanceButton != null)
            _levelingEnhanceButton.Pressed -= OnEnhancePressed;
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
        if (!ReferenceEquals(_inventory, inventory))
        {
            UnbindCurrentInventory();
            _inventory = inventory;

            if (_inventory != null && GodotObject.IsInstanceValid(_inventory))
            {
                var callable = new Callable(this, nameof(OnInventoryChanged));
                if (!_inventory.IsConnected(InventoryController.SignalName.InventoryChanged, callable))
                    _inventory.Connect(InventoryController.SignalName.InventoryChanged, callable);

                var gearXpCallable = new Callable(this, nameof(OnGearXpChanged));
                if (!_inventory.IsConnected(InventoryController.SignalName.GearXpChanged, gearXpCallable))
                    _inventory.Connect(InventoryController.SignalName.GearXpChanged, gearXpCallable);

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

        foreach (var slotView in _slotViews.Values)
        {
            slotView.Root.Inventory = _inventory;
            slotView.Root.Equipment = _equipment;
        }

        if (_levelingTargetSlot != null)
        {
            _levelingTargetSlot.Inventory = _inventory;
            _levelingTargetSlot.Equipment = _equipment;
        }
        if (_levelingMaterialSlot != null)
        {
            _levelingMaterialSlot.Inventory = _inventory;
            _levelingMaterialSlot.Equipment = _equipment;
        }

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

    private void OnInventoryChanged()
    {
        // Inventory changes can invalidate leveling references (item moved/consumed
        // from the referenced slot). Re-resolve the panel state.
        RefreshLeveling();
    }

    private void OnGearXpChanged(int totalGearXp)
    {
        RefreshLeveling();
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
        RefreshLeveling();
    }

    private void ApplyLevelingExpansion()
    {
        if (_levelingContainer != null)
            _levelingContainer.Visible = _levelingExpanded;

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
        RefreshLeveling();
    }

    private void BuildLevelingPanel()
    {
        if (_levelingContainer == null)
            return;

        foreach (var child in _levelingContainer.GetChildren())
        {
            _levelingContainer.RemoveChild(child);
            child.QueueFree();
        }

        var slotRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slotRow.AddThemeConstantOverride("separation", 12);
        _levelingContainer.AddChild(slotRow);

        BuildLevelingTargetSlot(slotRow);
        BuildLevelingMaterialSlot(slotRow);

        _levelingLevelLabel = new Label
        {
            Text = "Lv -",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _levelingContainer.AddChild(_levelingLevelLabel);

        _levelingXpLabel = new Label
        {
            Text = "XP -",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _levelingContainer.AddChild(_levelingXpLabel);

        _levelingBankLabel = new Label
        {
            Text = "GearXP: 0",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _levelingContainer.AddChild(_levelingBankLabel);

        _levelingMessageLabel = new Label
        {
            Text = string.Empty,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.65f),
        };
        _levelingContainer.AddChild(_levelingMessageLabel);

        _levelingSubstatLabel = new Label
        {
            Text = string.Empty,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _levelingSubstatLabel.AddThemeColorOverride("font_color", new Color(0.6f, 1.0f, 0.6f));
        _levelingContainer.AddChild(_levelingSubstatLabel);

        _levelingEnhanceButton = new Button
        {
            Text = "Enhance",
            Disabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        _levelingEnhanceButton.Pressed += OnEnhancePressed;
        _levelingContainer.AddChild(_levelingEnhanceButton);
    }

    private void BuildLevelingTargetSlot(HBoxContainer row)
    {
        _levelingTargetSlot = new GearLevelingReferenceSlot
        {
            Name = "TargetSlot",
            Kind = GearLevelingReferenceKind.Target,
            Inventory = _inventory,
            Equipment = _equipment,
            CustomMinimumSize = new Vector2(LevelingSlotSize, LevelingSlotSize),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _levelingTargetSlot.ReferenceChanged = OnLevelingReferenceChanged;
        _levelingTargetSlot.FocusRequested = FocusWindow;

        _levelingTargetIcon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _levelingTargetIcon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _levelingTargetSlot.AddChild(_levelingTargetIcon);

        _levelingTargetPlaceholder = new Label
        {
            Name = "Placeholder",
            Text = "target",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.45f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _levelingTargetPlaceholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _levelingTargetSlot.AddChild(_levelingTargetPlaceholder);

        row.AddChild(_levelingTargetSlot);
    }

    private void BuildLevelingMaterialSlot(HBoxContainer row)
    {
        _levelingMaterialSlot = new GearLevelingReferenceSlot
        {
            Name = "MaterialSlot",
            Kind = GearLevelingReferenceKind.Material,
            Inventory = _inventory,
            Equipment = _equipment,
            CustomMinimumSize = new Vector2(LevelingSlotSize, LevelingSlotSize),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _levelingMaterialSlot.ReferenceChanged = OnLevelingReferenceChanged;
        _levelingMaterialSlot.FocusRequested = FocusWindow;

        _levelingMaterialIcon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _levelingMaterialIcon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _levelingMaterialSlot.AddChild(_levelingMaterialIcon);

        _levelingMaterialPlaceholder = new Label
        {
            Name = "Placeholder",
            Text = "XP",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.45f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _levelingMaterialPlaceholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _levelingMaterialSlot.AddChild(_levelingMaterialPlaceholder);

        _levelingMaterialQuantity = new Label
        {
            Name = "Quantity",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _levelingMaterialQuantity.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _levelingMaterialSlot.AddChild(_levelingMaterialQuantity);

        row.AddChild(_levelingMaterialSlot);
    }

    private void RefreshLeveling()
    {
        if (_levelingContainer == null || !_levelingExpanded)
            return;

        var rules = _inventory != null && GodotObject.IsInstanceValid(_inventory)
            ? _inventory.GearGenerationRules
            : null;

        // Target slot: validate and render. If the source vanished or no longer
        // matches, the previous Enhance's roll summary is no longer about gear
        // the user can still see, so drop it.
        GearInstance targetGear = null;
        var hasTarget = _levelingTargetSlot != null && _levelingTargetSlot.ResolveTargetGear(out targetGear);
        if (!hasTarget)
        {
            _levelingTargetSlot?.ClearReference();
            _lastEnhanceSubstatMessage = string.Empty;
        }

        if (_levelingTargetIcon != null && _levelingTargetPlaceholder != null)
        {
            var hasIcon = hasTarget && targetGear?.Definition?.Icon != null;
            _levelingTargetIcon.Texture = hasIcon ? targetGear.Definition.Icon : null;
            _levelingTargetIcon.Visible = hasIcon;
            _levelingTargetIcon.Modulate = hasTarget
                ? GearQualityColors.GetColor(targetGear.Quality)
                : Colors.White;
            _levelingTargetPlaceholder.Visible = !hasTarget;
        }

        if (_levelingTargetSlot != null)
        {
            _levelingTargetSlot.TooltipText = hasTarget ? GearTooltipBuilder.Build(targetGear) : "Target gear";
        }

        // Material slot: validate and render. Material can be either an arcane_crystal
        // stack or an inventory gear entry (fodder).
        InventoryStackEntry crystalEntry = null;
        InventoryGearEntry fodderEntry = null;
        var materialKind = _levelingMaterialSlot != null
            ? _levelingMaterialSlot.ResolveMaterial(out crystalEntry, out fodderEntry)
            : GearLevelingMaterialKind.None;
        if (materialKind == GearLevelingMaterialKind.None)
            _levelingMaterialSlot?.ClearReference();

        var fodderIsSelf = materialKind == GearLevelingMaterialKind.GearFodder &&
                           hasTarget &&
                           fodderEntry?.Gear != null &&
                           ReferenceEquals(fodderEntry.Gear, targetGear);

        if (_levelingMaterialIcon != null && _levelingMaterialPlaceholder != null && _levelingMaterialQuantity != null)
        {
            Texture2D icon = null;
            var iconColor = Colors.White;
            var showQuantity = false;
            var quantityText = string.Empty;

            switch (materialKind)
            {
                case GearLevelingMaterialKind.Crystal:
                    icon = crystalEntry.Stack.Item.Icon;
                    showQuantity = true;
                    quantityText = crystalEntry.Stack.Quantity.ToString();
                    break;
                case GearLevelingMaterialKind.GearFodder:
                    icon = fodderEntry.Gear.Definition?.Icon;
                    iconColor = GearQualityColors.GetColor(fodderEntry.Gear.Quality);
                    break;
            }

            _levelingMaterialIcon.Texture = icon;
            _levelingMaterialIcon.Visible = icon != null;
            _levelingMaterialIcon.Modulate = iconColor;
            _levelingMaterialPlaceholder.Visible = materialKind == GearLevelingMaterialKind.None;
            _levelingMaterialQuantity.Visible = showQuantity;
            _levelingMaterialQuantity.Text = quantityText;
        }

        if (_levelingMaterialSlot != null)
        {
            switch (materialKind)
            {
                case GearLevelingMaterialKind.Crystal:
                    _levelingMaterialSlot.TooltipText =
                        $"{crystalEntry.Stack.Item.DisplayName} x{crystalEntry.Stack.Quantity}";
                    break;
                case GearLevelingMaterialKind.GearFodder:
                    _levelingMaterialSlot.TooltipText = GearTooltipBuilder.Build(fodderEntry.Gear);
                    break;
                default:
                    _levelingMaterialSlot.TooltipText = "Arcane Crystal or fodder gear";
                    break;
            }
        }

        // Level / XP labels and button enable state.
        if (hasTarget && rules != null)
        {
            var maxLevel = GearLevelingService.GetMaxLevel(targetGear, rules);
            var requiredXp = GearLevelingService.GetRequiredExperienceForCurrentLevel(targetGear, rules);
            if (_levelingLevelLabel != null)
                _levelingLevelLabel.Text = $"Level: {targetGear.Level} / {maxLevel}";
            if (_levelingXpLabel != null)
            {
                _levelingXpLabel.Text = targetGear.Level >= maxLevel
                    ? "XP: max"
                    : $"XP: {targetGear.CurrentXp} / {requiredXp}";
            }

            var atMax = targetGear.Level >= maxLevel;
            var hasUsableMaterial =
                materialKind == GearLevelingMaterialKind.Crystal ||
                (materialKind == GearLevelingMaterialKind.GearFodder && !fodderIsSelf);
            var hasBank = _inventory != null && _inventory.GearXp > 0;
            // Enhance can fire on bank-only too (no material) so long as target is below max.
            var canEnhance = !atMax && (hasUsableMaterial || hasBank);

            if (_levelingEnhanceButton != null)
            {
                _levelingEnhanceButton.Text = "Enhance";
                _levelingEnhanceButton.Disabled = !canEnhance;
            }

            if (_levelingMessageLabel != null)
            {
                if (atMax)
                    _levelingMessageLabel.Text = "Already at max level.";
                else if (fodderIsSelf)
                    _levelingMessageLabel.Text = "Can't use the target itself as fodder.";
                else if (materialKind == GearLevelingMaterialKind.None)
                    _levelingMessageLabel.Text = hasBank
                        ? "Bank XP will be spent. Drop crystals or fodder gear for more."
                        : "Drop crystals or fodder gear into the material slot.";
                else if (materialKind == GearLevelingMaterialKind.Crystal)
                    _levelingMessageLabel.Text = $"Crystals: {crystalEntry.Stack.Quantity}";
                else // GearFodder, not self
                    _levelingMessageLabel.Text = $"Fodder XP: {GearLevelingService.ComputeFodderXp(fodderEntry.Gear, rules)}";
            }
        }
        else
        {
            // No target. Allow Store mode when valid inventory gear fodder is referenced.
            if (_levelingLevelLabel != null)
                _levelingLevelLabel.Text = "Level: -";
            if (_levelingXpLabel != null)
                _levelingXpLabel.Text = "XP: -";

            var canStore = rules != null && materialKind == GearLevelingMaterialKind.GearFodder && fodderEntry?.Gear != null;

            if (_levelingEnhanceButton != null)
            {
                _levelingEnhanceButton.Text = canStore ? "Store" : "Enhance";
                _levelingEnhanceButton.Disabled = !canStore;
            }

            if (_levelingMessageLabel != null)
            {
                if (canStore)
                    _levelingMessageLabel.Text = $"Store XP: {GearLevelingService.ComputeFodderXp(fodderEntry.Gear, rules)}";
                else if (materialKind == GearLevelingMaterialKind.Crystal)
                    _levelingMessageLabel.Text = "Drop gear into the target slot to spend crystals.";
                else
                    _levelingMessageLabel.Text = "Drop gear into the target slot.";
            }
        }

        if (_levelingBankLabel != null)
        {
            var bank = _inventory != null && GodotObject.IsInstanceValid(_inventory) ? _inventory.GearXp : 0;
            _levelingBankLabel.Text = $"GearXP: {bank}";
        }

        if (_levelingSubstatLabel != null)
        {
            var combined = JoinMessages(_lastEnhanceSubstatMessage, _lastEnhanceBankMessage);
            _levelingSubstatLabel.Text = combined;
            _levelingSubstatLabel.Visible = !string.IsNullOrEmpty(combined);
        }
    }

    private static string JoinMessages(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b ?? string.Empty;
        if (string.IsNullOrEmpty(b)) return a;
        return a + "\n" + b;
    }

    private void OnLevelingReferenceChanged()
    {
        // The user pointed the panel at a different gear / crystal stack, so the previous
        // Enhance's roll summary is no longer meaningful.
        _lastEnhanceSubstatMessage = string.Empty;
        _lastEnhanceBankMessage = string.Empty;
        RefreshLeveling();
    }

    private void OnEnhancePressed()
    {
        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return;

        var rules = _inventory.GearGenerationRules;
        if (rules == null)
        {
            if (_levelingMessageLabel != null)
                _levelingMessageLabel.Text = "Missing gear rules.";
            return;
        }

        GearInstance targetGear = null;
        var hasTarget = _levelingTargetSlot != null && _levelingTargetSlot.ResolveTargetGear(out targetGear);

        InventoryGearEntry fodderEntry = null;
        var materialKind = GearLevelingMaterialKind.None;
        if (_levelingMaterialSlot != null)
            materialKind = _levelingMaterialSlot.ResolveMaterial(out _, out fodderEntry);

        // Store mode: no target, valid fodder gear → bank its full XP yield.
        if (!hasTarget)
        {
            if (materialKind != GearLevelingMaterialKind.GearFodder || _levelingMaterialSlot == null || fodderEntry?.Gear == null)
            {
                RefreshLeveling();
                return;
            }

            var stored = GearLevelingService.StoreFodder(_inventory, _levelingMaterialSlot.InventorySlotIndex, rules);
            if (stored <= 0)
            {
                RefreshLeveling();
                return;
            }

            _lastEnhanceSubstatMessage = string.Empty;
            _lastEnhanceBankMessage = $"Stored GearXP: +{stored}";
            RefreshLeveling();
            return;
        }

        // Enhance mode: target present. Refuse self-fodder up front.
        if (materialKind == GearLevelingMaterialKind.GearFodder &&
            fodderEntry?.Gear != null &&
            ReferenceEquals(fodderEntry.Gear, targetGear))
        {
            if (_levelingMessageLabel != null)
                _levelingMessageLabel.Text = "Can't use the target itself as fodder.";
            return;
        }

        // Pass the material slot index even when no material is referenced; the service
        // tolerates a missing entry and may still spend stored bank XP on the target.
        var materialSlotIndex = _levelingMaterialSlot != null ? _levelingMaterialSlot.InventorySlotIndex : -1;

        var result = GearLevelingService.Enhance(targetGear, _inventory, materialSlotIndex, rules);

        if (!result.Changed)
        {
            RefreshLeveling();
            return;
        }

        _lastEnhanceSubstatMessage = FormatSubstatRolls(result.SubstatRolls);
        _lastEnhanceBankMessage = result.GearXpGained > 0
            ? $"Stored GearXP: +{result.GearXpGained}"
            : string.Empty;

        // Equipment doesn't actually swap, but stat resolution depends on equipped gear's
        // modifier values, so re-emit so listeners can pick up the new totals.
        if (_equipment != null && GodotObject.IsInstanceValid(_equipment))
            _equipment.EmitSignal(EquipmentController.SignalName.Changed);

        Refresh();
    }

    private static string FormatSubstatRolls(IReadOnlyList<GearStatModifier> rolls)
    {
        if (rolls == null || rolls.Count == 0)
            return string.Empty;

        // Aggregate deltas by stat id, preserving the order each stat first rolled.
        var order = new List<string>();
        var totals = new Dictionary<string, float>(System.StringComparer.Ordinal);
        foreach (var roll in rolls)
        {
            if (roll == null || string.IsNullOrEmpty(roll.StatId))
                continue;
            if (!totals.ContainsKey(roll.StatId))
            {
                order.Add(roll.StatId);
                totals[roll.StatId] = 0.0f;
            }
            totals[roll.StatId] += roll.Value;
        }

        if (order.Count == 0)
            return string.Empty;

        var parts = new List<string>(order.Count);
        foreach (var statId in order)
            parts.Add(GearTooltipBuilder.FormatModifier(new GearStatModifier
            {
                StatId = statId,
                Value = totals[statId],
            }));

        return "Substats: " + string.Join(", ", parts);
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
        SetTriplePercent(_statMoveSpeed, "Move Spd", _statsOwner.MovementSpeedMultiplier, _statsOwner.BaseMovementSpeedMultiplier);
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

    private void UnbindCurrentInventory()
    {
        if (!_inventoryChangedBound || _inventory == null || !GodotObject.IsInstanceValid(_inventory))
        {
            _inventoryChangedBound = false;
            return;
        }

        var callable = new Callable(this, nameof(OnInventoryChanged));
        if (_inventory.IsConnected(InventoryController.SignalName.InventoryChanged, callable))
            _inventory.Disconnect(InventoryController.SignalName.InventoryChanged, callable);

        var gearXpCallable = new Callable(this, nameof(OnGearXpChanged));
        if (_inventory.IsConnected(InventoryController.SignalName.GearXpChanged, gearXpCallable))
            _inventory.Disconnect(InventoryController.SignalName.GearXpChanged, gearXpCallable);

        _inventoryChangedBound = false;
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
