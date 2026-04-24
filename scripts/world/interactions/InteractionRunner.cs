using Godot;

public static class InteractionRunner
{
    public static bool HasInteractions(Node interactable)
    {
        if (!GodotObject.IsInstanceValid(interactable))
            return false;

        foreach (var child in interactable.GetChildren())
        {
            if (child is Interaction)
                return true;
        }

        return false;
    }

    public static InteractionResult Execute(Node interactable, Node interactor)
    {
        if (!GodotObject.IsInstanceValid(interactable))
            return InteractionResult.Continue;

        var context = new InteractionContext(
            interactor,
            interactable,
            FindWorld(interactable) ?? FindWorld(interactor));

        foreach (var child in interactable.GetChildren())
        {
            if (child is not Interaction interaction)
                continue;

            var result = interaction.Execute(context);
            if (result == InteractionResult.Stop)
                return InteractionResult.Stop;
        }

        return InteractionResult.Continue;
    }

    private static World FindWorld(Node node)
    {
        var current = node;
        while (current != null)
        {
            if (current is World world)
                return world;

            current = current.GetParent();
        }

        return null;
    }
}
