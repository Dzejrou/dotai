using Godot;

using System;

[GlobalClass]
public partial class ActorLevelScalingRules : Resource
{
    [Export(PropertyHint.Range, "0,2,0.01")]
    public float HealthPerLevelGrowth { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float PowerPerLevelGrowth { get; set; } = 0.08f;

    [Export(PropertyHint.Range, "0,10,0.05")]
    public float NormalRankMultiplier { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0,10,0.05")]
    public float EliteRankMultiplier { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0,10,0.05")]
    public float BossRankMultiplier { get; set; } = 4.0f;

    public float GetRankMultiplier(ActorRank rank) => rank switch
    {
        ActorRank.Elite => Math.Max(0.0f, EliteRankMultiplier),
        ActorRank.Boss => Math.Max(0.0f, BossRankMultiplier),
        _ => Math.Max(0.0f, NormalRankMultiplier),
    };

    public float GetHealthMultiplier(int level, ActorRank rank)
    {
        var levelSteps = Math.Max(1, level) - 1;
        return (1.0f + Math.Max(0.0f, HealthPerLevelGrowth) * levelSteps) * GetRankMultiplier(rank);
    }

    public float GetPowerMultiplier(int level, ActorRank rank)
    {
        var levelSteps = Math.Max(1, level) - 1;
        return (1.0f + Math.Max(0.0f, PowerPerLevelGrowth) * levelSteps) * GetRankMultiplier(rank);
    }

    public int ScaleMaxHealth(int baseMaxHealth, int level, ActorRank rank)
    {
        var scaled = Mathf.RoundToInt(baseMaxHealth * GetHealthMultiplier(level, rank));
        return Math.Max(1, scaled);
    }

    public float ScalePower(float basePower, int level, ActorRank rank)
    {
        return Math.Max(0.0f, basePower * GetPowerMultiplier(level, rank));
    }
}
