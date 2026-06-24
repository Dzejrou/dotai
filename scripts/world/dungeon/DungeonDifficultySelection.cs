using System;

// Immutable snapshot of one run's difficulty choices, captured when the run starts and never
// mutated afterward. Generated from the live HUB selections (or the rules defaults), then stored on
// the active run so plan generation, actor buffs, and score finalization all read the same fixed
// values instead of mutable HUB controls or current resource defaults.
//
// All reward adjustments are additive: the difficulty multiplier is 1 plus the sum of the five
// selected adjustments, with no per-option multiplication and no separate cap. With the shipped
// tables the multiplier ranges from 0.25x to 4.25x.
public sealed class DungeonDifficultySelection
{
    public DungeonDifficultySelection(
        int startingRoomLevel,
        int levelIncreasePerRoom,
        float healthPowerBonus,
        float resistanceBonus,
        float damageBonus,
        float startingLevelRewardAdjustment,
        float levelIncreaseRewardAdjustment,
        float healthPowerRewardAdjustment,
        float resistanceRewardAdjustment,
        float damageRewardAdjustment,
        bool hardcore = false)
    {
        StartingRoomLevel = Math.Max(1, startingRoomLevel);
        LevelIncreasePerRoom = Math.Max(0, levelIncreasePerRoom);
        HealthPowerBonus = healthPowerBonus;
        ResistanceBonus = resistanceBonus;
        DamageBonus = damageBonus;
        StartingLevelRewardAdjustment = startingLevelRewardAdjustment;
        LevelIncreaseRewardAdjustment = levelIncreaseRewardAdjustment;
        HealthPowerRewardAdjustment = healthPowerRewardAdjustment;
        ResistanceRewardAdjustment = resistanceRewardAdjustment;
        DamageRewardAdjustment = damageRewardAdjustment;
        Hardcore = hardcore;
    }

    // Selected gameplay values.
    public int StartingRoomLevel { get; }
    public int LevelIncreasePerRoom { get; }
    public float HealthPowerBonus { get; }
    public float ResistanceBonus { get; }
    public float DamageBonus { get; }

    // Whether this run is hardcore: a player death during the run finalizes it as Failed instead of
    // opening the softcore death/retry page. Captured at run start and read by the death flow; it
    // carries no reward adjustment and never affects the difficulty multiplier.
    public bool Hardcore { get; }

    // Per-selection reward adjustments that sum into the multiplier.
    public float StartingLevelRewardAdjustment { get; }
    public float LevelIncreaseRewardAdjustment { get; }
    public float HealthPowerRewardAdjustment { get; }
    public float ResistanceRewardAdjustment { get; }
    public float DamageRewardAdjustment { get; }

    // Sum of all five reward adjustments (the "Reward bonus" shown in the HUB summary, e.g. +0.75).
    public float TotalRewardAdjustment =>
        StartingLevelRewardAdjustment
        + LevelIncreaseRewardAdjustment
        + HealthPowerRewardAdjustment
        + ResistanceRewardAdjustment
        + DamageRewardAdjustment;

    // Final additive multiplier applied to the base score at finalization.
    public float DifficultyMultiplier => 1.0f + TotalRewardAdjustment;

    // True when no enemy stat buff is selected, so actor buffing can be skipped entirely.
    public bool HasActorBuffs => HealthPowerBonus != 0.0f || ResistanceBonus != 0.0f || DamageBonus != 0.0f;

    // Resolves a selection from the rules tables by per-row option index. Out-of-range indices clamp
    // to a valid option; an empty table contributes its neutral default (level/increase fall back to
    // the supplied fallbacks, bonuses contribute nothing).
    public static DungeonDifficultySelection FromIndices(
        DungeonDifficultyRules rules,
        int startingLevelIndex,
        int levelIncreaseIndex,
        int healthPowerIndex,
        int resistanceIndex,
        int damageIndex,
        int fallbackStartingLevel = 1,
        int fallbackLevelIncrease = 1,
        bool hardcore = false)
    {
        if (rules == null)
            return CreateDefault(null, fallbackStartingLevel, fallbackLevelIncrease, hardcore);

        ResolveOption(rules.StartingLevelOptions, startingLevelIndex, fallbackStartingLevel, 0.0f, out var startingLevel, out var startingLevelReward);
        ResolveOption(rules.LevelIncreaseOptions, levelIncreaseIndex, fallbackLevelIncrease, 0.0f, out var levelIncrease, out var levelIncreaseReward);
        ResolveOption(rules.EnemyStatOptions, healthPowerIndex, 0.0f, 0.0f, out var healthPower, out var healthPowerReward);
        ResolveOption(rules.EnemyStatOptions, resistanceIndex, 0.0f, 0.0f, out var resistance, out var resistanceReward);
        ResolveOption(rules.EnemyStatOptions, damageIndex, 0.0f, 0.0f, out var damage, out var damageReward);

        return new DungeonDifficultySelection(
            (int)Math.Round(startingLevel),
            (int)Math.Round(levelIncrease),
            healthPower,
            resistance,
            damage,
            startingLevelReward,
            levelIncreaseReward,
            healthPowerReward,
            resistanceReward,
            damageReward,
            hardcore);
    }

    // Default selection: the first option of every row (or the supplied fallbacks when a table is
    // empty/missing), which yields the shipped defaults (level 10, +1 per room, 0% stats, 1.0x).
    public static DungeonDifficultySelection CreateDefault(
        DungeonDifficultyRules rules,
        int fallbackStartingLevel = 1,
        int fallbackLevelIncrease = 1,
        bool hardcore = false)
    {
        if (rules == null)
        {
            return new DungeonDifficultySelection(
                fallbackStartingLevel,
                fallbackLevelIncrease,
                0.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                hardcore);
        }

        return FromIndices(
            rules,
            DungeonDifficultyRules.DefaultStartingLevelIndex,
            DungeonDifficultyRules.DefaultLevelIncreaseIndex,
            DungeonDifficultyRules.DefaultEnemyStatIndex,
            DungeonDifficultyRules.DefaultEnemyStatIndex,
            DungeonDifficultyRules.DefaultEnemyStatIndex,
            fallbackStartingLevel,
            fallbackLevelIncrease,
            hardcore);
    }

    private static void ResolveOption(
        Godot.Collections.Array<DungeonDifficultyOption> options,
        int index,
        float fallbackValue,
        float fallbackReward,
        out float value,
        out float reward)
    {
        var clamped = DungeonDifficultyRules.ClampIndex(options, index);
        if (clamped < 0 || options[clamped] == null)
        {
            value = fallbackValue;
            reward = fallbackReward;
            return;
        }

        value = options[clamped].Value;
        reward = options[clamped].RewardAdjustment;
    }
}
