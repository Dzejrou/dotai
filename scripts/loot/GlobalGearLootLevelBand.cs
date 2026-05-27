using Godot;

[GlobalClass]
public partial class GlobalGearLootLevelBand : Resource
{
    [Export(PropertyHint.Range, "1,200,1")]
    public int MinLevel { get; set; } = 1;

    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float TrashWeight { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float CommonWeight { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float UncommonWeight { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float RareWeight { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float EpicWeight { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float LegendaryWeight { get; set; } = 0.0f;

    public float TotalWeight =>
        Mathf.Max(0.0f, TrashWeight)
        + Mathf.Max(0.0f, CommonWeight)
        + Mathf.Max(0.0f, UncommonWeight)
        + Mathf.Max(0.0f, RareWeight)
        + Mathf.Max(0.0f, EpicWeight)
        + Mathf.Max(0.0f, LegendaryWeight);

    public bool TryPickQuality(RandomNumberGenerator random, out ItemQuality quality)
    {
        quality = ItemQuality.Common;

        var total = TotalWeight;
        if (random == null || total <= 0.0f)
            return false;

        var roll = random.Randf() * total;
        var running = 0.0f;

        running += Mathf.Max(0.0f, TrashWeight);
        if (roll < running) { quality = ItemQuality.Trash; return true; }

        running += Mathf.Max(0.0f, CommonWeight);
        if (roll < running) { quality = ItemQuality.Common; return true; }

        running += Mathf.Max(0.0f, UncommonWeight);
        if (roll < running) { quality = ItemQuality.Uncommon; return true; }

        running += Mathf.Max(0.0f, RareWeight);
        if (roll < running) { quality = ItemQuality.Rare; return true; }

        running += Mathf.Max(0.0f, EpicWeight);
        if (roll < running) { quality = ItemQuality.Epic; return true; }

        quality = ItemQuality.Legendary;
        return true;
    }
}
