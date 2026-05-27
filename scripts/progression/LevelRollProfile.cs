using Godot;

using System;

[GlobalClass]
public partial class LevelRollProfile : Resource
{
    [Export]
    public Godot.Collections.Array<LevelRollOffsetEntry> Offsets { get; set; } = new();

    public int Roll(int roomLevel, RandomNumberGenerator random)
    {
        var baseLevel = Math.Max(1, roomLevel);
        if (Offsets == null || Offsets.Count == 0 || random == null)
            return baseLevel;

        var totalWeight = 0.0f;
        foreach (var entry in Offsets)
        {
            if (entry == null)
                continue;

            var weight = Math.Max(0.0f, entry.Weight);
            totalWeight += weight;
        }

        if (totalWeight <= 0.0f)
            return baseLevel;

        var roll = random.RandfRange(0.0f, totalWeight);
        var cumulative = 0.0f;
        LevelRollOffsetEntry lastEntry = null;
        foreach (var entry in Offsets)
        {
            if (entry == null)
                continue;

            var weight = Math.Max(0.0f, entry.Weight);
            if (weight <= 0.0f)
                continue;

            cumulative += weight;
            lastEntry = entry;
            if (roll <= cumulative)
                return Math.Max(1, baseLevel + entry.LevelOffset);
        }

        return lastEntry != null
            ? Math.Max(1, baseLevel + lastEntry.LevelOffset)
            : baseLevel;
    }
}
