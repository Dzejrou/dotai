using Godot;

[GlobalClass]
public partial class RoomRegistry : Resource
{
    [Export]
    public Godot.Collections.Array<RoomRegistryEntry> Entries { get; set; } = new();

    public bool TryGetRoomScene(StringName screenId, out PackedScene roomScene)
    {
        roomScene = null;
        if (!HasValue(screenId))
            return false;

        foreach (var entry in Entries)
        {
            if (entry == null || !HasValue(entry.ScreenId) || entry.RoomScene == null)
                continue;

            if (entry.ScreenId != screenId)
                continue;

            roomScene = entry.RoomScene;
            return true;
        }

        return false;
    }

    private static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }
}
