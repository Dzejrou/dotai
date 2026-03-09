using Godot;
using System.Collections.Generic;

public static class DirectionHelper
{
    private static readonly string[] CardinalDirections = { "south", "east", "north", "west" };

    public static IReadOnlyList<string> GetCardinalDirections()
    {
        return CardinalDirections;
    }

    public static string GetDirectionName(Vector2 direction)
    {
        if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
            return direction.X > 0.0f ? "east" : "west";

        return direction.Y > 0.0f ? "south" : "north";
    }

    public static Vector2 GetDirectionVector(string direction)
    {
        return direction switch
        {
            "east" => Vector2.Right,
            "west" => Vector2.Left,
            "north" => Vector2.Up,
            _ => Vector2.Down,
        };
    }
}
