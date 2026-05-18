using Godot;

using System;
using System.Collections.Generic;

public partial class DebugTray : Control
{
    [Signal]
    public delegate void PlayerStatsRequestedEventHandler();

    [Export]
    public NodePath TrayPanelPath { get; set; } = new NodePath("Bottom/Panel");

    [Export]
    public NodePath StatusLabelPath { get; set; } = new NodePath("Bottom/Panel/VBox/Header/Status");

    [Export]
    public NodePath CardsContainerPath { get; set; } = new NodePath("Bottom/Panel/VBox/Scroll/Cards");

    [Export]
    public NodePath FactionSelectorPath { get; set; } = new NodePath("Bottom/Panel/VBox/Controls/FactionSelector");

    [Export]
    public NodePath FactionLabelPath { get; set; } = new NodePath("Bottom/Panel/VBox/Controls/FactionLabel");

    [Export]
    public NodePath ModeSelectorPath { get; set; } = new NodePath("Bottom/Panel/VBox/Controls/ModeSelector");

    [Export]
    public NodePath QualitySelectorPath { get; set; } = new NodePath("Bottom/Panel/VBox/Controls/QualitySelector");

    [Export]
    public NodePath QualityLabelPath { get; set; } = new NodePath("Bottom/Panel/VBox/Controls/QualityLabel");

    [Export]
    public NodePath StatsButtonPath { get; set; } = new NodePath("Bottom/Panel/VBox/Controls/StatsButton");

    [Export]
    public NodePath DebugSpawnerPath { get; set; } = new NodePath("../../World/DebugSpawner");

    private const float DragThreshold = 12.0f;
    private static readonly Vector2 PreviewCenter = new(48.0f, 52.0f);

    private static readonly EquipmentSlot[] GearSlotOrder =
    {
        EquipmentSlot.Head,
        EquipmentSlot.Torso,
        EquipmentSlot.Gloves,
        EquipmentSlot.Legs,
        EquipmentSlot.Boots,
        EquipmentSlot.Ring,
        EquipmentSlot.Artifact,
    };

    private static readonly GearQuality[] QualityOrder =
    {
        GearQuality.Trash,
        GearQuality.Common,
        GearQuality.Uncommon,
        GearQuality.Rare,
        GearQuality.Epic,
        GearQuality.Legendary,
    };

    private DebugSpawner _debugSpawner;
    private Control _trayPanel;
    private Label _statusLabel;
    private HBoxContainer _cardsContainer;
    private OptionButton _factionSelector;
    private Label _factionLabel;
    private OptionButton _modeSelector;
    private OptionButton _qualitySelector;
    private Label _qualityLabel;
    private Button _statsButton;
    private readonly Dictionary<string, Button> _cardsById = new();
    private readonly Dictionary<string, EquipmentSlot> _gearCardSlots = new();
    private readonly Dictionary<Button, Control.GuiInputEventHandler> _cardInputHandlers = new();
    private string _pressedCardId;
    private Vector2 _pressStartScreenPosition;
    private bool _draggingFromCard;
    private SpawnCatalogEntryKind _activeEntryKind = SpawnCatalogEntryKind.Character;
    private GearQuality _selectedGearQuality = GearQuality.Common;

    public bool TrayVisible => Visible;

    public bool HasPendingPlacement => _debugSpawner?.HasPendingPlacement ?? false;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _debugSpawner = GetNodeOrNull<DebugSpawner>(DebugSpawnerPath);
        _trayPanel = GetNodeOrNull<Control>(TrayPanelPath);
        _statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
        _cardsContainer = GetNodeOrNull<HBoxContainer>(CardsContainerPath);
        _factionSelector = GetNodeOrNull<OptionButton>(FactionSelectorPath);
        _factionLabel = GetNodeOrNull<Label>(FactionLabelPath);
        _modeSelector = GetNodeOrNull<OptionButton>(ModeSelectorPath);
        _qualitySelector = GetNodeOrNull<OptionButton>(QualitySelectorPath);
        _qualityLabel = GetNodeOrNull<Label>(QualityLabelPath);
        _statsButton = GetNodeOrNull<Button>(StatsButtonPath);
        if (_statsButton != null)
            _statsButton.Pressed += OnStatsButtonPressed;
        ConfigureControls();
        BuildCardsFromCatalog();

        Visible = false;
        UpdateCardSelection();
        UpdateStatusLabel();
    }

    public override void _ExitTree()
    {
        if (_statsButton != null && GodotObject.IsInstanceValid(_statsButton))
            _statsButton.Pressed -= OnStatsButtonPressed;

        ClearCards();
    }

    private void OnStatsButtonPressed()
    {
        EmitSignal(SignalName.PlayerStatsRequested);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible)
            return;

        switch (@event)
        {
            case InputEventMouseMotion mouseMotion:
                HandleMouseMotion(mouseMotion);
                break;
            case InputEventMouseButton mouseButton:
                HandleMouseButton(mouseButton);
                break;
        }
    }

    public void Open()
    {
        Visible = true;
        SyncFactionSelector();
        UpdateCardSelection();
        UpdateStatusLabel();
    }

    public void Close(bool cancelPlacement = true)
    {
        if (cancelPlacement)
            CancelPlacement();

        Visible = false;
        ClearPressedCardState();
    }

    public bool HandleEscape()
    {
        if (!Visible)
            return false;

        if (HasPendingPlacement)
        {
            CancelPlacement();
            return true;
        }

        return false;
    }

    public void CancelPlacement()
    {
        _debugSpawner?.CancelPlacement();
        ClearPressedCardState();
        UpdateCardSelection();
        UpdateStatusLabel();
    }

    private void BuildCardsFromCatalog()
    {
        if (_cardsContainer == null || _debugSpawner == null)
            return;

        ClearCards();

        if (_activeEntryKind == SpawnCatalogEntryKind.Gear)
        {
            BuildGearCards();
            return;
        }

        var rowsByCategory = new Dictionary<string, HBoxContainer>();

        foreach (var entry in _debugSpawner.GetCatalogEntries())
        {
            if (entry == null || entry.EntryKind != _activeEntryKind || string.IsNullOrWhiteSpace(entry.Id))
                continue;

            var category = NormalizeCategory(entry.Category);
            var categoryRow = GetOrCreateCategoryRow(category, rowsByCategory);
            if (categoryRow == null)
                continue;

            var card = CreateCard(entry);
            if (card == null)
                continue;

            categoryRow.AddChild(card);
            _cardsById[entry.Id] = card;

            Control.GuiInputEventHandler inputHandler = @event => BeginCardPress(entry.Id, @event);
            card.GuiInput += inputHandler;
            _cardInputHandlers[card] = inputHandler;
        }
    }

    private void BuildGearCards()
    {
        var rowsByCategory = new Dictionary<string, HBoxContainer>();
        var categoryRow = GetOrCreateCategoryRow("Gear Slots", rowsByCategory);
        if (categoryRow == null)
            return;

        foreach (var slot in GearSlotOrder)
        {
            var cardId = $"gear_{slot}";
            var card = CreateGearCard(slot);
            if (card == null)
                continue;

            categoryRow.AddChild(card);
            _cardsById[cardId] = card;
            _gearCardSlots[cardId] = slot;

            Control.GuiInputEventHandler inputHandler = @event => BeginCardPress(cardId, @event);
            card.GuiInput += inputHandler;
            _cardInputHandlers[card] = inputHandler;
        }
    }

    private Button CreateGearCard(EquipmentSlot slot)
    {
        var card = new Button
        {
            Name = $"gear_{slot}_Card",
            CustomMinimumSize = new Vector2(124.0f, 120.0f),
            ToggleMode = true,
            Text = string.Empty,
        };

        var margin = new MarginContainer
        {
            Name = "Margin",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.OffsetLeft = 8.0f;
        margin.OffsetTop = 8.0f;
        margin.OffsetRight = -8.0f;
        margin.OffsetBottom = -8.0f;
        card.AddChild(margin);

        var vBox = new VBoxContainer
        {
            Name = "VBox",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        vBox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vBox);

        var iconRect = new TextureRect
        {
            Name = "Icon",
            CustomMinimumSize = new Vector2(64.0f, 64.0f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        var slotRules = _debugSpawner?.GetGearSlotRules(slot);
        iconRect.Texture = slotRules?.Icon;
        vBox.AddChild(iconRect);

        var label = new Label
        {
            Name = "Label",
            Text = slot.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vBox.AddChild(label);

        return card;
    }

    private void ConfigureControls()
    {
        if (_modeSelector != null)
        {
            _modeSelector.Clear();
            AddModeOption("Characters", SpawnCatalogEntryKind.Character);
            AddModeOption("Drops", SpawnCatalogEntryKind.Drop);
            AddModeOption("Gear", SpawnCatalogEntryKind.Gear);
            _modeSelector.ItemSelected += OnModeSelected;
            SyncModeSelector();
        }

        if (_factionSelector != null)
        {
            _factionSelector.Clear();
            AddFactionOption("Enemies", Factions.Enemies.Key);
            AddFactionOption("Allies", Factions.Allies.Key);
            AddFactionOption("Neutral", Factions.Neutral.Key);
            _factionSelector.ItemSelected += OnFactionSelected;
            SyncFactionSelector();
        }

        if (_qualitySelector != null)
        {
            _qualitySelector.Clear();
            foreach (var quality in QualityOrder)
            {
                _qualitySelector.AddItem(quality.ToString());
                _qualitySelector.SetItemMetadata(_qualitySelector.ItemCount - 1, (int)quality);
            }
            _qualitySelector.ItemSelected += OnQualitySelected;
            SyncQualitySelector();
        }

        UpdateQualitySelectorVisibility();
    }

    private void AddModeOption(string label, SpawnCatalogEntryKind entryKind)
    {
        if (_modeSelector == null)
            return;

        _modeSelector.AddItem(label);
        _modeSelector.SetItemMetadata(_modeSelector.ItemCount - 1, (int)entryKind);
    }

    private void AddFactionOption(string label, string factionKey)
    {
        if (_factionSelector == null)
            return;

        _factionSelector.AddItem(label);
        _factionSelector.SetItemMetadata(_factionSelector.ItemCount - 1, factionKey);
    }

    private HBoxContainer GetOrCreateCategoryRow(string category, Dictionary<string, HBoxContainer> rowsByCategory)
    {
        if (rowsByCategory.TryGetValue(category, out var existingRow))
            return existingRow;

        var section = new VBoxContainer
        {
            Name = $"{category}_Section",
            CustomMinimumSize = new Vector2(140.0f, 0.0f),
        };
        section.AddThemeConstantOverride("separation", 6);

        var categoryLabel = new Label
        {
            Name = "CategoryLabel",
            Text = category,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        section.AddChild(categoryLabel);

        var row = new HBoxContainer
        {
            Name = "CardsRow",
        };
        row.AddThemeConstantOverride("separation", 12);
        section.AddChild(row);

        _cardsContainer.AddChild(section);
        rowsByCategory[category] = row;
        return row;
    }

    private static string NormalizeCategory(string category)
    {
        return string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category.Trim();
    }

    private Button CreateCard(SpawnCatalogEntry entry)
    {
        var card = new Button
        {
            Name = $"{entry.Id}_Card",
            CustomMinimumSize = new Vector2(124.0f, 120.0f),
            ToggleMode = true,
            Text = string.Empty,
        };

        var margin = new MarginContainer
        {
            Name = "Margin",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.OffsetLeft = 8.0f;
        margin.OffsetTop = 8.0f;
        margin.OffsetRight = -8.0f;
        margin.OffsetBottom = -8.0f;
        card.AddChild(margin);

        var vBox = new VBoxContainer
        {
            Name = "VBox",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        vBox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vBox);

        var previewContainer = new SubViewportContainer
        {
            Name = "PreviewContainer",
            CustomMinimumSize = new Vector2(96.0f, 96.0f),
            Stretch = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        vBox.AddChild(previewContainer);

        var previewViewport = new SubViewport
        {
            Name = "PreviewViewport",
            HandleInputLocally = false,
            Disable3D = true,
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = new Vector2I(96, 96),
        };
        previewContainer.AddChild(previewViewport);

        var animatedPreviewSprite = new AnimatedSprite2D
        {
            Name = "AnimatedPreviewSprite",
        };
        previewViewport.AddChild(animatedPreviewSprite);

        var staticPreviewSprite = new Sprite2D
        {
            Name = "StaticPreviewSprite",
            Centered = true,
            Visible = false,
        };
        previewViewport.AddChild(staticPreviewSprite);

        var label = new Label
        {
            Name = "Label",
            Text = entry.DisplayName,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vBox.AddChild(label);

        ConfigurePreview(entry.Id, animatedPreviewSprite, staticPreviewSprite);
        return card;
    }

    private void ConfigurePreview(string spawnId, AnimatedSprite2D animatedPreviewSprite, Sprite2D staticPreviewSprite)
    {
        if (_debugSpawner == null)
            return;

        var spriteFrames = _debugSpawner.GetPreviewFrames(spawnId);
        if (spriteFrames != null && animatedPreviewSprite != null)
        {
            var animationName = _debugSpawner.GetPreviewAnimationName(spawnId);
            animatedPreviewSprite.SpriteFrames = spriteFrames;
            animatedPreviewSprite.Scale = _debugSpawner.GetPreviewScale(spawnId);
            animatedPreviewSprite.Position = PreviewCenter + _debugSpawner.GetPreviewOffset(spawnId);
            animatedPreviewSprite.Visible = true;

            if (!animationName.IsEmpty && spriteFrames.HasAnimation(animationName))
            {
                animatedPreviewSprite.Play(animationName);
                if (staticPreviewSprite != null)
                    staticPreviewSprite.Visible = false;
                return;
            }

            if (spriteFrames.HasAnimation("walk_south"))
            {
                animatedPreviewSprite.Play("walk_south");
                if (staticPreviewSprite != null)
                    staticPreviewSprite.Visible = false;
                return;
            }
        }

        var texture = _debugSpawner.GetPreviewTexture(spawnId);
        if (texture == null || staticPreviewSprite == null)
            return;

        staticPreviewSprite.Texture = texture;
        staticPreviewSprite.Scale = _debugSpawner.GetPreviewScale(spawnId);
        staticPreviewSprite.Position = PreviewCenter + _debugSpawner.GetPreviewOffset(spawnId);
        staticPreviewSprite.Visible = true;
        if (animatedPreviewSprite != null)
            animatedPreviewSprite.Visible = false;
    }

    private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
    {
        if (string.IsNullOrEmpty(_pressedCardId) || _draggingFromCard)
            return;

        if ((mouseMotion.ButtonMask & MouseButtonMask.Left) == 0)
            return;

        if (mouseMotion.GlobalPosition.DistanceTo(_pressStartScreenPosition) < DragThreshold)
            return;

        _draggingFromCard = true;
        BeginPlacementForPressedCard();
        SyncFactionSelector();
        UpdateCardSelection();
        UpdateStatusLabel();
        GetViewport().SetInputAsHandled();
    }

    private void BeginPlacementForPressedCard()
    {
        if (_debugSpawner == null || string.IsNullOrEmpty(_pressedCardId))
            return;

        if (_activeEntryKind == SpawnCatalogEntryKind.Gear &&
            _gearCardSlots.TryGetValue(_pressedCardId, out var slot))
        {
            _debugSpawner.BeginGearPlacement(slot, _selectedGearQuality);
            return;
        }

        _debugSpawner.BeginPlacement(_pressedCardId);
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed && HasPendingPlacement)
        {
            CancelPlacement();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Left)
            return;

        var screenPosition = mouseButton.GlobalPosition;

        if (!mouseButton.Pressed)
        {
            HandleLeftMouseRelease(screenPosition, mouseButton.ShiftPressed);
            return;
        }

        if (!string.IsNullOrEmpty(_pressedCardId))
            return;

        if (HasPendingPlacement && !IsMouseOverTray(screenPosition) && _debugSpawner != null)
        {
            _debugSpawner.PlacePendingAtCursor(mouseButton.ShiftPressed);
            UpdateCardSelection();
            UpdateStatusLabel();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!HasPendingPlacement &&
            !IsMouseOverTray(screenPosition) &&
            _debugSpawner != null)
        {
            if (_debugSpawner.TryBeginPlacementFromActorAtScreenPosition(screenPosition) ||
                _debugSpawner.TrySelectFactionAtScreenPosition(screenPosition))
            {
                SyncFactionSelector();
                UpdateCardSelection();
                UpdateStatusLabel();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void HandleLeftMouseRelease(Vector2 screenPosition, bool shiftPressed)
    {
        if (string.IsNullOrEmpty(_pressedCardId))
            return;

        if (_draggingFromCard && _debugSpawner != null)
        {
            if (IsMouseOverTray(screenPosition))
                _debugSpawner.CancelPlacement();
            else
                _debugSpawner.PlacePendingAtCursor(shiftPressed);
        }
        else if (IsMouseOverCard(_pressedCardId, screenPosition))
        {
            BeginPlacementForPressedCard();
            SyncFactionSelector();
        }

        ClearPressedCardState();
        UpdateCardSelection();
        UpdateStatusLabel();
        GetViewport().SetInputAsHandled();
    }

    private void ClearPressedCardState()
    {
        _pressedCardId = null;
        _draggingFromCard = false;
    }

    private void UpdateCardSelection()
    {
        foreach (var (cardId, card) in _cardsById)
        {
            if (card == null)
                continue;

            var selected = false;
            if (_debugSpawner != null && HasPendingPlacement)
            {
                if (_debugSpawner.HasPendingGear &&
                    _gearCardSlots.TryGetValue(cardId, out var slot) &&
                    _debugSpawner.PendingGearSlot == slot)
                {
                    selected = true;
                }
                else if (_debugSpawner.PendingSpawnId == cardId)
                {
                    selected = true;
                }
            }

            card.ButtonPressed = selected;
        }
    }

    private void UpdateStatusLabel()
    {
        if (_statusLabel == null)
            return;

        var modeSummary = GetModeSummary();
        if (_draggingFromCard)
        {
            _statusLabel.Text = $"{modeSummary} Release in the world to place. Release over tray, right click, or Esc to cancel.";
            return;
        }

        if (HasPendingPlacement)
        {
            _statusLabel.Text = $"{modeSummary} Click in the world to place. Right click or Esc cancels.";
            return;
        }

        _statusLabel.Text = $"{modeSummary} Click a card to arm placement, or drag it out into the world.";
    }

    private string GetModeSummary()
    {
        if (_activeEntryKind == SpawnCatalogEntryKind.Drop)
            return "Mode: Drops.";

        if (_activeEntryKind == SpawnCatalogEntryKind.Gear)
            return $"Mode: Gear. Quality: {_selectedGearQuality}.";

        var factionKey = _debugSpawner?.SelectedFaction?.Key ?? Factions.Enemies.Key;
        return $"Mode: Characters. Faction: {Capitalize(factionKey)}.";
    }

    private bool IsMouseOverTray(Vector2 screenPosition)
    {
        return _trayPanel != null && _trayPanel.GetGlobalRect().HasPoint(screenPosition);
    }

    private bool IsMouseOverCard(string enemyId, Vector2 screenPosition)
    {
        return _cardsById.TryGetValue(enemyId, out var card) && card != null && card.GetGlobalRect().HasPoint(screenPosition);
    }

    private void BeginCardPress(string enemyId, InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton || mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
            return;

        _pressedCardId = enemyId;
        _pressStartScreenPosition = mouseButton.GlobalPosition;
        _draggingFromCard = false;
        GetViewport().SetInputAsHandled();
    }

    private void OnFactionSelected(long index)
    {
        if (_debugSpawner == null || _factionSelector == null)
            return;

        _debugSpawner.SetSelectedFaction(_factionSelector.GetItemMetadata((int)index).AsString());
        UpdateStatusLabel();
    }

    private void OnModeSelected(long index)
    {
        if (_modeSelector == null)
            return;

        _activeEntryKind = (SpawnCatalogEntryKind)_modeSelector.GetItemMetadata((int)index).AsInt32();
        CancelPlacement();
        BuildCardsFromCatalog();
        SyncModeSelector();
        SyncFactionSelector();
        UpdateQualitySelectorVisibility();
        UpdateCardSelection();
        UpdateStatusLabel();
    }

    private void OnQualitySelected(long index)
    {
        if (_qualitySelector == null)
            return;

        _selectedGearQuality = (GearQuality)_qualitySelector.GetItemMetadata((int)index).AsInt32();
        UpdateStatusLabel();
    }

    private void UpdateQualitySelectorVisibility()
    {
        var isGearMode = _activeEntryKind == SpawnCatalogEntryKind.Gear;
        if (_qualitySelector != null)
            _qualitySelector.Visible = isGearMode;
        if (_qualityLabel != null)
            _qualityLabel.Visible = isGearMode;
    }

    private void SyncQualitySelector()
    {
        if (_qualitySelector == null)
            return;

        for (var index = 0; index < _qualitySelector.ItemCount; index++)
        {
            if ((GearQuality)_qualitySelector.GetItemMetadata(index).AsInt32() != _selectedGearQuality)
                continue;

            _qualitySelector.Select(index);
            break;
        }
    }

    private void SyncFactionSelector()
    {
        if (_factionSelector == null)
            return;

        var isCharacterMode = _activeEntryKind == SpawnCatalogEntryKind.Character;
        _factionSelector.Visible = isCharacterMode;
        _factionSelector.Disabled = !isCharacterMode;
        if (_factionLabel != null)
            _factionLabel.Visible = isCharacterMode;

        var selectedKey = _debugSpawner?.SelectedFaction?.Key ?? Factions.Enemies.Key;
        for (var index = 0; index < _factionSelector.ItemCount; index++)
        {
            if (_factionSelector.GetItemMetadata(index).AsString() != selectedKey)
                continue;

            _factionSelector.Select(index);
            break;
        }
    }

    private void SyncModeSelector()
    {
        if (_modeSelector == null)
            return;

        for (var index = 0; index < _modeSelector.ItemCount; index++)
        {
            if ((SpawnCatalogEntryKind)_modeSelector.GetItemMetadata(index).AsInt32() != _activeEntryKind)
                continue;

            _modeSelector.Select(index);
            break;
        }
    }

    private void ClearCards()
    {
        _cardInputHandlers.Clear();
        _cardsById.Clear();
        _gearCardSlots.Clear();

        if (_cardsContainer == null)
            return;

        foreach (var child in _cardsContainer.GetChildren())
        {
            _cardsContainer.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string Capitalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unknown";

        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}
