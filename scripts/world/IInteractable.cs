using Godot;

public interface IInteractable
{
    bool CanInteract(Node interactor);
    string GetInteractionLabel(Node interactor);
    void Interact(Node interactor);
}
