using Godot;

[GlobalClass]
public partial class LevelUnlockInteraction : Interaction
{
    private int _requiredLevel = 2;

    [Export]
    public int RequiredLevel
    {
        get => _requiredLevel;
        set => _requiredLevel = Mathf.Max(1, value);
    }

    public override InteractionResult Execute(InteractionContext context)
    {
        if (context?.Interactable is not ILockable lockable)
            return InteractionResult.Continue;

        if (!lockable.IsLocked)
            return InteractionResult.Continue;

        if (context.Interactor is not Player player || player.Level < RequiredLevel)
        {
            if (context.Interactable is Node2D origin)
                FloatingText.ShowBad($"Requires level {RequiredLevel}", origin);
            return InteractionResult.Stop;
        }

        lockable.UnlockExternal();
        return InteractionResult.Continue;
    }
}
