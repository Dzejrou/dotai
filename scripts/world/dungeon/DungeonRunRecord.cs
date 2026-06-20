using System;

// Immutable snapshot of a finalized dungeon run: the live statistics captured at finalization
// plus the explicit outcome and the moment the run was finalized. Built exactly once by
// Dungeon.FinalizeRun and never mutated, so a later stat change cannot retroactively alter a
// recorded run.
public sealed class DungeonRunRecord
{
    // Captures a finalized run from its live stats and the instant it was finalized. Used by
    // Dungeon.FinalizeRun.
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
            stats.FurthestRoomLevel)
    {
    }

    // Rebuilds a record from validated save data. Kept explicit (no runtime stats object) so the
    // save layer converts through plain fields rather than serializing runtime objects. FinishedAt
    // is nullable because saves written before timestamps existed have none; such legacy records
    // render with a fallback rather than being dropped.
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
        int furthestRoomLevel)
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
}
