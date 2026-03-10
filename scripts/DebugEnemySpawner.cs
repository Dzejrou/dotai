using Godot;

public partial class DebugEnemySpawner : Node2D
{
    [Export]
    public PackedScene SkeletonScene { get; set; }

    [Export]
    public PackedScene OgreScene { get; set; }

    [Export]
    public NodePath TargetPath { get; set; } = new NodePath("../Player");

    [Export]
    public Vector2 SpawnOffset { get; set; } = new Vector2(36.0f, 0.0f);

    [Export]
    public float SpawnCooldown { get; set; } = 0.25f;

    private Node2D _target;
    private float _spawnCooldown;

    public override void _Ready()
    {
        _target = GetNodeOrNull<Node2D>(TargetPath);
        if (_target == null)
            _target = GetParentOrNull<Node>()?.GetNodeOrNull<Node2D>("Player");

        if (_target == null)
            GD.PrintErr("DebugEnemySpawner could not find target node.");
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
        skeleton.SetTarget(_target);
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
        ogre.SetTarget(_target);
        var parent = GetParent();
        if (parent != null)
            parent.AddChild(ogre);
    }

    private Vector2 GetSpawnPosition()
    {
        var spawnPosition = GlobalPosition;
        if (_target != null && _target.IsInsideTree())
            spawnPosition = _target.GlobalPosition + SpawnOffset;

        return spawnPosition;
    }
}
