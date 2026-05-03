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
    public ContentSet ContentTemplates { get; set; }

    [Export]
    public NodePath ContentRootPath { get; set; } = new("Unscaled/ContentRoot");

    [Export]
    public NodePath RoomClearUnlockerPath { get; set; } = new NodePath("RoomClearUnlocker");

    private readonly RandomNumberGenerator _random = new();
    private Content _activeContent;
    private Node _contentRoot;
    private bool _isCleared;
    private bool _contentInitialized;
    private bool _contentRootResolved;
    private RoomClearUnlocker _roomClearUnlocker;

    public override void _Ready()
    {
        base._Ready();
        RoomCleared += OnRoomCleared;
        _roomClearUnlocker = ResolveRoomClearUnlocker();
        _random.Randomize();
        SetTopDoorsLocked(true);
        InitializeContent();
        EvaluateRoomState();
    }

    public override void _ExitTree()
    {
        RoomCleared -= OnRoomCleared;
        _activeContent = null;
        _contentRoot = null;
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

    private void InitializeContent()
    {
        if (_contentInitialized)
            return;

        _contentInitialized = true;

        var contentRoot = ResolveContentRoot();
        if (contentRoot == null)
        {
            GD.PushWarning($"{nameof(CombatDungeonRoom)} '{Name}' could not resolve content root at '{ContentRootPath}'.");
            return;
        }

        if (ContentTemplates == null)
        {
            GD.PushWarning($"{nameof(CombatDungeonRoom)} '{Name}' does not define any content templates.");
            return;
        }

        var contentScene = ContentTemplates.PickTemplate(_random);
        if (contentScene == null)
        {
            GD.PushWarning($"{nameof(CombatDungeonRoom)} '{Name}' could not choose a valid content template.");
            return;
        }

        var contentInstance = contentScene.Instantiate();
        if (contentInstance == null)
        {
            GD.PushWarning($"{nameof(CombatDungeonRoom)} '{Name}' failed to instantiate content scene '{contentScene.ResourcePath}'.");
            return;
        }

        contentRoot.AddChild(contentInstance);
        if (contentInstance is not Content content)
        {
            GD.PushWarning(
                $"{nameof(CombatDungeonRoom)} '{Name}' instantiated '{contentScene.ResourcePath}', but its root is '{contentInstance.GetType().Name}' instead of {nameof(Content)}.");
            return;
        }

        _activeContent = content;
    }

    private void EvaluateRoomState()
    {
        if (_isCleared || GetActiveContent()?.IsEmpty != true)
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

    private Content GetActiveContent()
    {
        return GodotObject.IsInstanceValid(_activeContent) ? _activeContent : null;
    }

    private Node ResolveContentRoot()
    {
        if (_contentRootResolved)
            return GodotObject.IsInstanceValid(_contentRoot) ? _contentRoot : null;

        _contentRootResolved = true;
        _contentRoot = ContentRootPath.IsEmpty ? null : GetNodeOrNull<Node>(ContentRootPath);
        return _contentRoot;
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
