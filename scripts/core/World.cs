using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class World : Node2D
{
    private const float TransitionCooldownSeconds = 0.2f;

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
    public StringName InitialScreenId { get; set; } = "entrance_hall";

    [Export]
    public StringName InitialExitId { get; set; } = default;

    [Export]
    public bool UsePersistentRoomCache { get; set; } = true;

    [Signal]
    public delegate void PlayerDiedEventHandler();

    private Player _player;
    private Camera2D _playerCamera;
    private Node _roomContainer;
    private CorpseManager _corpseManager;
    private InventoryController _inventoryController;
    private Dungeon _dungeon;
    private RoomScreen _activeRoom;
    private bool _isGameOver;
    private float _transitionCooldownRemaining;
    private readonly Dictionary<StringName, RoomScreen> _persistentRoomsById = new();

    public RoomScreen ActiveRoom => GodotObject.IsInstanceValid(_activeRoom) ? _activeRoom : null;

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

        DisconnectActiveRoom();
        DetachOrFreeActiveRoom();

        _activeRoom = nextRoom;
        AttachActiveRoom();
        _activeRoom.TransitionTriggered += OnTransitionTriggered;

        PlacePlayerAtRoomEntry(_activeRoom, entryExitId);
        ApplyRoomCameraBounds(_activeRoom);
        return true;
    }

    private RoomScreen InstantiateRoom(StringName screenId, StringName entryExitId, RoomTransition sourceTransition)
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

        if (roomScene.Instantiate() is not RoomScreen room)
        {
            GD.PushError($"Registered room scene for '{screenId}' does not instantiate a {nameof(RoomScreen)} root.");
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

    private bool ShouldPersistRoom(RoomScreen room)
    {
        return UsePersistentRoomCache &&
            room != null &&
            GodotObject.IsInstanceValid(room) &&
            room.PersistInstance &&
            HasValue(room.ScreenId);
    }

    private bool TryGetCachedRoom(StringName screenId, out RoomScreen room)
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

    private void CacheRoom(RoomScreen room)
    {
        if (!ShouldPersistRoom(room))
            return;

        _persistentRoomsById[room.ScreenId] = room;
    }

    private void RemoveCachedRoom(RoomScreen room)
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

    private void PlacePlayerAtRoomEntry(RoomScreen room, StringName entryExitId)
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

    private void ApplyRoomCameraBounds(RoomScreen room)
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
