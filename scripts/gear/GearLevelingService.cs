using Godot;

using System;
using System.Collections.Generic;

// Stack items that grant gear XP qualify as gear leveling crystals; the per-unit
// XP is read from InventoryItemDefinition.GearXpPerUnit.
public static class GearLevelingMaterials
{
    public static bool IsCrystal(InventoryItemDefinition item)
        => item != null && item.GearXpPerUnit > 0;
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
        IReadOnlyList<GearStatModifier> substatRolls,
        int gearXpSpent,
        int gearXpGained)
    {
        Changed = changed;
        MaterialKind = materialKind;
        CrystalsConsumed = crystalsConsumed;
        XpApplied = xpApplied;
        LevelsGained = levelsGained;
        ReachedMaxLevel = reachedMaxLevel;
        SubstatRolls = substatRolls ?? Array.Empty<GearStatModifier>();
        GearXpSpent = gearXpSpent;
        GearXpGained = gearXpGained;
    }

    public bool Changed { get; }
    public GearEnhanceMaterialKind MaterialKind { get; }
    public int CrystalsConsumed { get; }

    // XP actually used to advance the target gear (from bank + material). Excludes
    // crystal partials lost to the level cap and excludes fodder overflow banked back.
    public int XpApplied { get; }
    public int LevelsGained { get; }
    public bool ReachedMaxLevel { get; }

    // Per-roll deltas reported by the milestone substat progression, in the order rolled.
    // Empty when no milestone was crossed (and always empty for Trash, whose max level is 1).
    public IReadOnlyList<GearStatModifier> SubstatRolls { get; }

    // XP drained from InventoryController.GearXp before any material was consumed.
    public int GearXpSpent { get; }

    // XP added back into InventoryController.GearXp (fodder overflow into bank).
    public int GearXpGained { get; }
}

// Applies XP to a target GearInstance from three potential sources, in order:
//
//   1. Stored InventoryController.GearXp (drained first; never consumes a material if
//      the bank alone hits max level).
//   2. A crystal stack — any InventoryItemDefinition with GearXpPerUnit > 0. Crystals
//      never overflow into the bank; consumption is capped at what's needed to reach max.
//   3. Gear fodder (an inventory gear entry). XP = baseFodderXp +
//      floor(totalInvestedXp * FodderInvestedXpRefundRate). Overflow past max is
//      added back into InventoryController.GearXp.
//
// Per-level XP requirement is read from the target quality's ExperienceToNextLevel
// table; missing/zero entries fall back to GearQualityRules.FallbackXpPerLevel.
// On level-up substat milestone rolls fire and main stats are rescaled.
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
            return EmptyResult(reachedMax: true);

        var startLevel = target.Level;
        var allRolls = new List<GearStatModifier>();
        var gearXpSpent = 0;
        var gearXpGained = 0;
        var xpAppliedTotal = 0;
        var crystalsConsumed = 0;
        var consumedKind = GearEnhanceMaterialKind.None;

        // 1) Spend stored GearXp first, capped at the XP needed to reach max.
        var bank = inventory.GearXp;
        if (bank > 0)
        {
            var needed = ComputeXpNeededToMax(target, qualityRules);
            var spend = (int)Math.Min(bank, needed);
            if (spend > 0 && inventory.TrySpendGearXp(spend))
            {
                gearXpSpent = spend;
                var pre = target.Level;
                var (_, leftover, rolls) = ApplyXpAndRollMilestones(target, rules, qualityRules, spend, pre);
                allRolls.AddRange(rolls);
                // Capped at needed, so leftover is 0 here; nothing to bank.
                xpAppliedTotal += (int)Math.Clamp(spend - leftover, 0L, int.MaxValue);
            }
        }

        // 2) Bank alone reached max — don't touch the material slot.
        if (target.Level >= maxLevel)
        {
            return new GearEnhanceResult(
                changed: gearXpSpent > 0,
                materialKind: GearEnhanceMaterialKind.None,
                crystalsConsumed: 0,
                xpApplied: xpAppliedTotal,
                levelsGained: target.Level - startLevel,
                reachedMaxLevel: true,
                substatRolls: allRolls,
                gearXpSpent: gearXpSpent,
                gearXpGained: 0);
        }

        // 3) Consume material.
        if (!inventory.TryGetEntry(materialInventorySlot, out var entry))
            return PartialResult();

        if (entry is InventoryStackEntry stackEntry)
        {
            var item = stackEntry.Stack?.Item;
            if (!GearLevelingMaterials.IsCrystal(item))
                return PartialResult();

            var xpPerCrystal = Math.Max(1, item.GearXpPerUnit);
            var available = stackEntry.Stack.Quantity;
            if (available <= 0)
                return PartialResult();

            var needed = ComputeXpNeededToMax(target, qualityRules);
            if (needed <= 0)
                return PartialResult();

            var crystalsForMax = (int)Math.Min(int.MaxValue, (needed + xpPerCrystal - 1) / xpPerCrystal);
            var crystalsToConsume = Math.Min(available, crystalsForMax);

            var consumed = inventory.TryConsumeFromStackSlot(
                materialInventorySlot, item.Id, crystalsToConsume);
            if (consumed <= 0)
                return PartialResult();

            consumedKind = GearEnhanceMaterialKind.Crystal;
            crystalsConsumed = consumed;
            var xp = (long)consumed * xpPerCrystal;
            var pre = target.Level;
            var (_, leftover, rolls) = ApplyXpAndRollMilestones(target, rules, qualityRules, xp, pre);
            allRolls.AddRange(rolls);
            xpAppliedTotal += (int)Math.Clamp(xp - leftover, 0L, int.MaxValue);
            // Crystals don't bank overflow by design: at most one crystal of partial XP
            // is lost to the level cap.
            return FinalResult();
        }

        if (entry is InventoryGearEntry gearEntry)
        {
            if (gearEntry.Gear == null)
                return PartialResult();
            if (ReferenceEquals(gearEntry.Gear, target))
                return PartialResult();

            var fodderGear = gearEntry.Gear;
            var fodderXp = ComputeFodderXp(fodderGear, rules);

            var taken = inventory.TakeEntry(materialInventorySlot);
            if (taken is not InventoryGearEntry takenGear || !ReferenceEquals(takenGear.Gear, fodderGear))
            {
                if (taken is InventoryGearEntry returnedGear)
                    inventory.TryPlaceGear(materialInventorySlot, returnedGear.Gear);
                return PartialResult();
            }

            consumedKind = GearEnhanceMaterialKind.GearFodder;
            var pre = target.Level;
            var (_, leftover, rolls) = ApplyXpAndRollMilestones(target, rules, qualityRules, fodderXp, pre);
            allRolls.AddRange(rolls);
            xpAppliedTotal += (int)Math.Clamp(fodderXp - leftover, 0L, int.MaxValue);

            // Fodder overflow past the level cap is banked back into the inventory.
            if (leftover > 0 && target.Level >= maxLevel)
            {
                inventory.AddGearXp(leftover);
                gearXpGained = leftover;
            }
            return FinalResult();
        }

        return PartialResult();

        GearEnhanceResult FinalResult() => new(
            changed: true,
            materialKind: consumedKind,
            crystalsConsumed: crystalsConsumed,
            xpApplied: xpAppliedTotal,
            levelsGained: target.Level - startLevel,
            reachedMaxLevel: target.Level >= maxLevel,
            substatRolls: allRolls,
            gearXpSpent: gearXpSpent,
            gearXpGained: gearXpGained);

        // Material couldn't be consumed (or none was referenced) but bank XP may have already
        // been spent and applied. Report whatever happened.
        GearEnhanceResult PartialResult() => new(
            changed: gearXpSpent > 0,
            materialKind: GearEnhanceMaterialKind.None,
            crystalsConsumed: 0,
            xpApplied: xpAppliedTotal,
            levelsGained: target.Level - startLevel,
            reachedMaxLevel: target.Level >= maxLevel,
            substatRolls: allRolls,
            gearXpSpent: gearXpSpent,
            gearXpGained: 0);
    }

    // Store mode: with no target selected, send a fodder gear's full computed XP straight
    // into InventoryController.GearXp. Returns the XP banked (0 if the slot doesn't
    // resolve to a usable inventory gear entry).
    public static int StoreFodder(
        InventoryController inventory,
        int materialInventorySlot,
        GearGenerationRules rules)
    {
        if (inventory == null || rules == null)
            return 0;
        if (!inventory.TryGetEntry(materialInventorySlot, out var entry))
            return 0;
        if (entry is not InventoryGearEntry gearEntry || gearEntry.Gear == null)
            return 0;

        var fodderGear = gearEntry.Gear;
        var xp = ComputeFodderXp(fodderGear, rules);

        var taken = inventory.TakeEntry(materialInventorySlot);
        if (taken is not InventoryGearEntry takenGear || !ReferenceEquals(takenGear.Gear, fodderGear))
        {
            if (taken is InventoryGearEntry returnedGear)
                inventory.TryPlaceGear(materialInventorySlot, returnedGear.Gear);
            return 0;
        }

        if (xp > 0)
            inventory.AddGearXp(xp);
        return xp;
    }

    public static int GetMaxLevel(GearInstance gear, GearGenerationRules rules)
    {
        if (gear == null || rules == null)
            return 1;
        var q = rules.GetQualityRules(gear.Quality);
        return q != null ? Math.Max(1, q.MaxLevel) : 1;
    }

    public static int GetRequiredExperienceForCurrentLevel(GearInstance gear, GearGenerationRules rules)
    {
        if (gear == null || rules == null)
            return GearQualityRules.FallbackXpPerLevel;
        var q = rules.GetQualityRules(gear.Quality);
        return q != null ? q.GetRequiredExperienceForLevel(gear.Level) : GearQualityRules.FallbackXpPerLevel;
    }

    public static long GetTotalAccumulatedExperience(GearInstance gear, GearGenerationRules rules)
    {
        if (gear == null || rules == null)
            return 0;
        var q = rules.GetQualityRules(gear.Quality);
        if (q == null)
            return Math.Max(0, gear.CurrentXp);
        return (long)q.GetTotalExperienceAtLevel(gear.Level) + Math.Max(0, gear.CurrentXp);
    }

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

    // Returns (levelsGained, leftoverXpAtCap, substatRolls). leftoverXpAtCap is the XP
    // that couldn't be applied because the target hit max level — callers decide whether
    // to bank or discard it. CurrentXp keeps in-level progress when max wasn't reached.
    private static (int levelsGained, int leftoverXpAtCap, IReadOnlyList<GearStatModifier> rolls)
        ApplyXpAndRollMilestones(
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

        var leftover = 0;
        if (target.Level >= maxLevel)
        {
            target.Level = maxLevel;
            leftover = (int)Math.Clamp(remaining, 0L, int.MaxValue);
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

        return (target.Level - startLevel, leftover, rolls);
    }

    private static GearEnhanceResult EmptyResult(bool reachedMax)
    {
        return new GearEnhanceResult(
            changed: false,
            materialKind: GearEnhanceMaterialKind.None,
            crystalsConsumed: 0,
            xpApplied: 0,
            levelsGained: 0,
            reachedMaxLevel: reachedMax,
            substatRolls: Array.Empty<GearStatModifier>(),
            gearXpSpent: 0,
            gearXpGained: 0);
    }
}
