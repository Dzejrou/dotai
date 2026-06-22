using Godot;

// Centrally inspector-editable reward tuning for dungeon runs: how a completed run's finalized
// score converts into saved Points (the player-facing "DP"). Kept data-driven for the same reason
// as DungeonDifficultyRules so the economy can be retuned without code changes. The shipped
// defaults live in resources/world/dungeon/dungeon_reward_rules.tres and are mirrored by
// CreateDefault() so a missing-resource fallback and the verifiers agree with the authored values.
//
// "Points" is the generic economy term used throughout the code; the displayed "DP" label is
// presentation text only and never appears as a code identifier.
[GlobalClass]
public partial class DungeonRewardRules : Resource
{
    // Finalized score required to earn a single Point. Must be positive; a non-positive value is
    // treated as misconfiguration and yields zero Points rather than dividing by zero.
    [Export]
    public int ScorePerPoint { get; set; } = DefaultScorePerPoint;

    // Upper bound on the Points a single run may award. Zero means uncapped; any positive value caps
    // the award. Negative values behave like a zero cap (uncapped) but are not expected.
    [Export]
    public int MaximumPointsPerRun { get; set; } = DefaultMaximumPointsPerRun;

    public const int DefaultScorePerPoint = 100;
    public const int DefaultMaximumPointsPerRun = 0;

    // Builds an instance with the shipped defaults, mirroring dungeon_reward_rules.tres so a missing
    // or unassigned resource still produces correct behavior and verifiers can construct rules
    // without loading the resource.
    public static DungeonRewardRules CreateDefault()
    {
        return new DungeonRewardRules
        {
            ScorePerPoint = DefaultScorePerPoint,
            MaximumPointsPerRun = DefaultMaximumPointsPerRun,
        };
    }

    // Converts a completed run's finalized (difficulty-adjusted) score into earned Points:
    //
    //   rawPoints = floor(finalScore / ScorePerPoint)
    //   result    = MaximumPointsPerRun > 0 ? min(rawPoints, MaximumPointsPerRun) : rawPoints
    //
    // Fails safe to zero for a non-positive ScorePerPoint (no division by zero, no accidental
    // reward) and for a non-positive score. A finalScore legitimately below ScorePerPoint yields
    // zero. Integer division floors for the non-negative inputs the finalize path produces.
    public int PointsForScore(int finalScore)
    {
        if (ScorePerPoint <= 0 || finalScore <= 0)
            return 0;

        var rawPoints = finalScore / ScorePerPoint;
        if (MaximumPointsPerRun > 0 && rawPoints > MaximumPointsPerRun)
            return MaximumPointsPerRun;

        return rawPoints;
    }
}
