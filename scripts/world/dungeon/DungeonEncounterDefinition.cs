using Godot;

using System;

[GlobalClass]
public partial class DungeonEncounterDefinition : Resource
{
    [Export]
    public string EncounterId { get; set; } = string.Empty;

    [Export(PropertyHint.Range, "1,64,1")]
    public int MinSpawnCount { get; set; } = 5;

    [Export(PropertyHint.Range, "1,64,1")]
    public int MaxSpawnCount { get; set; } = 8;

    [Export]
    public Godot.Collections.Array<DungeonEnemyOption> EnemyOptions { get; set; } = new();

    public bool IsConfigured
    {
        get
        {
            if (EnemyOptions == null || EnemyOptions.Count == 0)
                return false;

            foreach (var option in EnemyOptions)
            {
                if (option?.IsConfigured == true)
                    return true;
            }

            return false;
        }
    }

    public int GetResolvedMinSpawnCount()
    {
        return Math.Max(1, Math.Min(MinSpawnCount, MaxSpawnCount));
    }

    public int GetResolvedMaxSpawnCount()
    {
        return Math.Max(GetResolvedMinSpawnCount(), Math.Max(MinSpawnCount, MaxSpawnCount));
    }

    public PackedScene RollEnemyScene(RandomNumberGenerator random)
    {
        if (random == null || EnemyOptions == null || EnemyOptions.Count == 0)
            return null;

        var totalWeight = 0;
        foreach (var option in EnemyOptions)
        {
            if (option?.IsConfigured != true)
                continue;

            totalWeight += option.Weight;
        }

        if (totalWeight <= 0)
            return null;

        var roll = random.RandiRange(1, totalWeight);
        var cumulativeWeight = 0;
        foreach (var option in EnemyOptions)
        {
            if (option?.IsConfigured != true)
                continue;

            cumulativeWeight += option.Weight;
            if (roll <= cumulativeWeight)
                return option.EnemyScene;
        }

        return null;
    }
}
