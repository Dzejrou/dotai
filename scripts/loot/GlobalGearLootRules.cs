using Godot;

using System;

[GlobalClass]
public partial class GlobalGearLootRules : Resource
{
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float DropChance { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "1,8,1")]
    public int RollCount { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<GlobalGearLootLevelBand> LevelBands { get; set; } = new();

    public bool TryRollGear(
        int actorLevel,
        RandomNumberGenerator random,
        GearGenerationRules gearGenerationRules,
        out GearInstance gear)
    {
        gear = null;

        if (random == null || gearGenerationRules == null)
            return false;

        var chance = Mathf.Clamp(DropChance, 0.0f, 1.0f);
        if (chance <= 0.0f)
            return false;

        if (random.Randf() > chance)
            return false;

        var band = ResolveLevelBand(actorLevel);
        if (band == null)
            return false;

        if (!band.TryPickQuality(random, out var quality))
            return false;

        var slot = PickSlot(random);
        gear = GearGenerator.Generate(slot, quality, gearGenerationRules);
        return gear != null;
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
