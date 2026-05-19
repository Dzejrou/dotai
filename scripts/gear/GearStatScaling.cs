using System;
using System.Collections.Generic;

// Shared formula for a gear instance's current main stat value:
//   value = maxValueForQualityAndStat * level / 20
// Integer-valued stats are clamped to at least 1.0 so a level-1 roll never
// resolves to 0 after rounding.
public static class GearStatScaling
{
    public const int MaxLevelDenominator = 20;

    public static readonly HashSet<string> IntegerStats = new(StringComparer.Ordinal)
    {
        EquipmentStatIds.MaxHealth,
        EquipmentStatIds.MaxMana,
        EquipmentStatIds.MP5,
        EquipmentStatIds.Power,
        EquipmentStatIds.Haste,
    };

    public static float ComputeMainStatValue(string statId, float maxValueForQualityAndStat, int level)
    {
        var lvl = Math.Max(1, level);
        var value = maxValueForQualityAndStat * lvl / (float)MaxLevelDenominator;
        if (IntegerStats.Contains(statId))
            return Math.Max(1.0f, value);
        return value;
    }
}
