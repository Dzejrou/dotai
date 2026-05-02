using Godot;

public abstract partial class ActorSpawnPoint : Marker2D
{
    private const string PatrolPathNodeName = "PatrolPath";

    private Node2D _currentSpawnedActor;

    public void Respawn()
    {
        ClearSpawnedActor();

        var actor = SpawnActor();
        if (actor == null)
            return;

        AddChild(actor);
        actor.Position = Vector2.Zero;
        CopyPatrolPathToActor(actor);
        _currentSpawnedActor = actor;
    }

    public void Restore()
    {
        var actor = ResolveTrackedActor();
        if (actor == null)
        {
            Respawn();
            return;
        }

        if (actor is CombatCharacter combatCharacter)
        {
            if (combatCharacter.IsDead)
            {
                Respawn();
                return;
            }

            combatCharacter.RestoreCombatState();
            return;
        }

        GD.PushWarning($"{nameof(ActorSpawnPoint)} '{Name}' is occupied by '{actor.GetType().Name}', which is not a {nameof(CombatCharacter)}. Leaving it in place.");
    }

    public void ClearSpawnedActor()
    {
        var actor = ResolveTrackedActor();
        _currentSpawnedActor = null;
        if (actor == null)
            return;

        RemoveChild(actor);
        actor.QueueFree();
    }

    public bool IsOccupied()
    {
        var actor = ResolveTrackedActor();
        if (actor == null)
            return false;

        if (actor is CombatCharacter combatCharacter && combatCharacter.IsDead)
            return false;

        return true;
    }

    private Node2D ResolveTrackedActor()
    {
        if (_currentSpawnedActor == null)
            return null;

        if (!GodotObject.IsInstanceValid(_currentSpawnedActor) ||
            _currentSpawnedActor.IsQueuedForDeletion() ||
            _currentSpawnedActor.GetParent() != this)
        {
            _currentSpawnedActor = null;
            return null;
        }

        return _currentSpawnedActor;
    }

    protected Node2D InstantiateActorScene(PackedScene actorScene, bool clearLootTable, string spawnPointTypeName)
    {
        if (actorScene == null)
        {
            GD.PushWarning($"{spawnPointTypeName} '{Name}' is missing an actor scene.");
            return null;
        }

        if (actorScene.Instantiate<Node2D>() is not Node2D actor)
        {
            GD.PushWarning($"{spawnPointTypeName} '{Name}' could not instantiate a Node2D actor from '{actorScene.ResourcePath}'.");
            return null;
        }

        if (clearLootTable && actor is Actor combatActor)
            combatActor.LootTable = null;

        return actor;
    }

    private void CopyPatrolPathToActor(Node2D actor)
    {
        var sourcePatrolPath = GetNodeOrNull<Node>(PatrolPathNodeName);
        if (sourcePatrolPath == null)
            return;

        if (sourcePatrolPath.Duplicate() is not Node copiedPatrolPath)
        {
            GD.PushWarning($"{nameof(ActorSpawnPoint)} '{Name}' could not duplicate '{PatrolPathNodeName}' for spawned actor '{actor.Name}'.");
            return;
        }

        var existingPatrolPath = actor.GetNodeOrNull<Node>(PatrolPathNodeName);
        if (existingPatrolPath != null)
        {
            GD.PushWarning($"{nameof(ActorSpawnPoint)} '{Name}' replaced existing '{PatrolPathNodeName}' on spawned actor '{actor.Name}'.");
            actor.RemoveChild(existingPatrolPath);
            existingPatrolPath.QueueFree();
        }

        actor.AddChild(copiedPatrolPath);
    }

    protected abstract Node2D SpawnActor();
}
