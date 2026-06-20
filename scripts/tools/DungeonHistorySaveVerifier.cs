using Godot;

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

    private int _failures;

    public override void _Ready()
    {
        GD.Print("Dungeon history save verification:");

        Check("history round-trips through the save DTO", HistoryRoundTrips());
        Check("legacy version-1 save without history loads as empty", LegacySaveWithoutHistoryLoads());
        Check("newest-first ordering is preserved across save/load", NewestFirstOrderPreserved());
        Check("history is trimmed to the newest 100 on save and on load", TrimsToHundredOnSaveAndLoad());
        Check("ReplaceHistory replaces rather than appends and caps at 100", ReplaceHistoryReplacesAndCaps());
        Check("malformed history entries are skipped while neighbors and core survive", MalformedEntriesSkippedCoreSurvives());
        Check("missing or impossible identity/progress values are skipped", MissingOrImpossibleValuesSkipped());

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
                new(DungeonRunOutcome.Completed, 111UL, 2, 12, 12, 40, 0, 1, 12, 13),
                new(DungeonRunOutcome.GaveUp, 222UL, 1, 8, 3, 10, 1, 0, 4, 4),
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
            second.Seed == 222UL;
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

    private static DungeonRunRecord MakeRecord(DungeonRunOutcome outcome, ulong seed)
    {
        return new DungeonRunRecord(outcome, seed, 1, 12, 5, 20, 0, 1, 6, 6);
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
