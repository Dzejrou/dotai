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

    protected abstract Node2D SpawnActor();
}
