using Godot;

using System;
using System.Collections.Generic;

// Item id of the crystal stack accepted by the leveling material slot.
public static class GearLevelingMaterials
{
    public const string ArcaneCrystalId = "arcane_crystal";
}

public readonly struct GearEnhanceResult
{
    public GearEnhanceResult(
        bool changed,
        int crystalsConsumed,
        int xpApplied,
        int levelsGained,
        bool reachedMaxLevel,
        IReadOnlyList<GearStatModifier> substatRolls)
    {
        Changed = changed;
        CrystalsConsumed = crystalsConsumed;
        XpApplied = xpApplied;
        LevelsGained = levelsGained;
        ReachedMaxLevel = reachedMaxLevel;
        SubstatRolls = substatRolls ?? Array.Empty<GearStatModifier>();
    }

    public bool Changed { get; }
    public int CrystalsConsumed { get; }
    public int XpApplied { get; }
    public int LevelsGained { get; }
    public bool ReachedMaxLevel { get; }

    // Per-roll deltas reported by the milestone substat progression, in the order rolled.
    // Empty when no milestone was crossed (and always empty for Trash, whose max level is 1).
    public IReadOnlyList<GearStatModifier> SubstatRolls { get; }
}

// Applies Arcane Crystal XP to a target GearInstance.
//
// Rules (v1):
// - XP per crystal = rules.ArcaneCrystalXp (default 25).
// - XP per level = quality.XpPerLevel (default 100, editable per quality).
// - Max level per quality = quality.MaxLevel.
// - On Enhance:
//     - If target already at max level, consume nothing.
//     - Otherwise consume as many crystals as needed to reach max level,
//       or all crystals in the source stack if not enough.
//     - Apply partial XP toward the next level if the leftover is below a full level.
// - Main stats are scaled with the new level (see GearStatScaling).
// - Substats are unchanged (future "every 4 levels" roll lives in GearInstance.RecalculateMainStatsForLevel).
public static class GearLevelingService
{
    public static GearEnhanceResult Enhance(
        GearInstance target,
        InventoryController inventory,
        int materialInventorySlot,
        GearGenerationRules rules)
    {
        if (target == null || inventory == null || rules == null)
            return default;

        var qualityRules = rules.GetQualityRules(target.Quality);
        if (qualityRules == null)
            return default;

        var xpPerLevel = Math.Max(1, qualityRules.XpPerLevel);
        var xpPerCrystal = Math.Max(1, rules.ArcaneCrystalXp);
        var maxLevel = Math.Max(1, qualityRules.MaxLevel);

        if (target.Level >= maxLevel)
            return new GearEnhanceResult(false, 0, 0, 0, true, Array.Empty<GearStatModifier>());

        if (!inventory.TryGetEntry(materialInventorySlot, out var entry) ||
            entry is not InventoryStackEntry stackEntry)
            return default;

        var item = stackEntry.Stack.Item;
        if (item == null ||
            !string.Equals(item.Id, GearLevelingMaterials.ArcaneCrystalId, StringComparison.Ordinal))
            return default;

        var available = stackEntry.Stack.Quantity;
        if (available <= 0)
            return default;

        // XP needed to reach max level from where we are now.
        var levelsRemaining = maxLevel - target.Level;
        var xpNeededToMax = (long)levelsRemaining * xpPerLevel - target.CurrentXp;
        if (xpNeededToMax <= 0)
        {
            target.CurrentXp = 0;
            return new GearEnhanceResult(false, 0, 0, 0, true, Array.Empty<GearStatModifier>());
        }

        // Ceil-divide so the last crystal that crosses the threshold is still spent.
        var crystalsForMax = (int)((xpNeededToMax + xpPerCrystal - 1) / xpPerCrystal);
        var crystalsToConsume = Math.Min(available, crystalsForMax);

        var consumed = inventory.TryConsumeFromStackSlot(
            materialInventorySlot, GearLevelingMaterials.ArcaneCrystalId, crystalsToConsume);
        if (consumed <= 0)
            return default;

        var startLevel = target.Level;
        var xpToApply = (long)consumed * xpPerCrystal;
        var totalXp = (long)target.CurrentXp + xpToApply;

        var gainedLevels = 0;
        while (target.Level < maxLevel && totalXp >= xpPerLevel)
        {
            totalXp -= xpPerLevel;
            target.Level++;
            gainedLevels++;
        }

        if (target.Level >= maxLevel)
        {
            target.Level = maxLevel;
            target.CurrentXp = 0;
        }
        else
        {
            target.CurrentXp = (int)totalXp;
        }

        IReadOnlyList<GearStatModifier> rolls = Array.Empty<GearStatModifier>();
        if (gainedLevels > 0)
        {
            // Roll substats first so RecalculateMainStatsForLevel only has to walk MainStats.
            rolls = GearSubstatProgression.ApplyMilestoneRolls(target, startLevel, target.Level, rules);
            target.RecalculateMainStatsForLevel(rules);
        }

        return new GearEnhanceResult(
            changed: true,
            crystalsConsumed: consumed,
            xpApplied: (int)Math.Min(int.MaxValue, xpToApply),
            levelsGained: gainedLevels,
            reachedMaxLevel: target.Level >= maxLevel,
            substatRolls: rolls);
    }

    public static int GetMaxLevel(GearInstance gear, GearGenerationRules rules)
    {
        if (gear == null || rules == null)
            return 1;
        var q = rules.GetQualityRules(gear.Quality);
        return q != null ? Math.Max(1, q.MaxLevel) : 1;
    }

    public static int GetXpPerLevel(GearInstance gear, GearGenerationRules rules)
    {
        if (gear == null || rules == null)
            return 100;
        var q = rules.GetQualityRules(gear.Quality);
        return q != null ? Math.Max(1, q.XpPerLevel) : 100;
    }
}
