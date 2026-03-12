using Godot;

using System.Collections.Generic;

public partial class DebugSpawner : Node2D
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
    public SpawnCatalog SpawnCatalog { get; set; }

    [Export]
    public NodePath TargetPath { get; set; } = new NodePath("../Player");

    private readonly Dictionary<string, SpawnCatalogEntry> _entriesById = new();
    private readonly List<SpawnCatalogEntry> _orderedEntries = new();
    private readonly Dictionary<string, PreviewData> _previewById = new();
    private Node2D _target;
    private string _pendingSpawnId;
    private Sprite2D _placementGhost;

    public bool HasPendingPlacement => !string.IsNullOrEmpty(_pendingSpawnId);

    public string PendingSpawnId => _pendingSpawnId;

    public override void _Ready()
    {
        _target = GetNodeOrNull<Node2D>(TargetPath);
        if (_target == null)
            _target = GetParentOrNull<Node>()?.GetNodeOrNull<Node2D>("Player");

        if (_target == null)
            GD.PrintErr("DebugSpawner could not find target node.");

        BuildCatalogCache();
        BuildPreviewCache();
        EnsurePlacementGhost();
        HidePlacementGhost();
    }

    public override void _Process(double delta)
    {
        if (!HasPendingPlacement || _placementGhost == null)
            return;

        _placementGhost.GlobalPosition = GetMouseWorldPosition();
    }

    public IReadOnlyList<SpawnCatalogEntry> GetCatalogEntries() => _orderedEntries;

    public void BeginPlacement(string spawnId)
    {
        if (!_entriesById.ContainsKey(spawnId))
            return;

        _pendingSpawnId = spawnId;
        UpdatePlacementGhost(spawnId);
    }

    public void CancelPlacement()
    {
        _pendingSpawnId = null;
        HidePlacementGhost();
    }

    public bool PlacePendingAtCursor(bool preservePlacement = false)
    {
        if (!HasPendingPlacement)
            return false;

        var spawnId = _pendingSpawnId;
        var spawnPosition = GetMouseWorldPosition();
        var spawnedNode = SpawnNode(spawnId, spawnPosition);
        if (spawnedNode == null)
            return false;

        if (!preservePlacement)
            CancelPlacement();

        return true;
    }

    public void SpawnFromDebugUi(string spawnId)
    {
        var spawnPosition = GetMouseWorldPosition();
        SpawnNode(spawnId, spawnPosition);
    }

    public SpriteFrames GetPreviewFrames(string spawnId)
    {
        return _previewById.TryGetValue(spawnId, out var previewData) ? previewData.SpriteFrames : null;
    }

    public StringName GetPreviewAnimationName(string spawnId)
    {
        return _previewById.TryGetValue(spawnId, out var previewData) ? previewData.AnimationName : new StringName();
    }

    public Vector2 GetPreviewScale(string spawnId)
    {
        return _previewById.TryGetValue(spawnId, out var previewData) ? previewData.Scale : Vector2.One;
    }

    public Vector2 GetPreviewOffset(string spawnId)
    {
        return _previewById.TryGetValue(spawnId, out var previewData) ? previewData.Offset : Vector2.Zero;
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

    private Node2D SpawnNode(string spawnId, Vector2 spawnPosition)
    {
        if (!_entriesById.TryGetValue(spawnId, out var entry))
            return null;

        var spawnedNode = entry.SpawnScene?.Instantiate<Node2D>();
        if (spawnedNode == null)
            return null;

        spawnedNode.GlobalPosition = spawnPosition;
        spawnedNode.ZIndex = -1;

        if (spawnedNode is EnemyBase enemy)
            enemy.SetTarget(_target);

        var parent = GetParent();
        if (parent != null)
            parent.AddChild(spawnedNode);

        return spawnedNode;
    }

    private void BuildCatalogCache()
    {
        _orderedEntries.Clear();
        _entriesById.Clear();

        if (SpawnCatalog == null)
            return;

        foreach (var entry in SpawnCatalog.GetEnabledEntries())
        {
            if (entry == null || _entriesById.ContainsKey(entry.Id))
                continue;

            _orderedEntries.Add(entry);
            _entriesById[entry.Id] = entry;
        }
    }

    private void BuildPreviewCache()
    {
        _previewById.Clear();

        foreach (var entry in _orderedEntries)
        {
            var previewData = BuildPreviewData(entry.SpawnScene);
            if (previewData != null)
                _previewById[entry.Id] = previewData;
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
        if (_placementGhost != null)
            return;

        _placementGhost = new Sprite2D
        {
            Name = "PlacementGhost",
            Centered = true,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.55f),
            ZIndex = 200,
            TopLevel = true,
            Visible = false,
        };
        AddChild(_placementGhost);
    }

    private void UpdatePlacementGhost(string spawnId)
    {
        EnsurePlacementGhost();

        if (!_previewById.TryGetValue(spawnId, out var previewData) || previewData.Texture == null)
        {
            HidePlacementGhost();
            return;
        }

        _placementGhost.Texture = previewData.Texture;
        _placementGhost.Scale = previewData.Scale;
        _placementGhost.Offset = previewData.Offset;
        _placementGhost.GlobalPosition = GetMouseWorldPosition();
        _placementGhost.Visible = true;
    }

    private void HidePlacementGhost()
    {
        if (_placementGhost != null)
            _placementGhost.Visible = false;
    }
}
