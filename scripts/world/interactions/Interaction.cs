using Godot;

[GlobalClass]
public abstract partial class Interaction : Node
{
    public abstract InteractionResult Execute(InteractionContext context);
}
