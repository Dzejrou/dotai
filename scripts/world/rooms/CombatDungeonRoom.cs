using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class CombatDungeonRoom : Room
{
    private static readonly StringName TopLeftExitId = "north_west";
    private static readonly StringName TopRightExitId = "north_east";
    private static readonly StringName BottomReturnExitId = "south_return";

    [Signal]
    public delegate void RoomClearedEventHandler();

    [Export]
    public NodePath EncounterMarkersPath { get; set; } = new NodePath("Unscaled/EncounterMarkers");

    [Export]
    public NodePath RoomClearUnlockerPath { get; set; } = new NodePath("RoomClearUnlocker");

    private readonly Dictionary<StringName, Marker2D> _encounterMarkersById = new();
    private readonly Dictionary<Node, Callable> _enemyExitCallables = new();
    private bool _isCleared;
    private RoomClearUnlocker _roomClearUnlocker;

    public override void _Ready()
    {
        base._Ready();
        RoomCleared += OnRoomCleared;
        CacheEncounterMarkers();
        _roomClearUnlocker = ResolveRoomClearUnlocker();
        SetTopDoorsLocked(true);
    }

    public override void _ExitTree()
    {
        RoomCleared -= OnRoomCleared;
        DisconnectTrackedEnemies();
        _encounterMarkersById.Clear();
        _roomClearUnlocker = null;
        base._ExitTree();
    }

    public IReadOnlyList<StringName> GetEncounterMarkerIds()
    {
        CacheEncounterMarkers();
        return new List<StringName>(_encounterMarkersById.Keys);
    }

    public bool TryGetEncounterMarkerGlobalPosition(StringName markerId, out Vector2 globalPosition)
    {
        CacheEncounterMarkers();
        if (_encounterMarkersById.TryGetValue(markerId, out var marker))
        {
            globalPosition = marker.GlobalPosition;
            return true;
        }

        globalPosition = Vector2.Zero;
        return false;
    }

    public void ConfigureProgressionDoors(StringName targetScreenId, StringName targetExitId)
    {
        ConfigureDoor(TopLeftExitId, targetScreenId, targetExitId);
        ConfigureDoor(TopRightExitId, targetScreenId, targetExitId);
    }

    public void ConfigureReturnDoor(StringName targetScreenId, StringName targetExitId)
    {
        ConfigureDoor(BottomReturnExitId, targetScreenId, targetExitId);
    }

    public void PrepareEncounter()
    {
        _isCleared = false;
        DisconnectTrackedEnemies();
        SetTopDoorsLocked(true);
    }

    public void RegisterEncounterEnemy(Node enemy)
    {
        if (enemy == null || _enemyExitCallables.ContainsKey(enemy))
            return;

        var callable = Callable.From(() => OnTrackedEnemyExited(enemy));
        _enemyExitCallables[enemy] = callable;
        enemy.Connect(Node.SignalName.TreeExited, callable, (uint)ConnectFlags.OneShot);
    }

    public void FinalizeEncounterSetup()
    {
        EvaluateEncounterState();
    }

    private void ConfigureDoor(StringName exitId, StringName targetScreenId, StringName targetExitId)
    {
        var door = GetDoor(exitId);
        if (door == null)
            return;

        door.TargetScreenId = targetScreenId;
        door.TargetExitId = targetExitId;
    }

    private void CacheEncounterMarkers()
    {
        if (_encounterMarkersById.Count > 0)
            return;

        var markersRoot = GetNodeOrNull<Node>(EncounterMarkersPath);
        if (markersRoot == null)
            return;

        foreach (Node child in markersRoot.GetChildren())
        {
            if (child is Marker2D marker)
                _encounterMarkersById[marker.Name] = marker;
        }
    }

    private void DisconnectTrackedEnemies()
    {
        foreach (var entry in _enemyExitCallables)
        {
            var enemy = entry.Key;
            var callable = entry.Value;
            if (GodotObject.IsInstanceValid(enemy) && enemy.IsConnected(Node.SignalName.TreeExited, callable))
                enemy.Disconnect(Node.SignalName.TreeExited, callable);
        }

        _enemyExitCallables.Clear();
    }

    private void OnTrackedEnemyExited(Node enemy)
    {
        _enemyExitCallables.Remove(enemy);
        EvaluateEncounterState();
    }

    private void EvaluateEncounterState()
    {
        if (_isCleared || _enemyExitCallables.Count > 0)
            return;

        _isCleared = true;
        EmitSignal(SignalName.RoomCleared);
    }

    private void OnRoomCleared()
    {
        var roomClearUnlocker = ResolveRoomClearUnlocker();
        if (roomClearUnlocker != null)
        {
            roomClearUnlocker.UnlockTargets();
            return;
        }

        SetTopDoorsLocked(false);
    }

    private void SetTopDoorsLocked(bool isLocked)
    {
        SetDoorLockState(TopLeftExitId, isLocked);
        SetDoorLockState(TopRightExitId, isLocked);
    }

    private void SetDoorLockState(StringName exitId, bool isLocked)
    {
        var door = GetDoor(exitId);
        if (door != null)
            door.IsLocked = isLocked;
    }

    private RoomClearUnlocker ResolveRoomClearUnlocker()
    {
        if (_roomClearUnlocker != null)
            return _roomClearUnlocker;

        if (RoomClearUnlockerPath.IsEmpty)
            return null;

        _roomClearUnlocker = GetNodeOrNull<RoomClearUnlocker>(RoomClearUnlockerPath);
        return _roomClearUnlocker;
    }
}
