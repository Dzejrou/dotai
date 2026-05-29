using Godot;

using System;

[GlobalClass]
public partial class ExperienceTable : Resource
{
    [Export]
    public Godot.Collections.Array<int> ExperienceToNextLevel { get; set; } = new();

    public int GetRequiredExperienceForLevel(int level, int fallback)
    {
        var safeFallback = Math.Max(1, fallback);
        if (level <= 0 || ExperienceToNextLevel == null)
            return safeFallback;

        var index = level - 1;
        if (index >= ExperienceToNextLevel.Count)
            return safeFallback;

        var entry = ExperienceToNextLevel[index];
        return entry <= 0 ? safeFallback : entry;
    }

    // Reward lookup: clamps `level` into the table's valid range so callers
    // computing enemy XP at or above the player's max level still get a
    // table-backed requirement (the highest entry) instead of the fallback.
    public int GetRequiredExperienceForRewardLevel(int level, int fallback)
    {
        var safeFallback = Math.Max(1, fallback);
        if (ExperienceToNextLevel == null || ExperienceToNextLevel.Count == 0)
            return safeFallback;

        var clampedLevel = Math.Clamp(level, 1, ExperienceToNextLevel.Count);
        return GetRequiredExperienceForLevel(clampedLevel, safeFallback);
    }
}
