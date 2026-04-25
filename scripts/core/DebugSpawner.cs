using Godot;

using System.Collections.Generic;

public partial class DebugSpawner : Node2D
{
    private const float FactionPickRadius = 20.0f;

    private sealed class PreviewData
    {
        public SpriteFrames SpriteFrames { get; init; }
        public StringName AnimationName { get; init; }
        public Texture2D Texture { get; init; }
        public Vector2 Scale { get; init; } = Vector2.One;
        public Vector2 Offset { get; init; } = Vector2.Zero;
        public Faction DefaultFaction { get; init; }
        public SpawnCatalogEntryKind EntryKind { get; init; }
    }

    [Export]
    public SpawnCatalog SpawnCatalog { get; set; }

    private readonly Dictionary<string, SpawnCatalogEntry> _entriesById = new();
    private readonly List<SpawnCatalogEntry> _orderedEntries = new();
    private readonly Dictionary<string, PreviewData> _previewById = new();
    private string _pendingSpawnId;
    private Sprite2D _placementGhost;
    private Faction _selectedFaction = Factions.Enemies;

    public bool HasPendingPlacement => !string.IsNullOrEmpty(_pendingSpawnId);

    public string PendingSpawnId => _pendingSpawnId;
    public Faction SelectedFaction => _selectedFaction;

    public override void _Ready()
    {
        BuildCatalogCache();
        BuildPreviewCache();
        EnsurePlacementGhost();
        HidePlacementGhost();
    }

    public override void _ExitTree()
    {
        _previewById.Clear();
        _entriesById.Clear();
        _orderedEntries.Clear();

        if (_placementGhost != null)
            _placementGhost.QueueFree();

        _placementGhost = null;
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

        if (_previewById.TryGetValue(spawnId, out var previewData) &&
            previewData.EntryKind == SpawnCatalogEntryKind.Character &&
            previewData.DefaultFaction != null)
        {
            _selectedFaction = previewData.DefaultFaction;
        }
        _pendingSpawnId = spawnId;
        UpdatePlacementGhost(spawnId);
    }

    public void CancelPlacement()
    {
        _pendingSpawnId = null;
        HidePlacementGhost();
    }

    public void SetSelectedFaction(string factionKey)
    {
        _selectedFaction = Factions.Get(factionKey) ?? Factions.Enemies;
    }

    public bool TryBeginPlacementFromActorAtScreenPosition(Vector2 screenPosition)
    {
        var closestFactionMember = FindClosestFactionMemberAtScreenPosition(screenPosition);
        if (closestFactionMember?.Faction == null)
            return false;

        _selectedFaction = closestFactionMember.Faction;

        var spawnId = ResolveSpawnIdForNode(closestFactionMember as Node);
        if (string.IsNullOrEmpty(spawnId) || !_entriesById.ContainsKey(spawnId))
            return false;

        BeginPlacement(spawnId);
        return true;
    }

    public bool TrySelectFactionAtScreenPosition(Vector2 screenPosition)
    {
        var closestFactionMember = FindClosestFactionMemberAtScreenPosition(screenPosition);
        if (closestFactionMember?.Faction == null)
            return false;

        _selectedFaction = closestFactionMember.Faction;
        return true;
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

    public SpriteFrames GetPreviewFrames(string spawnId)
    {
        return _previewById.TryGetValue(spawnId, out var previewData) ? previewData.SpriteFrames : null;
    }

    public Texture2D GetPreviewTexture(string spawnId)
    {
        return _previewById.TryGetValue(spawnId, out var previewData) ? previewData.Texture : null;
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

        if (entry.EntryKind == SpawnCatalogEntryKind.Character)
        {
            var factionState = FactionState.ResolveFor(spawnedNode);
            factionState?.SetFaction(_selectedFaction);
        }

        var parent = GetParent();
        if (parent != null)
        {
            spawnedNode.GlobalPosition = spawnPosition;
            if (entry.EntryKind == SpawnCatalogEntryKind.Character)
                spawnedNode.ZIndex = -1;

            parent.AddChild(spawnedNode);
        }

        return spawnedNode;
    }

    private IFactionMember FindClosestFactionMemberAtScreenPosition(Vector2 screenPosition)
    {
        var viewport = GetViewport();
        if (viewport == null || GetTree() == null)
            return null;

        var worldPosition = viewport.GetCanvasTransform().AffineInverse() * screenPosition;
        IFactionMember closestFactionMember = null;
        var closestDistance = FactionPickRadius;

        foreach (var node in GetTree().GetNodesInGroup(CombatGroups.Actors))
        {
            if (node is not Node2D node2D ||
                node is not IFactionMember factionMember ||
                factionMember.Faction == null ||
                !node2D.IsInsideTree())
            {
                continue;
            }

            var distance = node2D.GlobalPosition.DistanceTo(worldPosition);
            if (distance > closestDistance)
                continue;

            closestDistance = distance;
            closestFactionMember = factionMember;
        }

        return closestFactionMember;
    }

    private string ResolveSpawnIdForNode(Node node)
    {
        if (node == null)
            return null;

        var sceneFilePath = node.SceneFilePath;
        if (string.IsNullOrEmpty(sceneFilePath))
            return null;

        foreach (var entry in _orderedEntries)
        {
            if (entry?.SpawnScene?.ResourcePath == sceneFilePath)
                return entry.Id;
        }

        return null;
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
            var previewData = BuildPreviewData(entry);
            if (previewData != null)
                _previewById[entry.Id] = previewData;
        }
    }

    private PreviewData BuildPreviewData(SpawnCatalogEntry entry)
    {
        var spawnedNode = entry?.SpawnScene?.Instantiate<Node>();
        if (spawnedNode == null)
            return null;

        var omniSprite = spawnedNode.GetNodeOrNull<OmniSprite>("OmniSprite");
        var animatedSprite = omniSprite?.AnimatedSprite ?? spawnedNode.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animatedSprite?.SpriteFrames != null)
        {
            var spriteFrames = animatedSprite.SpriteFrames;
            var animationName = spriteFrames.HasAnimation("idle_south") ?
                (StringName)"idle_south" :
                (spriteFrames.HasAnimation("walk_south") ? (StringName)"walk_south" : new StringName());
            var texture = animationName.IsEmpty ? null : spriteFrames.GetFrameTexture(animationName, 0);

            var previewData = new PreviewData
            {
                SpriteFrames = spriteFrames,
                AnimationName = animationName,
                Texture = texture,
                Scale = animatedSprite.Scale,
                Offset = animatedSprite.Position,
                DefaultFaction = ResolvePreviewFaction(spawnedNode),
                EntryKind = entry.EntryKind,
            };

            spawnedNode.Free();
            return previewData;
        }

        var sprite = omniSprite?.StaticSprite ?? spawnedNode.GetNodeOrNull<Sprite2D>("Sprite2D");
        var staticTexture = sprite?.Texture ?? (spawnedNode as Drop)?.WorldSprite;
        if (staticTexture == null)
        {
            spawnedNode.Free();
            return null;
        }

        var staticPreviewData = new PreviewData
        {
            Texture = staticTexture,
            Scale = sprite?.Scale ?? Vector2.One,
            Offset = sprite?.Position ?? Vector2.Zero,
            DefaultFaction = ResolvePreviewFaction(spawnedNode),
            EntryKind = entry.EntryKind,
        };

        spawnedNode.Free();
        return staticPreviewData;
    }

    private static Faction ResolvePreviewFaction(Node enemy)
    {
        var factionState = FactionState.ResolveFor(enemy);
        if (factionState == null)
            return null;

        return Factions.Get(factionState.FactionKey);
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
