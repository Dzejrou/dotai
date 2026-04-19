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
    public NodePath ExitsPath { get; set; } = new NodePath("Exits");

    [Export]
    public NodePath MarkersPath { get; set; } = new NodePath("Markers");

    [Export]
    public NodePath PlayerStartPath { get; set; } = new NodePath("Markers/PlayerStart");

    [Export]
    public NodePath CameraAnchorPath { get; set; } = new NodePath("Markers/CameraAnchor");

    public event Action<RoomExit> ExitTriggered;

    private readonly Dictionary<StringName, RoomExit> _exitsById = new();

    public override void _Ready()
    {
        CacheExits();
    }

    public override void _ExitTree()
    {
        foreach (var roomExit in _exitsById.Values)
            roomExit.TransitionRequested -= OnExitTransitionRequested;

        _exitsById.Clear();
    }

    public RoomExit GetExit(StringName exitId)
    {
        return HasValue(exitId) && _exitsById.TryGetValue(exitId, out var roomExit)
            ? roomExit
            : null;
    }

    public bool TryGetSpawnMarker(StringName exitId, out Marker2D marker)
    {
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

    private void OnExitTransitionRequested(RoomExit roomExit)
    {
        ExitTriggered?.Invoke(roomExit);
    }
    private static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }
}
