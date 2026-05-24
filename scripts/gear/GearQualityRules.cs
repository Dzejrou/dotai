using Godot;

[GlobalClass]
public partial class GearQualityRules : Resource
{
    [Export]
    public GearQuality Quality { get; set; } = GearQuality.Common;

    [Export(PropertyHint.Range, "1,40,1")]
    public int MaxLevel { get; set; } = 1;

    [Export(PropertyHint.Range, "0,8,1")]
    public int SubstatCount { get; set; } = 2;

    // Per-level XP requirement table. Index 0 = XP needed for level 1 -> 2,
    // index 1 = level 2 -> 3, etc. Length should equal MaxLevel - 1; if a
    // requested level falls outside the table the lookup falls back to
    // FallbackXpPerLevel.
    [Export]
    public Godot.Collections.Array<int> ExperienceToNextLevel { get; set; } = new();

    [Export(PropertyHint.Range, "0,100000,1")]
    public int BaseFodderXp { get; set; } = 25;

    // Base gold paid when selling a level-1 gear of this quality. The merchant
    // applies a level-progress multiplier on top via GearSellPricing. Set to 0
    // to disable selling for this quality.
    [Export(PropertyHint.Range, "0,100000,1")]
    public int BaseSellPrice { get; set; } = 0;

    // Fixed substat values per stat id. Designers edit one row per substat in the inspector.
    [Export]
    public Godot.Collections.Array<GearStatValueEntry> SubstatValues { get; set; } = new();

    public const int FallbackXpPerLevel = 100;

    // XP needed to advance from `level` to `level + 1`.
    public int GetRequiredExperienceForLevel(int level)
    {
        if (level < 1)
            return FallbackXpPerLevel;

        var index = level - 1;
        if (ExperienceToNextLevel != null &&
            index >= 0 &&
            index < ExperienceToNextLevel.Count)
        {
            var value = ExperienceToNextLevel[index];
            if (value > 0)
                return value;
        }

        return FallbackXpPerLevel;
    }

    // Total XP spent (across the table) to reach `level` from level 1.
    public int GetTotalExperienceAtLevel(int level)
    {
        if (level <= 1)
            return 0;

        var total = 0;
        for (var i = 1; i < level; i++)
            total += GetRequiredExperienceForLevel(i);
        return total;
    }
}
