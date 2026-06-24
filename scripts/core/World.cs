using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class World : Node2D
{
    [Export]
    public NodePath PlayerPath { get; set; } = new NodePath("Player");

    [Export]
    public NodePath RoomContainerPath { get; set; } = new NodePath("RoomContainer");

    [Export]
    public NodePath CorpseManagerPath { get; set; } = new NodePath("CorpseManager");

    [Export]
    public NodePath InventoryPath { get; set; } = new NodePath("Inventory");

    [Export]
    public NodePath DungeonPath { get; set; } = new NodePath("Dungeon");

    [Export]
    public RoomRegistry RoomRegistry { get; set; }

    [Export]
    public GlobalGearLootRules GlobalGearLootRules { get; set; }

    [Export]
    public GearGenerationRules GearGenerationRules { get; set; }

    [Export]
    public ActorLevelScalingRules ActorLevelScalingRules { get; set; }

    [Export]
    public ActorExperienceRewardRules ActorExperienceRewardRules { get; set; }

    [Export]
    public PackedScene GearDropScene { get; set; }

    [Export]
    public StringName InitialScreenId { get; set; } = "entrance_hall";

    [Export]
    public StringName InitialExitId { get; set; } = default;

    [Export]
    public bool UsePersistentRoomCache { get; set; } = true;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float TransitionCooldownSeconds { get; set; } = 0.2f;

    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void MerchantInteractionRequestedEventHandler(MerchantStock stock, Player player);

    [Signal]
    public delegate void DungeonEntranceInteractionRequestedEventHandler(Player player);

    // Raised once when an active run is finalized as Completed through the boss-room completion exit,
    // after the player has returned to the game world and the run has been recorded and its DP
    // awarded. Main reacts by opening the Dungeon page directly to the end-of-run summary. A GaveUp
    // finalization never raises it. Parameterless because the finalized DungeonRunRecord is a plain
    // C# object (not Variant-marshalable), so listeners read it from LastCompletedRunRecord.
    [Signal]
    public delegate void DungeonRunCompletedEventHandler();

    // Raised once when a hardcore dungeon run is finalized as Failed by a player death, after the
    // player has been returned outside the dungeon, revived, and the run recorded (awarding no DP).
    // Main reacts by opening the Dungeon page's end-of-run summary, reusing the completion summary.
    // Parameterless for the same reason as DungeonRunCompleted: the record is read from
    // LastFailedRunRecord.
    [Signal]
    public delegate void DungeonRunFailedEventHandler();

    // Raised once when a player dies during a non-hardcore (softcore) dungeon run. The run is left
    // active and unfinalized and the player body is kept alive (downed); Main reacts by opening the
    // Dungeon page's death/retry view so the player can Continue (retry the room) or Give Up.
    [Signal]
    public delegate void DungeonSoftcoreDeathEventHandler();

    public void RequestMerchantInteraction(MerchantStock stock, Player player)
    {
        if (stock == null || !GodotObject.IsInstanceValid(stock))
            return;
        if (player == null || !GodotObject.IsInstanceValid(player))
            return;

        EmitSignal(SignalName.MerchantInteractionRequested, stock, player);
    }

    // Interaction-only entry point: the entrance-hall dungeon entrance dispatches here when the
    // player interacts with it. World simply relays the request; Main opens the Dungeon HUB page
    // and grants entrance authorization. No run is started until the player presses Start.
    public void RequestDungeonEntranceInteraction(Player player)
    {
        if (player == null || !GodotObject.IsInstanceValid(player))
            return;

        EmitSignal(SignalName.DungeonEntranceInteractionRequested, player);
    }

    private Player _player;
    private Camera2D _playerCamera;
    private Node _roomContainer;
    private CorpseManager _corpseManager;
    private InventoryController _inventoryController;
    private Dungeon _dungeon;
    private Room _activeRoom;
    private CountdownHUD _countdownHud;
    private bool _isGameOver;
    private float _transitionCooldownRemaining;
    private readonly Dictionary<StringName, Room> _persistentRoomsById = new();

    private Room _debugRoom;
    private bool _debugKeepInstance;
    private string _debugRoomLabel;
    private Room _retainedDebugRoom;
    private string _retainedDebugRoomLabel;
    private RoomReturnLocation _debugReturnLocation;

    // Memory-only origin captured when a dungeon run is launched from the HUB, so abandoning or
    // completing the run restores the exact room/position the player started from.
    private RoomReturnLocation _dungeonReturnLocation;

    // The most recently completed run's finalized record, captured at the boss-room completion exit so
    // the Dungeon page can present its end-of-run summary. Replaced on each completion; a GaveUp
    // finalization never sets it. It is the same immutable record now stored newest-first in history.
    private DungeonRunRecord _lastCompletedRunRecord;

    // The most recently failed run's finalized record, captured when a hardcore death finalizes a run
    // as Failed so the Dungeon page can present its end-of-run summary. Replaced on each failure; a
    // Completed or GaveUp finalization never sets it. It is the same immutable record now stored
    // newest-first in history.
    private DungeonRunRecord _lastFailedRunRecord;

    public Room ActiveRoom => GodotObject.IsInstanceValid(_activeRoom) ? _activeRoom : null;

    // Read-only access for the Dungeon HUB page (run state, current node, generation defaults).
    public Dungeon Dungeon => _dungeon != null && GodotObject.IsInstanceValid(_dungeon) ? _dungeon : null;

    public bool HasActiveDungeonRun => Dungeon?.HasActiveRun == true;

    // The finalized record of the run that most recently completed through the boss-room exit, for the
    // Dungeon page's end-of-run summary. Null until a run has completed in this session.
    public DungeonRunRecord LastCompletedRunRecord => _lastCompletedRunRecord;

    // The finalized record of the run that most recently failed through a hardcore death, for the
    // Dungeon page's end-of-run summary. Null until a run has failed in this session.
    public DungeonRunRecord LastFailedRunRecord => _lastFailedRunRecord;

    public bool IsDebugRoomSessionActive =>
        _debugRoom != null && GodotObject.IsInstanceValid(_debugRoom) && _activeRoom == _debugRoom;

    public bool HasRetainedDebugRoom =>
        _retainedDebugRoom != null && GodotObject.IsInstanceValid(_retainedDebugRoom);

    public string RetainedDebugRoomLabel => HasRetainedDebugRoom ? _retainedDebugRoomLabel : null;

    public override void _Ready()
    {
        _roomContainer = GetNodeOrNull<Node>(RoomContainerPath);
        if (_roomContainer == null)
            GD.PushError($"{nameof(World)} could not resolve room container at '{RoomContainerPath}'.");
        if (RoomRegistry == null)
            GD.PushError($"{nameof(World)} is missing a room registry resource.");

        _dungeon = GetNodeOrNull<Dungeon>(DungeonPath);
        _corpseManager = ResolveCorpseManager();
        _inventoryController = ResolveInventoryController();
        _player = GetNodeOrNull<Player>(PlayerPath);
        _playerCamera = _player?.GetNodeOrNull<Camera2D>("Camera2D");

        if (_player != null)
            _player.Connect(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));

        LoadInitialRoom();
    }

    public override void _Process(double delta)
    {
        if (_transitionCooldownRemaining <= 0.0f)
            return;

        _transitionCooldownRemaining = Math.Max(0.0f, _transitionCooldownRemaining - (float)delta);
    }

    public override void _ExitTree()
    {
        DisconnectActiveRoom();
        FreeDetachedCachedRooms();
        _persistentRoomsById.Clear();
        ReleaseDebugRoomStateOnTeardown();

        if (GodotObject.IsInstanceValid(_player) &&
            _player.IsConnected(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied))))
        {
            _player.Disconnect(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
        }
    }

    public void RegisterCorpse(Corpse corpse)
    {
        ResolveCorpseManager()?.Register(corpse);
    }

    public InventoryController ResolveInventoryController()
    {
        if (_inventoryController != null && GodotObject.IsInstanceValid(_inventoryController))
            return _inventoryController;

        if (InventoryPath.IsEmpty)
            return null;

        _inventoryController = GetNodeOrNull<InventoryController>(InventoryPath);
        return _inventoryController;
    }

    public CountdownHUD ResolveCountdownHud()
    {
        if (_countdownHud != null && GodotObject.IsInstanceValid(_countdownHud))
            return _countdownHud;

        var current = GetParent();
        while (current != null)
        {
            if (current is Main main)
            {
                _countdownHud = main.ResolveCountdownHud();
                return _countdownHud;
            }

            current = current.GetParent();
        }

        return null;
    }

    private void LoadInitialRoom()
    {
        if (!HasValue(InitialScreenId))
        {
            GD.PushError($"{nameof(World)} is missing an initial room id.");
            return;
        }

        TransitionToRoom(InitialScreenId, InitialExitId);
    }

    private void OnPlayerDied()
    {
        if (_isGameOver)
            return;

        // Count the death for the active run before any flow may discard it.
        _dungeon?.RegisterPlayerDeath();

        // A death inside an active dungeon run never falls into the normal game-over flow. Keep the
        // player body alive (the Player consumes this one-shot suppression as it processes the same
        // PlayerDied emission) and resolve the dungeon death — hardcore fail or softcore retry — on
        // the next idle frame, once we are off the damage-application call stack.
        if (HasActiveDungeonRun)
        {
            _player?.SuppressNextDeathDespawn();
            Callable.From(ResolveDungeonRunDeath).CallDeferred();
            return;
        }

        _isGameOver = true;
        EmitSignal(SignalName.PlayerDied);
    }

    // Resolves a player death that happened inside an active dungeon run, deferred one frame off the
    // damage call stack. Hardcore finalizes the run as Failed and returns the player outside; softcore
    // surfaces the death/retry view, leaving the run active and the player downed. A run torn down
    // between the death and this call (e.g. scene exit) is a safe no-op.
    private void ResolveDungeonRunDeath()
    {
        if (_dungeon == null || !GodotObject.IsInstanceValid(_dungeon) || !_dungeon.HasActiveRun)
            return;

        if (_dungeon.ActiveRunIsHardcore)
            ResolveHardcoreDungeonDeath();
        else
            EmitSignal(SignalName.DungeonSoftcoreDeath);
    }

    // Hardcore death: finalize the run as Failed through the captured-origin return (records history,
    // awards no DP, surfaces the summary via DungeonRunFailed), then revive the kept-alive body
    // outside the dungeon. On a failed return the run stays active and the body stays downed.
    private void ResolveHardcoreDungeonDeath()
    {
        if (!TryFinishDungeonRun(DungeonRunOutcome.Failed))
            return;

        ReviveDungeonPlayer();
    }

    private CorpseManager ResolveCorpseManager()
    {
        if (_corpseManager != null || CorpseManagerPath.IsEmpty)
            return _corpseManager;

        _corpseManager = GetNodeOrNull<CorpseManager>(CorpseManagerPath);
        return _corpseManager;
    }

    private void OnTransitionTriggered(RoomTransition transition)
    {
        if (_transitionCooldownRemaining > 0.0f || transition == null)
            return;

        if (transition is Door door && door.IsLocked)
            return;

        if (!HasValue(transition.TargetScreenId))
        {
            GD.PushWarning($"{transition.Name} does not define a target screen id.");
            return;
        }

        // Dungeon return/abandonment doors target a sentinel screen id rather than a concrete
        // room: resolve it to the captured launch origin, restore position and finalize the run.
        // The boss completion exit uses a distinct sentinel so it finalizes as Completed.
        if (transition.TargetScreenId == global::Dungeon.ReturnScreenId)
        {
            if (TryFinishDungeonRun(DungeonRunOutcome.GaveUp))
                _transitionCooldownRemaining = TransitionCooldownSeconds;

            return;
        }

        if (transition.TargetScreenId == global::Dungeon.CompletionScreenId)
        {
            if (TryFinishDungeonRun(DungeonRunOutcome.Completed))
                _transitionCooldownRemaining = TransitionCooldownSeconds;

            return;
        }

        var previousRoom = _activeRoom;
        if (!TransitionToRoom(transition.TargetScreenId, transition.TargetExitId, transition))
            return;

        _dungeon?.OnTransitionCompleted(previousRoom, transition, _activeRoom);
        _transitionCooldownRemaining = TransitionCooldownSeconds;
    }

    private bool TransitionToRoom(StringName screenId, StringName entryExitId, RoomTransition sourceTransition = null)
    {
        var nextRoom = InstantiateRoom(screenId, entryExitId, sourceTransition);
        if (nextRoom == null)
            return false;

        SwapActiveRoom(nextRoom, entryExitId);
        return true;
    }

    // Tears down the current active room and installs an already-instantiated next room: disconnect,
    // exit and detach/free the old one, then attach the new one, place the player at its entry, apply
    // camera bounds and run OnEnter. Shared by ordinary transitions and the softcore retry rebuild,
    // which supplies a room directly rather than resolving one by screen id.
    private void SwapActiveRoom(Room nextRoom, StringName entryExitId)
    {
        var leavingDebugSession = IsDebugRoomSessionActive;

        DisconnectActiveRoom();
        CallActiveRoomExit();
        DetachOrFreeActiveRoom();

        // Any ordinary transition out of a debug room ends the debug session.
        if (leavingDebugSession)
            _debugReturnLocation = null;

        _activeRoom = nextRoom;
        AttachActiveRoom();
        _activeRoom.TransitionTriggered += OnTransitionTriggered;

        PlacePlayerAtRoomEntry(_activeRoom, entryExitId);
        ApplyRoomCameraBounds(_activeRoom);
        _activeRoom.OnEnter();
    }

    // Launches a plan-driven dungeon run from the HUB. Captures the current room/position as the
    // return origin, starts the run on the requested seed/overrides, then enters plan node 0 via
    // the normal dungeon_runtime transition. The caller only closes the HUB once this returns
    // true; on failure nothing moves and no return origin is retained.
    public bool TryStartDungeonRun(ulong seed, int ordinaryRoomCount, DungeonDifficultySelection difficulty, out string error)
    {
        error = null;

        if (_dungeon == null || !GodotObject.IsInstanceValid(_dungeon))
        {
            error = "Dungeon runtime is unavailable.";
            return false;
        }

        // Capture the origin before any state changes so a failed launch leaves it untouched.
        var returnLocation = CaptureDungeonReturnLocation();

        if (!_dungeon.TryStartRun(seed, ordinaryRoomCount, difficulty, out error))
            return false;

        // Enter plan node 0 through the standard dungeon transition. TryCreateRoom reuses the run
        // just started (it only auto-generates when no plan is active), so the selected settings
        // are honored exactly and no second random plan is created.
        if (!TransitionToRoom(global::Dungeon.RuntimeScreenId, default))
        {
            // Discard the half-started run so a later stray transition cannot reuse a partial
            // plan or silently generate a replacement.
            _dungeon.EndRun();
            error = "Dungeon run generated but its first room could not be entered.";
            return false;
        }

        _dungeonReturnLocation = returnLocation;
        return true;
    }

    // Gives up an active run from the HUB. Uses the same captured-origin return flow as a
    // successful boss exit (no encounter completion or rewards), finalizing as GaveUp. On failure
    // the active run and return origin are preserved so the caller can keep the HUB open and
    // surface the error.
    public bool TryGiveUpDungeonRun(out string error)
    {
        error = null;

        if (_dungeon == null || !GodotObject.IsInstanceValid(_dungeon) || !_dungeon.HasActiveRun)
        {
            error = "There is no active dungeon run to give up.";
            return false;
        }

        if (!TryFinishDungeonRun(DungeonRunOutcome.GaveUp))
        {
            error = "Could not return from the dungeon; the run is still active.";
            return false;
        }

        return true;
    }

    // Resolves a dungeon completion/return/abandonment exit: transitions to the captured launch
    // origin (entrance-hall fallback), restores the exact launch position, then finalizes the run
    // with the supplied outcome. Order is deliberate: a failed transition returns false and leaves
    // the run active and unfinalized; finalization (which records and clears the run) happens only
    // after the return succeeds.
    private bool TryFinishDungeonRun(DungeonRunOutcome outcome)
    {
        var returnLocation = _dungeonReturnLocation;
        var screenId = ResolveDungeonReturnScreenId(returnLocation);

        if (!TransitionToRoom(screenId, default))
            return false;

        // Restore the exact captured position only when returning to the captured origin; an
        // entrance-hall fallback uses that room's normal spawn instead.
        if (returnLocation?.PlayerPosition is Vector2 playerPosition &&
            screenId == returnLocation.ScreenId &&
            _player != null &&
            GodotObject.IsInstanceValid(_player))
        {
            _player.GlobalPosition = playerPosition;
            _player.Velocity = Vector2.Zero;
        }

        // A successful completion exit clears the terminal Boss room before the run is recorded.
        if (outcome == DungeonRunOutcome.Completed)
            _dungeon?.MarkActiveNodeCleared();

        // Record exactly one finalized run and clear it. FinalizeRun is idempotent, so a stray
        // repeat (e.g. cooldown race) cannot append a duplicate.
        var record = _dungeon?.FinalizeRun(outcome);
        _dungeonReturnLocation = null;

        // A completed or failed run surfaces its end-of-run summary: capture the finalized record and
        // notify Main, which opens the Dungeon page to it. DP was already awarded inside FinalizeRun
        // (zero for a failed run), so this is purely presentational and never re-awards. FinalizeRun
        // returns null for a stray repeat (no active run), so the summary opens exactly once. GaveUp
        // returns without notifying.
        if (record != null && outcome == DungeonRunOutcome.Completed)
        {
            _lastCompletedRunRecord = record;
            EmitSignal(SignalName.DungeonRunCompleted);
        }
        else if (record != null && outcome == DungeonRunOutcome.Failed)
        {
            _lastFailedRunRecord = record;
            EmitSignal(SignalName.DungeonRunFailed);
        }

        return true;
    }

    // Softcore death/retry: rebuilds the active node's room from scratch through the planned-room
    // path and drops the revived player at its spawn, keeping the run active. Preserves the plan,
    // active node, seed, difficulty, score and counters (the death was already counted); only the
    // failed room is reset. On failure the run and player are left as they were so the caller can keep
    // the death/retry view open and surface the error.
    public bool TryContinueDungeonRunAfterDeath(out string error)
    {
        error = null;

        if (_dungeon == null || !GodotObject.IsInstanceValid(_dungeon) || !_dungeon.HasActiveRun)
        {
            error = "There is no active dungeon run to continue.";
            return false;
        }

        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            error = "The player is unavailable.";
            return false;
        }

        // Rebuild the whole room (fresh enemies/chests/timers/doors/encounter state), not just its
        // content, so the room must be cleared again.
        if (!_dungeon.TryRecreateActiveRoom(out var freshRoom) || freshRoom == null)
        {
            error = "Could not rebuild the dungeon room for a retry.";
            return false;
        }

        // Swap in the fresh instance (the old active room is the discarded one, now unmanaged, so the
        // swap frees it) and place the player at the room's spawn marker.
        SwapActiveRoom(freshRoom, default);

        // Revive into the fresh room at full health and mana for the retry.
        ReviveDungeonPlayer();
        return true;
    }

    // Softcore death give-up: the same captured-origin return and GaveUp finalization as a HUB
    // give-up (records history, awards 0 DP), but the body was kept alive by the death flow, so it is
    // revived outside the dungeon afterward. On a failed return the run stays active and the body
    // stays downed so the caller can keep the death/retry view open.
    public bool TryGiveUpDungeonRunAfterDeath(out string error)
    {
        error = null;

        if (_dungeon == null || !GodotObject.IsInstanceValid(_dungeon) || !_dungeon.HasActiveRun)
        {
            error = "There is no active dungeon run to give up.";
            return false;
        }

        if (!TryFinishDungeonRun(DungeonRunOutcome.GaveUp))
        {
            error = "Could not return from the dungeon; the run is still active.";
            return false;
        }

        ReviveDungeonPlayer();
        return true;
    }

    private void ReviveDungeonPlayer()
    {
        if (_player != null && GodotObject.IsInstanceValid(_player))
            _player.ReviveToFull();
    }

    private RoomReturnLocation CaptureDungeonReturnLocation()
    {
        var screenId = _activeRoom != null && GodotObject.IsInstanceValid(_activeRoom)
            ? _activeRoom.ScreenId
            : default;

        // Origins the registry cannot rebuild (e.g. a transient generated room) fall back to the
        // entrance hall with no stored position rather than trapping the player on return.
        if (!HasValue(screenId) || RoomRegistry?.TryGetRoomScene(screenId, out _) != true)
            return new RoomReturnLocation(InitialScreenId, null);

        var playerPosition = _player != null && GodotObject.IsInstanceValid(_player)
            ? _player.GlobalPosition
            : (Vector2?)null;
        return new RoomReturnLocation(screenId, playerPosition);
    }

    private StringName ResolveDungeonReturnScreenId(RoomReturnLocation returnLocation)
    {
        var screenId = returnLocation?.ScreenId;
        if (HasValue(screenId) && RoomRegistry?.TryGetRoomScene(screenId, out _) == true)
            return screenId;

        return InitialScreenId;
    }

    public bool TryEnterDebugRoom(RoomTemplateDefinition definition, RoomContentOption contentOption, bool useExternalContent, bool keepInstance, int level)
    {
        if (definition?.RoomScene == null)
        {
            GD.PushWarning($"{nameof(World)} cannot enter a debug room without a room scene.");
            return false;
        }

        var roomInstance = definition.RoomScene.Instantiate();
        if (roomInstance is not Room room)
        {
            GD.PushError($"{nameof(World)} debug room definition '{definition.GetLabel()}' did not instantiate a {nameof(Room)} root.");
            roomInstance?.QueueFree();
            return false;
        }

        // Apply the requested level before the room enters the tree (and before content
        // spawns) so room-level actor rolls use it. The Room setter clamps to >= 1. A
        // retained instance keeps the level it was created with; re-entry never reapplies.
        room.Level = level;

        // Inject before the room enters the tree so _Ready sees the content.
        // A null content option is an intentional Empty selection.
        if (useExternalContent && !room.TryInjectContent(contentOption?.ContentScene))
        {
            room.QueueFree();
            return false;
        }

        return EnterDebugRoomInstance(room, ComposeDebugRoomLabel(definition, contentOption, useExternalContent), keepInstance);
    }

    public bool TryReenterRetainedDebugRoom()
    {
        if (!HasRetainedDebugRoom)
        {
            _retainedDebugRoom = null;
            _retainedDebugRoomLabel = null;
            return false;
        }

        if (_activeRoom == _retainedDebugRoom)
            return true;

        return EnterDebugRoomInstance(_retainedDebugRoom, _retainedDebugRoomLabel, keepInstance: true);
    }

    public bool TryReturnFromDebugRoom()
    {
        if (!IsDebugRoomSessionActive || _debugReturnLocation == null)
            return false;

        var returnLocation = _debugReturnLocation;
        if (!TransitionToRoom(returnLocation.ScreenId, default))
            return false;

        _debugReturnLocation = null;
        if (returnLocation.PlayerPosition is Vector2 playerPosition &&
            _player != null &&
            GodotObject.IsInstanceValid(_player))
        {
            _player.GlobalPosition = playerPosition;
            _player.Velocity = Vector2.Zero;
        }

        return true;
    }

    public void FreeRetainedDebugRoom()
    {
        var retainedRoom = _retainedDebugRoom;
        _retainedDebugRoom = null;
        _retainedDebugRoomLabel = null;

        if (retainedRoom == null || !GodotObject.IsInstanceValid(retainedRoom))
            return;

        // The player is standing in it; just stop retaining it so it is freed
        // like a temporary instance when left.
        if (retainedRoom == _activeRoom)
        {
            _debugKeepInstance = false;
            return;
        }

        retainedRoom.QueueFree();
    }

    private bool EnterDebugRoomInstance(Room room, string label, bool keepInstance)
    {
        if (room == null || !GodotObject.IsInstanceValid(room))
            return false;

        CaptureDebugReturnLocation();

        DisconnectActiveRoom();
        CallActiveRoomExit();
        DetachOrFreeActiveRoom();

        _activeRoom = room;
        _debugRoom = room;
        _debugKeepInstance = keepInstance;
        _debugRoomLabel = label;

        AttachActiveRoom();
        _activeRoom.TransitionTriggered += OnTransitionTriggered;

        PlacePlayerAtRoomEntry(_activeRoom, default);
        ApplyRoomCameraBounds(_activeRoom);
        _activeRoom.OnEnter();
        return true;
    }

    private void CaptureDebugReturnLocation()
    {
        // Entering a debug room from within a debug session keeps the original
        // launch location so Return always restores the true origin.
        if (_debugReturnLocation != null)
            return;

        var screenId = _activeRoom != null && GodotObject.IsInstanceValid(_activeRoom)
            ? _activeRoom.ScreenId
            : default;

        // Origins the registry cannot rebuild (e.g. generated dungeon rooms)
        // fall back to the initial room instead of leaving Return broken.
        if (!HasValue(screenId) || RoomRegistry?.TryGetRoomScene(screenId, out _) != true)
        {
            _debugReturnLocation = new RoomReturnLocation(InitialScreenId, null);
            return;
        }

        var playerPosition = _player != null && GodotObject.IsInstanceValid(_player)
            ? _player.GlobalPosition
            : (Vector2?)null;
        _debugReturnLocation = new RoomReturnLocation(screenId, playerPosition);
    }

    private void ReleaseActiveDebugRoom()
    {
        var room = _debugRoom;
        _debugRoom = null;

        if (room == null || !GodotObject.IsInstanceValid(room))
            return;

        var parent = room.GetParent();
        parent?.RemoveChild(room);

        if (_debugKeepInstance)
        {
            if (_retainedDebugRoom != room)
            {
                FreeRetainedDebugRoom();
                _retainedDebugRoom = room;
            }

            _retainedDebugRoomLabel = _debugRoomLabel;
        }
        else if (room != _retainedDebugRoom)
        {
            room.QueueFree();
        }

        _debugRoomLabel = null;
    }

    private void ReleaseDebugRoomStateOnTeardown()
    {
        if (_retainedDebugRoom != null &&
            GodotObject.IsInstanceValid(_retainedDebugRoom) &&
            _retainedDebugRoom.GetParent() == null)
        {
            _retainedDebugRoom.QueueFree();
        }

        _retainedDebugRoom = null;
        _retainedDebugRoomLabel = null;
        _debugRoom = null;
        _debugRoomLabel = null;
        _debugReturnLocation = null;
    }

    private static string ComposeDebugRoomLabel(RoomTemplateDefinition definition, RoomContentOption contentOption, bool useExternalContent)
    {
        var roomName = !string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.DisplayName
            : definition.GetLabel();

        string contentName;
        if (!useExternalContent)
            contentName = "Built-in content";
        else if (contentOption == null)
            contentName = "Empty";
        else if (!string.IsNullOrWhiteSpace(contentOption.DisplayName))
            contentName = contentOption.DisplayName;
        else
            contentName = contentOption.Id != null && !contentOption.Id.IsEmpty ? contentOption.Id : "Unnamed content";

        return $"{roomName} ({contentName})";
    }

    private Room InstantiateRoom(StringName screenId, StringName entryExitId, RoomTransition sourceTransition)
    {
        if (_dungeon != null &&
            _dungeon.TryCreateRoom(screenId, _activeRoom, sourceTransition, entryExitId, out var dungeonRoom) &&
            dungeonRoom != null)
        {
            return dungeonRoom;
        }

        if (TryGetCachedRoom(screenId, out var cachedRoom))
            return cachedRoom;

        if (RoomRegistry == null)
        {
            GD.PushError($"{nameof(World)} cannot resolve room '{screenId}' without a room registry.");
            return null;
        }

        if (!RoomRegistry.TryGetRoomScene(screenId, out var roomScene))
        {
            GD.PushError($"No room scene registered for screen id '{screenId}'.");
            return null;
        }

        if (roomScene == null)
        {
            GD.PushError($"Room registry entry for '{screenId}' does not define a room scene.");
            return null;
        }

        if (roomScene.Instantiate() is not Room room)
        {
            GD.PushError($"Registered room scene for '{screenId}' does not instantiate a {nameof(Room)} root.");
            return null;
        }

        CacheRoom(room);
        return room;
    }

    private void DisconnectActiveRoom()
    {
        if (_activeRoom != null && GodotObject.IsInstanceValid(_activeRoom))
            _activeRoom.TransitionTriggered -= OnTransitionTriggered;
    }

    private void DetachOrFreeActiveRoom()
    {
        if (_activeRoom == null || !GodotObject.IsInstanceValid(_activeRoom))
        {
            _activeRoom = null;
            return;
        }

        if (_activeRoom == _debugRoom)
        {
            ReleaseActiveDebugRoom();
            _activeRoom = null;
            return;
        }

        // Dungeon-owned plan rooms are retained by the Dungeon for the lifetime of the run, so
        // re-entry returns the same instance. Detach (don't free) here; the Dungeon frees them
        // on EndRun. This keeps room ownership between World and Dungeon explicit and avoids
        // double-freeing a still-cached room.
        if (_dungeon != null && _dungeon.IsManagedRoom(_activeRoom))
        {
            var dungeonParent = _activeRoom.GetParent();
            if (dungeonParent != null)
                dungeonParent.RemoveChild(_activeRoom);

            _activeRoom = null;
            return;
        }

        if (ShouldPersistRoom(_activeRoom))
        {
            var parent = _activeRoom.GetParent();
            if (parent != null)
                parent.RemoveChild(_activeRoom);
        }
        else
        {
            RemoveCachedRoom(_activeRoom);
            _activeRoom.QueueFree();
        }

        _activeRoom = null;
    }

    private void CallActiveRoomExit()
    {
        if (_activeRoom != null && GodotObject.IsInstanceValid(_activeRoom))
            _activeRoom.OnExit();
    }

    private void AttachActiveRoom()
    {
        if (_activeRoom == null || !GodotObject.IsInstanceValid(_activeRoom))
            return;

        var targetParent = _roomContainer ?? this;
        var currentParent = _activeRoom.GetParent();
        if (currentParent == targetParent)
            return;

        if (currentParent != null)
            currentParent.RemoveChild(_activeRoom);

        targetParent.AddChild(_activeRoom);
    }

    private bool ShouldPersistRoom(Room room)
    {
        return UsePersistentRoomCache &&
            room != null &&
            GodotObject.IsInstanceValid(room) &&
            room.PersistInstance &&
            HasValue(room.ScreenId);
    }

    private bool TryGetCachedRoom(StringName screenId, out Room room)
    {
        room = null;
        if (!UsePersistentRoomCache || !HasValue(screenId))
            return false;

        if (!_persistentRoomsById.TryGetValue(screenId, out var cachedRoom))
            return false;

        if (!GodotObject.IsInstanceValid(cachedRoom))
        {
            _persistentRoomsById.Remove(screenId);
            return false;
        }

        room = cachedRoom;
        return true;
    }

    private void CacheRoom(Room room)
    {
        if (!ShouldPersistRoom(room))
            return;

        _persistentRoomsById[room.ScreenId] = room;
    }

    private void RemoveCachedRoom(Room room)
    {
        if (room == null || !HasValue(room.ScreenId))
            return;

        if (_persistentRoomsById.TryGetValue(room.ScreenId, out var cachedRoom) &&
            (!GodotObject.IsInstanceValid(cachedRoom) || cachedRoom == room))
        {
            _persistentRoomsById.Remove(room.ScreenId);
        }
    }

    private void FreeDetachedCachedRooms()
    {
        foreach (var room in _persistentRoomsById.Values)
        {
            if (room == null || !GodotObject.IsInstanceValid(room) || room.GetParent() != null)
                continue;

            room.QueueFree();
        }
    }

    private void PlacePlayerAtRoomEntry(Room room, StringName entryExitId)
    {
        if (_player == null || room == null)
            return;

        Vector2 targetPosition;
        if (room.TryGetSpawnMarker(entryExitId, out var spawnMarker))
            targetPosition = spawnMarker.GlobalPosition;
        else
            targetPosition = room.GlobalPosition;

        _player.GlobalPosition = targetPosition;
        _player.Velocity = Vector2.Zero;
    }

    private void ApplyRoomCameraBounds(Room room)
    {
        if (_playerCamera == null || room == null)
            return;

        var worldBounds = room.GetWorldCameraBounds();
        _playerCamera.LimitEnabled = true;
        _playerCamera.LimitLeft = Mathf.FloorToInt(worldBounds.Position.X);
        _playerCamera.LimitTop = Mathf.FloorToInt(worldBounds.Position.Y);
        _playerCamera.LimitRight = Mathf.CeilToInt(worldBounds.End.X);
        _playerCamera.LimitBottom = Mathf.CeilToInt(worldBounds.End.Y);
        _playerCamera.ForceUpdateScroll();
    }

    private static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }
}
