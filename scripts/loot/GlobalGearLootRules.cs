using Godot;

using System;

public enum GlobalGearLootRollFailureReason
{
    None,
    MissingConfig,
    ChanceMiss,
    NoLevelBand,
    NoQualityWeight,
    GenerationFailure,
}

public readonly struct GlobalGearLootRollResult
{
    public GlobalGearLootRollResult(
        bool success,
        GlobalGearLootRollFailureReason failureReason,
        ItemQuality? quality,
        EquipmentSlot? slot)
    {
        Success = success;
        FailureReason = failureReason;
        Quality = quality;
        Slot = slot;
    }

    public bool Success { get; }
    public GlobalGearLootRollFailureReason FailureReason { get; }
    public ItemQuality? Quality { get; }
    public EquipmentSlot? Slot { get; }
}

[GlobalClass]
public partial class GlobalGearLootRules : Resource
{
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float DropChance { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "1,8,1")]
    public int RollCount { get; set; } = 1;

    [Export(PropertyHint.Range, "0,8,1")]
    public int NormalRankRollBonus { get; set; } = 0;

    [Export(PropertyHint.Range, "0,8,1")]
    public int EliteRankRollBonus { get; set; } = 1;

    [Export(PropertyHint.Range, "0,8,1")]
    public int BossRankRollBonus { get; set; } = 2;

    [Export]
    public Godot.Collections.Array<GlobalGearLootLevelBand> LevelBands { get; set; } = new();

    public int GetRankRollBonus(ActorRank rank) => rank switch
    {
        ActorRank.Elite => Math.Max(0, EliteRankRollBonus),
        ActorRank.Boss => Math.Max(0, BossRankRollBonus),
        _ => Math.Max(0, NormalRankRollBonus),
    };

    public int GetEffectiveRollCount(ActorRank rank)
    {
        return Math.Max(1, RollCount) + GetRankRollBonus(rank);
    }

    public bool TryRollGear(
        int actorLevel,
        RandomNumberGenerator random,
        GearGenerationRules gearGenerationRules,
        out GearInstance gear)
    {
        return TryRollGear(actorLevel, random, gearGenerationRules, out gear, out _);
    }

    public bool TryRollGear(
        int actorLevel,
        RandomNumberGenerator random,
        GearGenerationRules gearGenerationRules,
        out GearInstance gear,
        out GlobalGearLootRollResult result)
    {
        gear = null;

        if (random == null || gearGenerationRules == null)
        {
            result = new GlobalGearLootRollResult(false, GlobalGearLootRollFailureReason.MissingConfig, null, null);
            return false;
        }

        var chance = Mathf.Clamp(DropChance, 0.0f, 1.0f);
        if (chance <= 0.0f || random.Randf() > chance)
        {
            result = new GlobalGearLootRollResult(false, GlobalGearLootRollFailureReason.ChanceMiss, null, null);
            return false;
        }

        var band = ResolveLevelBand(actorLevel);
        if (band == null)
        {
            result = new GlobalGearLootRollResult(false, GlobalGearLootRollFailureReason.NoLevelBand, null, null);
            return false;
        }

        if (!band.TryPickQuality(random, out var quality))
        {
            result = new GlobalGearLootRollResult(false, GlobalGearLootRollFailureReason.NoQualityWeight, null, null);
            return false;
        }

        var slot = PickSlot(random);
        gear = GearGenerator.Generate(slot, quality, gearGenerationRules);
        if (gear == null)
        {
            result = new GlobalGearLootRollResult(false, GlobalGearLootRollFailureReason.GenerationFailure, quality, slot);
            return false;
        }

        result = new GlobalGearLootRollResult(true, GlobalGearLootRollFailureReason.None, quality, slot);
        return true;
    }

    private GlobalGearLootLevelBand ResolveLevelBand(int actorLevel)
    {
        GlobalGearLootLevelBand best = null;
        var bestMinLevel = int.MinValue;

        foreach (var band in LevelBands)
        {
            if (band == null)
                continue;
            if (band.MinLevel > actorLevel)
                continue;
            if (band.MinLevel <= bestMinLevel)
                continue;

            bestMinLevel = band.MinLevel;
            best = band;
        }

        return best;
    }

    private static EquipmentSlot PickSlot(RandomNumberGenerator random)
    {
        var values = Enum.GetValues<EquipmentSlot>();
        if (values.Length == 0)
            return EquipmentSlot.Head;

        var index = random.RandiRange(0, values.Length - 1);
        return values[index];
    }
}
