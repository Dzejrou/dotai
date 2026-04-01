using Godot;

[GlobalClass]
public abstract partial class WorldObject : StaticBody2D
{
    protected Sprite2D VisualSprite { get; private set; }
    protected CollisionShape2D CollisionShape { get; private set; }

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
