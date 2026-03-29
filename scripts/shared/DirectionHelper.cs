using Godot;

public static class DirectionHelper
{
    private static readonly string[] EightWayDirections =
    {
        "east",
        "south-east",
        "south",
        "south-west",
        "west",
        "north-west",
        "north",
        "north-east",
    };

    public static string GetDirectionName(Vector2 direction)
    {
        if (direction == Vector2.Zero)
            return "south";

        var octant = Mathf.PosMod(Mathf.RoundToInt(direction.Normalized().Angle() / Mathf.Pi * 4.0f), 8);
        return EightWayDirections[octant];
    }

    public static Vector2 GetDirectionVector(string direction)
    {
        return direction switch
        {
            "east" => Vector2.Right,
            "south-east" => (Vector2.Right + Vector2.Down).Normalized(),
            "south" => Vector2.Down,
            "south-west" => (Vector2.Left + Vector2.Down).Normalized(),
            "west" => Vector2.Left,
            "north-west" => (Vector2.Left + Vector2.Up).Normalized(),
            "north" => Vector2.Up,
            "north-east" => (Vector2.Right + Vector2.Up).Normalized(),
            _ => Vector2.Down,
        };
    }

    public static string GetCardinalFallbackDirectionName(string direction)
    {
        return direction switch
        {
            "north-east" => "north",
            "north-west" => "north",
            "south-east" => "south",
            "south-west" => "south",
            "east" => "east",
            "west" => "west",
            "north" => "north",
            _ => "south",
        };
    }
}
