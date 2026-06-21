using Godot;

using System;

// Headless developer tool that exercises the pure live-statistics and finalization behavior added
// for live dungeon stats: DungeonRunStats counters, the immutable DungeonRunRecord snapshot, and
// Dungeon.FinalizeRun (record creation, explicit outcome, idempotency, newest-first ordering, the
// 100-record cap, and that raw EndRun never records). It prints PASS/FAIL lines and quits with an
// exit code (0 = all passed, 1 = any failed). Like DungeonPlanVerifier it is NOT part of normal
// runtime; run it explicitly:
//
//   /Applications/Godot_mono.app/Contents/MacOS/Godot --headless \
//     --path /Users/jjindrak/Projects/Dotai \
//     --scene res://scenes/tools/dungeon_run_stats_verify.tscn
public partial class DungeonRunStatsVerifier : Node
{
    private const string RulesResourcePath = "res://resources/world/dungeon/dungeon_generation_rules.tres";

    [Export]
    public DungeonGenerationRules Rules { get; set; }

    private int _failures;

    public override void _Ready()
    {
        var rules = Rules ?? GD.Load<DungeonGenerationRules>(RulesResourcePath);
        if (rules == null)
        {
            GD.PrintErr($"FAIL: could not load dungeon generation rules from '{RulesResourcePath}'.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("Dungeon run-stats verification:");
        RunChecks(rules);

        GD.Print(_failures == 0
            ? "All dungeon run-stats checks passed."
            : $"{_failures} dungeon run-stats check(s) failed.");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private void RunChecks(DungeonGenerationRules rules)
    {
        Check("stat counters increment independently", StatCountersIncrement());
        Check("furthest reach keeps the highest one-based index and its level", FurthestReachTracksHighest());
        Check("record snapshots stats and is immutable to later mutation", RecordIsImmutableSnapshot());

        Check("base score accumulates and ignores non-positive awards", BaseScoreAccumulates());
        Check("record snapshots base score, the 1.0 multiplier and final score", RecordSnapshotsScore());
        Check("a gave-up record retains its accumulated base score", GaveUpRetainsScore());

        Check("start populates seed, starting level and planned length", StartPopulatesIdentity(rules));
        Check("finalize records exactly one run and clears it", FinalizeRecordsAndClears(rules));
        Check("finalize stamps the run with a finish time", FinalizeStampsFinishTime(rules));
        Check("finalize is idempotent with no active run", FinalizeIsIdempotent(rules));
        Check("outcome is recorded as supplied", OutcomeIsRecorded(rules));
        Check("EndRun clears without recording", EndRunDoesNotRecord(rules));
        Check("history is newest-first and capped at 100", HistoryNewestFirstAndCapped(rules));
    }

    private static bool StatCountersIncrement()
    {
        var stats = new DungeonRunStats(42UL, 3, 12);
        stats.IncrementRoomsCleared();
        stats.IncrementRoomsCleared();
        stats.IncrementEnemiesKilled();
        stats.IncrementBossesDefeated();
        stats.IncrementPlayerDeaths();

        return stats.Seed == 42UL &&
            stats.StartingRoomLevel == 3 &&
            stats.PlannedRunLength == 12 &&
            stats.RoomsCleared == 2 &&
            stats.EnemiesKilled == 1 &&
            stats.BossesDefeated == 1 &&
            stats.PlayerDeaths == 1;
    }

    private static bool FurthestReachTracksHighest()
    {
        var stats = new DungeonRunStats(1UL, 1, 5);
        stats.RecordRoomReached(1, 1);
        stats.RecordRoomReached(3, 3);
        // A regression (lower index) must not lower the recorded furthest reach.
        stats.RecordRoomReached(2, 2);

        return stats.FurthestRoomIndex == 3 && stats.FurthestRoomLevel == 3;
    }

    private static bool RecordIsImmutableSnapshot()
    {
        var stats = new DungeonRunStats(7UL, 2, 9);
        stats.IncrementEnemiesKilled();
        stats.RecordRoomReached(4, 5);

        var finishedAt = new DateTimeOffset(2026, 6, 20, 13, 45, 0, TimeSpan.FromHours(2));
        var record = new DungeonRunRecord(stats, DungeonRunOutcome.Completed, finishedAt);

        // Mutating the live stats after the snapshot must not change the record.
        stats.IncrementEnemiesKilled();
        stats.IncrementBossesDefeated();

        return record.Outcome == DungeonRunOutcome.Completed &&
            record.FinishedAt == finishedAt &&
            record.Seed == 7UL &&
            record.StartingRoomLevel == 2 &&
            record.PlannedRunLength == 9 &&
            record.EnemiesKilled == 1 &&
            record.BossesDefeated == 0 &&
            record.FurthestRoomIndex == 4 &&
            record.FurthestRoomLevel == 5;
    }

    private static bool BaseScoreAccumulates()
    {
        var stats = new DungeonRunStats(1UL, 1, 5);
        if (stats.BaseScore != 0)
            return false;

        stats.AddScore(100);
        stats.AddScore(150);
        stats.AddScore(0);    // zero-point content is a no-op
        stats.AddScore(-50);  // points are never subtracted

        return stats.BaseScore == 250;
    }

    private static bool RecordSnapshotsScore()
    {
        var stats = new DungeonRunStats(5UL, 1, 6);
        stats.AddScore(100);
        stats.AddScore(150);
        stats.AddScore(500);

        var record = new DungeonRunRecord(stats, DungeonRunOutcome.Completed, DateTimeOffset.Now);

        // Mutating the live stats after the snapshot must not change the record's score.
        stats.AddScore(999);

        // This slice scores at an unmodified 1.0 multiplier, so final score equals base score.
        return record.BaseScore == 750 &&
            record.DifficultyMultiplier == 1.0f &&
            record.FinalScore == 750;
    }

    private static bool GaveUpRetainsScore()
    {
        var stats = new DungeonRunStats(9UL, 1, 6);
        stats.AddScore(100);
        stats.AddScore(150);

        var record = new DungeonRunRecord(stats, DungeonRunOutcome.GaveUp, DateTimeOffset.Now);

        return record.Outcome == DungeonRunOutcome.GaveUp &&
            record.BaseScore == 250 &&
            record.FinalScore == 250;
    }

    private bool StartPopulatesIdentity(DungeonGenerationRules rules)
    {
        var dungeon = new Dungeon { GenerationRules = rules };
        try
        {
            if (!dungeon.TryStartRun(123UL, ordinaryRoomCount: 4, startingRoomLevel: 5, out var error))
            {
                GD.PrintErr($"  unexpected start failure: {error}");
                return false;
            }

            var stats = dungeon.ActiveStats;
            // 4 ordinary + Pre-Boss + Boss = 6 planned rooms, starting level 5.
            return stats != null &&
                stats.Seed == 123UL &&
                stats.StartingRoomLevel == 5 &&
                stats.PlannedRunLength == 6 &&
                dungeon.History.Count == 0;
        }
        finally
        {
            dungeon.Free();
        }
    }

    private bool FinalizeRecordsAndClears(DungeonGenerationRules rules)
    {
        var dungeon = new Dungeon { GenerationRules = rules };
        try
        {
            if (!dungeon.TryStartRun(1UL, null, null, out _))
                return false;

            var record = dungeon.FinalizeRun(DungeonRunOutcome.Completed);
            return record != null &&
                dungeon.History.Count == 1 &&
                ReferenceEquals(dungeon.History[0], record) &&
                !dungeon.HasActiveRun &&
                dungeon.ActiveStats == null;
        }
        finally
        {
            dungeon.Free();
        }
    }

    private bool FinalizeStampsFinishTime(DungeonGenerationRules rules)
    {
        var dungeon = new Dungeon { GenerationRules = rules };
        try
        {
            var before = DateTimeOffset.Now;
            if (!dungeon.TryStartRun(1UL, null, null, out _))
                return false;

            var record = dungeon.FinalizeRun(DungeonRunOutcome.Completed);
            var after = DateTimeOffset.Now;

            // A freshly finalized run always carries a finish time, captured at finalization.
            return record?.FinishedAt != null &&
                record.FinishedAt.Value >= before &&
                record.FinishedAt.Value <= after;
        }
        finally
        {
            dungeon.Free();
        }
    }

    private bool FinalizeIsIdempotent(DungeonGenerationRules rules)
    {
        var dungeon = new Dungeon { GenerationRules = rules };
        try
        {
            if (!dungeon.TryStartRun(1UL, null, null, out _))
                return false;

            dungeon.FinalizeRun(DungeonRunOutcome.Completed);

            // A repeated callback with no active run records nothing and returns null.
            var second = dungeon.FinalizeRun(DungeonRunOutcome.GaveUp);
            return second == null && dungeon.History.Count == 1;
        }
        finally
        {
            dungeon.Free();
        }
    }

    private bool OutcomeIsRecorded(DungeonGenerationRules rules)
    {
        var dungeon = new Dungeon { GenerationRules = rules };
        try
        {
            if (!dungeon.TryStartRun(1UL, null, null, out _))
                return false;
            dungeon.FinalizeRun(DungeonRunOutcome.GaveUp);

            if (!dungeon.TryStartRun(2UL, null, null, out _))
                return false;
            dungeon.FinalizeRun(DungeonRunOutcome.Completed);

            // Newest-first: the Completed run is at index 0, the earlier GaveUp at index 1.
            return dungeon.History.Count == 2 &&
                dungeon.History[0].Outcome == DungeonRunOutcome.Completed &&
                dungeon.History[1].Outcome == DungeonRunOutcome.GaveUp;
        }
        finally
        {
            dungeon.Free();
        }
    }

    private bool EndRunDoesNotRecord(DungeonGenerationRules rules)
    {
        var dungeon = new Dungeon { GenerationRules = rules };
        try
        {
            if (!dungeon.TryStartRun(1UL, null, null, out _))
                return false;

            dungeon.EndRun();
            return dungeon.History.Count == 0 && !dungeon.HasActiveRun;
        }
        finally
        {
            dungeon.Free();
        }
    }

    private bool HistoryNewestFirstAndCapped(DungeonGenerationRules rules)
    {
        var dungeon = new Dungeon { GenerationRules = rules };
        try
        {
            const ulong baseSeed = 1000UL;
            const int runs = 105;
            for (var i = 0; i < runs; i++)
            {
                if (!dungeon.TryStartRun(baseSeed + (ulong)i, null, null, out _))
                    return false;

                dungeon.FinalizeRun(DungeonRunOutcome.Completed);
            }

            // Capped at 100, newest-first: index 0 is the last run, index 99 the oldest retained.
            return dungeon.History.Count == 100 &&
                dungeon.History[0].Seed == baseSeed + (runs - 1) &&
                dungeon.History[99].Seed == baseSeed + (runs - 100);
        }
        finally
        {
            dungeon.Free();
        }
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
