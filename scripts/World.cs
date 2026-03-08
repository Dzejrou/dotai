using Godot;
using System;

[GlobalClass]
public partial class World : Node2D
{
    [Export]
    public PackedScene SkeletonScene { get; set; }

    [Export]
    public PackedScene OgreScene { get; set; }

    [Export]
    public NodePath PlayerPath { get; set; } = new NodePath("Player");

    [Export]
    public NodePath GameOverPath { get; set; } = new NodePath("GameOver/Root");

    [Export]
    public Vector2 SkeletonSpawnOffset { get; set; } = new Vector2(36.0f, 0.0f);

    [Export]
    public float SkeletonSpawnCooldown { get; set; } = 0.25f;

    private Control _gameOverRoot;
    private bool _gameOverActive;
    private Player _player;
    private float _spawnCooldown;
    private bool _restartingFromGameOver;
    private Label _healthText;
    private ColorRect _healthBackground;
    private ColorRect _healthFill;
    private const int HealthBarWidth = 140;
    private const int HealthBarHeight = 16;

    public override void _Ready()
    {
        _player = GetNodeOrNull<Player>(PlayerPath);
        if (_player != null)
        {
            _player.Connect(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
            _player.Connect(Player.SignalName.HealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
        }

        _gameOverRoot = GetNodeOrNull<Control>(GameOverPath);
        if (_gameOverRoot == null)
            return;

        _gameOverRoot.Visible = false;
        _gameOverRoot.ProcessMode = ProcessModeEnum.Always;
        ProcessMode = ProcessModeEnum.Always;
        CreateHud();
        UpdatePlayerHealthHud(_player);
    }

    public override void _ExitTree()
    {
        if (_player != null && _player.IsConnected(Player.SignalName.HealthChanged, new Callable(this, nameof(OnPlayerHealthChanged))))
        {
            _player.Disconnect(Player.SignalName.HealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
        }

        if (_player != null && _player.IsConnected(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied))))
        {
            _player.Disconnect(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_gameOverActive)
            return;

        if (_spawnCooldown > 0.0f)
            _spawnCooldown -= (float)delta;

        var attemptedSpawn = false;
        if (_spawnCooldown > 0.0f)
            return;

        if (Input.IsKeyPressed(Key.P) && SkeletonScene != null)
        {
            SpawnSkeleton();
            attemptedSpawn = true;
        }

        if (Input.IsKeyPressed(Key.O) && OgreScene != null)
        {
            SpawnOgre();
            attemptedSpawn = true;
        }

        if (!attemptedSpawn)
            return;

        _spawnCooldown = Mathf.Max(0.0f, SkeletonSpawnCooldown);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_gameOverActive || _restartingFromGameOver)
            return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            RestartFromGameOver();
        }
    }

    private void SpawnSkeleton()
    {
        if (SkeletonScene == null)
            return;

        var skeleton = SkeletonScene.Instantiate<Skeleton>();
        if (skeleton == null)
            return;

        var spawnPosition = GlobalPosition;
        if (_player != null && _player.IsInsideTree())
            spawnPosition = _player.GlobalPosition + SkeletonSpawnOffset;

        skeleton.GlobalPosition = spawnPosition;
        skeleton.ZIndex = -1;
        AddChild(skeleton);
    }

    private void SpawnOgre()
    {
        if (OgreScene == null)
            return;

        var ogre = OgreScene.Instantiate<Ogre>();
        if (ogre == null)
            return;

        var spawnPosition = GlobalPosition;
        if (_player != null && _player.IsInsideTree())
            spawnPosition = _player.GlobalPosition + SkeletonSpawnOffset;

        ogre.GlobalPosition = spawnPosition;
        ogre.ZIndex = -1;
        AddChild(ogre);
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

    private void RestartFromGameOver()
    {
        _restartingFromGameOver = true;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    private void OnPlayerHealthChanged(int health, int maxHealth)
    {
        UpdatePlayerHealthHud(health, maxHealth);
    }

    private void UpdatePlayerHealthHud(Player player)
    {
        if (player == null)
            return;

        UpdatePlayerHealthHud(player.CurrentHealth, player.MaxHealth);
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
            Position = Vector2.Zero,
            Size = new Vector2(HealthBarWidth, HealthBarHeight)
        };
        healthPanel.AddChild(_healthBackground);

        _healthFill = new ColorRect
        {
            Name = "HealthBarFill",
            Color = new Color(1.0f, 0.0f, 0.0f, 1.0f),
            Position = Vector2.Zero,
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
        _healthText.OffsetTop = -5.0f;
        _healthText.AddThemeFontSizeOverride("font_size", 18);
        healthPanel.AddChild(_healthText);
    }
}
