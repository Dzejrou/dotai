using Godot;

public sealed class InteractionContext
{
    public InteractionContext(Node interactor, Node interactable, World world = null)
    {
        Interactor = interactor;
        Interactable = interactable;
        World = world;
    }

    public Node Interactor { get; }
    public Node Interactable { get; }
    public World World { get; }
}
