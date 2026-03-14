using Godot;

using System;
using System.Collections.Generic;

public partial class DebugTray : Control
{
    [Export]
    public NodePath TrayPanelPath { get; set; } = new NodePath("Bottom/Panel");

    [Export]
    public NodePath StatusLabelPath { get; set; } = new NodePath("Bottom/Panel/VBox/Header/Status");

    [Export]
    public NodePath CardsContainerPath { get; set; } = new NodePath("Bottom/Panel/VBox/Scroll/Cards");

    [Export]
    public NodePath DebugSpawnerPath { get; set; } = new NodePath("../../World/DebugSpawner");

    private const float DragThreshold = 12.0f;
    private static readonly Vector2 PreviewCenter = new(48.0f, 52.0f);

    private DebugSpawner _debugSpawner;
    private Control _trayPanel;
    private Label _statusLabel;
    private HBoxContainer _cardsContainer;
    private readonly Dictionary<string, Button> _cardsById = new();
    private readonly Dictionary<Button, Control.GuiInputEventHandler> _cardInputHandlers = new();
    private string _pressedCardId;
    private Vector2 _pressStartScreenPosition;
    private bool _draggingFromCard;

    public bool TrayVisible => Visible;

    public bool HasPendingPlacement => _debugSpawner?.HasPendingPlacement ?? false;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _debugSpawner = GetNodeOrNull<DebugSpawner>(DebugSpawnerPath);
        _trayPanel = GetNodeOrNull<Control>(TrayPanelPath);
        _statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
        _cardsContainer = GetNodeOrNull<HBoxContainer>(CardsContainerPath);

        BuildCardsFromCatalog();

        Visible = false;
        UpdateCardSelection();
        UpdateStatusLabel();
    }

    public override void _ExitTree()
    {
        foreach (var (button, handler) in _cardInputHandlers)
        {
            if (button != null)
                button.GuiInput -= handler;
        }

        if (_cardsContainer != null)
        {
            foreach (var child in _cardsContainer.GetChildren())
                child.QueueFree();
        }

        _cardsById.Clear();
        _cardInputHandlers.Clear();
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

        foreach (var child in _cardsContainer.GetChildren())
            child.QueueFree();

        _cardsById.Clear();
        _cardInputHandlers.Clear();
        var rowsByCategory = new Dictionary<string, HBoxContainer>();

        foreach (var entry in _debugSpawner.GetCatalogEntries())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
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

        var previewSprite = new AnimatedSprite2D
        {
            Name = "PreviewSprite",
        };
        previewViewport.AddChild(previewSprite);

        var label = new Label
        {
            Name = "Label",
            Text = entry.DisplayName,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vBox.AddChild(label);

        ConfigurePreview(entry.Id, previewSprite);
        return card;
    }

    private void ConfigurePreview(string enemyId, AnimatedSprite2D previewSprite)
    {
        if (_debugSpawner == null || previewSprite == null)
            return;

        var spriteFrames = _debugSpawner.GetPreviewFrames(enemyId);
        var animationName = _debugSpawner.GetPreviewAnimationName(enemyId);
        previewSprite.SpriteFrames = spriteFrames;
        previewSprite.Scale = _debugSpawner.GetPreviewScale(enemyId);
        previewSprite.Position = PreviewCenter + _debugSpawner.GetPreviewOffset(enemyId);

        if (spriteFrames == null)
            return;

        if (!animationName.IsEmpty && spriteFrames.HasAnimation(animationName))
        {
            previewSprite.Play(animationName);
            return;
        }

        if (spriteFrames.HasAnimation("walk_south"))
            previewSprite.Play("walk_south");
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
        _debugSpawner?.BeginPlacement(_pressedCardId);
        UpdateCardSelection();
        UpdateStatusLabel();
        GetViewport().SetInputAsHandled();
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
            _debugSpawner?.BeginPlacement(_pressedCardId);
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
        foreach (var (enemyId, card) in _cardsById)
        {
            if (card == null)
                continue;

            card.ButtonPressed = HasPendingPlacement && _debugSpawner?.PendingSpawnId == enemyId;
        }
    }

    private void UpdateStatusLabel()
    {
        if (_statusLabel == null)
            return;

        if (_draggingFromCard)
        {
            _statusLabel.Text = "Release in the world to place. Release over tray, right click, or Esc to cancel.";
            return;
        }

        if (HasPendingPlacement)
        {
            _statusLabel.Text = "Click in the world to place. Right click or Esc cancels.";
            return;
        }

        _statusLabel.Text = "Click a card to arm placement, or drag it out into the world.";
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
}
