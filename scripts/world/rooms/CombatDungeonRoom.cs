using Godot;

[GlobalClass]
public partial class CombatDungeonRoom : Room
{
    private static readonly StringName TopLeftExitId = "north_west";
    private static readonly StringName TopRightExitId = "north_east";
    private static readonly StringName BottomReturnExitId = "south_return";

    [Signal]
    public delegate void RoomClearedEventHandler();

    [Export]
    public NodePath RoomClearUnlockerPath { get; set; } = new NodePath("RoomClearUnlocker");

    private Content _activeContent;
    private bool _isCleared;
    private bool _contentResolved;
    private RoomClearUnlocker _roomClearUnlocker;

    public override void _Ready()
    {
        base._Ready();
        RoomCleared += OnRoomCleared;
        _roomClearUnlocker = ResolveRoomClearUnlocker();
        SetTopDoorsLocked(true);
        EvaluateRoomState();
    }

    public override void _ExitTree()
    {
        RoomCleared -= OnRoomCleared;
        _activeContent = null;
        _roomClearUnlocker = null;
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        EvaluateRoomState();
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

    private void ConfigureDoor(StringName exitId, StringName targetScreenId, StringName targetExitId)
    {
        var door = GetDoor(exitId);
        if (door == null)
            return;

        door.TargetScreenId = targetScreenId;
        door.TargetExitId = targetExitId;
    }

    private void EvaluateRoomState()
    {
        if (_isCleared || ResolveActiveContent()?.IsEmpty != true)
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

    private Content ResolveActiveContent()
    {
        if (_contentResolved)
            return GodotObject.IsInstanceValid(_activeContent) ? _activeContent : null;

        _contentResolved = true;
        _activeContent = GetInjectedContent();
        return _activeContent;
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
