using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class World : Node2D
{
    private const float TransitionCooldownSeconds = 0.2f;

    private static readonly Dictionary<StringName, string> RoomScenePathsByScreenId = new()
    {
        ["entrance_hall"] = "res://scenes/world/rooms/entrance_hall_room.tscn",
        ["transition_test"] = "res://scenes/world/rooms/transition_test_room.tscn",
    };

    [Export]
    public NodePath PlayerPath { get; set; } = new NodePath("Player");

    [Export]
    public NodePath RoomContainerPath { get; set; } = new NodePath("RoomContainer");

    [Export]
    public NodePath CorpseManagerPath { get; set; } = new NodePath("CorpseManager");

    [Export]
    public StringName InitialScreenId { get; set; } = "entrance_hall";

    [Export]
    public StringName InitialExitId { get; set; } = default;

    [Signal]
    public delegate void PlayerDiedEventHandler();

    private Player _player;
    private Node _roomContainer;
    private CorpseManager _corpseManager;
    private RoomScreen _activeRoom;
    private bool _isGameOver;
    private float _transitionCooldownRemaining;

    public override void _Ready()
    {
        _roomContainer = GetNodeOrNull<Node>(RoomContainerPath);
        if (_roomContainer == null)
            GD.PushError($"{nameof(World)} could not resolve room container at '{RoomContainerPath}'.");

        _corpseManager = ResolveCorpseManager();
        _player = GetNodeOrNull<Player>(PlayerPath);

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

    private void OnRoomExitTriggered(RoomExit roomExit)
    {
        if (_transitionCooldownRemaining > 0.0f || roomExit == null || roomExit.IsLocked)
            return;

        if (!HasValue(roomExit.TargetScreenId))
        {
            GD.PushWarning($"{roomExit.Name} does not define a target screen id.");
            return;
        }

        if (!TransitionToRoom(roomExit.TargetScreenId, roomExit.TargetExitId))
            return;

        _transitionCooldownRemaining = TransitionCooldownSeconds;
    }

    private bool TransitionToRoom(StringName screenId, StringName entryExitId)
    {
        var nextRoom = InstantiateRoom(screenId);
        if (nextRoom == null)
            return false;

        DisconnectActiveRoom();

        if (_activeRoom != null && GodotObject.IsInstanceValid(_activeRoom))
            _activeRoom.QueueFree();

        _activeRoom = nextRoom;
        (_roomContainer ?? this).AddChild(_activeRoom);
        _activeRoom.ExitTriggered += OnRoomExitTriggered;

        PlacePlayerAtRoomEntry(_activeRoom, entryExitId);
        return true;
    }

    private RoomScreen InstantiateRoom(StringName screenId)
    {
        if (!RoomScenePathsByScreenId.TryGetValue(screenId, out var scenePath))
        {
            GD.PushError($"No room scene registered for screen id '{screenId}'.");
            return null;
        }

        var roomScene = ResourceLoader.Load<PackedScene>(scenePath);
        if (roomScene == null)
        {
            GD.PushError($"Failed to load room scene at '{scenePath}'.");
            return null;
        }

        if (roomScene.Instantiate() is not RoomScreen room)
        {
            GD.PushError($"Scene '{scenePath}' does not instantiate a {nameof(RoomScreen)} root.");
            return null;
        }

        return room;
    }

    private void DisconnectActiveRoom()
    {
        if (_activeRoom != null)
            _activeRoom.ExitTriggered -= OnRoomExitTriggered;
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

    private static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }
}
