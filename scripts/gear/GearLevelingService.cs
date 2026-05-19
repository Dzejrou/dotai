using Godot;

using System;
using System.Collections.Generic;

// Item id of the crystal stack accepted by the leveling material slot.
public static class GearLevelingMaterials
{
    public const string ArcaneCrystalId = "arcane_crystal";
}

public enum GearEnhanceMaterialKind
{
    None,
    Crystal,
    GearFodder,
}

public readonly struct GearEnhanceResult
{
    public GearEnhanceResult(
        bool changed,
        GearEnhanceMaterialKind materialKind,
        int crystalsConsumed,
        int xpApplied,
        int levelsGained,
        bool reachedMaxLevel,
        IReadOnlyList<GearStatModifier> substatRolls)
    {
        Changed = changed;
        MaterialKind = materialKind;
        CrystalsConsumed = crystalsConsumed;
        XpApplied = xpApplied;
        LevelsGained = levelsGained;
        ReachedMaxLevel = reachedMaxLevel;
        SubstatRolls = substatRolls ?? Array.Empty<GearStatModifier>();
    }

    public bool Changed { get; }
    public GearEnhanceMaterialKind MaterialKind { get; }
    public int CrystalsConsumed { get; }
    public int XpApplied { get; }
    public int LevelsGained { get; }
    public bool ReachedMaxLevel { get; }

    // Per-roll deltas reported by the milestone substat progression, in the order rolled.
    // Empty when no milestone was crossed (and always empty for Trash, whose max level is 1).
    public IReadOnlyList<GearStatModifier> SubstatRolls { get; }
}

// Applies XP to a target GearInstance from one of two material sources:
//
//   - Arcane Crystal stack: XP per crystal = rules.ArcaneCrystalXp.
//   - Gear fodder (inventory gear, never equipped): XP = baseFodderXp +
//     floor(totalInvestedXp * FodderInvestedXpRefundRate), where invested XP is the
//     fodder gear's completed-level total from its own quality table plus its CurrentXp.
//
// Per-level XP requirement is read from the target quality's ExperienceToNextLevel
// table; a missing/zero entry falls back to GearQualityRules.FallbackXpPerLevel (100).
// On level-up:
//   - Substat milestone rolls fire if a milestone level was crossed.
//   - Main stats are rescaled with the new level (see GearStatScaling).
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

        var maxLevel = Math.Max(1, qualityRules.MaxLevel);
        if (target.Level >= maxLevel)
            return new GearEnhanceResult(false, GearEnhanceMaterialKind.None, 0, 0, 0, true, Array.Empty<GearStatModifier>());

        if (!inventory.TryGetEntry(materialInventorySlot, out var entry))
            return default;

        if (entry is InventoryStackEntry stackEntry)
        {
            var item = stackEntry.Stack?.Item;
            if (item == null ||
                !string.Equals(item.Id, GearLevelingMaterials.ArcaneCrystalId, StringComparison.Ordinal))
                return default;

            return EnhanceWithCrystals(target, inventory, materialInventorySlot, rules, qualityRules, stackEntry);
        }

        if (entry is InventoryGearEntry gearEntry)
        {
            if (gearEntry.Gear == null)
                return default;
            // Refuse self-fodder: target and fodder must not be the same GearInstance.
            if (ReferenceEquals(gearEntry.Gear, target))
                return default;

            return EnhanceWithGearFodder(target, inventory, materialInventorySlot, rules, qualityRules, gearEntry.Gear);
        }

        return default;
    }

    public static int GetMaxLevel(GearInstance gear, GearGenerationRules rules)
    {
        if (gear == null || rules == null)
            return 1;
        var q = rules.GetQualityRules(gear.Quality);
        return q != null ? Math.Max(1, q.MaxLevel) : 1;
    }

    // XP needed to advance from the gear's current level to the next level (table lookup).
    public static int GetRequiredExperienceForCurrentLevel(GearInstance gear, GearGenerationRules rules)
    {
        if (gear == null || rules == null)
            return GearQualityRules.FallbackXpPerLevel;
        var q = rules.GetQualityRules(gear.Quality);
        return q != null ? q.GetRequiredExperienceForLevel(gear.Level) : GearQualityRules.FallbackXpPerLevel;
    }

    // Completed-level XP total + CurrentXp.
    public static long GetTotalAccumulatedExperience(GearInstance gear, GearGenerationRules rules)
    {
        if (gear == null || rules == null)
            return 0;
        var q = rules.GetQualityRules(gear.Quality);
        if (q == null)
            return Math.Max(0, gear.CurrentXp);
        return (long)q.GetTotalExperienceAtLevel(gear.Level) + Math.Max(0, gear.CurrentXp);
    }

    // Fodder XP yield: baseFodderXp + floor(invested * refundRate). Returns 0 if the
    // fodder gear's quality rules are missing.
    public static int ComputeFodderXp(GearInstance fodderGear, GearGenerationRules rules)
    {
        if (fodderGear == null || rules == null)
            return 0;
        var q = rules.GetQualityRules(fodderGear.Quality);
        if (q == null)
            return 0;
        var baseFodder = Math.Max(0, q.BaseFodderXp);
        var invested = (long)q.GetTotalExperienceAtLevel(fodderGear.Level) + Math.Max(0, fodderGear.CurrentXp);
        var refundRate = Math.Clamp(rules.FodderInvestedXpRefundRate, 0.0f, 1.0f);
        var refund = (long)Math.Floor(invested * refundRate);
        var total = baseFodder + refund;
        return (int)Math.Clamp(total, 0L, int.MaxValue);
    }

    private static GearEnhanceResult EnhanceWithCrystals(
        GearInstance target,
        InventoryController inventory,
        int materialInventorySlot,
        GearGenerationRules rules,
        GearQualityRules qualityRules,
        InventoryStackEntry stackEntry)
    {
        var xpPerCrystal = Math.Max(1, rules.ArcaneCrystalXp);
        var available = stackEntry.Stack.Quantity;
        if (available <= 0)
            return default;

        var xpNeededToMax = ComputeXpNeededToMax(target, qualityRules);
        if (xpNeededToMax <= 0)
        {
            target.CurrentXp = 0;
            return new GearEnhanceResult(false, GearEnhanceMaterialKind.Crystal, 0, 0, 0, true, Array.Empty<GearStatModifier>());
        }

        // Ceil-divide so the last crystal that crosses the threshold is still spent.
        var crystalsForMax = (int)Math.Min(int.MaxValue, (xpNeededToMax + xpPerCrystal - 1) / xpPerCrystal);
        var crystalsToConsume = Math.Min(available, crystalsForMax);

        var consumed = inventory.TryConsumeFromStackSlot(
            materialInventorySlot, GearLevelingMaterials.ArcaneCrystalId, crystalsToConsume);
        if (consumed <= 0)
            return default;

        // TODO: future overflow XP -> Arcane Crystal refund. When fodder/crystals exceed
        // the XP needed to reach max level, convert the overflow back to crystals
        // (rounded down) and add them to inventory; drop in world if full.
        // Currently bounded by crystalsForMax so this path can't overshoot for crystals.

        var startLevel = target.Level;
        var xpAmount = (long)consumed * xpPerCrystal;
        var rolls = ApplyXpAndRollMilestones(target, rules, qualityRules, xpAmount, startLevel);

        return new GearEnhanceResult(
            changed: true,
            materialKind: GearEnhanceMaterialKind.Crystal,
            crystalsConsumed: consumed,
            xpApplied: (int)Math.Min(int.MaxValue, xpAmount),
            levelsGained: target.Level - startLevel,
            reachedMaxLevel: target.Level >= Math.Max(1, qualityRules.MaxLevel),
            substatRolls: rolls);
    }

    private static GearEnhanceResult EnhanceWithGearFodder(
        GearInstance target,
        InventoryController inventory,
        int materialInventorySlot,
        GearGenerationRules rules,
        GearQualityRules targetQualityRules,
        GearInstance fodderGear)
    {
        var fodderXp = ComputeFodderXp(fodderGear, rules);

        // Remove the fodder gear from inventory before applying XP — the inventory entry
        // is what authorises the spend, and the target levels up in place either way.
        // TODO: future overflow XP -> Arcane Crystal refund. If fodderXp exceeds the XP
        // needed to reach max level, convert the overflow back to crystals (rounded down)
        // and add them to inventory; drop in world if full.
        var taken = inventory.TakeEntry(materialInventorySlot);
        if (taken is not InventoryGearEntry takenGear || !ReferenceEquals(takenGear.Gear, fodderGear))
        {
            // Race: the inventory slot vanished or changed between Enhance dispatch and now.
            // Put it back if we accidentally took something else; otherwise just bail.
            if (taken != null)
            {
                if (taken is InventoryGearEntry returnedGear)
                    inventory.TryPlaceGear(materialInventorySlot, returnedGear.Gear);
                // No clean rollback for stacks — TakeEntry already emitted a change signal.
            }
            return default;
        }

        var startLevel = target.Level;
        var rolls = ApplyXpAndRollMilestones(target, rules, targetQualityRules, fodderXp, startLevel);

        return new GearEnhanceResult(
            changed: true,
            materialKind: GearEnhanceMaterialKind.GearFodder,
            crystalsConsumed: 0,
            xpApplied: fodderXp,
            levelsGained: target.Level - startLevel,
            reachedMaxLevel: target.Level >= Math.Max(1, targetQualityRules.MaxLevel),
            substatRolls: rolls);
    }

    private static long ComputeXpNeededToMax(GearInstance target, GearQualityRules qualityRules)
    {
        var maxLevel = Math.Max(1, qualityRules.MaxLevel);
        if (target.Level >= maxLevel)
            return 0;

        long needed = 0;
        for (var lvl = target.Level; lvl < maxLevel; lvl++)
            needed += qualityRules.GetRequiredExperienceForLevel(lvl);
        needed -= target.CurrentXp;
        return Math.Max(0, needed);
    }

    private static IReadOnlyList<GearStatModifier> ApplyXpAndRollMilestones(
        GearInstance target,
        GearGenerationRules rules,
        GearQualityRules qualityRules,
        long xpAmount,
        int startLevel)
    {
        var maxLevel = Math.Max(1, qualityRules.MaxLevel);
        var remaining = (long)Math.Max(0, target.CurrentXp) + Math.Max(0, xpAmount);

        while (target.Level < maxLevel)
        {
            var req = qualityRules.GetRequiredExperienceForLevel(target.Level);
            if (req <= 0)
                req = GearQualityRules.FallbackXpPerLevel;
            if (remaining < req)
                break;
            remaining -= req;
            target.Level++;
        }

        if (target.Level >= maxLevel)
        {
            target.Level = maxLevel;
            target.CurrentXp = 0;
        }
        else
        {
            target.CurrentXp = (int)Math.Clamp(remaining, 0L, int.MaxValue);
        }

        IReadOnlyList<GearStatModifier> rolls = Array.Empty<GearStatModifier>();
        if (target.Level > startLevel)
        {
            rolls = GearSubstatProgression.ApplyMilestoneRolls(target, startLevel, target.Level, rules);
            target.RecalculateMainStatsForLevel(rules);
        }
        return rolls;
    }
}
