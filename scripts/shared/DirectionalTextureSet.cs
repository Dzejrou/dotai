using Godot;

[GlobalClass]
public partial class DirectionalTextureSet : Resource
{
    [Export]
    public Texture2D EastTexture { get; set; }

    [Export]
    public Texture2D SouthEastTexture { get; set; }

    [Export]
    public Texture2D SouthTexture { get; set; }

    [Export]
    public Texture2D SouthWestTexture { get; set; }

    [Export]
    public Texture2D WestTexture { get; set; }

    [Export]
    public Texture2D NorthWestTexture { get; set; }

    [Export]
    public Texture2D NorthTexture { get; set; }

    [Export]
    public Texture2D NorthEastTexture { get; set; }

    public Texture2D ResolveTexture(Vector2 direction)
    {
        var directionName = DirectionHelper.GetDirectionName(direction);
        var fallbackDirection = DirectionHelper.GetCardinalFallbackDirectionName(directionName);

        return ResolveTextureForDirection(directionName) ??
               ResolveTextureForDirection(fallbackDirection) ??
               SouthTexture ??
               EastTexture ??
               WestTexture ??
               NorthTexture;
    }

    private Texture2D ResolveTextureForDirection(string direction)
    {
        return direction switch
        {
            "east" => EastTexture,
            "south-east" => SouthEastTexture,
            "south" => SouthTexture,
            "south-west" => SouthWestTexture,
            "west" => WestTexture,
            "north-west" => NorthWestTexture,
            "north" => NorthTexture,
            "north-east" => NorthEastTexture,
            _ => SouthTexture,
        };
    }
}
