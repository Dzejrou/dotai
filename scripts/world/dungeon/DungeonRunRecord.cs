using System;

// Immutable snapshot of a finalized dungeon run: the live statistics captured at finalization
// plus the explicit outcome. Built exactly once by Dungeon.FinalizeRun and never mutated, so a
// later stat change cannot retroactively alter a recorded run.
//
// This slice keeps records in memory only; persistence and the History UI are later slices.
public sealed class DungeonRunRecord
{
    // Captures a finalized run from its live stats. Used by Dungeon.FinalizeRun.
    public DungeonRunRecord(DungeonRunStats stats, DungeonRunOutcome outcome)
        : this(
            outcome,
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
    // save layer converts through plain fields rather than serializing runtime objects.
    public DungeonRunRecord(
        DungeonRunOutcome outcome,
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
