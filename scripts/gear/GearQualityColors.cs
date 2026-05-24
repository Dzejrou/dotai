using Godot;

public static class ItemQualityColors
{
    private static readonly Color TrashColor = new(0.62f, 0.62f, 0.62f);
    private static readonly Color CommonColor = new(1.0f, 1.0f, 1.0f);
    private static readonly Color UncommonColor = new(0.35f, 0.82f, 0.35f);
    private static readonly Color RareColor = new(0.30f, 0.55f, 1.00f);
    private static readonly Color EpicColor = new(0.70f, 0.40f, 1.00f);
    private static readonly Color LegendaryColor = new(1.00f, 0.60f, 0.20f);

    public static Color GetColor(ItemQuality quality) => quality switch
    {
        ItemQuality.Trash => TrashColor,
        ItemQuality.Common => CommonColor,
        ItemQuality.Uncommon => UncommonColor,
        ItemQuality.Rare => RareColor,
        ItemQuality.Epic => EpicColor,
        ItemQuality.Legendary => LegendaryColor,
        _ => CommonColor,
    };
}
