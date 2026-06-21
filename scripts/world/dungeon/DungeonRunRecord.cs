using System;

// Immutable snapshot of a finalized dungeon run: the live statistics captured at finalization
// plus the explicit outcome and the moment the run was finalized. Built exactly once by
// Dungeon.FinalizeRun and never mutated, so a later stat change cannot retroactively alter a
// recorded run.
public sealed class DungeonRunRecord
{
    // Difficulty is not yet selectable, so every finalized run scores at this unmodified multiplier
    // in the current slice. Captured per record so a later difficulty slice can vary it without
    // touching the room-award lifecycle or rewriting already-recorded runs.
    public const float UnmodifiedDifficultyMultiplier = 1.0f;

    // Captures a finalized run from its live stats and the instant it was finalized. Used by
    // Dungeon.FinalizeRun. A freshly finalized run always carries score: base score from the live
    // stats, the unmodified multiplier, and the rounded final score.
    public DungeonRunRecord(DungeonRunStats stats, DungeonRunOutcome outcome, DateTimeOffset finishedAt)
        : this(
            outcome,
            finishedAt,
            RequireStats(stats).Seed,
            stats.StartingRoomLevel,
            stats.PlannedRunLength,
            stats.RoomsCleared,
            stats.EnemiesKilled,
            stats.PlayerDeaths,
            stats.BossesDefeated,
            stats.FurthestRoomIndex,
            stats.FurthestRoomLevel,
            stats.BaseScore,
            UnmodifiedDifficultyMultiplier,
            ComputeFinalScore(stats.BaseScore, UnmodifiedDifficultyMultiplier))
    {
    }

    // Rebuilds a record from validated save data. Kept explicit (no runtime stats object) so the
    // save layer converts through plain fields rather than serializing runtime objects. FinishedAt
    // is nullable because saves written before timestamps existed have none; such legacy records
    // render with a fallback rather than being dropped. The score trio is likewise nullable: a save
    // written before scoring existed (or one whose score data was malformed) loads as unknown
    // score, distinct from a legitimate zero score.
    public DungeonRunRecord(
        DungeonRunOutcome outcome,
        DateTimeOffset? finishedAt,
        ulong seed,
        int startingRoomLevel,
        int plannedRunLength,
        int roomsCleared,
        int enemiesKilled,
        int playerDeaths,
        int bossesDefeated,
        int furthestRoomIndex,
        int furthestRoomLevel,
        int? baseScore = null,
        float? difficultyMultiplier = null,
        int? finalScore = null)
    {
        Outcome = outcome;
        FinishedAt = finishedAt;
        Seed = seed;
        StartingRoomLevel = startingRoomLevel;
        PlannedRunLength = plannedRunLength;
        RoomsCleared = roomsCleared;
        EnemiesKilled = enemiesKilled;
        PlayerDeaths = playerDeaths;
        BossesDefeated = bossesDefeated;
        FurthestRoomIndex = furthestRoomIndex;
        FurthestRoomLevel = furthestRoomLevel;
        BaseScore = baseScore;
        DifficultyMultiplier = difficultyMultiplier;
        FinalScore = finalScore;
    }

    // Rounds a base score by a difficulty multiplier into the final score. Centralized so the
    // finalize-from-stats path and any future difficulty slice round identically.
    public static int ComputeFinalScore(int baseScore, float difficultyMultiplier)
    {
        return (int)Math.Round((double)baseScore * difficultyMultiplier, MidpointRounding.AwayFromZero);
    }

    private static DungeonRunStats RequireStats(DungeonRunStats stats)
    {
        return stats ?? throw new ArgumentNullException(nameof(stats));
    }

    public DungeonRunOutcome Outcome { get; }

    // When the run was finalized. Null only for legacy saved records written before timestamps
    // existed; the History UI shows a fallback for those.
    public DateTimeOffset? FinishedAt { get; }

    public ulong Seed { get; }
    public int StartingRoomLevel { get; }
    public int PlannedRunLength { get; }
    public int RoomsCleared { get; }
    public int EnemiesKilled { get; }
    public int PlayerDeaths { get; }
    public int BossesDefeated { get; }
    public int FurthestRoomIndex { get; }
    public int FurthestRoomLevel { get; }

    // Finalized score snapshot. All three are populated for any run finalized in-game. They are
    // null only for legacy saved records written before scoring existed (or records whose saved
    // score was malformed); the History UI shows a fallback for those rather than implying a zero
    // score. FinalScore is the rounded BaseScore * DifficultyMultiplier captured at finalization.
    public int? BaseScore { get; }
    public float? DifficultyMultiplier { get; }
    public int? FinalScore { get; }
}
