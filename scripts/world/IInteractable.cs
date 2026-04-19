using Godot;

public interface IInteractable
{
    bool CanInteract(Node interactor);
    void Interact(Node interactor);
}
