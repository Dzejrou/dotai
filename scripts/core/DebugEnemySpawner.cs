using Godot;

using System.Collections.Generic;

public partial class DebugEnemySpawner : Node2D
{
    public enum EnemyType
    {
        Skeleton,
        Ogre,
        SkeletonMage
    }

    private sealed class PreviewData
    {
        public SpriteFrames SpriteFrames { get; init; }
        public StringName AnimationName { get; init; }
        public Texture2D Texture { get; init; }
        public Vector2 Scale { get; init; } = Vector2.One;
        public Vector2 Offset { get; init; } = Vector2.Zero;
    }

    [Export]
    public PackedScene SkeletonScene { get; set; }

    [Export]
    public PackedScene OgreScene { get; set; }

    [Export]
    public PackedScene SkeletonMageScene { get; set; }

    [Export]
    public NodePath TargetPath { get; set; } = new NodePath("../Player");

    private readonly Dictionary<EnemyType, PreviewData> _preview_by_type = new();
    private Node2D _target;
    private EnemyType? _pending_enemy_type;
    private Sprite2D _placement_ghost;

    public bool HasPendingPlacement => _pending_enemy_type.HasValue;

    public EnemyType? PendingEnemyType => _pending_enemy_type;

    public override void _Ready()
    {
        _target = GetNodeOrNull<Node2D>(TargetPath);
        if (_target == null)
            _target = GetParentOrNull<Node>()?.GetNodeOrNull<Node2D>("Player");

        if (_target == null)
            GD.PrintErr("DebugEnemySpawner could not find target node.");

        BuildPreviewCache();
        EnsurePlacementGhost();
        HidePlacementGhost();
    }

    public override void _Process(double delta)
    {
        if (!HasPendingPlacement || _placement_ghost == null)
            return;

        _placement_ghost.GlobalPosition = GetMouseWorldPosition();
    }

    public void BeginPlacement(EnemyType enemyType)
    {
        _pending_enemy_type = enemyType;
        UpdatePlacementGhost(enemyType);
    }

    public void CancelPlacement()
    {
        _pending_enemy_type = null;
        HidePlacementGhost();
    }

    public bool PlacePendingAtCursor()
    {
        if (!_pending_enemy_type.HasValue)
            return false;

        var enemyType = _pending_enemy_type.Value;
        var spawnPosition = GetMouseWorldPosition();
        var enemy = SpawnEnemy(enemyType, spawnPosition);
        if (enemy == null)
            return false;

        CancelPlacement();
        return true;
    }

    public SpriteFrames GetPreviewFrames(EnemyType enemyType)
    {
        return _preview_by_type.TryGetValue(enemyType, out var previewData) ? previewData.SpriteFrames : null;
    }

    public StringName GetPreviewAnimationName(EnemyType enemyType)
    {
        return _preview_by_type.TryGetValue(enemyType, out var previewData) ? previewData.AnimationName : new StringName();
    }

    public Vector2 GetPreviewScale(EnemyType enemyType)
    {
        return _preview_by_type.TryGetValue(enemyType, out var previewData) ? previewData.Scale : Vector2.One;
    }

    public Vector2 GetPreviewOffset(EnemyType enemyType)
    {
        return _preview_by_type.TryGetValue(enemyType, out var previewData) ? previewData.Offset : Vector2.Zero;
    }

    public Vector2 GetMouseWorldPosition()
    {
        var spawnPosition = GlobalPosition;
        var viewport = GetViewport();
        if (viewport != null)
        {
            var mousePosition = viewport.GetMousePosition();
            var canvasTransform = viewport.GetCanvasTransform();
            spawnPosition = canvasTransform.AffineInverse() * mousePosition;
        }

        return spawnPosition;
    }

    private EnemyBase SpawnEnemy(EnemyType enemyType, Vector2 spawnPosition)
    {
        var enemyScene = GetSceneForType(enemyType);
        var enemy = enemyScene?.Instantiate<EnemyBase>();
        if (enemy == null)
            return null;

        enemy.GlobalPosition = spawnPosition;
        enemy.ZIndex = -1;
        enemy.SetTarget(_target);
        var parent = GetParent();
        if (parent != null)
            parent.AddChild(enemy);

        return enemy;
    }

    private PackedScene GetSceneForType(EnemyType enemyType)
    {
        return enemyType switch
        {
            EnemyType.Skeleton => SkeletonScene,
            EnemyType.Ogre => OgreScene,
            EnemyType.SkeletonMage => SkeletonMageScene,
            _ => null,
        };
    }

    private void BuildPreviewCache()
    {
        CachePreviewData(EnemyType.Skeleton, SkeletonScene);
        CachePreviewData(EnemyType.Ogre, OgreScene);
        CachePreviewData(EnemyType.SkeletonMage, SkeletonMageScene);
    }

    private void CachePreviewData(EnemyType enemyType, PackedScene enemyScene)
    {
        var previewData = BuildPreviewData(enemyScene);
        if (previewData != null)
            _preview_by_type[enemyType] = previewData;
    }

    private PreviewData BuildPreviewData(PackedScene enemyScene)
    {
        var enemy = enemyScene?.Instantiate<Node>();
        if (enemy == null)
            return null;

        var animatedSprite = enemy.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animatedSprite?.SpriteFrames == null)
        {
            enemy.Free();
            return null;
        }

        var spriteFrames = animatedSprite.SpriteFrames;
        var animationName = spriteFrames.HasAnimation("breathing-idle_south") ?
            (StringName)"breathing-idle_south" :
            (spriteFrames.HasAnimation("walk_south") ? (StringName)"walk_south" : new StringName());
        var texture = animationName.IsEmpty ? null : spriteFrames.GetFrameTexture(animationName, 0);

        var previewData = new PreviewData
        {
            SpriteFrames = spriteFrames,
            AnimationName = animationName,
            Texture = texture,
            Scale = animatedSprite.Scale,
            Offset = animatedSprite.Position,
        };

        enemy.Free();
        return previewData;
    }

    private void EnsurePlacementGhost()
    {
        if (_placement_ghost != null)
            return;

        _placement_ghost = new Sprite2D
        {
            Name = "PlacementGhost",
            Centered = true,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.55f),
            ZIndex = 200,
            TopLevel = true,
            Visible = false,
        };
        AddChild(_placement_ghost);
    }

    private void UpdatePlacementGhost(EnemyType enemyType)
    {
        EnsurePlacementGhost();

        if (!_preview_by_type.TryGetValue(enemyType, out var previewData) || previewData.Texture == null)
        {
            HidePlacementGhost();
            return;
        }

        _placement_ghost.Texture = previewData.Texture;
        _placement_ghost.Scale = previewData.Scale;
        _placement_ghost.Offset = previewData.Offset;
        _placement_ghost.GlobalPosition = GetMouseWorldPosition();
        _placement_ghost.Visible = true;
    }

    private void HidePlacementGhost()
    {
        if (_placement_ghost != null)
            _placement_ghost.Visible = false;
    }
}
