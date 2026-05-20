using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class GearLevelingPanel : VBoxContainer
{
    [Export(PropertyHint.Range, "48,128,1")]
    public int LevelingSlotSize { get; set; } = 64;

    public Action FocusRequested { get; set; }

    private InventoryController _inventory;
    private EquipmentController _equipment;
    private bool _inventoryChangedBound;

    private GearLevelingReferenceSlot _targetSlot;
    private TextureRect _targetIcon;
    private Label _targetPlaceholder;
    private GearLevelingReferenceSlot _materialSlot;
    private TextureRect _materialIcon;
    private Label _materialPlaceholder;
    private Label _materialQuantity;
    private Label _levelLabel;
    private Label _xpLabel;
    private Label _messageLabel;
    private Label _substatLabel;
    private Label _bankLabel;
    private Button _enhanceButton;

    private string _lastEnhanceSubstatMessage = string.Empty;
    private string _lastEnhanceBankMessage = string.Empty;

    public override void _Ready()
    {
        BuildUi();
    }

    public override void _ExitTree()
    {
        UnbindCurrentInventory();

        if (_enhanceButton != null)
            _enhanceButton.Pressed -= OnEnhancePressed;
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

        _equipment = equipment;

        if (_targetSlot != null)
        {
            _targetSlot.Inventory = _inventory;
            _targetSlot.Equipment = _equipment;
        }
        if (_materialSlot != null)
        {
            _materialSlot.Inventory = _inventory;
            _materialSlot.Equipment = _equipment;
        }

        RefreshPanel();
    }

    public void RefreshPanel()
    {
        if (!Visible)
            return;

        var rules = _inventory != null && GodotObject.IsInstanceValid(_inventory)
            ? _inventory.GearGenerationRules
            : null;

        // Target slot: validate and render. If the source vanished or no longer
        // matches, the previous Enhance's roll summary is no longer about gear
        // the user can still see, so drop it.
        GearInstance targetGear = null;
        var hasTarget = _targetSlot != null && _targetSlot.ResolveTargetGear(out targetGear);
        if (!hasTarget)
        {
            _targetSlot?.ClearReference();
            _lastEnhanceSubstatMessage = string.Empty;
        }

        if (_targetIcon != null && _targetPlaceholder != null)
        {
            var hasIcon = hasTarget && targetGear?.Definition?.Icon != null;
            _targetIcon.Texture = hasIcon ? targetGear.Definition.Icon : null;
            _targetIcon.Visible = hasIcon;
            _targetIcon.Modulate = hasTarget
                ? GearQualityColors.GetColor(targetGear.Quality)
                : Colors.White;
            _targetPlaceholder.Visible = !hasTarget;
        }

        if (_targetSlot != null)
        {
            _targetSlot.TooltipText = hasTarget ? GearTooltipBuilder.Build(targetGear) : "Target gear";
        }

        // Material slot: validate and render. Material can be either an arcane_crystal
        // stack or an inventory gear entry (fodder).
        InventoryStackEntry crystalEntry = null;
        InventoryGearEntry fodderEntry = null;
        var materialKind = _materialSlot != null
            ? _materialSlot.ResolveMaterial(out crystalEntry, out fodderEntry)
            : GearLevelingMaterialKind.None;
        if (materialKind == GearLevelingMaterialKind.None)
            _materialSlot?.ClearReference();

        var fodderIsSelf = materialKind == GearLevelingMaterialKind.GearFodder &&
                           hasTarget &&
                           fodderEntry?.Gear != null &&
                           ReferenceEquals(fodderEntry.Gear, targetGear);

        if (_materialIcon != null && _materialPlaceholder != null && _materialQuantity != null)
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

            _materialIcon.Texture = icon;
            _materialIcon.Visible = icon != null;
            _materialIcon.Modulate = iconColor;
            _materialPlaceholder.Visible = materialKind == GearLevelingMaterialKind.None;
            _materialQuantity.Visible = showQuantity;
            _materialQuantity.Text = quantityText;
        }

        if (_materialSlot != null)
        {
            switch (materialKind)
            {
                case GearLevelingMaterialKind.Crystal:
                    _materialSlot.TooltipText =
                        $"{crystalEntry.Stack.Item.DisplayName} x{crystalEntry.Stack.Quantity}";
                    break;
                case GearLevelingMaterialKind.GearFodder:
                    _materialSlot.TooltipText = GearTooltipBuilder.Build(fodderEntry.Gear);
                    break;
                default:
                    _materialSlot.TooltipText = "Arcane Crystal or fodder gear";
                    break;
            }
        }

        // Level / XP labels and button enable state.
        if (hasTarget && rules != null)
        {
            var maxLevel = GearLevelingService.GetMaxLevel(targetGear, rules);
            var requiredXp = GearLevelingService.GetRequiredExperienceForCurrentLevel(targetGear, rules);
            if (_levelLabel != null)
                _levelLabel.Text = $"Level: {targetGear.Level} / {maxLevel}";
            if (_xpLabel != null)
            {
                _xpLabel.Text = targetGear.Level >= maxLevel
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

            if (_enhanceButton != null)
            {
                _enhanceButton.Text = "Enhance";
                _enhanceButton.Disabled = !canEnhance;
            }

            if (_messageLabel != null)
            {
                if (atMax)
                    _messageLabel.Text = "Already at max level.";
                else if (fodderIsSelf)
                    _messageLabel.Text = "Can't use the target itself as fodder.";
                else if (materialKind == GearLevelingMaterialKind.None)
                    _messageLabel.Text = hasBank
                        ? "Bank XP will be spent. Drop crystals or fodder gear for more."
                        : "Drop crystals or fodder gear into the material slot.";
                else if (materialKind == GearLevelingMaterialKind.Crystal)
                    _messageLabel.Text = $"Crystals: {crystalEntry.Stack.Quantity}";
                else // GearFodder, not self
                    _messageLabel.Text = $"Fodder XP: {GearLevelingService.ComputeFodderXp(fodderEntry.Gear, rules)}";
            }
        }
        else
        {
            // No target. Allow Store mode when valid inventory gear fodder is referenced.
            if (_levelLabel != null)
                _levelLabel.Text = "Level: -";
            if (_xpLabel != null)
                _xpLabel.Text = "XP: -";

            var canStore = rules != null && materialKind == GearLevelingMaterialKind.GearFodder && fodderEntry?.Gear != null;

            if (_enhanceButton != null)
            {
                _enhanceButton.Text = canStore ? "Store" : "Enhance";
                _enhanceButton.Disabled = !canStore;
            }

            if (_messageLabel != null)
            {
                if (canStore)
                    _messageLabel.Text = $"Store XP: {GearLevelingService.ComputeFodderXp(fodderEntry.Gear, rules)}";
                else if (materialKind == GearLevelingMaterialKind.Crystal)
                    _messageLabel.Text = "Drop gear into the target slot to spend crystals.";
                else
                    _messageLabel.Text = "Drop gear into the target slot.";
            }
        }

        if (_bankLabel != null)
        {
            var bank = _inventory != null && GodotObject.IsInstanceValid(_inventory) ? _inventory.GearXp : 0;
            _bankLabel.Text = $"GearXP: {bank}";
        }

        if (_substatLabel != null)
        {
            var combined = JoinMessages(_lastEnhanceSubstatMessage, _lastEnhanceBankMessage);
            _substatLabel.Text = combined;
            _substatLabel.Visible = !string.IsNullOrEmpty(combined);
        }
    }

    private void BuildUi()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var slotRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slotRow.AddThemeConstantOverride("separation", 12);
        AddChild(slotRow);

        BuildTargetSlot(slotRow);
        BuildMaterialSlot(slotRow);

        _levelLabel = new Label
        {
            Text = "Lv -",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        AddChild(_levelLabel);

        _xpLabel = new Label
        {
            Text = "XP -",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        AddChild(_xpLabel);

        _bankLabel = new Label
        {
            Text = "GearXP: 0",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        AddChild(_bankLabel);

        _messageLabel = new Label
        {
            Text = string.Empty,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.65f),
        };
        AddChild(_messageLabel);

        _substatLabel = new Label
        {
            Text = string.Empty,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _substatLabel.AddThemeColorOverride("font_color", new Color(0.6f, 1.0f, 0.6f));
        AddChild(_substatLabel);

        _enhanceButton = new Button
        {
            Text = "Enhance",
            Disabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        _enhanceButton.Pressed += OnEnhancePressed;
        AddChild(_enhanceButton);
    }

    private void BuildTargetSlot(HBoxContainer row)
    {
        _targetSlot = new GearLevelingReferenceSlot
        {
            Name = "TargetSlot",
            Kind = GearLevelingReferenceKind.Target,
            Inventory = _inventory,
            Equipment = _equipment,
            CustomMinimumSize = new Vector2(LevelingSlotSize, LevelingSlotSize),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _targetSlot.ReferenceChanged = OnLevelingReferenceChanged;
        _targetSlot.FocusRequested = InvokeFocusRequested;

        _targetIcon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _targetIcon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _targetSlot.AddChild(_targetIcon);

        _targetPlaceholder = new Label
        {
            Name = "Placeholder",
            Text = "target",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.45f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _targetPlaceholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _targetSlot.AddChild(_targetPlaceholder);

        row.AddChild(_targetSlot);
    }

    private void BuildMaterialSlot(HBoxContainer row)
    {
        _materialSlot = new GearLevelingReferenceSlot
        {
            Name = "MaterialSlot",
            Kind = GearLevelingReferenceKind.Material,
            Inventory = _inventory,
            Equipment = _equipment,
            CustomMinimumSize = new Vector2(LevelingSlotSize, LevelingSlotSize),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _materialSlot.ReferenceChanged = OnLevelingReferenceChanged;
        _materialSlot.FocusRequested = InvokeFocusRequested;

        _materialIcon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _materialIcon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _materialSlot.AddChild(_materialIcon);

        _materialPlaceholder = new Label
        {
            Name = "Placeholder",
            Text = "XP",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.45f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _materialPlaceholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _materialSlot.AddChild(_materialPlaceholder);

        _materialQuantity = new Label
        {
            Name = "Quantity",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _materialQuantity.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _materialSlot.AddChild(_materialQuantity);

        row.AddChild(_materialSlot);
    }

    private void InvokeFocusRequested()
    {
        FocusRequested?.Invoke();
    }

    private void OnInventoryChanged()
    {
        // Inventory changes can invalidate leveling references (item moved/consumed
        // from the referenced slot). Re-resolve the panel state.
        RefreshPanel();
    }

    private void OnGearXpChanged(int totalGearXp)
    {
        RefreshPanel();
    }

    private void OnLevelingReferenceChanged()
    {
        // The user pointed the panel at a different gear / crystal stack, so the previous
        // Enhance's roll summary is no longer meaningful.
        _lastEnhanceSubstatMessage = string.Empty;
        _lastEnhanceBankMessage = string.Empty;
        RefreshPanel();
    }

    private void OnEnhancePressed()
    {
        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory))
            return;

        var rules = _inventory.GearGenerationRules;
        if (rules == null)
        {
            if (_messageLabel != null)
                _messageLabel.Text = "Missing gear rules.";
            return;
        }

        GearInstance targetGear = null;
        var hasTarget = _targetSlot != null && _targetSlot.ResolveTargetGear(out targetGear);

        InventoryGearEntry fodderEntry = null;
        var materialKind = GearLevelingMaterialKind.None;
        if (_materialSlot != null)
            materialKind = _materialSlot.ResolveMaterial(out _, out fodderEntry);

        // Store mode: no target, valid fodder gear → bank its full XP yield.
        if (!hasTarget)
        {
            if (materialKind != GearLevelingMaterialKind.GearFodder || _materialSlot == null || fodderEntry?.Gear == null)
            {
                RefreshPanel();
                return;
            }

            var stored = GearLevelingService.StoreFodder(_inventory, _materialSlot.InventorySlotIndex, rules);
            if (stored <= 0)
            {
                RefreshPanel();
                return;
            }

            _lastEnhanceSubstatMessage = string.Empty;
            _lastEnhanceBankMessage = $"Stored GearXP: +{stored}";
            RefreshPanel();
            return;
        }

        // Enhance mode: target present. Refuse self-fodder up front.
        if (materialKind == GearLevelingMaterialKind.GearFodder &&
            fodderEntry?.Gear != null &&
            ReferenceEquals(fodderEntry.Gear, targetGear))
        {
            if (_messageLabel != null)
                _messageLabel.Text = "Can't use the target itself as fodder.";
            return;
        }

        // Pass the material slot index even when no material is referenced; the service
        // tolerates a missing entry and may still spend stored bank XP on the target.
        var materialSlotIndex = _materialSlot != null ? _materialSlot.InventorySlotIndex : -1;

        var result = GearLevelingService.Enhance(targetGear, _inventory, materialSlotIndex, rules);

        if (!result.Changed)
        {
            RefreshPanel();
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

        RefreshPanel();
    }

    private static string FormatSubstatRolls(IReadOnlyList<GearStatModifier> rolls)
    {
        if (rolls == null || rolls.Count == 0)
            return string.Empty;

        // Aggregate deltas by stat id, preserving the order each stat first rolled.
        var order = new List<string>();
        var totals = new Dictionary<string, float>(StringComparer.Ordinal);
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

    private static string JoinMessages(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b ?? string.Empty;
        if (string.IsNullOrEmpty(b)) return a;
        return a + "\n" + b;
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
}
