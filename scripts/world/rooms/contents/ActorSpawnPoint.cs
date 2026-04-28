using Godot;

public abstract partial class ActorSpawnPoint : Marker2D
{
    private Node2D _currentSpawnedActor;

    public void Respawn()
    {
        ClearSpawnedActor();

        var actor = SpawnActor();
        if (actor == null)
            return;

        AddChild(actor);
        actor.Position = Vector2.Zero;
        _currentSpawnedActor = actor;
    }

    public void ClearSpawnedActor()
    {
        _currentSpawnedActor = null;

        foreach (var child in GetChildren())
        {
            if (child is not Node node || !GodotObject.IsInstanceValid(node))
                continue;

            RemoveChild(node);
            node.QueueFree();
        }
    }

    public bool IsOccupied()
    {
        if (_currentSpawnedActor == null)
            return false;

        if (!GodotObject.IsInstanceValid(_currentSpawnedActor) ||
            _currentSpawnedActor.IsQueuedForDeletion() ||
            _currentSpawnedActor.GetParent() != this)
        {
            _currentSpawnedActor = null;
            return false;
        }

        return true;
    }

    protected abstract Node2D SpawnActor();
}
