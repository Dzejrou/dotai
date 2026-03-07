using Godot;
using System;

[GlobalClass]
public partial class World : Node2D
{
    [Export]
    public PackedScene SkeletonScene { get; set; }

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

    public override void _Ready()
    {
        _player = GetNodeOrNull<Player>(PlayerPath);
        if (_player != null)
            _player.Connect(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));

        _gameOverRoot = GetNodeOrNull<Control>(GameOverPath);
        if (_gameOverRoot == null)
            return;

        _gameOverRoot.Visible = false;
        _gameOverRoot.ProcessMode = ProcessModeEnum.Always;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_spawnCooldown > 0.0f)
            _spawnCooldown -= (float)delta;

        if (!Input.IsKeyPressed(Key.P) || SkeletonScene == null)
            return;

        if (_spawnCooldown > 0.0f)
            return;

        SpawnSkeleton();
        _spawnCooldown = Mathf.Max(0.0f, SkeletonSpawnCooldown);
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
        AddChild(skeleton);
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
}
