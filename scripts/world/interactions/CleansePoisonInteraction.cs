using Godot;

[GlobalClass]
public partial class CleansePoisonInteraction : Interaction
{
    public override InteractionResult Execute(InteractionContext context)
    {
        if (context?.Interactor is not Player player)
            return InteractionResult.Continue;

        var statusEffectController = player.GetNodeOrNull<StatusEffectController>("StatusEffectController");
        if (statusEffectController == null || !statusEffectController.HasStatus(PoisonedEffect.StatusKeyName))
            return InteractionResult.Continue;

        statusEffectController.RemoveStatus(PoisonedEffect.StatusKeyName);
        return InteractionResult.Continue;
    }
}
