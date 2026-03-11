using Godot;

using System.Collections.Generic;

public partial class DebugTray : Control
{
    [Export]
    public NodePath TrayPanelPath { get; set; } = new NodePath("Bottom/Panel");

    [Export]
    public NodePath StatusLabelPath { get; set; } = new NodePath("Bottom/Panel/VBox/Header/Status");

    [Export]
    public NodePath DebugEnemySpawnerPath { get; set; } = new NodePath("../../World/DebugEnemySpawner");

    [Export]
    public NodePath SkeletonCardPath { get; set; } = new NodePath("Bottom/Panel/VBox/Scroll/Cards/SkeletonCard");

    [Export]
    public NodePath OgreCardPath { get; set; } = new NodePath("Bottom/Panel/VBox/Scroll/Cards/OgreCard");

    [Export]
    public NodePath SkeletonMageCardPath { get; set; } = new NodePath("Bottom/Panel/VBox/Scroll/Cards/SkeletonMageCard");

    [Export]
    public NodePath SkeletonPreviewPath { get; set; } = new NodePath("Bottom/Panel/VBox/Scroll/Cards/SkeletonCard/Margin/VBox/PreviewContainer/SkeletonPreviewViewport/SkeletonPreview");

    [Export]
    public NodePath OgrePreviewPath { get; set; } = new NodePath("Bottom/Panel/VBox/Scroll/Cards/OgreCard/Margin/VBox/PreviewContainer/OgrePreviewViewport/OgrePreview");

    [Export]
    public NodePath SkeletonMagePreviewPath { get; set; } = new NodePath("Bottom/Panel/VBox/Scroll/Cards/SkeletonMageCard/Margin/VBox/PreviewContainer/SkeletonMagePreviewViewport/SkeletonMagePreview");

    private const float DragThreshold = 12.0f;
    private static readonly Vector2 PreviewCenter = new(48.0f, 52.0f);

    private DebugEnemySpawner _debug_enemy_spawner;
    private Control _tray_panel;
    private Label _status_label;
    private Button _skeleton_card;
    private Button _ogre_card;
    private Button _skeleton_mage_card;
    private AnimatedSprite2D _skeleton_preview;
    private AnimatedSprite2D _ogre_preview;
    private AnimatedSprite2D _skeleton_mage_preview;
    private readonly Dictionary<DebugEnemySpawner.EnemyType, Button> _cards_by_type = new();
    private DebugEnemySpawner.EnemyType? _pressed_card_type;
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
        _skeleton_card = GetNodeOrNull<Button>(SkeletonCardPath);
        _ogre_card = GetNodeOrNull<Button>(OgreCardPath);
        _skeleton_mage_card = GetNodeOrNull<Button>(SkeletonMageCardPath);
        _skeleton_preview = GetNodeOrNull<AnimatedSprite2D>(SkeletonPreviewPath);
        _ogre_preview = GetNodeOrNull<AnimatedSprite2D>(OgrePreviewPath);
        _skeleton_mage_preview = GetNodeOrNull<AnimatedSprite2D>(SkeletonMagePreviewPath);

        RegisterCard(DebugEnemySpawner.EnemyType.Skeleton, _skeleton_card, _skeleton_preview);
        RegisterCard(DebugEnemySpawner.EnemyType.Ogre, _ogre_card, _ogre_preview);
        RegisterCard(DebugEnemySpawner.EnemyType.SkeletonMage, _skeleton_mage_card, _skeleton_mage_preview);

        if (_skeleton_card != null)
            _skeleton_card.GuiInput += OnSkeletonCardGuiInput;
        if (_ogre_card != null)
            _ogre_card.GuiInput += OnOgreCardGuiInput;
        if (_skeleton_mage_card != null)
            _skeleton_mage_card.GuiInput += OnSkeletonMageCardGuiInput;

        Visible = false;
        UpdateCardSelection();
        UpdateStatusLabel();
    }

    public override void _ExitTree()
    {
        if (_skeleton_card != null)
            _skeleton_card.GuiInput -= OnSkeletonCardGuiInput;
        if (_ogre_card != null)
            _ogre_card.GuiInput -= OnOgreCardGuiInput;
        if (_skeleton_mage_card != null)
            _skeleton_mage_card.GuiInput -= OnSkeletonMageCardGuiInput;
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

    private void RegisterCard(DebugEnemySpawner.EnemyType enemyType, Button card, AnimatedSprite2D previewSprite)
    {
        if (card == null || previewSprite == null || _debug_enemy_spawner == null)
            return;

        _cards_by_type[enemyType] = card;

        var spriteFrames = _debug_enemy_spawner.GetPreviewFrames(enemyType);
        var animationName = _debug_enemy_spawner.GetPreviewAnimationName(enemyType);
        previewSprite.SpriteFrames = spriteFrames;
        previewSprite.Scale = _debug_enemy_spawner.GetPreviewScale(enemyType);
        previewSprite.Position = PreviewCenter + _debug_enemy_spawner.GetPreviewOffset(enemyType);

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
        if (!_pressed_card_type.HasValue || _dragging_from_card)
            return;

        if ((mouseMotion.ButtonMask & MouseButtonMask.Left) == 0)
            return;

        if (mouseMotion.GlobalPosition.DistanceTo(_press_start_screen_position) < DragThreshold)
            return;

        _dragging_from_card = true;
        _debug_enemy_spawner?.BeginPlacement(_pressed_card_type.Value);
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

        if (_pressed_card_type.HasValue)
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
        if (!_pressed_card_type.HasValue)
            return;

        if (_dragging_from_card && _debug_enemy_spawner != null)
        {
            if (IsMouseOverTray(screenPosition))
                _debug_enemy_spawner.CancelPlacement();
            else
                _debug_enemy_spawner.PlacePendingAtCursor(shiftPressed);
        }
        else if (IsMouseOverCard(_pressed_card_type.Value, screenPosition))
        {
            _debug_enemy_spawner?.BeginPlacement(_pressed_card_type.Value);
        }

        ClearPressedCardState();
        UpdateCardSelection();
        UpdateStatusLabel();
        GetViewport().SetInputAsHandled();
    }

    private void ClearPressedCardState()
    {
        _pressed_card_type = null;
        _dragging_from_card = false;
    }

    private void UpdateCardSelection()
    {
        foreach (var (enemyType, card) in _cards_by_type)
        {
            if (card == null)
                continue;

            card.ButtonPressed = HasPendingPlacement && _debug_enemy_spawner?.PendingEnemyType == enemyType;
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

    private bool IsMouseOverCard(DebugEnemySpawner.EnemyType enemyType, Vector2 screenPosition)
    {
        return _cards_by_type.TryGetValue(enemyType, out var card) && card != null && card.GetGlobalRect().HasPoint(screenPosition);
    }

    private void BeginCardPress(DebugEnemySpawner.EnemyType enemyType, InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton || mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
            return;

        _pressed_card_type = enemyType;
        _press_start_screen_position = mouseButton.GlobalPosition;
        _dragging_from_card = false;
        GetViewport().SetInputAsHandled();
    }

    private void OnSkeletonCardGuiInput(InputEvent @event)
    {
        BeginCardPress(DebugEnemySpawner.EnemyType.Skeleton, @event);
    }

    private void OnOgreCardGuiInput(InputEvent @event)
    {
        BeginCardPress(DebugEnemySpawner.EnemyType.Ogre, @event);
    }

    private void OnSkeletonMageCardGuiInput(InputEvent @event)
    {
        BeginCardPress(DebugEnemySpawner.EnemyType.SkeletonMage, @event);
    }
}
