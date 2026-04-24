using Godot;

[GlobalClass]
public abstract partial class WorldObject : StaticBody2D, IInteractable, IInteractionPromptAnchor
{
    [Export]
    public Vector2 InteractionPromptOffset { get; set; } = new(0.0f, -56.0f);

    protected Sprite2D VisualSprite { get; private set; }
    protected CollisionShape2D CollisionShape { get; private set; }

    public override void _EnterTree()
    {
        AddToGroup(InteractionGroups.Interactables);
    }

    public virtual bool CanInteract(Node interactor)
    {
        return interactor != null &&
            interactor.IsInsideTree() &&
            IsInsideTree() &&
            InteractionRunner.HasInteractions(this);
    }

    public virtual void Interact(Node interactor)
    {
        InteractionRunner.Execute(this, interactor);
    }

    protected void InitializeWorldObject(Sprite2D visualSprite = null, CollisionShape2D collisionShape = null)
    {
        VisualSprite = visualSprite ?? GetNodeOrNull<Sprite2D>("Sprite2D");
        CollisionShape = collisionShape ?? GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
    }

    protected void SetCollisionEnabled(bool enabled)
    {
        if (CollisionShape == null)
            return;

        CollisionShape.SetDeferred("disabled", !enabled);
    }
}
