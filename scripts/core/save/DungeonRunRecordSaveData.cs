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
    // caller skips this record and preserves its neighbors) for an unknown outcome, a missing
    // required value, or any impossible value.
    public bool TryToRecord(out DungeonRunRecord record)
    {
        record = null;

        if (!Enum.TryParse(Outcome, ignoreCase: false, out DungeonRunOutcome outcome) ||
            !Enum.IsDefined(typeof(DungeonRunOutcome), outcome))
        {
            return false;
        }

        // Required identity/progress values must be present and possible. Missing numeric fields
        // deserialize to 0, so this also rejects a record like { "Outcome": "Completed" } rather
        // than admitting a bogus level-0, zero-length run. The furthest room reached cannot lie
        // beyond the planned run.
        if (StartingRoomLevel < 1 ||
            PlannedRunLength < 1 ||
            FurthestRoomLevel < 1 ||
            FurthestRoomIndex < 1 ||
            FurthestRoomIndex > PlannedRunLength)
        {
            return false;
        }

        // Kill/death/boss counters and Seed may legitimately be 0, but none may be negative, and
        // rooms cleared cannot exceed the rooms reached (nor the planned length).
        if (EnemiesKilled < 0 ||
            PlayerDeaths < 0 ||
            BossesDefeated < 0 ||
            RoomsCleared < 0 ||
            RoomsCleared > FurthestRoomIndex ||
            RoomsCleared > PlannedRunLength)
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
