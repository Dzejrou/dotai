using Godot;

using System;
using System.Collections.Generic;

// Substat upgrades that fire when gear crosses a milestone level (4, 8, 12, 16, 20).
//
// Per milestone crossed:
//   - If the gear has fewer substats than its quality's intended SubstatCount,
//     add a new unique substat from the rules' SubstatPool (excluding main-stat
//     and existing-substat stat ids), valued at the quality's fixed substat value.
//   - Otherwise, pick one existing substat at random and increase its value by the
//     quality's fixed substat value for that stat id.
//
// Returned list reports the per-roll *delta* (StatId + Value), in the order rolled.
// The UI is responsible for aggregating by stat id if it wants a clean summary.
public static class GearSubstatProgression
{
    public static readonly int[] MilestoneLevels = { 4, 8, 12, 16, 20 };

    public static List<GearStatModifier> ApplyMilestoneRolls(
        GearInstance gear,
        int previousLevel,
        int newLevel,
        GearGenerationRules rules)
    {
        var rolls = new List<GearStatModifier>();
        if (gear == null || rules == null || newLevel <= previousLevel)
            return rolls;

        var qualityRules = rules.GetQualityRules(gear.Quality);
        if (qualityRules == null)
            return rolls;

        var milestones = CountMilestonesCrossed(previousLevel, newLevel);
        for (var i = 0; i < milestones; i++)
        {
            var roll = RollOnce(gear, qualityRules, rules);
            if (roll != null)
                rolls.Add(roll);
        }

        return rolls;
    }

    public static int CountMilestonesCrossed(int previousLevel, int newLevel)
    {
        var count = 0;
        foreach (var ml in MilestoneLevels)
        {
            if (ml > previousLevel && ml <= newLevel)
                count++;
        }
        return count;
    }

    private static GearStatModifier RollOnce(
        GearInstance gear,
        GearQualityRules qualityRules,
        GearGenerationRules rules)
    {
        var intendedCount = Math.Max(0, qualityRules.SubstatCount);

        if (gear.Substats.Count < intendedCount)
            return RollAddNewSubstat(gear, rules);

        return RollUpgradeExistingSubstat(gear, rules);
    }

    private static GearStatModifier RollAddNewSubstat(GearInstance gear, GearGenerationRules rules)
    {
        var pool = new List<string>(rules.SubstatPool.Count);
        foreach (var statId in rules.SubstatPool)
        {
            if (string.IsNullOrEmpty(statId))
                continue;
            if (HasModifierWithStatId(gear.MainStats, statId))
                continue;
            if (HasModifierWithStatId(gear.Substats, statId))
                continue;
            pool.Add(statId);
        }

        if (pool.Count == 0)
            return null;

        var pick = pool[(int)(GD.Randi() % (uint)pool.Count)];
        if (!rules.TryGetSubstatValue(gear.Quality, pick, out var value))
            return null;

        gear.AddSubstat(new GearStatModifier { StatId = pick, Value = value });
        return new GearStatModifier { StatId = pick, Value = value };
    }

    private static GearStatModifier RollUpgradeExistingSubstat(GearInstance gear, GearGenerationRules rules)
    {
        if (gear.Substats.Count == 0)
            return null;

        var index = (int)(GD.Randi() % (uint)gear.Substats.Count);
        var target = gear.Substats[index];
        if (target == null || string.IsNullOrEmpty(target.StatId))
            return null;

        if (!rules.TryGetSubstatValue(gear.Quality, target.StatId, out var delta))
            return null;

        target.Value += delta;
        return new GearStatModifier { StatId = target.StatId, Value = delta };
    }

    private static bool HasModifierWithStatId(IReadOnlyList<GearStatModifier> modifiers, string statId)
    {
        foreach (var modifier in modifiers)
        {
            if (modifier != null && string.Equals(modifier.StatId, statId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
