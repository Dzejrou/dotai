using Godot;

public partial class StaticActorSpawnPoint : ActorSpawnPoint
{
    [Export]
    public PackedScene ActorScene { get; set; }

    [Export]
    public bool ClearLootTable { get; set; }

    protected override Node2D SpawnActor()
    {
        return InstantiateActorScene(ActorScene, ClearLootTable, nameof(StaticActorSpawnPoint));
    }
}
