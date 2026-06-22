using Godot;

using System;
using System.Collections.Generic;

// Headless developer tool that exercises the dungeon Points (the player-facing "DP") reward economy:
// the pure finalized-score -> Points conversion (threshold, floor, uncapped default, positive cap,
// fail-safe), the award applied exactly once at the run-finalization boundary for a completed run
// (and never for a gave-up or sub-threshold run), the narrow Points API (TrySpendPoints zero-cost
// success and insufficient-balance failure, the clamping debug/load setter), and that loading
// finalized history never re-awards Points.
//
// The award checks drive a real Dungeon from real room definitions (a combat-only plan whose first
// room authors 100 completion points, scored at an unmodified 1.0 multiplier) and exercise the
// shared completion path the boss exit uses (MarkActiveNodeCleared) so no heavy Boss room is
// instantiated. Like the other verifiers it prints PASS/FAIL lines and quits with an exit code
// (0 = all passed). Run it:
//
//   /Applications/Godot_mono.app/Contents/MacOS/Godot --headless \
//     --path /Users/jjindrak/Projects/Dotai \
//     --scene res://scenes/tools/dungeon_rewards_verify.tscn
public partial class DungeonRewardsVerifier : Node
{
    private const string CombatDefinitionPath = "res://resources/world/room_definitions/combat_dungeon_room_definition.tres";
    private const string TimedDefinitionPath = "res://resources/world/room_definitions/timed_dungeon_room_definition.tres";
    private const string SpecialDefinitionPath = "res://resources/world/room_definitions/special_dungeon_room_definition.tres";
    private const string BossDefinitionPath = "res://resources/world/room_definitions/boss_room_definition.tres";
    private const string RewardRulesPath = "res://resources/world/dungeon/dungeon_reward_rules.tres";

    private static readonly StringName RuntimeScreenId = Dungeon.RuntimeScreenId;

    private int _failures;

    public override void _Ready()
    {
        GD.Print("Dungeon rewards verification:");

        Check("authored reward rules and CreateDefault agree (100 / uncapped)", DefaultRulesMatchSpec());
        Check("a 100 final score earns exactly 1 Point", HundredScoreEarnsOnePoint());
        Check("scores below and between thresholds floor toward zero", FloorBehaviorBelowAndBetweenThresholds());
        Check("the uncapped default awards the full floored amount", UncappedAwardsFullAmount());
        Check("a positive MaximumPointsPerRun caps a large reward", PositiveCapLimitsReward());
        Check("a non-positive ScorePerPoint fails safe to zero", InvalidScorePerPointFailsSafe());
        Check("a completed run credits its finalized Points at finalization", CompletedRunCreditsPoints());
        Check("a gave-up run credits zero Points despite an earned score", GaveUpRunCreditsZero());
        Check("a completed run below the threshold credits zero Points", SubThresholdCompletionCreditsZero());
        Check("repeated finalization cannot double-award; new runs accumulate", RepeatedFinalizationDoesNotDoubleAward());
        Check("TrySpendPoints: zero-cost succeeds, insufficient and negative fail, valid spends apply", TrySpendPointsBehaves());
        Check("the debug/load setter clamps negatives and replaces (never adds)", SetPointsClampsAndReplaces());
        Check("loading finalized history never re-awards Points", LoadingHistoryDoesNotAward());

        GD.Print(_failures == 0
            ? "All dungeon rewards checks passed."
            : $"{_failures} dungeon rewards check(s) failed.");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    // Pure score -> Points conversion -------------------------------------------------------------

    private static bool DefaultRulesMatchSpec()
    {
        var rules = DungeonRewardRules.CreateDefault();
        if (rules.ScorePerPoint != 100 || rules.MaximumPointsPerRun != 0)
            return false;

        // The shipped resource must mirror CreateDefault() so the missing-resource fallback agrees
        // with what ships.
        var authored = GD.Load<DungeonRewardRules>(RewardRulesPath);
        return authored != null && authored.ScorePerPoint == 100 && authored.MaximumPointsPerRun == 0;
    }

    private static bool HundredScoreEarnsOnePoint()
    {
        return new DungeonRewardRules { ScorePerPoint = 100, MaximumPointsPerRun = 0 }.PointsForScore(100) == 1;
    }

    private static bool FloorBehaviorBelowAndBetweenThresholds()
    {
        var rules = new DungeonRewardRules { ScorePerPoint = 100, MaximumPointsPerRun = 0 };
        return rules.PointsForScore(0) == 0 &&
            rules.PointsForScore(50) == 0 &&    // below the first threshold
            rules.PointsForScore(99) == 0 &&
            rules.PointsForScore(100) == 1 &&   // exactly at the threshold
            rules.PointsForScore(150) == 1 &&   // between thresholds floors down
            rules.PointsForScore(199) == 1 &&
            rules.PointsForScore(200) == 2;
    }

    private static bool UncappedAwardsFullAmount()
    {
        var rules = new DungeonRewardRules { ScorePerPoint = 100, MaximumPointsPerRun = 0 };
        return rules.PointsForScore(1000) == 10 && rules.PointsForScore(12345) == 123;
    }

    private static bool PositiveCapLimitsReward()
    {
        var rules = new DungeonRewardRules { ScorePerPoint = 100, MaximumPointsPerRun = 3 };
        return rules.PointsForScore(1000) == 3 &&   // 10 floored, capped to 3
            rules.PointsForScore(300) == 3 &&       // exactly at the cap
            rules.PointsForScore(250) == 2;         // under the cap is unaffected
    }

    private static bool InvalidScorePerPointFailsSafe()
    {
        // A misconfigured non-positive ScorePerPoint must never divide by zero or hand out a reward.
        return new DungeonRewardRules { ScorePerPoint = 0 }.PointsForScore(1000) == 0 &&
            new DungeonRewardRules { ScorePerPoint = -100 }.PointsForScore(1000) == 0;
    }

    // Award at the finalization boundary ----------------------------------------------------------

    private bool CompletedRunCreditsPoints()
    {
        var rules = BuildRules(combat: 1.0f, timed: 0.0f, special: 0.0f, ordinaryRoomCount: 3);
        if (rules == null)
            return false;

        // Leave RewardRules unset to also prove the shipped-default fallback (ResolveRewardRules)
        // awards correctly.
        var dungeon = new Dungeon { GenerationRules = rules };
        try
        {
            if (!EnterAndClearFirstCombatRoom(dungeon) || dungeon.Points != 0)
                return false;

            var record = dungeon.FinalizeRun(DungeonRunOutcome.Completed);
            // FinalScore 100 at 100-per-point earns and records exactly 1 Point.
            return record != null &&
                record.Outcome == DungeonRunOutcome.Completed &&
                record.FinalScore == 100 &&
                record.PointsEarned == 1 &&
                dungeon.Points == 1;
        }
        finally
        {
            dungeon.EndRun();
            dungeon.Free();
        }
    }

    private bool GaveUpRunCreditsZero()
    {
        var rules = BuildRules(combat: 1.0f, timed: 0.0f, special: 0.0f, ordinaryRoomCount: 3);
        if (rules == null)
            return false;

        var dungeon = new Dungeon { GenerationRules = rules, RewardRules = DungeonRewardRules.CreateDefault() };
        try
        {
            if (!EnterAndClearFirstCombatRoom(dungeon))
                return false;

            // The run carries a positive final score, but giving up grants no Points.
            var record = dungeon.FinalizeRun(DungeonRunOutcome.GaveUp);
            return record != null &&
                record.Outcome == DungeonRunOutcome.GaveUp &&
                record.FinalScore == 100 &&
                record.PointsEarned == 0 &&
                dungeon.Points == 0;
        }
        finally
        {
            dungeon.EndRun();
            dungeon.Free();
        }
    }

    private bool SubThresholdCompletionCreditsZero()
    {
        // A Special-only first room authors 0 points, so a completed run finalizes with a final
        // score below ScorePerPoint and legitimately earns zero Points (recorded as an explicit 0).
        var rules = BuildRules(combat: 0.0f, timed: 0.0f, special: 1.0f, ordinaryRoomCount: 1);
        if (rules == null)
            return false;

        var dungeon = new Dungeon { GenerationRules = rules, RewardRules = DungeonRewardRules.CreateDefault() };
        try
        {
            if (!dungeon.TryStartRun(404UL, null, null, out _))
                return false;
            if (!dungeon.TryCreateRoom(RuntimeScreenId, null, null, default, out _))
                return false;

            var node0 = dungeon.ActivePlan?.Nodes[0];
            if (node0 == null || node0.Kind != DungeonRoomKind.Special || (node0.ContentOption?.CompletionPoints ?? -1) != 0)
                return false;

            dungeon.MarkActiveNodeCleared();
            var record = dungeon.FinalizeRun(DungeonRunOutcome.Completed);
            return record != null &&
                record.Outcome == DungeonRunOutcome.Completed &&
                record.FinalScore == 0 &&
                record.PointsEarned == 0 &&
                dungeon.Points == 0;
        }
        finally
        {
            dungeon.EndRun();
            dungeon.Free();
        }
    }

    private bool RepeatedFinalizationDoesNotDoubleAward()
    {
        var rules = BuildRules(combat: 1.0f, timed: 0.0f, special: 0.0f, ordinaryRoomCount: 3);
        if (rules == null)
            return false;

        var dungeon = new Dungeon { GenerationRules = rules, RewardRules = DungeonRewardRules.CreateDefault() };
        try
        {
            if (!EnterAndClearFirstCombatRoom(dungeon))
                return false;

            dungeon.FinalizeRun(DungeonRunOutcome.Completed);
            if (dungeon.Points != 1)
                return false;

            // Repeated completion callbacks on the now-cleared run record nothing and award nothing.
            if (dungeon.FinalizeRun(DungeonRunOutcome.Completed) != null ||
                dungeon.FinalizeRun(DungeonRunOutcome.Completed) != null ||
                dungeon.Points != 1)
            {
                return false;
            }

            // A genuinely new completed run accumulates on top of the saved balance.
            if (!EnterAndClearFirstCombatRoom(dungeon))
                return false;
            dungeon.FinalizeRun(DungeonRunOutcome.Completed);
            return dungeon.Points == 2;
        }
        finally
        {
            dungeon.EndRun();
            dungeon.Free();
        }
    }

    // Narrow Points API ---------------------------------------------------------------------------

    private static bool TrySpendPointsBehaves()
    {
        var dungeon = new Dungeon();
        try
        {
            // Empty balance: a zero-cost spend still succeeds and changes nothing.
            if (!dungeon.TrySpendPoints(0) || dungeon.Points != 0)
                return false;

            // Insufficient balance and negative amounts fail, leaving the balance untouched.
            if (dungeon.TrySpendPoints(5) || dungeon.TrySpendPoints(-1) || dungeon.Points != 0)
                return false;

            dungeon.SetPointsForDebugOrLoad(10);

            // Zero-cost still succeeds with a balance; a valid spend applies; overspend fails.
            if (!dungeon.TrySpendPoints(0) || dungeon.Points != 10)
                return false;
            if (!dungeon.TrySpendPoints(4) || dungeon.Points != 6)
                return false;
            if (dungeon.TrySpendPoints(7) || dungeon.Points != 6)
                return false;

            // An exact spend drains to zero.
            return dungeon.TrySpendPoints(6) && dungeon.Points == 0;
        }
        finally
        {
            dungeon.Free();
        }
    }

    private static bool SetPointsClampsAndReplaces()
    {
        var dungeon = new Dungeon();
        try
        {
            dungeon.SetPointsForDebugOrLoad(25);
            if (dungeon.Points != 25)
                return false;

            // Replacement, never addition.
            dungeon.SetPointsForDebugOrLoad(7);
            if (dungeon.Points != 7)
                return false;

            // A malformed negative value clamps to zero.
            dungeon.SetPointsForDebugOrLoad(-100);
            return dungeon.Points == 0;
        }
        finally
        {
            dungeon.Free();
        }
    }

    private static bool LoadingHistoryDoesNotAward()
    {
        var dungeon = new Dungeon();
        try
        {
            // A completed record carrying a positive PointsEarned, replayed into history (as a save
            // load does), must not touch the live Points balance: the saved balance is restored
            // separately and awarding is never re-run.
            var completed = new DungeonRunRecord(
                DungeonRunOutcome.Completed, DateTimeOffset.Now, 5UL, 1, 12, 12, 30, 0, 1, 12, 12,
                1000, 1.0f, 1000, null, null, null, null, 10);

            dungeon.ReplaceHistory(new List<DungeonRunRecord> { completed });

            return dungeon.History.Count == 1 &&
                dungeon.History[0].PointsEarned == 10 &&
                dungeon.Points == 0;
        }
        finally
        {
            dungeon.Free();
        }
    }

    // Helpers -------------------------------------------------------------------------------------

    // Starts a combat-only run on the given dungeon and clears its first (100-point) room so the run
    // carries a base/final score of 100 at the unmodified 1.0 multiplier. Returns false if setup or
    // the expected score does not hold.
    private static bool EnterAndClearFirstCombatRoom(Dungeon dungeon)
    {
        if (!dungeon.TryStartRun(7777UL, null, null, out _))
            return false;
        if (!dungeon.TryCreateRoom(RuntimeScreenId, null, null, default, out _))
            return false;

        var node0 = dungeon.ActivePlan?.Nodes[0];
        if (node0 == null || node0.Kind != DungeonRoomKind.Combat || (node0.ContentOption?.CompletionPoints ?? -1) != 100)
            return false;

        dungeon.MarkActiveNodeCleared();
        return dungeon.ActiveStats != null && dungeon.ActiveStats.BaseScore == 100;
    }

    // Assembles focused generation rules from the real room definitions, varying only the ordinary
    // kind weights and count so the entered nodes are predictable. Mirrors the DungeonScoreVerifier
    // helper. The combat definition is always present because the generator requires a usable combat
    // fallback for any ordinary run.
    private static DungeonGenerationRules BuildRules(float combat, float timed, float special, int ordinaryRoomCount)
    {
        var combatDef = GD.Load<RoomTemplateDefinition>(CombatDefinitionPath);
        var timedDef = GD.Load<RoomTemplateDefinition>(TimedDefinitionPath);
        var specialDef = GD.Load<RoomTemplateDefinition>(SpecialDefinitionPath);
        var bossDef = GD.Load<RoomTemplateDefinition>(BossDefinitionPath);
        if (combatDef == null || timedDef == null || specialDef == null || bossDef == null)
            return null;

        var rules = new DungeonGenerationRules
        {
            OrdinaryRoomCount = ordinaryRoomCount,
            StartingRoomLevel = 1,
            LevelIncreasePerRoom = 1,
            CombatWeight = combat,
            TimedWeight = timed,
            SpecialWeight = special,
            SpecialRoomPity = 0,
            SpecialRoomDefinition = specialDef,
            BossRoomDefinition = bossDef,
            PreBossContentId = "pre_boss",
            BossContentId = "demon_boss_content",
        };
        rules.CombatRoomDefinitions.Add(combatDef);
        rules.TimedRoomDefinitions.Add(timedDef);
        return rules;
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
