using Godot;

using System;

[GlobalClass]
public partial class RoomExit : Area2D
{
    [Export]
    public StringName ExitId { get; set; } = default;

    [Export]
    public StringName TargetScreenId { get; set; } = default;

    [Export]
    public StringName TargetExitId { get; set; } = default;

    [Export]
    public StringName Direction { get; set; } = default;

    [Export]
    public StringName TransitionType { get; set; } = default;

    [Export]
    public bool IsLocked { get; set; }

    [Export]
    public bool OneWay { get; set; }

    [Export]
    public NodePath SpawnPointPath { get; set; } = new NodePath("SpawnPoint");

    public event Action<RoomExit> TransitionRequested;

    private bool _playerInside;
    private bool _transitionQueued;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
    }

    public Marker2D GetSpawnPoint()
    {
        if (SpawnPointPath == null || SpawnPointPath.IsEmpty)
            return null;

        return GetNodeOrNull<Marker2D>(SpawnPointPath);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_playerInside || _transitionQueued || IsLocked || body is not Player)
            return;

        _playerInside = true;
        _transitionQueued = true;
        CallDeferred(nameof(NotifyTransitionRequestedDeferred));
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player)
        {
            _playerInside = false;
            _transitionQueued = false;
        }
    }

    private void NotifyTransitionRequestedDeferred()
    {
        _transitionQueued = false;
        if (!_playerInside || !IsInsideTree() || IsLocked)
            return;

        TransitionRequested?.Invoke(this);
    }
}
