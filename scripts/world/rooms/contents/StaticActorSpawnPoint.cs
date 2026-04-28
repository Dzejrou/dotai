using Godot;

public partial class StaticActorSpawnPoint : ActorSpawnPoint
{
    [Export]
    public PackedScene ActorScene { get; set; }

    [Export]
    public bool ClearLootTable { get; set; }

    protected override Node2D SpawnActor()
    {
        if (ActorScene == null)
        {
            GD.PushWarning($"{nameof(StaticActorSpawnPoint)} '{Name}' is missing an actor scene.");
            return null;
        }

        if (ActorScene.Instantiate<Node2D>() is not Node2D actor)
        {
            GD.PushWarning($"{nameof(StaticActorSpawnPoint)} '{Name}' could not instantiate a Node2D actor from '{ActorScene.ResourcePath}'.");
            return null;
        }

        if (ClearLootTable && actor is Actor combatActor)
            combatActor.LootTable = null;

        return actor;
    }
}
