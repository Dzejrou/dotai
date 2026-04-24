using Godot;

using System;

[GlobalClass]
public partial class Door : Area2D, IInteractable, IInteractionPromptAnchor, ILockable
{
    private static readonly Color LockedIndicatorColor = new(0.88f, 0.24f, 0.24f, 1.0f);
    private static readonly Color UnlockedIndicatorColor = new(0.30f, 0.86f, 0.34f, 1.0f);

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

    [Export]
    public NodePath LockIndicatorPath { get; set; } = new NodePath("LockIndicator");

    [Export]
    public bool ShowUnlockedIndicator { get; set; }

    [Export]
    public Vector2 IndicatorOffset { get; set; } = new(0.0f, -30.0f);

    [Export]
    public Vector2 InteractionPromptOffset { get; set; } = new(0.0f, -56.0f);

    [Export]
    public bool UnlockOnInteractWhenLocked { get; set; }

    [Export]
    public bool IsLocked
    {
        get => _isLocked;
        set => SetLocked(value);
    }

    public event Action<Door> TransitionRequested;

    private bool _isLocked;
    private bool _playerInside;
    private bool _transitionQueued;

    public override void _Ready()
    {
        AddToGroup(InteractionGroups.Interactables);
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        RefreshLockIndicator();
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
    }

    public bool CanInteract(Node interactor)
    {
        return IsLocked && UnlockOnInteractWhenLocked && interactor is Player;
    }

    public void Interact(Node interactor)
    {
        if (interactor is not Player)
            return;

        if (!IsLocked)
        {
            QueueTransition();
            return;
        }

        if (!UnlockOnInteractWhenLocked)
            return;

        if (!TryUnlock(interactor))
            return;

        if (_playerInside)
            QueueTransition();
    }

    public bool TryUnlock(Node interactor)
    {
        if (!IsLocked)
            return true;

        SetLocked(false);
        return true;
    }

    public Marker2D GetSpawnPoint()
    {
        if (SpawnPointPath == null || SpawnPointPath.IsEmpty)
            return null;

        return GetNodeOrNull<Marker2D>(SpawnPointPath);
    }

    private void SetLocked(bool isLocked)
    {
        _isLocked = isLocked;
        RefreshLockIndicator();
    }

    private void RefreshLockIndicator()
    {
        var indicator = GetNodeOrNull<Polygon2D>(LockIndicatorPath);
        if (indicator == null)
            return;

        indicator.Position = IndicatorOffset;
        indicator.Color = IsLocked ? LockedIndicatorColor : UnlockedIndicatorColor;
        indicator.Visible = IsLocked || ShowUnlockedIndicator;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player)
            return;

        _playerInside = true;
        if (!IsLocked)
            QueueTransition();
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is not Player)
            return;

        _playerInside = false;
        _transitionQueued = false;
    }

    private void QueueTransition()
    {
        if (_transitionQueued || !HasValue(TargetScreenId))
            return;

        _transitionQueued = true;
        CallDeferred(nameof(NotifyTransitionRequestedDeferred));
    }

    private void NotifyTransitionRequestedDeferred()
    {
        _transitionQueued = false;
        if (!_playerInside || !IsInsideTree() || IsLocked || !HasValue(TargetScreenId))
            return;

        TransitionRequested?.Invoke(this);
    }

    private static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }
}
