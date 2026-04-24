using Godot;

[GlobalClass]
public partial class DropLootInteraction : Interaction
{
    public override InteractionResult Execute(InteractionContext context)
    {
        if (context?.Interactable is not Chest chest)
            return InteractionResult.Continue;

        if (chest.HasDroppedLoot)
            return InteractionResult.Continue;

        return chest.TryDropLoot()
            ? InteractionResult.Continue
            : InteractionResult.Stop;
    }
}
