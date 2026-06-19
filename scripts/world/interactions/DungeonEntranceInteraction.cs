using Godot;

// Proxies an entrance-hall dungeon-entrance interaction to World, mirroring how
// MerchantInteraction dispatches the merchant window. World relays it to Main, which opens the
// HUB on the Dungeon page and grants entrance authorization for that HUB session. No run is
// started here, so authorization is never faked by launching early.
[GlobalClass]
public partial class DungeonEntranceInteraction : Interaction
{
    public override InteractionResult Execute(InteractionContext context)
    {
        if (context?.Interactor is not Player player)
            return InteractionResult.Continue;

        var world = context.World;
        if (world == null || !GodotObject.IsInstanceValid(world))
        {
            GD.PushWarning($"{nameof(DungeonEntranceInteraction)}: no World available to dispatch the entrance request.");
            return InteractionResult.Stop;
        }

        world.RequestDungeonEntranceInteraction(player);
        return InteractionResult.Stop;
    }
}
