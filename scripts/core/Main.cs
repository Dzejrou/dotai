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
    private Control _gameOverRoot;
    private PauseMenu _pauseMenuRoot;
    private DebugTray _debugTrayRoot;
    private bool _gameOverActive;
    private bool _restartingFromGameOver;
    private bool _pauseMenuOpen;
    private Label _healthText;
    private ColorRect _healthBackground;
    private ColorRect _healthFill;
    private const int HealthBarWidth = 140;
    private const int HealthBarHeight = 16;
    private int _windowPresetIndex;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _world = GetNodeOrNull<World>(WorldPath);
        if (_world != null)
        {
            _world.Connect(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
            _world.Connect(World.SignalName.PlayerHealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
        }

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
        if (player != null)
            UpdatePlayerHealthHud(player.CurrentHealth, player.MaxHealth);
        else
            UpdatePlayerHealthHud(0, 0);

        InitializeWindowPreset();
    }

    public override void _ExitTree()
    {
        if (_world == null)
            return;

        if (_world.IsConnected(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied))))
            _world.Disconnect(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));

        if (_world.IsConnected(World.SignalName.PlayerHealthChanged, new Callable(this, nameof(OnPlayerHealthChanged))))
            _world.Disconnect(World.SignalName.PlayerHealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));

        if (_pauseMenuRoot != null && _pauseMenuRoot.IsConnected(PauseMenu.SignalName.ResumeRequested, new Callable(this, nameof(OnPauseMenuResumeRequested))))
            _pauseMenuRoot.Disconnect(PauseMenu.SignalName.ResumeRequested, new Callable(this, nameof(OnPauseMenuResumeRequested)));

        if (_pauseMenuRoot != null && _pauseMenuRoot.IsConnected(PauseMenu.SignalName.DebugRequested, new Callable(this, nameof(OnPauseMenuDebugRequested))))
            _pauseMenuRoot.Disconnect(PauseMenu.SignalName.DebugRequested, new Callable(this, nameof(OnPauseMenuDebugRequested)));
    }

    public override void _Input(InputEvent @event)
    {
        if (TryHandleWindowResizeInput(@event))
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
        _gameOverActive = true;
        GetTree().Paused = true;

        if (_gameOverRoot == null)
            return;

        _gameOverRoot.Visible = true;
    }

    private void OnPlayerHealthChanged(int health, int maxHealth)
    {
        UpdatePlayerHealthHud(health, maxHealth);
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

    private void UpdatePlayerHealthHud(int health, int maxHealth)
    {
        if (_healthText == null || _healthFill == null || _healthBackground == null)
            return;

        _healthText.Text = $"{health}/{maxHealth}";

        var safeMax = Math.Max(1, maxHealth);
        var healthRatio = Math.Clamp((float)health / safeMax, 0.0f, 1.0f);
        _healthFill.Size = new Vector2(HealthBarWidth * healthRatio, HealthBarHeight);
    }

    private void CreateHud()
    {
        var hudCanvas = new CanvasLayer
        {
            Name = "WorldHUD",
            Layer = 100
        };
        AddChild(hudCanvas);

        var healthPanel = new Control
        {
            Name = "HealthPanel",
            CustomMinimumSize = new Vector2(220.0f, 24.0f),
        };
        healthPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        healthPanel.OffsetLeft = 8.0f;
        healthPanel.OffsetTop = 8.0f;
        hudCanvas.AddChild(healthPanel);

        _healthBackground = new ColorRect
        {
            Name = "HealthBarBackground",
            Color = Colors.Black,
            Size = new Vector2(HealthBarWidth, HealthBarHeight)
        };
        healthPanel.AddChild(_healthBackground);

        _healthFill = new ColorRect
        {
            Name = "HealthBarFill",
            Color = new Color(1.0f, 0.0f, 0.0f, 1.0f),
            Size = new Vector2(HealthBarWidth, HealthBarHeight)
        };
        healthPanel.AddChild(_healthFill);

        _healthText = new Label
        {
            Name = "HealthText",
            Text = "0/0",
            Modulate = new Color(1.0f, 1.0f, 1.0f, 1.0f)
        };
        _healthText.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _healthText.OffsetLeft = HealthBarWidth + 10.0f;
        _healthText.OffsetTop = 1.0f;
        _healthText.AddThemeFontSizeOverride("font_size", 18);
        healthPanel.AddChild(_healthText);
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
