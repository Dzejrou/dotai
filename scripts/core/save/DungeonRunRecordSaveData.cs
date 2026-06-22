using System;
using System.Globalization;

// Plain, serializable save representation of one finalized DungeonRunRecord. Conversion to/from
// the runtime record is explicit so runtime objects are never serialized directly. Outcome is
// stored as its enum name and FinishedAt as a round-trip ISO 8601 string, both forward-compatible
// and human-readable.
public sealed class DungeonRunRecordSaveData
{
    public string Outcome { get; set; }

    // Round-trip ("o") timestamp of when the run was finalized, or null/absent for legacy entries
    // saved before timestamps existed (loaded as an unknown finish time, never dropped).
    public string FinishedAt { get; set; }

    public ulong Seed { get; set; }
    public int StartingRoomLevel { get; set; }
    public int PlannedRunLength { get; set; }
    public int RoomsCleared { get; set; }
    public int EnemiesKilled { get; set; }
    public int PlayerDeaths { get; set; }
    public int BossesDefeated { get; set; }
    public int FurthestRoomIndex { get; set; }
    public int FurthestRoomLevel { get; set; }

    // Additive score fields. Nullable so they are simply absent from older saves (and serialize as
    // null), which loads as an unknown score rather than a fabricated zero. A legitimate zero score
    // is written as an explicit 0 in all three, keeping it distinct from a legacy null.
    public int? BaseScore { get; set; }
    public float? DifficultyMultiplier { get; set; }
    public int? FinalScore { get; set; }

    // Selected difficulty fields (the starting room level is already StartingRoomLevel above).
    // Nullable and treated as an all-or-nothing group: absent in legacy saves, which loads as unknown
    // difficulty rather than fabricated zeros. A legitimate 0% bonus is written as an explicit 0.
    public int? LevelIncreasePerRoom { get; set; }
    public float? HealthPowerBonus { get; set; }
    public float? ResistanceBonus { get; set; }
    public float? DamageBonus { get; set; }

    public static DungeonRunRecordSaveData FromRecord(DungeonRunRecord record)
    {
        return new DungeonRunRecordSaveData
        {
            Outcome = record.Outcome.ToString(),
            FinishedAt = record.FinishedAt?.ToString("o", CultureInfo.InvariantCulture),
            Seed = record.Seed,
            StartingRoomLevel = record.StartingRoomLevel,
            PlannedRunLength = record.PlannedRunLength,
            RoomsCleared = record.RoomsCleared,
            EnemiesKilled = record.EnemiesKilled,
            PlayerDeaths = record.PlayerDeaths,
            BossesDefeated = record.BossesDefeated,
            FurthestRoomIndex = record.FurthestRoomIndex,
            FurthestRoomLevel = record.FurthestRoomLevel,
            BaseScore = record.BaseScore,
            DifficultyMultiplier = record.DifficultyMultiplier,
            FinalScore = record.FinalScore,
            LevelIncreasePerRoom = record.LevelIncreasePerRoom,
            HealthPowerBonus = record.HealthPowerBonus,
            ResistanceBonus = record.ResistanceBonus,
            DamageBonus = record.DamageBonus,
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

        // The timestamp is secondary display data: a missing or unparseable value loads as an
        // unknown finish time (null) rather than dropping an otherwise valid record.
        DateTimeOffset? finishedAt = null;
        if (!string.IsNullOrEmpty(FinishedAt) &&
            DateTimeOffset.TryParse(
                FinishedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedFinishedAt))
        {
            finishedAt = parsedFinishedAt;
        }

        // Score is additive, secondary data (like FinishedAt). Treat the three fields as an
        // all-or-nothing group: only when all are present and individually valid is this a real
        // score snapshot; otherwise it loads as unknown (the legacy fallback) without dropping the
        // record. This keeps a legitimate zero score distinct from a legacy record that simply has
        // no score data, and a malformed score never invalidates an otherwise valid record or its
        // neighbors.
        int? baseScore = null;
        float? difficultyMultiplier = null;
        int? finalScore = null;
        if (BaseScore.HasValue && DifficultyMultiplier.HasValue && FinalScore.HasValue &&
            BaseScore.Value >= 0 &&
            float.IsFinite(DifficultyMultiplier.Value) && DifficultyMultiplier.Value > 0.0f &&
            FinalScore.Value >= 0)
        {
            baseScore = BaseScore.Value;
            difficultyMultiplier = DifficultyMultiplier.Value;
            finalScore = FinalScore.Value;
        }

        // Difficulty selection is secondary display data, treated as an all-or-nothing group like the
        // score trio: only when every field is present and individually valid does it load; otherwise
        // it loads as unknown (the legacy fallback) without dropping the record. A legitimate 0% bonus
        // stays distinct from a legacy record that simply has no difficulty data.
        int? levelIncreasePerRoom = null;
        float? healthPowerBonus = null;
        float? resistanceBonus = null;
        float? damageBonus = null;
        if (LevelIncreasePerRoom.HasValue && HealthPowerBonus.HasValue &&
            ResistanceBonus.HasValue && DamageBonus.HasValue &&
            LevelIncreasePerRoom.Value >= 0 &&
            float.IsFinite(HealthPowerBonus.Value) &&
            float.IsFinite(ResistanceBonus.Value) &&
            float.IsFinite(DamageBonus.Value))
        {
            levelIncreasePerRoom = LevelIncreasePerRoom.Value;
            healthPowerBonus = HealthPowerBonus.Value;
            resistanceBonus = ResistanceBonus.Value;
            damageBonus = DamageBonus.Value;
        }

        record = new DungeonRunRecord(
            outcome,
            finishedAt,
            Seed,
            StartingRoomLevel,
            PlannedRunLength,
            RoomsCleared,
            EnemiesKilled,
            PlayerDeaths,
            BossesDefeated,
            FurthestRoomIndex,
            FurthestRoomLevel,
            baseScore,
            difficultyMultiplier,
            finalScore,
            levelIncreasePerRoom,
            healthPowerBonus,
            resistanceBonus,
            damageBonus);
        return true;
    }
}
