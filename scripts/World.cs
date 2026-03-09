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
    public Vector2 SkeletonSpawnOffset { get; set; } = new Vector2(36.0f, 0.0f);

    [Export]
    public float SkeletonSpawnCooldown { get; set; } = 0.25f;

    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void PlayerHealthChangedEventHandler(int health, int maxHealth);

    private Player _player;
    private float _spawnCooldown;
    private bool _isGameOver;

    public override void _Ready()
    {
        _player = GetNodeOrNull<Player>(PlayerPath);
        if (_player != null)
        {
            _player.Connect(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
            _player.Connect(Player.SignalName.HealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
            EmitSignal(SignalName.PlayerHealthChanged, _player.CurrentHealth, _player.MaxHealth);
        }
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
        if (_isGameOver)
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
        // Input handling for restart is now handled by Main.
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
        if (_isGameOver)
            return;

        _isGameOver = true;
        EmitSignal(SignalName.PlayerDied);
    }

    private void OnPlayerHealthChanged(int health, int maxHealth)
    {
        EmitSignal(SignalName.PlayerHealthChanged, health, maxHealth);
    }
}
