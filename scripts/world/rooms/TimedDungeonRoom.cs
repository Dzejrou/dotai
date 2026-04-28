using Godot;

[GlobalClass]
public partial class TimedDungeonRoom : TimedRoom
{
    [Export]
    public NodePath ContentPath { get; set; } = new("Unscaled/EnemyContent");

    [Export]
    public NodePath ProgressionDoorPath { get; set; } = new("Scaled/Exits/NorthCenterDoor");

    [Export]
    public NodePath BonusChestPath { get; set; } = new("Unscaled/BonusChest");

    private Content _content;
    private Door _progressionDoor;
    private Chest _bonusChest;
    private bool _bonusChestRemoved;
    private bool _contentResolved;
    private bool _progressionDoorResolved;
    private bool _bonusChestResolved;

    public override void _Ready()
    {
        ResolveContent();
        ResolveProgressionDoor();
        ResolveBonusChest();
        SetProgressionDoorLocked(true);
    }

    public void ConfigureProgressionDoor(StringName targetScreenId, StringName targetExitId)
    {
        var door = ResolveProgressionDoor();
        if (door == null)
            return;

        door.TargetScreenId = targetScreenId;
        door.TargetExitId = targetExitId;
    }

    protected override bool IsTimedObjectiveCleared()
    {
        var content = ResolveContent();
        return content?.IsEmpty == true;
    }

    protected override void OnTimedRoomCleared()
    {
        SetProgressionDoorLocked(false);
        ResolveBonusChest()?.UnlockExternal();
    }

    protected override void OnTimerExpired()
    {
        ResolveContent()?.Respawn();
        RemoveBonusChestOnFirstFailure();
        RestartTimer();
    }

    private void RemoveBonusChestOnFirstFailure()
    {
        if (_bonusChestRemoved)
            return;

        var bonusChest = ResolveBonusChest();
        _bonusChestRemoved = true;
        if (bonusChest == null || !GodotObject.IsInstanceValid(bonusChest))
        {
            _bonusChest = null;
            return;
        }

        var parent = bonusChest.GetParent();
        if (parent != null)
            parent.RemoveChild(bonusChest);

        bonusChest.QueueFree();
        _bonusChest = null;
    }

    private void SetProgressionDoorLocked(bool isLocked)
    {
        var door = ResolveProgressionDoor();
        if (door != null)
            door.IsLocked = isLocked;
    }

    private Content ResolveContent()
    {
        if (_contentResolved)
            return GodotObject.IsInstanceValid(_content) ? _content : null;

        _contentResolved = true;
        _content = ContentPath.IsEmpty ? null : GetNodeOrNull<Content>(ContentPath);
        if (_content == null)
            GD.PushError($"{nameof(TimedDungeonRoom)} '{Name}' could not resolve content at '{ContentPath}'.");

        return _content;
    }

    private Door ResolveProgressionDoor()
    {
        if (_progressionDoorResolved)
            return GodotObject.IsInstanceValid(_progressionDoor) ? _progressionDoor : null;

        _progressionDoorResolved = true;
        _progressionDoor = ProgressionDoorPath.IsEmpty ? null : GetNodeOrNull<Door>(ProgressionDoorPath);
        if (_progressionDoor == null)
            GD.PushError($"{nameof(TimedDungeonRoom)} '{Name}' could not resolve progression door at '{ProgressionDoorPath}'.");

        return _progressionDoor;
    }

    private Chest ResolveBonusChest()
    {
        if (_bonusChestRemoved)
            return null;

        if (_bonusChestResolved)
            return GodotObject.IsInstanceValid(_bonusChest) ? _bonusChest : null;

        _bonusChestResolved = true;
        _bonusChest = BonusChestPath.IsEmpty ? null : GetNodeOrNull<Chest>(BonusChestPath);
        if (_bonusChest == null)
            GD.PushWarning($"{nameof(TimedDungeonRoom)} '{Name}' could not resolve bonus chest at '{BonusChestPath}'.");

        return _bonusChest;
    }
}
