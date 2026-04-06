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
    private Control _gameOverRoot;
    private PauseMenu _pauseMenuRoot;
    private DebugTray _debugTrayRoot;
    private bool _gameOverActive;
    private bool _restartingFromGameOver;
    private bool _pauseMenuOpen;
    private PlayerSpellBar _spellBar;
    private Label _interactionPrompt;
    private static readonly Color InteractionPromptColor = new Color(0.98f, 0.86f, 0.42f, 1.0f);
    private const string PlayerSpellBarScenePath = "res://scenes/ui/player_spell_bar.tscn";
    private const string InteractionActionName = "interact_action";
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
            UpdateInteractionPrompt(player.HasInteractionTarget, player.CurrentInteractionLabel);
        }
        else
        {
            _spellBar?.Bind(null);
            UpdateInteractionPrompt(false, string.Empty);
        }

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

        TryHandlePauseMenuInput(@event);
    }

    private void OnPlayerDied()
    {
        if (_gameOverActive)
            return;

        ClosePauseMenu();
        CloseDebugTray(false);
        UpdateInteractionPrompt(false, string.Empty);
        _gameOverActive = true;
        GetTree().Paused = true;

        if (_gameOverRoot == null)
            return;

        _gameOverRoot.Visible = true;
    }

    private void OnPlayerInteractionAvailabilityChanged(bool available, string label)
    {
        UpdateInteractionPrompt(available, label);
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

    private void UpdateInteractionPrompt(bool available, string label)
    {
        if (_interactionPrompt == null)
            return;

        _interactionPrompt.Visible = available && !string.IsNullOrWhiteSpace(label);
        if (!_interactionPrompt.Visible)
            return;

        var actionLabel = ResolveActionLabel(InteractionActionName);
        _interactionPrompt.Text = $"{actionLabel}: {label}";
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

        _interactionPrompt = new Label
        {
            Name = "InteractionPrompt",
            Visible = false,
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = InteractionPromptColor,
            CustomMinimumSize = new Vector2(240.0f, 24.0f),
        };
        _interactionPrompt.AnchorLeft = 0.5f;
        _interactionPrompt.AnchorRight = 0.5f;
        _interactionPrompt.AnchorTop = 1.0f;
        _interactionPrompt.AnchorBottom = 1.0f;
        _interactionPrompt.OffsetLeft = -120.0f;
        _interactionPrompt.OffsetRight = 120.0f;
        _interactionPrompt.OffsetTop = -92.0f;
        _interactionPrompt.OffsetBottom = -68.0f;
        _interactionPrompt.AddThemeFontSizeOverride("font_size", 18);
        hudCanvas.AddChild(_interactionPrompt);
    }

    private string ResolveActionLabel(StringName action)
    {
        foreach (var inputEvent in InputMap.ActionGetEvents(action))
        {
            if (inputEvent is not InputEventKey keyEvent)
                continue;

            var keycode = keyEvent.PhysicalKeycode != Key.None
                ? keyEvent.PhysicalKeycode
                : keyEvent.Keycode;
            if (keycode != Key.None)
                return OS.GetKeycodeString(keycode).ToUpperInvariant();
        }

        return action.ToString();
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
