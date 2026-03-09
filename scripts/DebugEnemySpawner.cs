using Godot;

public partial class DebugEnemySpawner : Node2D
{
    [Export]
    public PackedScene SkeletonScene { get; set; }

    [Export]
    public PackedScene OgreScene { get; set; }

    [Export]
    public NodePath PlayerPath { get; set; } = new NodePath("../Player");

    [Export]
    public Vector2 SpawnOffset { get; set; } = new Vector2(36.0f, 0.0f);

    [Export]
    public float SpawnCooldown { get; set; } = 0.25f;

    private Player _player;
    private float _spawnCooldown;

    public override void _Ready()
    {
        _player = GetNodeOrNull<Player>(PlayerPath);
        if (_player == null)
            _player = GetParentOrNull<Node>()?.GetNodeOrNull<Player>("Player");

        if (_player == null)
            GD.PrintErr("DebugEnemySpawner could not find Player node.");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_spawnCooldown > 0.0f)
            _spawnCooldown -= (float)delta;

        if (_spawnCooldown > 0.0f)
            return;

        var attemptedSpawn = false;

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

        _spawnCooldown = Mathf.Max(0.0f, SpawnCooldown);
    }

    private void SpawnSkeleton()
    {
        var skeleton = SkeletonScene?.Instantiate<Skeleton>();
        if (skeleton == null)
            return;

        skeleton.GlobalPosition = GetSpawnPosition();
        skeleton.ZIndex = -1;
        skeleton.SetPlayer(_player);
        var parent = GetParent();
        if (parent != null)
            parent.AddChild(skeleton);
    }

    private void SpawnOgre()
    {
        var ogre = OgreScene?.Instantiate<Ogre>();
        if (ogre == null)
            return;

        ogre.GlobalPosition = GetSpawnPosition();
        ogre.ZIndex = -1;
        ogre.SetPlayer(_player);
        var parent = GetParent();
        if (parent != null)
            parent.AddChild(ogre);
    }

    private Vector2 GetSpawnPosition()
    {
        var spawnPosition = GlobalPosition;
        if (_player != null && _player.IsInsideTree())
            spawnPosition = _player.GlobalPosition + SpawnOffset;

        return spawnPosition;
    }
}
