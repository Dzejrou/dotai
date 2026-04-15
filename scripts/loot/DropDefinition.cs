using Godot;

[GlobalClass]
public partial class DropDefinition : Resource
{
    [Export]
    public string Id { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public PackedScene DropScene { get; set; }

    public Drop CreateDropInstance()
    {
        if (DropScene == null)
            return null;

        var instance = DropScene.Instantiate();
        if (instance is Drop drop)
            return drop;

        GD.PushError($"DropDefinition '{DisplayName}' points to a scene that does not inherit Drop.");
        instance.Free();
        return null;
    }
}
