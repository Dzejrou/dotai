using Godot;

[GlobalClass]
public partial class Merchant : Actor, ITargetable, IInteractable, IInteractionPromptAnchor
{
    [Export]
    public Vector2 InteractionPromptOffset { get; set; } = new(0.0f, -48.0f);

    public bool CanBeTargeted => !IsDead;

    public override void _EnterTree()
    {
        base._EnterTree();
        AddToGroup(InteractionGroups.Interactables);
    }

    public override void _Ready()
    {
        InitializeActor(GetNodeOrNull<OmniSprite>("OmniSprite"));
        PlayIdleIfAvailable();
    }

    public bool CanInteract(Node interactor)
    {
        return interactor != null &&
            interactor.IsInsideTree() &&
            IsInsideTree() &&
            !IsDead &&
            InteractionRunner.HasInteractions(this);
    }

    public void Interact(Node interactor)
    {
        InteractionRunner.Execute(this, interactor);
    }

    protected override void OnActorExitTree()
    {
        RemoveFromGroup(InteractionGroups.Interactables);
    }
}
