using Godot;

[GlobalClass]
public partial class SpecialDungeonRoom : Room
{
    private static readonly StringName TopProgressionExitId = "north_center";
    private static readonly StringName BottomReturnExitId = "south_return";

    public void ConfigureProgressionDoor(StringName targetScreenId, StringName targetExitId)
    {
        ConfigureDoor(TopProgressionExitId, targetScreenId, targetExitId);
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
}
