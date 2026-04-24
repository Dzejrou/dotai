using Godot;

[GlobalClass]
public partial class UnlockChestInteraction : Interaction
{
    public override InteractionResult Execute(InteractionContext context)
    {
        if (context?.Interactable is not Chest chest)
            return InteractionResult.Continue;

        return chest.TryUnlock(context.Interactor)
            ? InteractionResult.Continue
            : InteractionResult.Stop;
    }
}
