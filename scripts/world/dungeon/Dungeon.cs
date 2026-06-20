using Godot;

using System;
using System.Collections.Generic;

// Drives a live dungeon run entirely from one deterministic DungeonRunPlan. A run is
// generated once on the first transition into the dungeon, then rooms are instantiated and
// traversed strictly from the plan's nodes, edges, levels and preselected content. No room
// kind, definition or content is ever rerolled while entering rooms.
[GlobalClass]
public partial class Dungeon : Node
{
    // Screen id that routes a transition into plan-driven dungeon traversal.
    public static readonly StringName RuntimeScreenId = "dungeon_runtime";

    // Sentinel screen id used by dungeon return/abandonment doors. World resolves it to the
    // captured launch-origin room (with an entrance-hall fallback) and finalizes the run as
    // GaveUp.
    public static readonly StringName ReturnScreenId = "dungeon_return";

    // Sentinel screen id used only by the terminal Boss room's post-victory exit. It returns the
    // player the same captured-origin way as ReturnScreenId, but makes completion structurally
    // distinct so the run finalizes as Completed without inspecting boss rank or assuming any
    // boss is terminal.
    public static readonly StringName CompletionScreenId = "dungeon_complete";

    // Newest-first in-memory finalized-run history is trimmed to this many records.
    private const int MaxHistoryRecords = 100;

    private static readonly StringName SouthReturnExitId = "south_return";
    private static readonly StringName BossReturnExitId = "north_center";

    [Signal]
    public delegate void RunStateChangedEventHandler();

    // The only generation source: the plan is produced from this resource. The legacy
    // live-random exports were removed in favor of plan-driven traversal.
    [Export]
    public DungeonGenerationRules GenerationRules { get; set; }

    private readonly DungeonRunPlanGenerator _generator = new();
    private readonly RandomNumberGenerator _seedRng = new();

    // Plan rooms instantiated lazily on first entry and retained by stable node id for the
    // lifetime of the run, so re-entering a node returns its existing instance.
    private readonly Dictionary<StringName, Room> _roomsByNodeId = new();

    private DungeonRunPlan _activePlan;
    private StringName _activeNodeId;
    private ulong _runSeed;

    // Authoritative live statistics for the active run. Mutated only here; null when no run is
    // active. Rooms are counted at most once each through this set of cleared node ids.
    private DungeonRunStats _activeStats;
    private readonly HashSet<StringName> _clearedNodeIds = new();

    // Spawn points in managed rooms whose TrackedActorDied signal this Dungeon is observing for
    // hostile-death counting; disconnected when the run is cleared.
    private readonly List<ActorSpawnPoint> _deathTrackedSpawnPoints = new();

    // Newest-first finalized-run records, in memory only for this slice. Survives subsequent runs
    // while this Dungeon node lives, but is not persisted (lost on scene reload).
    private readonly List<DungeonRunRecord> _history = new();

    // Read-only run state, suitable for a future Dungeon HUB / debug display.
    public bool HasActiveRun => _activePlan != null;
    public DungeonRunPlan ActivePlan => _activePlan;
    public StringName ActiveNodeId => _activeNodeId;
    public ulong RunSeed => _runSeed;
    public DungeonRoomNode ActiveNode => _activePlan?.GetNodeById(_activeNodeId);

    // Read-only live statistics for the active run (null when no run is active).
    public DungeonRunStats ActiveStats => _activeStats;

    // Read-only newest-first finalized history, for the later persistence/UI slices.
    public IReadOnlyList<DungeonRunRecord> History => _history;

    // Replaces the in-memory finalized history with loaded records (e.g. from a save), preserving
    // newest-first order and the cap without exposing a mutable collection. Replacement, never
    // append, so loading cannot duplicate existing records. Deliberately leaves the active run and
    // its live stats untouched, matching the save system's partial world-state load semantics.
    public void ReplaceHistory(IEnumerable<DungeonRunRecord> records)
    {
        _history.Clear();

        if (records != null)
        {
            foreach (var record in records)
            {
                if (record == null)
                    continue;

                if (_history.Count >= MaxHistoryRecords)
                    break;

                _history.Add(record);
            }
        }

        EmitSignal(SignalName.RunStateChanged);
    }

    public override void _Ready()
    {
        // Seed source for run seeds only; plan decisions use the per-run seed, never Randomize.
        _seedRng.Randomize();
    }

    public override void _ExitTree()
    {
        EndRun();
    }

    public bool TryCreateRoom(StringName screenId, Room currentRoom, RoomTransition sourceTransition, StringName entryExitId, out Room room)
    {
        room = null;
        if (screenId != RuntimeScreenId)
            return false;

        // The first transition into the dungeon from outside starts a fresh, randomly seeded
        // run. A generation failure refuses the transition and leaves no partial active run.
        if (_activePlan == null && !TryStartRun(NextRunSeed(), null, null, out var startError))
        {
            GD.PushError(startError);
            return false;
        }

        if (!TryResolveTargetNode(sourceTransition, out var targetNode, out var resolveError))
        {
            GD.PushError(resolveError);
            return false;
        }

        if (!TryGetOrCreatePlanRoom(targetNode, out var targetRoom))
            return false;

        var sourceNode = _activeNodeId != null ? _activePlan.GetNodeById(_activeNodeId) : null;

        // Advance only after the room exists: an invalid node/type/content never moves the run.
        _activeNodeId = targetNode.Id;
        RegisterForwardProgress(sourceNode, targetNode);
        ConfigureRoomDoors(targetRoom, targetNode);
        room = targetRoom;
        EmitSignal(SignalName.RunStateChanged);
        return true;
    }

    public void OnTransitionCompleted(Room previousRoom, RoomTransition usedTransition, Room nextRoom)
    {
        // Leaving a plan room for anything the run does not own (entrance hall via south_return
        // or the boss's post-victory north door) ends the run.
        if (HasActiveRun && IsManagedRoom(previousRoom) && !IsManagedRoom(nextRoom))
            EndRun();
    }

    // True for rooms this Dungeon instantiated for the active run and still owns. World uses
    // this to detach (rather than free) a cached room during another dungeon transition.
    public bool IsManagedRoom(Room room)
    {
        if (room == null)
            return false;

        foreach (var cached in _roomsByNodeId.Values)
        {
            if (cached == room)
                return true;
        }

        return false;
    }

    // Run-start entry point kept structured so a future Dungeon HUB can supply the seed,
    // ordinary-room count and starting level without changing traversal. Null overrides fall
    // back to the configured GenerationRules defaults.
    public bool TryStartRun(ulong seed, int? ordinaryRoomCount, int? startingRoomLevel, out string error)
    {
        error = null;
        EndRun();

        if (GenerationRules == null)
        {
            error = $"{nameof(Dungeon)} cannot start a run without a {nameof(GenerationRules)} resource.";
            return false;
        }

        var result = _generator.Generate(GenerationRules, seed, ordinaryRoomCount, startingRoomLevel);
        if (!result.Succeeded)
        {
            error = $"{nameof(Dungeon)} run-plan generation failed: {result.Error}";
            return false;
        }

        _activePlan = result.Plan;
        _runSeed = seed;
        _activeNodeId = null;
        _clearedNodeIds.Clear();

        // Starting level is the first plan node's level (StartingRoomLevel before any edge delta).
        var startingLevel = _activePlan.Length > 0 ? _activePlan.Nodes[0].Level : startingRoomLevel ?? 1;
        _activeStats = new DungeonRunStats(seed, startingLevel, _activePlan.Length);

        EmitSignal(SignalName.RunStateChanged);
        return true;
    }

    // Raw teardown: clears the active run without recording anything. Used for replacement,
    // _ExitTree and the defensive abandon path. History records are produced only by FinalizeRun.
    public void EndRun()
    {
        ClearActiveRun();
    }

    // Builds exactly one immutable record for the active run, prepends it to the newest-first
    // in-memory history (trimmed to MaxHistoryRecords), then clears the run. Idempotent: with no
    // active run it records nothing and returns null, so repeated callbacks cannot duplicate a
    // record. Boss death never calls this; completion is routed explicitly by World.
    public DungeonRunRecord FinalizeRun(DungeonRunOutcome outcome)
    {
        if (_activePlan == null || _activeStats == null)
            return null;

        var record = new DungeonRunRecord(_activeStats, outcome, DateTimeOffset.Now);
        _history.Insert(0, record);
        if (_history.Count > MaxHistoryRecords)
            _history.RemoveRange(MaxHistoryRecords, _history.Count - MaxHistoryRecords);

        ClearActiveRun();
        return record;
    }

    // Counts the active room as cleared once (used for the terminal Boss room when its completion
    // exit is traversed). Idempotent per node via the cleared-node set.
    public void MarkActiveNodeCleared()
    {
        if (MarkNodeCleared(ActiveNode))
            EmitSignal(SignalName.RunStateChanged);
    }

    // Counts a player death for the active run. Game-over/reload may discard the run right after,
    // but the death is modeled regardless. No-op without an active run.
    public void RegisterPlayerDeath()
    {
        if (_activeStats == null)
            return;

        _activeStats.IncrementPlayerDeaths();
        EmitSignal(SignalName.RunStateChanged);
    }

    private void ClearActiveRun()
    {
        var hadActiveRun = _activePlan != null;

        DisconnectDeathTracking();

        foreach (var cached in _roomsByNodeId.Values)
        {
            if (cached == null || !GodotObject.IsInstanceValid(cached))
                continue;

            // Free only instances this Dungeon still solely owns (already detached). An
            // attached room is owned by World/the tree and freed there, so this never
            // double-frees the currently attached room.
            if (cached.GetParent() != null)
                continue;

            cached.QueueFree();
        }

        _roomsByNodeId.Clear();
        _activePlan = null;
        _activeNodeId = null;
        _runSeed = 0;
        _activeStats = null;
        _clearedNodeIds.Clear();

        if (hadActiveRun)
            EmitSignal(SignalName.RunStateChanged);
    }

    // On a forward transition (advancing to a higher-index node) counts the source room as cleared
    // once. Return/abandonment exits never reach here, so they never clear the current room. Also
    // records the furthest room reached for the live stats.
    private void RegisterForwardProgress(DungeonRoomNode sourceNode, DungeonRoomNode targetNode)
    {
        if (sourceNode != null && targetNode != null && targetNode.Index > sourceNode.Index)
            MarkNodeCleared(sourceNode);

        if (targetNode != null)
            _activeStats?.RecordRoomReached(targetNode.Index + 1, targetNode.Level);
    }

    // Adds a node to the cleared set and increments RoomsCleared the first time. Returns true only
    // when the node was newly counted, so callers can decide whether to emit a state change.
    private bool MarkNodeCleared(DungeonRoomNode node)
    {
        if (_activeStats == null || node?.Id == null || node.Id.IsEmpty)
            return false;

        if (!_clearedNodeIds.Add(node.Id))
            return false;

        _activeStats.IncrementRoomsCleared();
        return true;
    }

    // Subscribes to every ActorSpawnPoint in a freshly instantiated managed room, including boss
    // and summon spawners that spawn their actors later, so hostile deaths are counted from the
    // authoritative spawn lifecycle rather than per-frame tree scans.
    private void TrackRoomDeaths(Node root)
    {
        var callable = new Callable(this, nameof(OnManagedActorDied));
        foreach (var spawnPoint in FindSpawnPoints(root))
        {
            if (spawnPoint.IsConnected(ActorSpawnPoint.SignalName.TrackedActorDied, callable))
                continue;

            spawnPoint.Connect(ActorSpawnPoint.SignalName.TrackedActorDied, callable);
            _deathTrackedSpawnPoints.Add(spawnPoint);
        }
    }

    private void DisconnectDeathTracking()
    {
        var callable = new Callable(this, nameof(OnManagedActorDied));
        foreach (var spawnPoint in _deathTrackedSpawnPoints)
        {
            if (spawnPoint != null &&
                GodotObject.IsInstanceValid(spawnPoint) &&
                spawnPoint.IsConnected(ActorSpawnPoint.SignalName.TrackedActorDied, callable))
            {
                spawnPoint.Disconnect(ActorSpawnPoint.SignalName.TrackedActorDied, callable);
            }
        }

        _deathTrackedSpawnPoints.Clear();
    }

    // Counts a hostile actor death in a managed room. Never counts the player or friendly/allied/
    // neutral actors (e.g. shop NPCs). A boss counts toward both EnemiesKilled and BossesDefeated;
    // rank is used only for the counter, never to infer completion.
    private void OnManagedActorDied(CombatCharacter actor)
    {
        if (_activeStats == null || actor == null || !GodotObject.IsInstanceValid(actor) || actor is Player)
            return;

        var faction = actor.Faction;
        if (faction == null || !faction.IsHostileTo(Factions.Allies))
            return;

        _activeStats.IncrementEnemiesKilled();

        if (actor is Actor rankedActor && rankedActor.Rank == ActorRank.Boss)
            _activeStats.IncrementBossesDefeated();

        EmitSignal(SignalName.RunStateChanged);
    }

    private static IEnumerable<ActorSpawnPoint> FindSpawnPoints(Node root)
    {
        if (root == null)
            yield break;

        foreach (var child in root.GetChildren())
        {
            if (child is ActorSpawnPoint spawnPoint)
                yield return spawnPoint;

            foreach (var nested in FindSpawnPoints(child))
                yield return nested;
        }
    }

    private bool TryResolveTargetNode(RoomTransition sourceTransition, out DungeonRoomNode targetNode, out string error)
    {
        targetNode = null;
        error = null;

        // First room of the run is always plan node 0.
        if (_activeNodeId == null)
        {
            if (_activePlan.Length == 0)
            {
                error = $"{nameof(Dungeon)} run plan has no nodes.";
                return false;
            }

            targetNode = _activePlan.Nodes[0];
            return true;
        }

        var activeNode = _activePlan.GetNodeById(_activeNodeId);
        if (activeNode == null)
        {
            error = $"{nameof(Dungeon)} active node '{_activeNodeId}' is not present in the run plan.";
            return false;
        }

        // Select the matching edge by the used door's exit id, then resolve the stable
        // destination node id through the plan. Combat's two doors resolve independently.
        var exitId = sourceTransition?.ExitId;
        targetNode = DungeonTraversal.ResolveDestination(_activePlan, activeNode, exitId, out _);
        if (targetNode == null)
        {
            error = $"{nameof(Dungeon)} could not resolve a destination from node '{activeNode.Id}' via exit '{exitId}'.";
            return false;
        }

        return true;
    }

    private bool TryGetOrCreatePlanRoom(DungeonRoomNode node, out Room room)
    {
        room = null;

        if (_roomsByNodeId.TryGetValue(node.Id, out var cached))
        {
            if (GodotObject.IsInstanceValid(cached))
            {
                room = cached;
                return true;
            }

            _roomsByNodeId.Remove(node.Id);
        }

        if (!TryInstantiatePlanRoom(node, out var created))
            return false;

        _roomsByNodeId[node.Id] = created;
        room = created;
        return true;
    }

    private bool TryInstantiatePlanRoom(DungeonRoomNode node, out Room room)
    {
        room = null;

        if (node.Definition?.RoomScene == null)
        {
            GD.PushError($"{nameof(Dungeon)} node '{node.Id}' has no room scene to instantiate.");
            return false;
        }

        var instance = node.Definition.RoomScene.Instantiate();
        if (instance is not Room instantiatedRoom)
        {
            GD.PushError($"{nameof(Dungeon)} node '{node.Id}' room scene did not instantiate a {nameof(Room)} root.");
            instance?.QueueFree();
            return false;
        }

        if (!RoomTypeMatchesKind(instantiatedRoom, node.Kind))
        {
            GD.PushError($"{nameof(Dungeon)} node '{node.Id}' instantiated '{instantiatedRoom.GetType().Name}', which does not match kind '{node.Kind}'.");
            instantiatedRoom.QueueFree();
            return false;
        }

        // Level and content are fixed by the plan and applied before the room enters the tree
        // so content spawns at the planned level. No randomness is consumed here.
        instantiatedRoom.Level = node.Level;
        if (!instantiatedRoom.TryInjectContent(node.ContentOption?.ContentScene))
        {
            GD.PushError($"{nameof(Dungeon)} node '{node.Id}' failed to inject its preselected content.");
            instantiatedRoom.QueueFree();
            return false;
        }

        // Observe spawn-point deaths in this managed room (including summon/boss spawners that
        // spawn later) so hostile kills accrue to the run's live statistics.
        TrackRoomDeaths(instantiatedRoom);

        room = instantiatedRoom;
        return true;
    }

    private void ConfigureRoomDoors(Room room, DungeonRoomNode node)
    {
        // Progression doors come straight from the node's edges: each edge's door routes back
        // into the Dungeon, which resolves the actual destination node when the door is used.
        foreach (var edge in node.Edges)
        {
            if (edge != null && HasValue(edge.SourceExitId))
                ConfigureDoorTarget(room, edge.SourceExitId, RuntimeScreenId, SouthReturnExitId);
        }

        // Return/abandonment doors route to the dungeon-return sentinel so World restores the
        // captured launch origin (and exact player position) and then finalizes the run:
        //  - Combat/Special abandon through their south_return door (GaveUp).
        //  - The Boss room's post-victory north_center door instead targets the completion
        //    sentinel so the same captured-origin return finalizes the run as Completed. Reaching
        //    completion is structural (this exit), never inferred from the boss dying.
        // Timed rooms have no abandonment door, so they are intentionally left untouched.
        if (room is CombatDungeonRoom || room is SpecialDungeonRoom)
            ConfigureDoorTarget(room, SouthReturnExitId, ReturnScreenId, default);
        else if (room is BossRoom)
            ConfigureDoorTarget(room, BossReturnExitId, CompletionScreenId, default);
    }

    private static void ConfigureDoorTarget(Room room, StringName exitId, StringName targetScreenId, StringName targetExitId)
    {
        var door = room.GetDoor(exitId);
        if (door == null)
            return;

        door.TargetScreenId = targetScreenId;
        door.TargetExitId = targetExitId;
    }

    private static bool RoomTypeMatchesKind(Room room, DungeonRoomKind kind)
    {
        return kind switch
        {
            DungeonRoomKind.Combat => room is CombatDungeonRoom,
            DungeonRoomKind.Timed => room is TimedDungeonRoom,
            DungeonRoomKind.Special => room is SpecialDungeonRoom,
            DungeonRoomKind.Boss => room is BossRoom,
            _ => false,
        };
    }

    private ulong NextRunSeed()
    {
        // Combine two 32-bit draws into a full 64-bit run seed.
        var high = (ulong)_seedRng.Randi();
        var low = (ulong)_seedRng.Randi();
        return (high << 32) | low;
    }

    private static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }
}
