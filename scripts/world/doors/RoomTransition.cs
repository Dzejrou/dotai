using Godot;

using System;

[GlobalClass]
public partial class RoomTransition : Area2D
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
    public bool OneWay { get; set; }

    [Export]
    public NodePath SpawnPointPath { get; set; } = new NodePath("SpawnPoint");

    public event Action<RoomTransition> TransitionRequested;

    private bool _playerInside;
    private bool _transitionQueued;
    private bool _bodySignalsConnected;

    protected bool IsPlayerInside => _playerInside;

    public override void _EnterTree()
    {
        EnsureBodySignalsConnected();
    }

    public override void _ExitTree()
    {
        DisconnectBodySignals();
    }

    public Marker2D GetSpawnPoint()
    {
        if (SpawnPointPath == null || SpawnPointPath.IsEmpty)
            return null;

        return GetNodeOrNull<Marker2D>(SpawnPointPath);
    }

    protected virtual bool CanQueueTransition()
    {
        return true;
    }

    protected void QueueTransition()
    {
        if (_transitionQueued || !HasValue(TargetScreenId) || !CanQueueTransition())
            return;

        _transitionQueued = true;
        CallDeferred(nameof(NotifyTransitionRequestedDeferred));
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player)
            return;

        _playerInside = true;
        if (CanQueueTransition())
            QueueTransition();
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is not Player)
            return;

        _playerInside = false;
        _transitionQueued = false;
    }

    private void NotifyTransitionRequestedDeferred()
    {
        _transitionQueued = false;
        if (!_playerInside || !IsInsideTree() || !HasValue(TargetScreenId) || !CanQueueTransition())
            return;

        TransitionRequested?.Invoke(this);
    }

    protected static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }

    private void EnsureBodySignalsConnected()
    {
        if (_bodySignalsConnected)
            return;

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        _bodySignalsConnected = true;
    }

    private void DisconnectBodySignals()
    {
        if (!_bodySignalsConnected)
            return;

        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
        _bodySignalsConnected = false;
    }
}
