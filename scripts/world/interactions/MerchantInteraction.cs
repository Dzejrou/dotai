using Godot;

[GlobalClass]
public partial class MerchantInteraction : Interaction
{
    [Export]
    public NodePath StockPath { get; set; } = new NodePath();

    public override InteractionResult Execute(InteractionContext context)
    {
        if (context?.Interactor is not Player player)
            return InteractionResult.Continue;

        var stock = ResolveStock(context.Interactable);
        if (stock == null)
        {
            GD.PushWarning($"{nameof(MerchantInteraction)}: could not resolve a {nameof(MerchantStock)} sibling.");
            return InteractionResult.Stop;
        }

        var world = context.World;
        if (world == null || !GodotObject.IsInstanceValid(world))
        {
            GD.PushWarning($"{nameof(MerchantInteraction)}: no World available to dispatch open request.");
            return InteractionResult.Stop;
        }

        world.RequestMerchantInteraction(stock, player);
        return InteractionResult.Stop;
    }

    private MerchantStock ResolveStock(Node interactable)
    {
        if (interactable == null)
            return null;

        if (!StockPath.IsEmpty)
        {
            var explicitStock = GetNodeOrNull<MerchantStock>(StockPath);
            if (explicitStock != null)
                return explicitStock;
        }

        // Look for a sibling MerchantStock under the same interactable root.
        foreach (var child in interactable.GetChildren())
        {
            if (child is MerchantStock stock)
                return stock;
        }

        return null;
    }
}
