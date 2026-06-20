using System;

// Plain, serializable save representation of one finalized DungeonRunRecord. Conversion to/from
// the runtime record is explicit so runtime objects are never serialized directly. Outcome is
// stored as its enum name for forward-compatible, human-readable saves.
public sealed class DungeonRunRecordSaveData
{
    public string Outcome { get; set; }
    public ulong Seed { get; set; }
    public int StartingRoomLevel { get; set; }
    public int PlannedRunLength { get; set; }
    public int RoomsCleared { get; set; }
    public int EnemiesKilled { get; set; }
    public int PlayerDeaths { get; set; }
    public int BossesDefeated { get; set; }
    public int FurthestRoomIndex { get; set; }
    public int FurthestRoomLevel { get; set; }

    public static DungeonRunRecordSaveData FromRecord(DungeonRunRecord record)
    {
        return new DungeonRunRecordSaveData
        {
            Outcome = record.Outcome.ToString(),
            Seed = record.Seed,
            StartingRoomLevel = record.StartingRoomLevel,
            PlannedRunLength = record.PlannedRunLength,
            RoomsCleared = record.RoomsCleared,
            EnemiesKilled = record.EnemiesKilled,
            PlayerDeaths = record.PlayerDeaths,
            BossesDefeated = record.BossesDefeated,
            FurthestRoomIndex = record.FurthestRoomIndex,
            FurthestRoomLevel = record.FurthestRoomLevel,
        };
    }

    // Validates this entry independently and rebuilds the runtime record. Returns false (so the
    // caller skips this record and preserves its neighbors) for an unknown outcome or any
    // impossible negative value.
    public bool TryToRecord(out DungeonRunRecord record)
    {
        record = null;

        if (!Enum.TryParse(Outcome, ignoreCase: false, out DungeonRunOutcome outcome) ||
            !Enum.IsDefined(typeof(DungeonRunOutcome), outcome))
        {
            return false;
        }

        if (StartingRoomLevel < 0 ||
            PlannedRunLength < 0 ||
            RoomsCleared < 0 ||
            EnemiesKilled < 0 ||
            PlayerDeaths < 0 ||
            BossesDefeated < 0 ||
            FurthestRoomIndex < 0 ||
            FurthestRoomLevel < 0)
        {
            return false;
        }

        record = new DungeonRunRecord(
            outcome,
            Seed,
            StartingRoomLevel,
            PlannedRunLength,
            RoomsCleared,
            EnemiesKilled,
            PlayerDeaths,
            BossesDefeated,
            FurthestRoomIndex,
            FurthestRoomLevel);
        return true;
    }
}
