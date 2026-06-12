using Godot;

// Lightweight, memory-only return point captured before entering a room out of
// band (debug launcher today, dungeon entrances later). Restoring by screen id
// plus player position is intentional: it does not keep the origin room
// instance alive, so persistent-room caching applies naturally on the way back.
public sealed class RoomReturnLocation
{
    public RoomReturnLocation(StringName screenId, Vector2? playerPosition)
    {
        ScreenId = screenId;
        PlayerPosition = playerPosition;
    }

    public StringName ScreenId { get; }

    public Vector2? PlayerPosition { get; }
}
