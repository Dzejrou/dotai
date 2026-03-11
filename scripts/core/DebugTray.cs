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
    public NodePath DebugEnemySpawnerPath { get; set; } = new NodePath("../../World/DebugEnemySpawner");

    private const float DragThreshold = 12.0f;
    private static readonly Vector2 PreviewCenter = new(48.0f, 52.0f);

    private DebugEnemySpawner _debug_enemy_spawner;
    private Control _tray_panel;
    private Label _status_label;
    private HBoxContainer _cards_container;
    private readonly Dictionary<string, Button> _cards_by_id = new();
    private readonly Dictionary<Button, Control.GuiInputEventHandler> _card_input_handlers = new();
    private string _pressed_card_id;
    private Vector2 _press_start_screen_position;
    private bool _dragging_from_card;

    public bool TrayVisible => Visible;

    public bool HasPendingPlacement => _debug_enemy_spawner?.HasPendingPlacement ?? false;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _debug_enemy_spawner = GetNodeOrNull<DebugEnemySpawner>(DebugEnemySpawnerPath);
        _tray_panel = GetNodeOrNull<Control>(TrayPanelPath);
        _status_label = GetNodeOrNull<Label>(StatusLabelPath);
        _cards_container = GetNodeOrNull<HBoxContainer>(CardsContainerPath);

        BuildCardsFromCatalog();

        Visible = false;
        UpdateCardSelection();
        UpdateStatusLabel();
    }

    public override void _ExitTree()
    {
        foreach (var (button, handler) in _card_input_handlers)
        {
            if (button != null)
                button.GuiInput -= handler;
        }
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
        _debug_enemy_spawner?.CancelPlacement();
        ClearPressedCardState();
        UpdateCardSelection();
        UpdateStatusLabel();
    }

    private void BuildCardsFromCatalog()
    {
        if (_cards_container == null || _debug_enemy_spawner == null)
            return;

        foreach (var child in _cards_container.GetChildren())
            child.QueueFree();

        _cards_by_id.Clear();
        _card_input_handlers.Clear();
        var rows_by_category = new Dictionary<string, HBoxContainer>();

        foreach (var entry in _debug_enemy_spawner.GetCatalogEntries())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                continue;

            var category = NormalizeCategory(entry.Category);
            var category_row = GetOrCreateCategoryRow(category, rows_by_category);
            if (category_row == null)
                continue;

            var card = CreateCard(entry);
            if (card == null)
                continue;

            category_row.AddChild(card);
            _cards_by_id[entry.Id] = card;

            Control.GuiInputEventHandler input_handler = @event => BeginCardPress(entry.Id, @event);
            card.GuiInput += input_handler;
            _card_input_handlers[card] = input_handler;
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

        _cards_container.AddChild(section);
        rowsByCategory[category] = row;
        return row;
    }

    private static string NormalizeCategory(string category)
    {
        return string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category.Trim();
    }

    private Button CreateCard(EnemyCatalogEntry entry)
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

        var preview_container = new SubViewportContainer
        {
            Name = "PreviewContainer",
            CustomMinimumSize = new Vector2(96.0f, 96.0f),
            Stretch = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        vBox.AddChild(preview_container);

        var preview_viewport = new SubViewport
        {
            Name = "PreviewViewport",
            HandleInputLocally = false,
            Disable3D = true,
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = new Vector2I(96, 96),
        };
        preview_container.AddChild(preview_viewport);

        var preview_sprite = new AnimatedSprite2D
        {
            Name = "PreviewSprite",
        };
        preview_viewport.AddChild(preview_sprite);

        var label = new Label
        {
            Name = "Label",
            Text = entry.DisplayName,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vBox.AddChild(label);

        ConfigurePreview(entry.Id, preview_sprite);
        return card;
    }

    private void ConfigurePreview(string enemyId, AnimatedSprite2D previewSprite)
    {
        if (_debug_enemy_spawner == null || previewSprite == null)
            return;

        var spriteFrames = _debug_enemy_spawner.GetPreviewFrames(enemyId);
        var animationName = _debug_enemy_spawner.GetPreviewAnimationName(enemyId);
        previewSprite.SpriteFrames = spriteFrames;
        previewSprite.Scale = _debug_enemy_spawner.GetPreviewScale(enemyId);
        previewSprite.Position = PreviewCenter + _debug_enemy_spawner.GetPreviewOffset(enemyId);

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
        if (string.IsNullOrEmpty(_pressed_card_id) || _dragging_from_card)
            return;

        if ((mouseMotion.ButtonMask & MouseButtonMask.Left) == 0)
            return;

        if (mouseMotion.GlobalPosition.DistanceTo(_press_start_screen_position) < DragThreshold)
            return;

        _dragging_from_card = true;
        _debug_enemy_spawner?.BeginPlacement(_pressed_card_id);
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

        if (!string.IsNullOrEmpty(_pressed_card_id))
            return;

        if (HasPendingPlacement && !IsMouseOverTray(screenPosition) && _debug_enemy_spawner != null)
        {
            _debug_enemy_spawner.PlacePendingAtCursor(mouseButton.ShiftPressed);
            UpdateCardSelection();
            UpdateStatusLabel();
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleLeftMouseRelease(Vector2 screenPosition, bool shiftPressed)
    {
        if (string.IsNullOrEmpty(_pressed_card_id))
            return;

        if (_dragging_from_card && _debug_enemy_spawner != null)
        {
            if (IsMouseOverTray(screenPosition))
                _debug_enemy_spawner.CancelPlacement();
            else
                _debug_enemy_spawner.PlacePendingAtCursor(shiftPressed);
        }
        else if (IsMouseOverCard(_pressed_card_id, screenPosition))
        {
            _debug_enemy_spawner?.BeginPlacement(_pressed_card_id);
        }

        ClearPressedCardState();
        UpdateCardSelection();
        UpdateStatusLabel();
        GetViewport().SetInputAsHandled();
    }

    private void ClearPressedCardState()
    {
        _pressed_card_id = null;
        _dragging_from_card = false;
    }

    private void UpdateCardSelection()
    {
        foreach (var (enemyId, card) in _cards_by_id)
        {
            if (card == null)
                continue;

            card.ButtonPressed = HasPendingPlacement && _debug_enemy_spawner?.PendingEnemyId == enemyId;
        }
    }

    private void UpdateStatusLabel()
    {
        if (_status_label == null)
            return;

        if (_dragging_from_card)
        {
            _status_label.Text = "Release in the world to place. Release over tray, right click, or Esc to cancel.";
            return;
        }

        if (HasPendingPlacement)
        {
            _status_label.Text = "Click in the world to place. Right click or Esc cancels.";
            return;
        }

        _status_label.Text = "Click a card to arm placement, or drag it out into the world.";
    }

    private bool IsMouseOverTray(Vector2 screenPosition)
    {
        return _tray_panel != null && _tray_panel.GetGlobalRect().HasPoint(screenPosition);
    }

    private bool IsMouseOverCard(string enemyId, Vector2 screenPosition)
    {
        return _cards_by_id.TryGetValue(enemyId, out var card) && card != null && card.GetGlobalRect().HasPoint(screenPosition);
    }

    private void BeginCardPress(string enemyId, InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton || mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
            return;

        _pressed_card_id = enemyId;
        _press_start_screen_position = mouseButton.GlobalPosition;
        _dragging_from_card = false;
        GetViewport().SetInputAsHandled();
    }
}
