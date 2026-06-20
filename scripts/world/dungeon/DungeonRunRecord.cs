using System;

// Immutable snapshot of a finalized dungeon run: the live statistics captured at finalization
// plus the explicit outcome. Built exactly once by Dungeon.FinalizeRun and never mutated, so a
// later stat change cannot retroactively alter a recorded run.
//
// This slice keeps records in memory only; persistence and the History UI are later slices.
public sealed class DungeonRunRecord
{
    public DungeonRunRecord(DungeonRunStats stats, DungeonRunOutcome outcome)
    {
        if (stats == null)
            throw new ArgumentNullException(nameof(stats));

        Outcome = outcome;
        Seed = stats.Seed;
        StartingRoomLevel = stats.StartingRoomLevel;
        PlannedRunLength = stats.PlannedRunLength;
        RoomsCleared = stats.RoomsCleared;
        EnemiesKilled = stats.EnemiesKilled;
        PlayerDeaths = stats.PlayerDeaths;
        BossesDefeated = stats.BossesDefeated;
        FurthestRoomIndex = stats.FurthestRoomIndex;
        FurthestRoomLevel = stats.FurthestRoomLevel;
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
