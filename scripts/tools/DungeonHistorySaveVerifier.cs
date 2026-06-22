using Godot;

using System;
using System.Collections.Generic;
using System.Text.Json;

// Headless developer tool that exercises dungeon-history save persistence: round-trip through the
// save DTO, legacy version-1 saves without a history field, newest-first ordering, the latest-100
// cap on save and load, history replacement (never append), and per-entry corruption isolation
// (malformed entries skipped while valid neighbors and core save data survive).
//
// It serializes/deserializes in memory only and never touches the on-disk save slot. Like the
// other verifiers it prints PASS/FAIL lines and quits with an exit code (0 = all passed). Run it:
//
//   /Applications/Godot_mono.app/Contents/MacOS/Godot --headless \
//     --path /Users/jjindrak/Projects/Dotai \
//     --scene res://scenes/tools/dungeon_history_save_verify.tscn
public partial class DungeonHistorySaveVerifier : Node
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    // Distinct finalization timestamps (with non-UTC offsets) used to prove timestamps survive the
    // save round-trip without losing their instant or offset.
    private static readonly DateTimeOffset FirstFinishedAt = new(2026, 6, 20, 13, 45, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset SecondFinishedAt = new(2026, 6, 19, 9, 5, 30, TimeSpan.FromHours(-5));

    private int _failures;

    public override void _Ready()
    {
        GD.Print("Dungeon history save verification:");

        Check("history round-trips through the save DTO", HistoryRoundTrips());
        Check("finish timestamps round-trip with instant and offset intact", TimestampRoundTrips());
        Check("a history entry without a timestamp loads as unknown but survives", MissingTimestampLoadsAsUnknown());
        Check("a history entry with an unparseable timestamp loads as unknown but survives", UnparseableTimestampLoadsAsUnknown());
        Check("legacy version-1 save without history loads as empty", LegacySaveWithoutHistoryLoads());
        Check("newest-first ordering is preserved across save/load", NewestFirstOrderPreserved());
        Check("history is trimmed to the newest 100 on save and on load", TrimsToHundredOnSaveAndLoad());
        Check("ReplaceHistory replaces rather than appends and caps at 100", ReplaceHistoryReplacesAndCaps());
        Check("malformed history entries are skipped while neighbors and core survive", MalformedEntriesSkippedCoreSurvives());
        Check("missing or impossible identity/progress values are skipped", MissingOrImpossibleValuesSkipped());

        Check("score round-trips through save/load for Completed and GaveUp", ScoreRoundTrips());
        Check("a legacy entry without score fields loads with unknown score", LegacyScoreLoadsAsUnknown());
        Check("a legitimate zero score is distinct from a legacy unknown score", ZeroScoreDistinctFromLegacy());
        Check("a malformed score field degrades to unknown without dropping the record or neighbors", MalformedScoreDegradesButSurvives());

        Check("PointsEarned round-trips for Completed (positive) and GaveUp (zero)", PointsEarnedRoundTrips());
        Check("a legacy entry without PointsEarned loads with an unknown award", LegacyPointsEarnedLoadsAsUnknown());
        Check("a legitimate zero PointsEarned is distinct from a legacy unknown award", ZeroPointsEarnedDistinctFromLegacy());
        Check("a malformed PointsEarned degrades to unknown without dropping the record", MalformedPointsEarnedDegradesButSurvives());
        Check("the Points balance round-trips through save/load", PointsBalanceRoundTrips());
        Check("a legacy save without a Dungeon section defaults the Points balance to zero", LegacySaveWithoutPointsDefaultsZero());

        GD.Print(_failures == 0
            ? "All dungeon history save checks passed."
            : $"{_failures} dungeon history save check(s) failed.");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private static bool HistoryRoundTrips()
    {
        var data = new SaveGameData
        {
            Player = new PlayerSaveData { Level = 7, CurrentExperience = 123 },
            Inventory = new InventorySaveData { Gold = 50, GearXp = 9 },
            DungeonHistory = DungeonHistorySaveSerializer.CreateSnapshot(new List<DungeonRunRecord>
            {
                new(DungeonRunOutcome.Completed, FirstFinishedAt, 111UL, 2, 12, 12, 40, 0, 1, 12, 13),
                new(DungeonRunOutcome.GaveUp, SecondFinishedAt, 222UL, 1, 8, 3, 10, 1, 0, 4, 4),
            }),
        };

        var json = JsonSerializer.Serialize(data, WriteOptions);
        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        // Core data survives.
        if (parsed.Player?.Level != 7 || parsed.Inventory?.Gold != 50 || parsed.Inventory?.GearXp != 9)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        if (skipped != 0 || records.Count != 2)
            return false;

        var first = records[0];
        var second = records[1];
        return first.Outcome == DungeonRunOutcome.Completed &&
            first.FinishedAt == FirstFinishedAt &&
            first.Seed == 111UL &&
            first.StartingRoomLevel == 2 &&
            first.PlannedRunLength == 12 &&
            first.RoomsCleared == 12 &&
            first.EnemiesKilled == 40 &&
            first.PlayerDeaths == 0 &&
            first.BossesDefeated == 1 &&
            first.FurthestRoomIndex == 12 &&
            first.FurthestRoomLevel == 13 &&
            second.Outcome == DungeonRunOutcome.GaveUp &&
            second.FinishedAt == SecondFinishedAt &&
            second.Seed == 222UL;
    }

    private static bool TimestampRoundTrips()
    {
        var history = new List<DungeonRunRecord>
        {
            new(DungeonRunOutcome.Completed, FirstFinishedAt, 111UL, 2, 12, 12, 40, 0, 1, 12, 13),
        };

        var json = JsonSerializer.Serialize(
            new SaveGameData { DungeonHistory = DungeonHistorySaveSerializer.CreateSnapshot(history) },
            WriteOptions);
        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);

        if (skipped != 0 || records.Count != 1)
            return false;

        var finishedAt = records[0].FinishedAt;
        // Both the instant (== compares instants) and the stored offset must survive the round-trip.
        return finishedAt == FirstFinishedAt && finishedAt.Value.Offset == FirstFinishedAt.Offset;
    }

    private static bool MissingTimestampLoadsAsUnknown()
    {
        // A valid entry whose object simply omits the FinishedAt field (e.g. saved before timestamps
        // existed) must load with a null finish time rather than being skipped.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""DungeonHistory"": [
                { ""Outcome"": ""Completed"", ""Seed"": 7, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 12, ""RoomsCleared"": 12, ""EnemiesKilled"": 30, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 12, ""FurthestRoomLevel"": 12 }
            ]
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return skipped == 0 && records.Count == 1 && records[0].Seed == 7UL && records[0].FinishedAt == null;
    }

    private static bool UnparseableTimestampLoadsAsUnknown()
    {
        // A non-empty but unparseable FinishedAt is treated as an unknown finish time; the otherwise
        // valid record is still kept.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""DungeonHistory"": [
                { ""Outcome"": ""GaveUp"", ""FinishedAt"": ""not-a-date"", ""Seed"": 8, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 8, ""RoomsCleared"": 3, ""EnemiesKilled"": 10, ""PlayerDeaths"": 1, ""BossesDefeated"": 0, ""FurthestRoomIndex"": 4, ""FurthestRoomLevel"": 4 }
            ]
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return skipped == 0 && records.Count == 1 && records[0].Seed == 8UL && records[0].FinishedAt == null;
    }

    private static bool LegacySaveWithoutHistoryLoads()
    {
        // A version-1 save written before dungeon history existed: no DungeonHistory field at all.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""Player"": { ""Level"": 4, ""CurrentExperience"": 10 },
            ""Inventory"": { ""Gold"": 33, ""GearXp"": 2 },
            ""Equipment"": {}
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        if (parsed.Player?.Level != 4 || parsed.Inventory?.Gold != 33)
            return false;

        if (parsed.DungeonHistory == null || parsed.DungeonHistory.Count != 0)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return skipped == 0 && records.Count == 0;
    }

    private static bool NewestFirstOrderPreserved()
    {
        var history = new List<DungeonRunRecord>();
        for (ulong i = 0; i < 5; i++)
            history.Add(MakeRecord(DungeonRunOutcome.Completed, 500UL + i));

        var json = JsonSerializer.Serialize(
            new SaveGameData { DungeonHistory = DungeonHistorySaveSerializer.CreateSnapshot(history) },
            WriteOptions);
        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out _);

        if (records.Count != 5)
            return false;

        for (var i = 0; i < 5; i++)
        {
            if (records[i].Seed != 500UL + (ulong)i)
                return false;
        }

        return true;
    }

    private static bool TrimsToHundredOnSaveAndLoad()
    {
        var history = new List<DungeonRunRecord>();
        for (ulong i = 0; i < 105; i++)
            history.Add(MakeRecord(DungeonRunOutcome.Completed, 1000UL + i));

        // Save side: keep only the newest 100 (the first 100 of newest-first input).
        var snapshot = DungeonHistorySaveSerializer.CreateSnapshot(history);
        if (snapshot.Count != 100 || snapshot[0].Seed != 1000UL || snapshot[99].Seed != 1099UL)
            return false;

        // Load side: an oversized snapshot is clamped to the newest 100 with nothing skipped.
        var oversized = new List<DungeonRunRecordSaveData>();
        for (ulong i = 0; i < 105; i++)
            oversized.Add(DungeonRunRecordSaveData.FromRecord(MakeRecord(DungeonRunOutcome.GaveUp, 2000UL + i)));

        var records = DungeonHistorySaveSerializer.FromSnapshot(oversized, out var skipped);
        return records.Count == 100 && skipped == 0 && records[0].Seed == 2000UL && records[99].Seed == 2099UL;
    }

    private static bool ReplaceHistoryReplacesAndCaps()
    {
        var dungeon = new Dungeon();
        try
        {
            dungeon.ReplaceHistory(new List<DungeonRunRecord>
            {
                MakeRecord(DungeonRunOutcome.Completed, 1UL),
                MakeRecord(DungeonRunOutcome.GaveUp, 2UL),
            });
            if (dungeon.History.Count != 2 || dungeon.History[0].Seed != 1UL)
                return false;

            // A second replace overwrites the first set rather than appending.
            dungeon.ReplaceHistory(new List<DungeonRunRecord> { MakeRecord(DungeonRunOutcome.Completed, 9UL) });
            if (dungeon.History.Count != 1 || dungeon.History[0].Seed != 9UL)
                return false;

            var big = new List<DungeonRunRecord>();
            for (ulong i = 0; i < 130; i++)
                big.Add(MakeRecord(DungeonRunOutcome.Completed, 3000UL + i));

            dungeon.ReplaceHistory(big);
            return dungeon.History.Count == 100 &&
                dungeon.History[0].Seed == 3000UL &&
                dungeon.History[99].Seed == 3099UL;
        }
        finally
        {
            dungeon.Free();
        }
    }

    private static bool MalformedEntriesSkippedCoreSurvives()
    {
        // Two valid entries (seed 11, 44) surround three malformed ones: a wrong-typed Seed, an
        // unknown Outcome, and a negative counter.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""Player"": { ""Level"": 12, ""CurrentExperience"": 99 },
            ""Inventory"": { ""Gold"": 77, ""GearXp"": 5 },
            ""Equipment"": {},
            ""DungeonHistory"": [
                { ""Outcome"": ""Completed"", ""Seed"": 11, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 12, ""RoomsCleared"": 12, ""EnemiesKilled"": 30, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 12, ""FurthestRoomLevel"": 12 },
                { ""Outcome"": ""Completed"", ""Seed"": ""not-a-number"" },
                { ""Outcome"": ""Bogus"", ""Seed"": 22 },
                { ""Outcome"": ""GaveUp"", ""Seed"": 33, ""EnemiesKilled"": -5 },
                { ""Outcome"": ""GaveUp"", ""Seed"": 44, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 8, ""RoomsCleared"": 3, ""EnemiesKilled"": 10, ""PlayerDeaths"": 1, ""BossesDefeated"": 0, ""FurthestRoomIndex"": 4, ""FurthestRoomLevel"": 4 }
            ]
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        // The malformed history entries did not break the root save: core data survives.
        if (parsed.Player?.Level != 12 || parsed.Inventory?.Gold != 77)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return records.Count == 2 && skipped == 3 && records[0].Seed == 11UL && records[1].Seed == 44UL;
    }

    private static bool MissingOrImpossibleValuesSkipped()
    {
        // Valid neighbors (seeds 100, 104) surround three impossible records: missing required
        // values (numeric fields default to 0, so it reads as level-0/zero-length), a furthest
        // index beyond the planned length, and rooms cleared beyond the rooms reached.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""Player"": { ""Level"": 8, ""CurrentExperience"": 5 },
            ""Inventory"": { ""Gold"": 21, ""GearXp"": 3 },
            ""Equipment"": {},
            ""DungeonHistory"": [
                { ""Outcome"": ""Completed"", ""Seed"": 100, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 12, ""RoomsCleared"": 12, ""EnemiesKilled"": 10, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 12, ""FurthestRoomLevel"": 12 },
                { ""Outcome"": ""Completed"", ""Seed"": 101 },
                { ""Outcome"": ""GaveUp"", ""Seed"": 102, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 8, ""RoomsCleared"": 2, ""EnemiesKilled"": 4, ""PlayerDeaths"": 0, ""BossesDefeated"": 0, ""FurthestRoomIndex"": 20, ""FurthestRoomLevel"": 3 },
                { ""Outcome"": ""Completed"", ""Seed"": 103, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 8, ""RoomsCleared"": 50, ""EnemiesKilled"": 4, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 4, ""FurthestRoomLevel"": 4 },
                { ""Outcome"": ""GaveUp"", ""Seed"": 104, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 6, ""RoomsCleared"": 6, ""EnemiesKilled"": 9, ""PlayerDeaths"": 1, ""BossesDefeated"": 0, ""FurthestRoomIndex"": 6, ""FurthestRoomLevel"": 6 }
            ]
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        // The impossible history entries did not break the root save: core data survives.
        if (parsed.Player?.Level != 8 || parsed.Inventory?.Gold != 21)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return records.Count == 2 && skipped == 3 && records[0].Seed == 100UL && records[1].Seed == 104UL;
    }

    private static bool ScoreRoundTrips()
    {
        // A Completed run (base 750) and a GaveUp run (base 200) both carry an explicit 1.0
        // multiplier and matching final score through serialization and back.
        var history = new List<DungeonRunRecord>
        {
            new(DungeonRunOutcome.Completed, FirstFinishedAt, 111UL, 2, 12, 12, 40, 0, 1, 12, 13, 750, 1.0f, 750),
            new(DungeonRunOutcome.GaveUp, SecondFinishedAt, 222UL, 1, 8, 3, 10, 1, 0, 4, 4, 200, 1.0f, 200),
        };

        var json = JsonSerializer.Serialize(
            new SaveGameData { DungeonHistory = DungeonHistorySaveSerializer.CreateSnapshot(history) },
            WriteOptions);
        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);

        if (skipped != 0 || records.Count != 2)
            return false;

        return records[0].BaseScore == 750 &&
            records[0].DifficultyMultiplier == 1.0f &&
            records[0].FinalScore == 750 &&
            records[1].Outcome == DungeonRunOutcome.GaveUp &&
            records[1].BaseScore == 200 &&
            records[1].DifficultyMultiplier == 1.0f &&
            records[1].FinalScore == 200;
    }

    private static bool LegacyScoreLoadsAsUnknown()
    {
        // A valid entry whose object simply omits the score fields (saved before scoring existed)
        // must load with a null score, not a fabricated zero, and must not be skipped.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""DungeonHistory"": [
                { ""Outcome"": ""Completed"", ""Seed"": 7, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 12, ""RoomsCleared"": 12, ""EnemiesKilled"": 30, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 12, ""FurthestRoomLevel"": 12 }
            ]
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return skipped == 0 &&
            records.Count == 1 &&
            records[0].Seed == 7UL &&
            records[0].BaseScore == null &&
            records[0].DifficultyMultiplier == null &&
            records[0].FinalScore == null;
    }

    private static bool ZeroScoreDistinctFromLegacy()
    {
        // First entry is a legitimate zero-score run (all three fields present and zero/1.0); the
        // second is a legacy run with no score fields. They must load as distinguishable: explicit
        // 0 versus null.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""DungeonHistory"": [
                { ""Outcome"": ""GaveUp"", ""Seed"": 1, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 8, ""RoomsCleared"": 0, ""EnemiesKilled"": 0, ""PlayerDeaths"": 0, ""BossesDefeated"": 0, ""FurthestRoomIndex"": 1, ""FurthestRoomLevel"": 1, ""BaseScore"": 0, ""DifficultyMultiplier"": 1.0, ""FinalScore"": 0 },
                { ""Outcome"": ""Completed"", ""Seed"": 2, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 12, ""RoomsCleared"": 12, ""EnemiesKilled"": 10, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 12, ""FurthestRoomLevel"": 12 }
            ]
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return skipped == 0 &&
            records.Count == 2 &&
            records[0].Seed == 1UL && records[0].BaseScore == 0 && records[0].FinalScore == 0 &&
            records[1].Seed == 2UL && records[1].BaseScore == null && records[1].FinalScore == null;
    }

    private static bool MalformedScoreDegradesButSurvives()
    {
        // Three valid records; the middle one has impossible score values (negative base). Its core
        // identity/progress is valid, so the record survives with its score degraded to unknown
        // while both neighbors load with their real scores. Nothing is skipped.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""Player"": { ""Level"": 3, ""CurrentExperience"": 1 },
            ""Inventory"": { ""Gold"": 1, ""GearXp"": 0 },
            ""Equipment"": {},
            ""DungeonHistory"": [
                { ""Outcome"": ""Completed"", ""Seed"": 11, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 12, ""RoomsCleared"": 12, ""EnemiesKilled"": 10, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 12, ""FurthestRoomLevel"": 12, ""BaseScore"": 300, ""DifficultyMultiplier"": 1.0, ""FinalScore"": 300 },
                { ""Outcome"": ""GaveUp"", ""Seed"": 22, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 8, ""RoomsCleared"": 2, ""EnemiesKilled"": 4, ""PlayerDeaths"": 0, ""BossesDefeated"": 0, ""FurthestRoomIndex"": 3, ""FurthestRoomLevel"": 3, ""BaseScore"": -5, ""DifficultyMultiplier"": 1.0, ""FinalScore"": 0 },
                { ""Outcome"": ""Completed"", ""Seed"": 33, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 6, ""RoomsCleared"": 6, ""EnemiesKilled"": 5, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 6, ""FurthestRoomLevel"": 6, ""BaseScore"": 0, ""DifficultyMultiplier"": 1.0, ""FinalScore"": 0 }
            ]
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        // The malformed score did not break the root save.
        if (parsed.Player?.Level != 3)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return skipped == 0 &&
            records.Count == 3 &&
            records[0].Seed == 11UL && records[0].BaseScore == 300 &&
            records[1].Seed == 22UL && records[1].BaseScore == null && records[1].FinalScore == null &&
            records[2].Seed == 33UL && records[2].BaseScore == 0;
    }

    private static bool PointsEarnedRoundTrips()
    {
        // A Completed run earning 7 Points and a GaveUp run earning an explicit 0 both round-trip
        // their award through serialization and back.
        var history = new List<DungeonRunRecord>
        {
            new(DungeonRunOutcome.Completed, FirstFinishedAt, 111UL, 2, 12, 12, 40, 0, 1, 12, 13, 750, 1.0f, 750, null, null, null, null, 7),
            new(DungeonRunOutcome.GaveUp, SecondFinishedAt, 222UL, 1, 8, 3, 10, 1, 0, 4, 4, 200, 1.0f, 200, null, null, null, null, 0),
        };

        var json = JsonSerializer.Serialize(
            new SaveGameData { DungeonHistory = DungeonHistorySaveSerializer.CreateSnapshot(history) },
            WriteOptions);
        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);

        return skipped == 0 &&
            records.Count == 2 &&
            records[0].PointsEarned == 7 &&
            records[1].PointsEarned == 0;
    }

    private static bool LegacyPointsEarnedLoadsAsUnknown()
    {
        // A valid entry whose object omits PointsEarned (saved before Points existed) loads with a
        // null award rather than a fabricated zero, and is not skipped.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""DungeonHistory"": [
                { ""Outcome"": ""Completed"", ""Seed"": 7, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 12, ""RoomsCleared"": 12, ""EnemiesKilled"": 30, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 12, ""FurthestRoomLevel"": 12 }
            ]
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return skipped == 0 && records.Count == 1 && records[0].Seed == 7UL && records[0].PointsEarned == null;
    }

    private static bool ZeroPointsEarnedDistinctFromLegacy()
    {
        // First entry awards an explicit 0 Points; the second is a legacy run with no PointsEarned.
        // They must load distinguishably: explicit 0 versus null.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""DungeonHistory"": [
                { ""Outcome"": ""GaveUp"", ""Seed"": 1, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 8, ""RoomsCleared"": 0, ""EnemiesKilled"": 0, ""PlayerDeaths"": 0, ""BossesDefeated"": 0, ""FurthestRoomIndex"": 1, ""FurthestRoomLevel"": 1, ""PointsEarned"": 0 },
                { ""Outcome"": ""Completed"", ""Seed"": 2, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 12, ""RoomsCleared"": 12, ""EnemiesKilled"": 10, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 12, ""FurthestRoomLevel"": 12 }
            ]
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return skipped == 0 &&
            records.Count == 2 &&
            records[0].Seed == 1UL && records[0].PointsEarned == 0 &&
            records[1].Seed == 2UL && records[1].PointsEarned == null;
    }

    private static bool MalformedPointsEarnedDegradesButSurvives()
    {
        // A negative PointsEarned is impossible; the record's identity/progress is valid, so it
        // survives with its award degraded to unknown rather than being dropped or skipped.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""DungeonHistory"": [
                { ""Outcome"": ""Completed"", ""Seed"": 11, ""StartingRoomLevel"": 1, ""PlannedRunLength"": 12, ""RoomsCleared"": 12, ""EnemiesKilled"": 10, ""PlayerDeaths"": 0, ""BossesDefeated"": 1, ""FurthestRoomIndex"": 12, ""FurthestRoomLevel"": 12, ""PointsEarned"": -4 }
            ]
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        var records = DungeonHistorySaveSerializer.FromSnapshot(parsed.DungeonHistory, out var skipped);
        return skipped == 0 && records.Count == 1 && records[0].Seed == 11UL && records[0].PointsEarned == null;
    }

    private static bool PointsBalanceRoundTrips()
    {
        var json = JsonSerializer.Serialize(
            new SaveGameData { Dungeon = new DungeonSaveData { Points = 42 } },
            WriteOptions);
        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        return parsed?.Dungeon != null && parsed.Dungeon.Points == 42;
    }

    private static bool LegacySaveWithoutPointsDefaultsZero()
    {
        // A save written before the Points balance existed has no Dungeon section: the balance loads
        // as zero (the initialized default), mirroring the apply-time `data.Dungeon?.Points ?? 0`.
        const string json = @"{
            ""Schema"": ""dotai.savegame"",
            ""Version"": 1,
            ""Player"": { ""Level"": 4, ""CurrentExperience"": 10 },
            ""Inventory"": { ""Gold"": 33, ""GearXp"": 2 },
            ""Equipment"": {}
        }";

        var parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        if (parsed == null)
            return false;

        // Core data still survives, and the missing balance reads as zero.
        return parsed.Inventory?.Gold == 33 && (parsed.Dungeon?.Points ?? 0) == 0;
    }

    private static DungeonRunRecord MakeRecord(DungeonRunOutcome outcome, ulong seed)
    {
        return new DungeonRunRecord(outcome, FirstFinishedAt, seed, 1, 12, 5, 20, 0, 1, 6, 6);
    }

    private void Check(string description, bool passed)
    {
        if (passed)
        {
            GD.Print($"  PASS: {description}");
            return;
        }

        _failures++;
        GD.PrintErr($"  FAIL: {description}");
    }
}
