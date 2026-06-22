using Godot;

public abstract partial class ActorSpawnPoint : Marker2D
{
    [Signal]
    public delegate void OccupancyChangedEventHandler(bool occupied);

    // Raised once when the tracked spawned CombatCharacter dies, carrying the dead actor so an
    // owner (e.g. Dungeon) can attribute the death authoritatively from the spawn lifecycle
    // instead of scanning the tree. Fires for actors spawned later too (summons/boss).
    [Signal]
    public delegate void TrackedActorDiedEventHandler(CombatCharacter actor);

    private const string PatrolPathNodeName = "PatrolPath";

    private Node2D _currentSpawnedActor;
    private CombatCharacter _trackedCombatCharacter;
    private bool _isOccupied;
    private DungeonActorBuff _dungeonActorBuff;

    public Node2D CurrentSpawnedActor => ResolveTrackedActor();

    // Configures the dungeon difficulty buff stamped onto every actor this spawn point establishes.
    // Set by Dungeon while preparing a managed room (before the room enters the tree), so the buff is
    // in place by the time actors spawn or are adopted. Application is idempotent and unique, so it
    // never stacks across repeated spawns, restorations or reconciliations.
    public void SetDungeonActorBuff(DungeonActorBuff buff)
    {
        _dungeonActorBuff = buff;
        // Stamp an already-live tracked actor (a spawn point configured after its actor is up); a
        // no-op before tree entry, where the actor is not yet initialized.
        ApplyDungeonBuffToTrackedActor(initializeHealth: true);
    }

    public override void _EnterTree()
    {
        ReconcileTrackedActor();
    }

    public override void _Ready()
    {
        // Authored actors are adopted in _EnterTree, before their own _Ready runs. By now they are
        // fully initialized, so a configured dungeon buff is stamped onto them here at full boosted
        // Max Health. Spawner-driven actors are still empty at this point and are buffed on Respawn.
        ApplyDungeonBuffToTrackedActor(initializeHealth: true);
    }

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

        // Freshly spawned: stamp the dungeon buff and begin at full boosted Max Health.
        ApplyDungeonBuffToTrackedActor(initializeHealth: true);
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
            // RestoreCombatState cleared all effects and healed to base full; re-stamp the dungeon buff
            // and refill to the boosted maximum so a restored actor matches a freshly spawned one.
            ApplyDungeonBuffToTrackedActor(initializeHealth: true);
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
            _currentSpawnedActor = FindExistingChildActor();

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
        var actor = _trackedCombatCharacter;
        SetOccupied(false);

        if (actor != null && GodotObject.IsInstanceValid(actor))
            EmitSignal(SignalName.TrackedActorDied, actor);
    }

    private void OnTrackedActorTreeExited()
    {
        var actor = _currentSpawnedActor;
        if (actor != null &&
            GodotObject.IsInstanceValid(actor) &&
            !actor.IsQueuedForDeletion() &&
            actor.GetParent() == this)
        {
            return;
        }

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

    private void ReconcileTrackedActor()
    {
        var actor = ResolveTrackedActor();
        if (actor == null)
        {
            SetOccupied(false);
            return;
        }

        ConnectTrackedActorSignals(actor);
        SetOccupied(!IsTrackedActorDead(actor));
    }

    private Node2D FindExistingChildActor()
    {
        foreach (var child in GetChildren())
        {
            if (child is not CombatCharacter candidate ||
                candidate.IsQueuedForDeletion())
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static bool IsTrackedActorDead(Node2D actor)
    {
        return actor is CombatCharacter combatCharacter && combatCharacter.IsDead;
    }

    private void ApplyDungeonBuffToTrackedActor(bool initializeHealth)
    {
        if (_dungeonActorBuff == null)
            return;

        // Only stamp an actor that is fully in the tree (and therefore initialized). Before tree
        // entry the actor's status controller is not ready, so this safely skips.
        if (ResolveTrackedActor() is CombatCharacter combatCharacter && combatCharacter.IsInsideTree())
            _dungeonActorBuff.ApplyTo(combatCharacter, initializeHealth);
    }

    protected abstract Node2D SpawnActor();
}
