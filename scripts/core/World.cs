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

    public Room ActiveRoom => GodotObject.IsInstanceValid(_activeRoom) ? _activeRoom : null;

    // Read-only access for the Dungeon HUB page (run state, current node, generation defaults).
    public Dungeon Dungeon => _dungeon != null && GodotObject.IsInstanceValid(_dungeon) ? _dungeon : null;

    public bool HasActiveDungeonRun => Dungeon?.HasActiveRun == true;

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

        // Count the death for the active run before the game-over/reload flow may discard it.
        _dungeon?.RegisterPlayerDeath();

        _isGameOver = true;
        EmitSignal(SignalName.PlayerDied);
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
        return true;
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
        _dungeon?.FinalizeRun(outcome);
        _dungeonReturnLocation = null;
        return true;
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
