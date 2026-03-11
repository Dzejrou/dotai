using Godot;

using System.Collections.Generic;

public partial class DebugEnemySpawner : Node2D
{
    private sealed class PreviewData
    {
        public SpriteFrames SpriteFrames { get; init; }
        public StringName AnimationName { get; init; }
        public Texture2D Texture { get; init; }
        public Vector2 Scale { get; init; } = Vector2.One;
        public Vector2 Offset { get; init; } = Vector2.Zero;
    }

    [Export]
    public EnemyCatalog EnemyCatalog { get; set; }

    [Export]
    public NodePath TargetPath { get; set; } = new NodePath("../Player");

    private readonly Dictionary<string, EnemyCatalogEntry> _entries_by_id = new();
    private readonly List<EnemyCatalogEntry> _ordered_entries = new();
    private readonly Dictionary<string, PreviewData> _preview_by_id = new();
    private Node2D _target;
    private string _pending_enemy_id;
    private Sprite2D _placement_ghost;

    public bool HasPendingPlacement => !string.IsNullOrEmpty(_pending_enemy_id);

    public string PendingEnemyId => _pending_enemy_id;

    public override void _Ready()
    {
        _target = GetNodeOrNull<Node2D>(TargetPath);
        if (_target == null)
            _target = GetParentOrNull<Node>()?.GetNodeOrNull<Node2D>("Player");

        if (_target == null)
            GD.PrintErr("DebugEnemySpawner could not find target node.");

        BuildCatalogCache();
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

    public IReadOnlyList<EnemyCatalogEntry> GetCatalogEntries() => _ordered_entries;

    public void BeginPlacement(string enemyId)
    {
        if (!_entries_by_id.ContainsKey(enemyId))
            return;

        _pending_enemy_id = enemyId;
        UpdatePlacementGhost(enemyId);
    }

    public void CancelPlacement()
    {
        _pending_enemy_id = null;
        HidePlacementGhost();
    }

    public bool PlacePendingAtCursor(bool preservePlacement = false)
    {
        if (!HasPendingPlacement)
            return false;

        var enemyId = _pending_enemy_id;
        var spawnPosition = GetMouseWorldPosition();
        var enemy = SpawnEnemy(enemyId, spawnPosition);
        if (enemy == null)
            return false;

        if (!preservePlacement)
            CancelPlacement();

        return true;
    }

    public void SpawnEnemyFromDebugUi(string enemyId)
    {
        var spawnPosition = GetMouseWorldPosition();
        SpawnEnemy(enemyId, spawnPosition);
    }

    public SpriteFrames GetPreviewFrames(string enemyId)
    {
        return _preview_by_id.TryGetValue(enemyId, out var previewData) ? previewData.SpriteFrames : null;
    }

    public StringName GetPreviewAnimationName(string enemyId)
    {
        return _preview_by_id.TryGetValue(enemyId, out var previewData) ? previewData.AnimationName : new StringName();
    }

    public Vector2 GetPreviewScale(string enemyId)
    {
        return _preview_by_id.TryGetValue(enemyId, out var previewData) ? previewData.Scale : Vector2.One;
    }

    public Vector2 GetPreviewOffset(string enemyId)
    {
        return _preview_by_id.TryGetValue(enemyId, out var previewData) ? previewData.Offset : Vector2.Zero;
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

    private EnemyBase SpawnEnemy(string enemyId, Vector2 spawnPosition)
    {
        if (!_entries_by_id.TryGetValue(enemyId, out var entry))
            return null;

        var enemy = entry.EnemyScene?.Instantiate<EnemyBase>();
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

    private void BuildCatalogCache()
    {
        _ordered_entries.Clear();
        _entries_by_id.Clear();

        if (EnemyCatalog == null)
            return;

        foreach (var entry in EnemyCatalog.GetEnabledEntries())
        {
            if (entry == null || _entries_by_id.ContainsKey(entry.Id))
                continue;

            _ordered_entries.Add(entry);
            _entries_by_id[entry.Id] = entry;
        }
    }

    private void BuildPreviewCache()
    {
        _preview_by_id.Clear();

        foreach (var entry in _ordered_entries)
        {
            var previewData = BuildPreviewData(entry.EnemyScene);
            if (previewData != null)
                _preview_by_id[entry.Id] = previewData;
        }
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

    private void UpdatePlacementGhost(string enemyId)
    {
        EnsurePlacementGhost();

        if (!_preview_by_id.TryGetValue(enemyId, out var previewData) || previewData.Texture == null)
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
