using Godot;

using System;

[GlobalClass]
public partial class Main : Node2D
{
    [Export]
    public NodePath WorldPath { get; set; } = new NodePath("World");

    [Export]
    public NodePath GameOverPath { get; set; } = new NodePath("GameOver/Root");

    private World _world;
    private Control _gameOverRoot;
    private bool _gameOverActive;
    private bool _restartingFromGameOver;
    private Label _healthText;
    private ColorRect _healthBackground;
    private ColorRect _healthFill;
    private const int HealthBarWidth = 140;
    private const int HealthBarHeight = 16;

    public override void _Ready()
    {
        _world = GetNodeOrNull<World>(WorldPath);
        if (_world != null)
        {
            _world.Connect(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
            _world.Connect(World.SignalName.PlayerHealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
        }

        _gameOverRoot = GetNodeOrNull<Control>(GameOverPath);
        CreateHud();

        if (_gameOverRoot != null)
        {
            _gameOverRoot.Visible = false;
            _gameOverRoot.ProcessMode = ProcessModeEnum.Always;
        }

        var playerPath = _world != null && !_world.PlayerPath.IsEmpty ? _world.PlayerPath : new NodePath("Player");
        var player = _world?.GetNodeOrNull<Player>(playerPath);
        if (player != null)
            UpdatePlayerHealthHud(player.CurrentHealth, player.MaxHealth);
    }

    public override void _ExitTree()
    {
        if (_world == null)
            return;

        if (_world.IsConnected(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied))))
            _world.Disconnect(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));

        if (_world.IsConnected(World.SignalName.PlayerHealthChanged, new Callable(this, nameof(OnPlayerHealthChanged))))
            _world.Disconnect(World.SignalName.PlayerHealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
    }

    public override void _Input(InputEvent @event)
    {
        if (!_gameOverActive || _restartingFromGameOver)
            return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            RestartFromGameOver();
    }

    private void OnPlayerDied()
    {
        if (_gameOverActive)
            return;

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

        const float leftOffset = 8.0f;
        const float topOffset = 8.0f;

        _healthBackground = new ColorRect
        {
            Name = "HealthBarBackground",
            Color = Colors.Black,
            Position = new Vector2(leftOffset, topOffset),
            Size = new Vector2(HealthBarWidth, HealthBarHeight)
        };
        hudCanvas.AddChild(_healthBackground);

        _healthFill = new ColorRect
        {
            Name = "HealthBarFill",
            Color = new Color(1.0f, 0.0f, 0.0f, 1.0f),
            Position = new Vector2(leftOffset, topOffset),
            Size = new Vector2(HealthBarWidth, HealthBarHeight)
        };
        hudCanvas.AddChild(_healthFill);

        _healthText = new Label
        {
            Name = "HealthText",
            Text = "0/0",
            Modulate = new Color(1.0f, 1.0f, 1.0f, 1.0f)
        };
        _healthText.Position = new Vector2(leftOffset + HealthBarWidth + 10.0f, topOffset);
        _healthText.AddThemeFontSizeOverride("font_size", 18);
        hudCanvas.AddChild(_healthText);
    }
}
