using Godot;

using System;

[GlobalClass]
public partial class Main : Node2D
{
    private static readonly Vector2I[] WindowPresets =
    {
        new Vector2I(960, 540),
        new Vector2I(1280, 720),
        new Vector2I(1600, 900),
        new Vector2I(1920, 1080),
        new Vector2I(2560, 1440),
    };

    [Export]
    public NodePath WorldPath { get; set; } = new NodePath("World");

    [Export]
    public NodePath GameOverPath { get; set; } = new NodePath("GameOver/Root");

    [Export]
    public NodePath PauseMenuPath { get; set; } = new NodePath("PauseMenu/Root");

    [Export]
    public NodePath DebugTrayPath { get; set; } = new NodePath("DebugTray/Root");

    private World _world;
    private Player _player;
    private InventoryController _inventoryController;
    private Control _gameOverRoot;
    private PauseMenu _pauseMenuRoot;
    private DebugTray _debugTrayRoot;
    private bool _gameOverActive;
    private bool _restartingFromGameOver;
    private bool _pauseMenuOpen;
    private PlayerSpellBar _spellBar;
    private PlayerSpellBindingWindow _spellBindingWindow;
    private InventoryWindow _inventoryWindow;
    private Sprite2D _interactionPrompt;
    private const string PlayerSpellBarScenePath = "res://scenes/ui/player_spell_bar.tscn";
    private const string PlayerSpellBindingWindowScenePath = "res://scenes/ui/player_spell_binding_window.tscn";
    private const string InventoryWindowScenePath = "res://scenes/ui/inventory_window.tscn";
    private const string InteractionPromptGlyphPath = "res://assets/glyphs/letter_g.png";
    private const string SpellBookActionName = "spell_book";
    private const string ToggleInventoryActionName = "toggle_inventory";
    private int _windowPresetIndex;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _world = GetNodeOrNull<World>(WorldPath);
        if (_world != null)
            _world.Connect(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));

        _gameOverRoot = GetNodeOrNull<Control>(GameOverPath);
        _pauseMenuRoot = GetNodeOrNull<PauseMenu>(PauseMenuPath);
        _debugTrayRoot = GetNodeOrNull<DebugTray>(DebugTrayPath);
        CreateHud();

        if (_gameOverRoot != null)
        {
            _gameOverRoot.Visible = false;
            _gameOverRoot.ProcessMode = ProcessModeEnum.Always;
        }

        if (_pauseMenuRoot != null)
        {
            _pauseMenuRoot.Visible = false;
            _pauseMenuRoot.ProcessMode = ProcessModeEnum.Always;
            _pauseMenuRoot.Connect(PauseMenu.SignalName.ResumeRequested, new Callable(this, nameof(OnPauseMenuResumeRequested)));
            _pauseMenuRoot.Connect(PauseMenu.SignalName.DebugRequested, new Callable(this, nameof(OnPauseMenuDebugRequested)));
        }

        if (_debugTrayRoot != null)
        {
            _debugTrayRoot.Visible = false;
            _debugTrayRoot.ProcessMode = ProcessModeEnum.Always;
        }

        var playerPath = _world != null && !_world.PlayerPath.IsEmpty ? _world.PlayerPath : new NodePath("Player");
        var player = _world?.GetNodeOrNull<Player>(playerPath);
        _player = player;
        if (player != null)
        {
            player.Connect(Player.SignalName.InteractionAvailabilityChanged, new Callable(this, nameof(OnPlayerInteractionAvailabilityChanged)));
            _spellBar?.Bind(player);
            _spellBindingWindow?.Bind(player);
            UpdateInteractionPrompt(player.HasInteractionTarget);
        }
        else
        {
            _spellBar?.Bind(null);
            _spellBindingWindow?.Bind(null);
            UpdateInteractionPrompt(false);
        }

        _inventoryController = _world != null && !_world.InventoryPath.IsEmpty
            ? _world.ResolveInventoryController()
            : null;
        _inventoryWindow?.Bind(_inventoryController);

        InitializeWindowPreset();
    }

    public override void _ExitTree()
    {
        if (_world == null)
            return;

        if (GodotObject.IsInstanceValid(_world) &&
            _world.IsConnected(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied))))
            _world.Disconnect(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));

        if (GodotObject.IsInstanceValid(_player) &&
            _player.IsConnected(Player.SignalName.InteractionAvailabilityChanged, new Callable(this, nameof(OnPlayerInteractionAvailabilityChanged))))
            _player.Disconnect(Player.SignalName.InteractionAvailabilityChanged, new Callable(this, nameof(OnPlayerInteractionAvailabilityChanged)));

        if (GodotObject.IsInstanceValid(_pauseMenuRoot) &&
            _pauseMenuRoot.IsConnected(PauseMenu.SignalName.ResumeRequested, new Callable(this, nameof(OnPauseMenuResumeRequested))))
            _pauseMenuRoot.Disconnect(PauseMenu.SignalName.ResumeRequested, new Callable(this, nameof(OnPauseMenuResumeRequested)));

        if (GodotObject.IsInstanceValid(_pauseMenuRoot) &&
            _pauseMenuRoot.IsConnected(PauseMenu.SignalName.DebugRequested, new Callable(this, nameof(OnPauseMenuDebugRequested))))
            _pauseMenuRoot.Disconnect(PauseMenu.SignalName.DebugRequested, new Callable(this, nameof(OnPauseMenuDebugRequested)));
    }

    public override void _Input(InputEvent @event)
    {
        if (TryHandleWindowResizeInput(@event))
            return;

        if (TryHandleNavigationDebugInput(@event))
            return;

        if (_gameOverActive && !_restartingFromGameOver)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
                RestartFromGameOver();

            return;
        }

        if (TryHandleSpellBookInput(@event))
            return;

        if (TryHandleInventoryInput(@event))
            return;

        TryHandlePauseMenuInput(@event);
    }

    public override void _Process(double delta)
    {
        UpdateInteractionPromptPosition();
    }

    private void OnPlayerDied()
    {
        if (_gameOverActive)
            return;

        ClosePauseMenu();
        CloseDebugTray(false);
        UpdateInteractionPrompt(false);
        _gameOverActive = true;
        _spellBindingWindow?.CloseWindow();
        _inventoryWindow?.CloseWindow();
        GetTree().Paused = true;

        if (_gameOverRoot == null)
            return;

        _gameOverRoot.Visible = true;
    }

    private void OnPlayerInteractionAvailabilityChanged(bool available)
    {
        UpdateInteractionPrompt(available);
    }

    private void RestartFromGameOver()
    {
        _restartingFromGameOver = true;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    private void OnPauseMenuResumeRequested()
    {
        ClosePauseMenu();
    }

    private void OnPauseMenuDebugRequested()
    {
        ClosePauseMenu();
        OpenDebugTray();
    }

    private void UpdateInteractionPrompt(bool available)
    {
        if (_interactionPrompt == null)
            return;

        _interactionPrompt.Visible = available && _player != null && _player.CurrentInteractionTarget != null;
        UpdateInteractionPromptPosition();
    }

    private void CreateHud()
    {
        var hudCanvas = new CanvasLayer
        {
            Name = "WorldHUD",
            Layer = 100
        };
        AddChild(hudCanvas);

        var spellBarScene = ResourceLoader.Load<PackedScene>(PlayerSpellBarScenePath);
        if (spellBarScene?.Instantiate<PlayerSpellBar>() is PlayerSpellBar spellBar)
        {
            _spellBar = spellBar;
            hudCanvas.AddChild(_spellBar);
        }

        var spellBindingWindowScene = ResourceLoader.Load<PackedScene>(PlayerSpellBindingWindowScenePath);
        if (spellBindingWindowScene?.Instantiate<PlayerSpellBindingWindow>() is PlayerSpellBindingWindow spellBindingWindow)
        {
            _spellBindingWindow = spellBindingWindow;
            hudCanvas.AddChild(_spellBindingWindow);
        }

        var inventoryWindowScene = ResourceLoader.Load<PackedScene>(InventoryWindowScenePath);
        if (inventoryWindowScene?.Instantiate<InventoryWindow>() is InventoryWindow inventoryWindow)
        {
            _inventoryWindow = inventoryWindow;
            hudCanvas.AddChild(_inventoryWindow);
        }

        var interactionPromptTexture = ResourceLoader.Load<Texture2D>(InteractionPromptGlyphPath);
        if (interactionPromptTexture == null)
        {
            GD.PushError($"Failed to load interaction prompt glyph at {InteractionPromptGlyphPath}.");
            return;
        }

        _interactionPrompt = new Sprite2D
        {
            Name = "InteractionPrompt",
            Visible = false,
            Centered = true,
            Texture = interactionPromptTexture,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = 1000,
        };
        AddChild(_interactionPrompt);
    }

    private void UpdateInteractionPromptPosition()
    {
        if (_interactionPrompt == null || !_interactionPrompt.Visible)
            return;

        if (_player == null ||
            !_player.HasInteractionTarget ||
            _player.CurrentInteractionTarget == null ||
            !_player.TryGetInteractionPromptPosition(out var promptPosition))
        {
            _interactionPrompt.Visible = false;
            return;
        }

        _interactionPrompt.GlobalPosition = promptPosition;
    }

    private void InitializeWindowPreset()
    {
        var currentSize = DisplayServer.WindowGetSize();
        var closestPresetIndex = 0;
        var closestDistanceSq = int.MaxValue;

        for (var i = 0; i < WindowPresets.Length; i++)
        {
            var preset = WindowPresets[i];
            var dx = currentSize.X - preset.X;
            var dy = currentSize.Y - preset.Y;
            var distanceSq = dx * dx + dy * dy;
            if (distanceSq < closestDistanceSq)
            {
                closestDistanceSq = distanceSq;
                closestPresetIndex = i;
            }
        }

        _windowPresetIndex = Mathf.Clamp(closestPresetIndex, 0, WindowPresets.Length - 1);
    }

    private bool TryHandleWindowResizeInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        var shouldIncrease = keyEvent.PhysicalKeycode == Key.Key9;
        var shouldDecrease = keyEvent.PhysicalKeycode == Key.Key0;

        if (!shouldIncrease && !shouldDecrease)
            return false;

        var newIndex = _windowPresetIndex;
        if (shouldIncrease)
            newIndex++;
        else
            newIndex--;

        newIndex = Mathf.Clamp(newIndex, 0, WindowPresets.Length - 1);
        if (newIndex == _windowPresetIndex)
            return true;

        _windowPresetIndex = newIndex;
        var newSize = WindowPresets[_windowPresetIndex];
        DisplayServer.WindowSetSize(newSize);
        GD.Print($"Window size set to {newSize.X}x{newSize.Y}");

        return true;
    }

    private bool TryHandlePauseMenuInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (keyEvent.PhysicalKeycode == Key.P)
        {
            _spellBindingWindow?.CloseWindow();
            if (_debugTrayRoot != null && _debugTrayRoot.TrayVisible)
                CloseDebugTray();
            else
            {
                ClosePauseMenu();
                OpenDebugTray();
            }

            return true;
        }

        if (keyEvent.PhysicalKeycode != Key.Escape)
            return false;

        if (_debugTrayRoot != null && _debugTrayRoot.TrayVisible)
        {
            if (_debugTrayRoot.HandleEscape())
                return true;

            CloseDebugTray();
            OpenPauseMenu();
            return true;
        }

        if (_pauseMenuOpen)
            ClosePauseMenu();
        else
            OpenPauseMenu();

        return true;
    }

    private bool TryHandleSpellBookInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (!InputMap.HasAction(SpellBookActionName) || !@event.IsActionPressed(SpellBookActionName))
            return false;

        if (_pauseMenuOpen || (_debugTrayRoot != null && _debugTrayRoot.TrayVisible))
            return true;

        _inventoryWindow?.CloseWindow();
        _spellBindingWindow?.ToggleWindow();
        return true;
    }

    private bool TryHandleInventoryInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (!InputMap.HasAction(ToggleInventoryActionName) || !@event.IsActionPressed(ToggleInventoryActionName))
            return false;

        if (_pauseMenuOpen || (_debugTrayRoot != null && _debugTrayRoot.TrayVisible))
            return true;

        _spellBindingWindow?.CloseWindow();
        _inventoryWindow?.ToggleWindow();
        return true;
    }

    private bool TryHandleNavigationDebugInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (keyEvent.PhysicalKeycode != Key.Key8)
            return false;

        var enabled = NavigationDebugSettings.Toggle();
        GD.Print($"Navigation debug {(enabled ? "enabled" : "disabled")}");
        return true;
    }

    private void OpenPauseMenu()
    {
        CloseDebugTray();
        _spellBindingWindow?.CloseWindow();
        _inventoryWindow?.CloseWindow();
        _pauseMenuOpen = true;
        if (_pauseMenuRoot != null)
            _pauseMenuRoot.Visible = true;

        GetTree().Paused = true;
    }

    private void ClosePauseMenu()
    {
        _pauseMenuOpen = false;
        if (_pauseMenuRoot != null)
            _pauseMenuRoot.Visible = false;

        if (!_gameOverActive)
            GetTree().Paused = false;
    }

    private void OpenDebugTray()
    {
        _inventoryWindow?.CloseWindow();

        if (_debugTrayRoot != null)
            _debugTrayRoot.Open();

        if (!_gameOverActive)
            GetTree().Paused = false;
    }

    private void CloseDebugTray(bool cancelPlacement = true)
    {
        if (_debugTrayRoot != null)
            _debugTrayRoot.Close(cancelPlacement);
    }
}
