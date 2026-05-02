using Godot;

[GlobalClass]
public partial class RandomActorSpawnOption : Resource
{
    [Export]
    public PackedScene ActorScene { get; set; }

    [Export(PropertyHint.Range, "0,100,0.1,or_greater")]
    public float Weight { get; set; } = 1.0f;

    [Export]
    public bool ClearLootTable { get; set; }

    public bool IsConfigured => ActorScene != null && float.IsFinite(Weight) && Weight > 0.0f;
}
