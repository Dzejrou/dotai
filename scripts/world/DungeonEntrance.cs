using Godot;

// Interaction-only dungeon entrance. Unlike the old walk-triggered door it never starts a run
// by itself: it just exposes the standard interaction prompt and runs its child interactions
// (DungeonEntranceInteraction) when the player presses the interact key. Starting the run is
// entirely the Dungeon HUB's responsibility.
[GlobalClass]
public partial class DungeonEntrance : Node2D, IInteractable, IInteractionPromptAnchor
{
    [Export]
    public Vector2 InteractionPromptOffset { get; set; } = new(0.0f, -56.0f);

    public override void _EnterTree()
    {
        AddToGroup(InteractionGroups.Interactables);
    }

    public bool CanInteract(Node interactor)
    {
        return interactor is Player &&
            interactor.IsInsideTree() &&
            IsInsideTree() &&
            InteractionRunner.HasInteractions(this);
    }

    public void Interact(Node interactor)
    {
        if (interactor is not Player)
            return;

        InteractionRunner.Execute(this, interactor);
    }
}
