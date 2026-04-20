using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class RoomScreen : Node2D
{
    [Export]
    public StringName ScreenId { get; set; } = default;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public StringName Theme { get; set; } = default;

    [Export]
    public StringName RoomType { get; set; } = default;

    [Export]
    public StringName RoomSize { get; set; } = default;

    [Export]
    public NodePath ExitsPath { get; set; } = new NodePath("Scaled/Exits");

    [Export]
    public NodePath MarkersPath { get; set; } = new NodePath("Scaled/Markers");

    [Export]
    public NodePath PlayerStartPath { get; set; } = new NodePath("Scaled/Markers/PlayerStart");

    [Export]
    public NodePath CameraAnchorPath { get; set; } = new NodePath("Scaled/Markers/CameraAnchor");

    [Export]
    public NodePath UnscaledPath { get; set; } = new NodePath("Unscaled");

    [Export]
    public NodePath ScaledPath { get; set; } = new NodePath("Scaled");

    [Export]
    public Rect2 CameraBoundsRect { get; set; } = new Rect2(0.0f, 0.0f, 400.0f, 400.0f);

    public event Action<RoomExit> ExitTriggered;

    private readonly Dictionary<StringName, RoomExit> _exitsById = new();

    public override void _Ready()
    {
        EnsureExitsCached();
    }

    public override void _ExitTree()
    {
        foreach (var roomExit in _exitsById.Values)
            roomExit.TransitionRequested -= OnExitTransitionRequested;

        _exitsById.Clear();
    }

    public RoomExit GetExit(StringName exitId)
    {
        EnsureExitsCached();
        return HasValue(exitId) && _exitsById.TryGetValue(exitId, out var roomExit)
            ? roomExit
            : null;
    }

    public bool TryGetSpawnMarker(StringName exitId, out Marker2D marker)
    {
        EnsureExitsCached();
        marker = null;

        if (HasValue(exitId))
        {
            var entryExit = GetExit(exitId);
            marker = entryExit?.GetSpawnPoint();
            if (marker != null)
                return true;
        }

        marker = GetNodeOrNull<Marker2D>(PlayerStartPath);
        return marker != null;
    }

    public Marker2D GetCameraAnchor()
    {
        if (CameraAnchorPath.IsEmpty)
            return null;

        return GetNodeOrNull<Marker2D>(CameraAnchorPath);
    }

    public Rect2 GetWorldCameraBounds()
    {
        var localBounds = CameraBoundsRect.Size.X > 0.0f && CameraBoundsRect.Size.Y > 0.0f
            ? CameraBoundsRect
            : new Rect2(0.0f, 0.0f, 400.0f, 400.0f);

        var transformRoot = GetNodeOrNull<Node2D>(ScaledPath) ?? this;
        var transform = transformRoot.GlobalTransform;

        var topLeft = transform * localBounds.Position;
        var topRight = transform * new Vector2(localBounds.End.X, localBounds.Position.Y);
        var bottomRight = transform * localBounds.End;
        var bottomLeft = transform * new Vector2(localBounds.Position.X, localBounds.End.Y);

        var minX = Mathf.Min(Mathf.Min(topLeft.X, topRight.X), Mathf.Min(bottomRight.X, bottomLeft.X));
        var minY = Mathf.Min(Mathf.Min(topLeft.Y, topRight.Y), Mathf.Min(bottomRight.Y, bottomLeft.Y));
        var maxX = Mathf.Max(Mathf.Max(topLeft.X, topRight.X), Mathf.Max(bottomRight.X, bottomLeft.X));
        var maxY = Mathf.Max(Mathf.Max(topLeft.Y, topRight.Y), Mathf.Max(bottomRight.Y, bottomLeft.Y));

        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }

    public Node2D GetUnscaledRoot()
    {
        if (UnscaledPath.IsEmpty)
            return null;

        return GetNodeOrNull<Node2D>(UnscaledPath);
    }

    private void CacheExits()
    {
        _exitsById.Clear();

        var exitsRoot = GetNodeOrNull<Node>(ExitsPath);
        if (exitsRoot == null)
            return;

        foreach (Node child in exitsRoot.GetChildren())
        {
            if (child is not RoomExit roomExit || !HasValue(roomExit.ExitId))
                continue;

            _exitsById[roomExit.ExitId] = roomExit;
            roomExit.TransitionRequested += OnExitTransitionRequested;
        }
    }

    private void EnsureExitsCached()
    {
        if (_exitsById.Count > 0)
            return;

        CacheExits();
    }

    private void OnExitTransitionRequested(RoomExit roomExit)
    {
        ExitTriggered?.Invoke(roomExit);
    }
    private static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }
}
