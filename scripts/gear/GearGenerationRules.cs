using Godot;

[GlobalClass]
public partial class GearGenerationRules : Resource
{
    [Export]
    public Godot.Collections.Array<GearQualityRules> Qualities { get; set; } = new();

    [Export]
    public Godot.Collections.Array<GearSlotRules> Slots { get; set; } = new();

    [Export]
    public Godot.Collections.Array<GearMainStatScaleEntry> MainStatScales { get; set; } = new();

    [Export]
    public Godot.Collections.Array<string> SubstatPool { get; set; } = new();

    [Export(PropertyHint.Range, "1,10000,1")]
    public int ArcaneCrystalXp { get; set; } = 25;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float FodderInvestedXpRefundRate { get; set; } = 0.80f;

    public GearQualityRules GetQualityRules(GearQuality quality)
    {
        foreach (var entry in Qualities)
        {
            if (entry != null && entry.Quality == quality)
                return entry;
        }
        return null;
    }

    public GearSlotRules GetSlotRules(EquipmentSlot slot)
    {
        foreach (var entry in Slots)
        {
            if (entry != null && entry.Slot == slot)
                return entry;
        }
        return null;
    }

    public float GetMainStatMaxValue(string statId, GearQuality quality)
    {
        foreach (var entry in MainStatScales)
        {
            if (entry != null &&
                entry.Quality == quality &&
                string.Equals(entry.StatId, statId, System.StringComparison.Ordinal))
            {
                return entry.MaxValue;
            }
        }
        return 0.0f;
    }

    public bool TryGetSubstatValue(GearQuality quality, string statId, out float value)
    {
        var qualityRules = GetQualityRules(quality);
        if (qualityRules != null)
        {
            foreach (var entry in qualityRules.SubstatValues)
            {
                if (entry != null && string.Equals(entry.StatId, statId, System.StringComparison.Ordinal))
                {
                    value = entry.Value;
                    return true;
                }
            }
        }

        value = 0.0f;
        return false;
    }
}
