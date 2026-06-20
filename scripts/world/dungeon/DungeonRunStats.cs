// Mutable, authoritative live statistics for one active dungeon run.
//
// Owned and mutated exclusively by Dungeon and exposed read-only to the HUB. There is
// intentionally no elapsed-time statistic. When a run is finalized these values are snapshotted
// into an immutable DungeonRunRecord and this instance is discarded.
public sealed class DungeonRunStats
{
    public DungeonRunStats(ulong seed, int startingRoomLevel, int plannedRunLength)
    {
        Seed = seed;
        StartingRoomLevel = startingRoomLevel;
        PlannedRunLength = plannedRunLength;
    }

    // Run identity and plan shape, fixed at run start.
    public ulong Seed { get; }
    public int StartingRoomLevel { get; }
    public int PlannedRunLength { get; }

    // Accumulated live counters.
    public int RoomsCleared { get; private set; }
    public int EnemiesKilled { get; private set; }
    public int PlayerDeaths { get; private set; }
    public int BossesDefeated { get; private set; }

    // Furthest reach, as a one-based room index and the level of that furthest room.
    public int FurthestRoomIndex { get; private set; }
    public int FurthestRoomLevel { get; private set; }

    public void IncrementRoomsCleared() => RoomsCleared++;

    public void IncrementEnemiesKilled() => EnemiesKilled++;

    public void IncrementPlayerDeaths() => PlayerDeaths++;

    public void IncrementBossesDefeated() => BossesDefeated++;

    // Records reaching a room, keeping the furthest one-based index and that room's level. Levels
    // increase monotonically with index in the current linear plan, so the furthest index also
    // carries the highest level reached.
    public void RecordRoomReached(int oneBasedIndex, int roomLevel)
    {
        if (oneBasedIndex <= FurthestRoomIndex)
            return;

        FurthestRoomIndex = oneBasedIndex;
        FurthestRoomLevel = roomLevel;
    }
}
