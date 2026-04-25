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

    public virtual Drop CreateDropInstance()
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

    public virtual void ConfigureDrop(Drop drop, int amount)
    {
        if (drop is GoldSackDrop goldSackDrop)
            goldSackDrop.GoldAmount = Mathf.Max(0, amount);
    }
}
