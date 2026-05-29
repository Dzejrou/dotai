using Godot;

using System;

[GlobalClass]
public partial class ActorExperienceRewardRules : Resource
{
    [Export(PropertyHint.Range, "0.1,200,0.1,or_greater")]
    public float SameLevelKillsBase { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0,200,0.1,or_greater")]
    public float SameLevelKillsMaxBonus { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "1,200,1,or_greater")]
    public int SameLevelKillsMaxLevel { get; set; } = 60;

    [Export(PropertyHint.Range, "0,10,0.05,or_greater")]
    public float SameLevelKillsExponent { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0,100,0.05,or_greater")]
    public float NormalRankMultiplier { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0,100,0.05,or_greater")]
    public float EliteRankMultiplier { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0,100,0.05,or_greater")]
    public float BossRankMultiplier { get; set; } = 10.0f;

    [Export]
    public Godot.Collections.Array<ActorExperienceRewardLevelDifferenceEntry> LevelDifferenceMultipliers { get; set; } = new();

    public float GetSameLevelKills(int playerLevel)
    {
        var level = Math.Max(1, playerLevel);
        var maxLevel = Math.Max(1, SameLevelKillsMaxLevel);
        var baseKills = Math.Max(0.0f, SameLevelKillsBase);
        var maxBonus = Math.Max(0.0f, SameLevelKillsMaxBonus);
        var exponent = Math.Max(0.0f, SameLevelKillsExponent);
        var ratio = Math.Clamp((float)level / maxLevel, 0.0f, 1.0f);
        var bonus = maxBonus * MathF.Pow(ratio, exponent);
        return Math.Max(0.1f, baseKills + bonus);
    }

    public float GetRankMultiplier(ActorRank rank) => rank switch
    {
        ActorRank.Elite => Math.Max(0.0f, EliteRankMultiplier),
        ActorRank.Boss => Math.Max(0.0f, BossRankMultiplier),
        _ => Math.Max(0.0f, NormalRankMultiplier),
    };

    // Step function: returns the multiplier from the entry with the highest MinDifference <= levelDifference.
    // If LevelDifferenceMultipliers is empty, no shaping is applied (returns 1.0).
    // If the difference falls below every configured entry, returns 0.0 (no reward for greatly under-leveled foes).
    public float GetLevelDifferenceMultiplier(int levelDifference)
    {
        if (LevelDifferenceMultipliers == null || LevelDifferenceMultipliers.Count == 0)
            return 1.0f;

        ActorExperienceRewardLevelDifferenceEntry best = null;
        foreach (var entry in LevelDifferenceMultipliers)
        {
            if (entry == null || entry.MinDifference > levelDifference)
                continue;
            if (best == null || entry.MinDifference > best.MinDifference)
                best = entry;
        }

        return best == null ? 0.0f : Math.Max(0.0f, best.Multiplier);
    }
}
