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
    private Label _manaText;
    private ColorRect _manaBackground;
    private ColorRect _manaFill;
    private PlayerSpellBar _spellBar;
    private bool _playerIsPoisoned;
    private const int HealthBarWidth = 140;
    private const int HealthBarHeight = 16;
    private const int ManaBarWidth = 140;
    private const int ManaBarHeight = 16;
    private static readonly Color PlayerHealthFillColor = new Color(0.88f, 0.24f, 0.24f, 1.0f);
    private static readonly Color PlayerHealthBackgroundColor = new Color(0.32f, 0.12f, 0.12f, 0.85f);
    private static readonly Color PoisonedPlayerHealthFillColor = new Color(0.42f, 0.92f, 0.42f, 1.0f);
    private static readonly Color PoisonedPlayerHealthBackgroundColor = new Color(0.12f, 0.28f, 0.12f, 0.85f);
    private const string PlayerSpellBarScenePath = "res://scenes/ui/player_spell_bar.tscn";
    private int _windowPresetIndex;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _world = GetNodeOrNull<World>(WorldPath);
        if (_world != null)
        {
            _world.Connect(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
            _world.Connect(World.SignalName.PlayerHealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
            _world.Connect(World.SignalName.PlayerManaChanged, new Callable(this, nameof(OnPlayerManaChanged)));
            _world.Connect(World.SignalName.PlayerStatusVisualStateChanged, new Callable(this, nameof(OnPlayerStatusVisualStateChanged)));
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
        {
            var playerStatusController = player.GetNodeOrNull<StatusEffectController>("StatusEffectController");
            _playerIsPoisoned = playerStatusController?.HasStatus(PoisonedEffect.StatusKeyName) ?? false;
            RefreshPlayerHealthColors();
            UpdatePlayerHealthHud(player.CurrentHealth, player.MaxHealableHealth);
            UpdatePlayerManaHud(player.CurrentMana, player.MaxManaValue);
            _spellBar?.Bind(player);
        }
        else
        {
            UpdatePlayerHealthHud(0, 0);
            UpdatePlayerManaHud(0, 0);
            _spellBar?.Bind(null);
        }

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

        if (_world.IsConnected(World.SignalName.PlayerManaChanged, new Callable(this, nameof(OnPlayerManaChanged))))
            _world.Disconnect(World.SignalName.PlayerManaChanged, new Callable(this, nameof(OnPlayerManaChanged)));

        if (_world.IsConnected(World.SignalName.PlayerStatusVisualStateChanged, new Callable(this, nameof(OnPlayerStatusVisualStateChanged))))
            _world.Disconnect(World.SignalName.PlayerStatusVisualStateChanged, new Callable(this, nameof(OnPlayerStatusVisualStateChanged)));

        if (_pauseMenuRoot != null && _pauseMenuRoot.IsConnected(PauseMenu.SignalName.ResumeRequested, new Callable(this, nameof(OnPauseMenuResumeRequested))))
            _pauseMenuRoot.Disconnect(PauseMenu.SignalName.ResumeRequested, new Callable(this, nameof(OnPauseMenuResumeRequested)));

        if (_pauseMenuRoot != null && _pauseMenuRoot.IsConnected(PauseMenu.SignalName.DebugRequested, new Callable(this, nameof(OnPauseMenuDebugRequested))))
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

    private void OnPlayerManaChanged(int mana, int maxMana)
    {
        UpdatePlayerManaHud(mana, maxMana);
    }

    private void OnPlayerStatusVisualStateChanged(StringName statusKey, bool active)
    {
        if (statusKey != PoisonedEffect.StatusKeyName)
            return;

        _playerIsPoisoned = active;
        RefreshPlayerHealthColors();
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
        RefreshPlayerHealthColors();
    }

    private void UpdatePlayerManaHud(int mana, int maxMana)
    {
        if (_manaText == null || _manaFill == null || _manaBackground == null)
            return;

        _manaText.Text = $"{mana}/{maxMana}";

        var safeMax = Math.Max(1, maxMana);
        var manaRatio = Math.Clamp((float)mana / safeMax, 0.0f, 1.0f);
        _manaFill.Size = new Vector2(ManaBarWidth * manaRatio, ManaBarHeight);
    }

    private void RefreshPlayerHealthColors()
    {
        if (_healthFill == null || _healthBackground == null)
            return;

        if (_playerIsPoisoned)
        {
            _healthFill.Color = PoisonedPlayerHealthFillColor;
            _healthBackground.Color = PoisonedPlayerHealthBackgroundColor;
            return;
        }

        _healthFill.Color = PlayerHealthFillColor;
        _healthBackground.Color = PlayerHealthBackgroundColor;
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
            Color = PlayerHealthBackgroundColor,
            Size = new Vector2(HealthBarWidth, HealthBarHeight)
        };
        healthPanel.AddChild(_healthBackground);

        _healthFill = new ColorRect
        {
            Name = "HealthBarFill",
            Color = PlayerHealthFillColor,
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

        var manaPanel = new Control
        {
            Name = "ManaPanel",
            CustomMinimumSize = new Vector2(220.0f, 24.0f),
        };
        manaPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        manaPanel.OffsetLeft = 8.0f;
        manaPanel.OffsetTop = 32.0f;
        hudCanvas.AddChild(manaPanel);

        _manaBackground = new ColorRect
        {
            Name = "ManaBarBackground",
            Color = Colors.Black,
            Size = new Vector2(ManaBarWidth, ManaBarHeight)
        };
        manaPanel.AddChild(_manaBackground);

        _manaFill = new ColorRect
        {
            Name = "ManaBarFill",
            Color = new Color(0.2f, 0.45f, 1.0f, 1.0f),
            Size = new Vector2(ManaBarWidth, ManaBarHeight)
        };
        manaPanel.AddChild(_manaFill);

        _manaText = new Label
        {
            Name = "ManaText",
            Text = "0/0",
            Modulate = new Color(1.0f, 1.0f, 1.0f, 1.0f)
        };
        _manaText.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _manaText.OffsetLeft = ManaBarWidth + 10.0f;
        _manaText.OffsetTop = 1.0f;
        _manaText.AddThemeFontSizeOverride("font_size", 18);
        manaPanel.AddChild(_manaText);

        var spellBarScene = ResourceLoader.Load<PackedScene>(PlayerSpellBarScenePath);
        if (spellBarScene?.Instantiate<PlayerSpellBar>() is PlayerSpellBar spellBar)
        {
            _spellBar = spellBar;
            hudCanvas.AddChild(_spellBar);
        }
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
