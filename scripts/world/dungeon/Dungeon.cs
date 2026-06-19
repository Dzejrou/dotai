using Godot;

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
    // captured launch-origin room (with an entrance-hall fallback) and ends the run.
    public static readonly StringName ReturnScreenId = "dungeon_return";

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

    // Read-only run state, suitable for a future Dungeon HUB / debug display.
    public bool HasActiveRun => _activePlan != null;
    public DungeonRunPlan ActivePlan => _activePlan;
    public StringName ActiveNodeId => _activeNodeId;
    public ulong RunSeed => _runSeed;
    public DungeonRoomNode ActiveNode => _activePlan?.GetNodeById(_activeNodeId);

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

        // Advance only after the room exists: an invalid node/type/content never moves the run.
        _activeNodeId = targetNode.Id;
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
        EmitSignal(SignalName.RunStateChanged);
        return true;
    }

    public void EndRun()
    {
        var hadActiveRun = _activePlan != null;

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

        if (hadActiveRun)
            EmitSignal(SignalName.RunStateChanged);
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
        // captured launch origin (and exact player position) and then ends the run:
        //  - Combat/Special abandon through their south_return door.
        //  - The Boss room's post-victory north_center door returns the same way instead of
        //    always going back to the entrance hall.
        // Timed rooms have no abandonment door, so they are intentionally left untouched.
        if (room is CombatDungeonRoom || room is SpecialDungeonRoom)
            ConfigureDoorTarget(room, SouthReturnExitId, ReturnScreenId, default);
        else if (room is BossRoom)
            ConfigureDoorTarget(room, BossReturnExitId, ReturnScreenId, default);
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
