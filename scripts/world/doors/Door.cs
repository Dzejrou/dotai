using Godot;

[GlobalClass]
public partial class Door : RoomTransition, IInteractable, IInteractionPromptAnchor, ILockable
{
    private static readonly Color LockedIndicatorColor = new(0.88f, 0.24f, 0.24f, 1.0f);
    private static readonly Color UnlockedIndicatorColor = new(0.30f, 0.86f, 0.34f, 1.0f);

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

    private bool _isLocked;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup(InteractionGroups.Interactables);
        RefreshLockIndicator();
    }

    public bool CanInteract(Node interactor)
    {
        if (interactor is not Player)
            return false;

        if (!IsLocked)
            return false;

        return InteractionRunner.HasInteractions(this) || UnlockOnInteractWhenLocked;
    }

    public void Interact(Node interactor)
    {
        if (interactor is not Player)
            return;

        if (!IsLocked)
            return;

        if (InteractionRunner.HasInteractions(this))
        {
            var result = InteractionRunner.Execute(this, interactor);
            if (result == InteractionResult.Stop)
                return;

            if (!IsLocked && IsPlayerInside)
                QueueTransition();
            return;
        }

        if (!UnlockOnInteractWhenLocked)
            return;

        if (!TryUnlock(interactor))
            return;

        if (IsPlayerInside)
            QueueTransition();
    }

    public bool TryUnlock(Node interactor)
    {
        if (!IsLocked)
            return true;

        UnlockExternal();
        return true;
    }

    public void UnlockExternal()
    {
        if (!IsLocked)
            return;

        SetLocked(false);
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

    protected override bool CanQueueTransition()
    {
        return !IsLocked;
    }
}
