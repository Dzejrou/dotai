using Godot;

public abstract partial class ActorSpawnPoint : Marker2D
{
    [Signal]
    public delegate void OccupancyChangedEventHandler(bool occupied);

    private const string PatrolPathNodeName = "PatrolPath";

    private Node2D _currentSpawnedActor;
    private CombatCharacter _trackedCombatCharacter;
    private bool _isOccupied;

    public Node2D CurrentSpawnedActor => ResolveTrackedActor();

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
        ConnectTrackedActorSignals(actor);
        SetOccupied(true);
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
        DisconnectTrackedActorSignals();
        _currentSpawnedActor = null;
        SetOccupied(false);
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
        {
            SetOccupied(false);
            return false;
        }

        return _isOccupied;
    }

    private Node2D ResolveTrackedActor()
    {
        if (_currentSpawnedActor == null)
            return null;

        if (!GodotObject.IsInstanceValid(_currentSpawnedActor) ||
            _currentSpawnedActor.IsQueuedForDeletion() ||
            _currentSpawnedActor.GetParent() != this)
        {
            DisconnectTrackedActorSignals();
            _currentSpawnedActor = null;
            SetOccupied(false);
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

    private void ConnectTrackedActorSignals(Node2D actor)
    {
        if (actor == null)
            return;

        var treeExitedCallable = new Callable(this, nameof(OnTrackedActorTreeExited));
        if (!actor.IsConnected(Node.SignalName.TreeExited, treeExitedCallable))
            actor.Connect(Node.SignalName.TreeExited, treeExitedCallable, (uint)ConnectFlags.OneShot);

        _trackedCombatCharacter = actor as CombatCharacter;
        if (_trackedCombatCharacter == null)
            return;

        var diedCallable = new Callable(this, nameof(OnTrackedCombatCharacterDied));
        if (!_trackedCombatCharacter.IsConnected(CombatCharacter.SignalName.Died, diedCallable))
            _trackedCombatCharacter.Connect(CombatCharacter.SignalName.Died, diedCallable, (uint)ConnectFlags.OneShot);
    }

    private void DisconnectTrackedActorSignals()
    {
        var treeExitedCallable = new Callable(this, nameof(OnTrackedActorTreeExited));
        if (_currentSpawnedActor != null &&
            GodotObject.IsInstanceValid(_currentSpawnedActor) &&
            _currentSpawnedActor.IsConnected(Node.SignalName.TreeExited, treeExitedCallable))
        {
            _currentSpawnedActor.Disconnect(Node.SignalName.TreeExited, treeExitedCallable);
        }

        var diedCallable = new Callable(this, nameof(OnTrackedCombatCharacterDied));
        if (_trackedCombatCharacter != null &&
            GodotObject.IsInstanceValid(_trackedCombatCharacter) &&
            _trackedCombatCharacter.IsConnected(CombatCharacter.SignalName.Died, diedCallable))
        {
            _trackedCombatCharacter.Disconnect(CombatCharacter.SignalName.Died, diedCallable);
        }

        _trackedCombatCharacter = null;
    }

    private void OnTrackedCombatCharacterDied()
    {
        SetOccupied(false);
    }

    private void OnTrackedActorTreeExited()
    {
        DisconnectTrackedActorSignals();
        _currentSpawnedActor = null;
        SetOccupied(false);
    }

    private void SetOccupied(bool occupied)
    {
        if (_isOccupied == occupied)
            return;

        _isOccupied = occupied;
        EmitSignal(SignalName.OccupancyChanged, occupied);
    }

    protected abstract Node2D SpawnActor();
}
