using Godot;

using System;

// Headless developer tool that exercises the content-authored dungeon score award lifecycle on a
// real Dungeon driven from a real run plan: that entering a room awards nothing, that the
// forward-progression and final-completion paths award the selected content's points exactly once
// through the existing cleared-node boundary, that a zero-point Special room clears without scoring,
// and that giving up keeps points already earned but never the abandoned room's. It also pins the
// authored completion-point values on the shipped room-definition resources.
//
// It builds focused generation rules from the real room definitions so the entered nodes are
// predictable, and avoids instantiating the heavy Boss room by exercising the shared completion
// path (MarkActiveNodeCleared) on an ordinary node. Like the other verifiers it prints PASS/FAIL
// lines and quits with an exit code (0 = all passed). Run it:
//
//   /Applications/Godot_mono.app/Contents/MacOS/Godot --headless \
//     --path /Users/jjindrak/Projects/Dotai \
//     --scene res://scenes/tools/dungeon_score_verify.tscn
public partial class DungeonScoreVerifier : Node
{
    private const string CombatDefinitionPath = "res://resources/world/room_definitions/combat_dungeon_room_definition.tres";
    private const string TimedDefinitionPath = "res://resources/world/room_definitions/timed_dungeon_room_definition.tres";
    private const string SpecialDefinitionPath = "res://resources/world/room_definitions/special_dungeon_room_definition.tres";
    private const string BossDefinitionPath = "res://resources/world/room_definitions/boss_room_definition.tres";

    private static readonly StringName RuntimeScreenId = Dungeon.RuntimeScreenId;

    private int _failures;

    public override void _Ready()
    {
        GD.Print("Dungeon score verification:");

        Check("authored completion points match the spec table", AuthoredCompletionPointsMatchSpec());
        Check("entering a room awards nothing and completion awards content points exactly once", EnterThenCompleteAwardsOnce());
        Check("first forward progression awards the source room's content points", ForwardProgressionAwardsSource());
        Check("giving up keeps earned points but not the abandoned room's", GiveUpKeepsEarnedNotAbandoned());
        Check("a zero-point Special room clears without changing score", ZeroPointSpecialClearsWithoutScore());

        GD.Print(_failures == 0
            ? "All dungeon score checks passed."
            : $"{_failures} dungeon score check(s) failed.");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private static bool AuthoredCompletionPointsMatchSpec()
    {
        return ContentPoints(CombatDefinitionPath, "combat_dungeon_content_1") == 100 &&
            ContentPoints(CombatDefinitionPath, "combat_dungeon_content_2") == 100 &&
            ContentPoints(TimedDefinitionPath, "timed_dungeon_ogres") == 150 &&
            ContentPoints(SpecialDefinitionPath, "dryad_rest") == 0 &&
            ContentPoints(SpecialDefinitionPath, "merchant_shop") == 0 &&
            ContentPoints(SpecialDefinitionPath, "pre_boss") == 0 &&
            ContentPoints(BossDefinitionPath, "demon_boss_content") == 500;
    }

    // Enters the first room (a combat room under combat-only rules) and proves entry alone scores
    // nothing, then drives the shared completion path the boss exit uses (MarkActiveNodeCleared):
    // the first call awards the content's points, a repeated call awards nothing.
    private bool EnterThenCompleteAwardsOnce()
    {
        var rules = BuildRules(combat: 1.0f, timed: 0.0f, special: 0.0f, ordinaryRoomCount: 3);
        if (rules == null)
            return false;

        var dungeon = new Dungeon { GenerationRules = rules };
        try
        {
            if (!dungeon.TryStartRun(101UL, null, null, out _))
                return false;

            if (!dungeon.TryCreateRoom(RuntimeScreenId, null, null, default, out _))
                return false;

            var node0 = dungeon.ActivePlan?.Nodes[0];
            var expected = node0?.ContentOption?.CompletionPoints ?? -1;
            if (expected <= 0)
                return false; // combat-only rules must yield a positive-point first room

            // Entering the room must not have scored.
            if (dungeon.ActiveStats.BaseScore != 0)
                return false;

            dungeon.MarkActiveNodeCleared();
            if (dungeon.ActiveStats.BaseScore != expected)
                return false;

            // The same node cannot award again.
            dungeon.MarkActiveNodeCleared();
            return dungeon.ActiveStats.BaseScore == expected &&
                dungeon.ActiveStats.RoomsCleared == 1;
        }
        finally
        {
            dungeon.EndRun();
            dungeon.Free();
        }
    }

    // Enters the first room then advances forward through a synthetic transition matching the
    // node's progression exit; that forward step is the only thing that clears the source room and
    // awards its content points.
    private bool ForwardProgressionAwardsSource()
    {
        var rules = BuildRules(combat: 1.0f, timed: 0.0f, special: 0.0f, ordinaryRoomCount: 3);
        if (rules == null)
            return false;

        var dungeon = new Dungeon { GenerationRules = rules };
        var source = new RoomTransition();
        try
        {
            if (!dungeon.TryStartRun(202UL, null, null, out _))
                return false;

            if (!dungeon.TryCreateRoom(RuntimeScreenId, null, null, default, out var room0))
                return false;

            var node0 = dungeon.ActivePlan.Nodes[0];
            var expected = node0.ContentOption?.CompletionPoints ?? -1;
            if (expected <= 0 || node0.Edges.Count == 0)
                return false;

            // Forward through the node's own progression exit advances to node 1.
            source.ExitId = node0.Edges[0].SourceExitId;
            if (!dungeon.TryCreateRoom(RuntimeScreenId, room0, source, default, out _))
                return false;

            // Source room cleared and scored once; entering node 1 added nothing.
            return dungeon.ActiveStats.BaseScore == expected &&
                dungeon.ActiveStats.RoomsCleared == 1;
        }
        finally
        {
            source.Free();
            dungeon.EndRun();
            dungeon.Free();
        }
    }

    // Clears the first room by advancing forward, then finalizes as GaveUp without clearing the
    // current room: the record keeps the earned points and gains nothing for the abandoned room.
    private bool GiveUpKeepsEarnedNotAbandoned()
    {
        var rules = BuildRules(combat: 1.0f, timed: 0.0f, special: 0.0f, ordinaryRoomCount: 3);
        if (rules == null)
            return false;

        var dungeon = new Dungeon { GenerationRules = rules };
        var source = new RoomTransition();
        try
        {
            if (!dungeon.TryStartRun(303UL, null, null, out _))
                return false;

            if (!dungeon.TryCreateRoom(RuntimeScreenId, null, null, default, out var room0))
                return false;

            var node0 = dungeon.ActivePlan.Nodes[0];
            var earned = node0.ContentOption?.CompletionPoints ?? -1;
            if (earned <= 0 || node0.Edges.Count == 0)
                return false;

            source.ExitId = node0.Edges[0].SourceExitId;
            if (!dungeon.TryCreateRoom(RuntimeScreenId, room0, source, default, out _))
                return false;

            // Give up while standing in the (uncleared) second room.
            var record = dungeon.FinalizeRun(DungeonRunOutcome.GaveUp);
            return record != null &&
                record.Outcome == DungeonRunOutcome.GaveUp &&
                record.BaseScore == earned &&
                record.FinalScore == earned;
        }
        finally
        {
            source.Free();
            dungeon.EndRun();
            dungeon.Free();
        }
    }

    // A Special-only first room (rest/shop) authors 0 points: clearing it still counts the room but
    // never changes the score.
    private bool ZeroPointSpecialClearsWithoutScore()
    {
        var rules = BuildRules(combat: 0.0f, timed: 0.0f, special: 1.0f, ordinaryRoomCount: 1);
        if (rules == null)
            return false;

        var dungeon = new Dungeon { GenerationRules = rules };
        try
        {
            if (!dungeon.TryStartRun(404UL, null, null, out _))
                return false;

            if (!dungeon.TryCreateRoom(RuntimeScreenId, null, null, default, out _))
                return false;

            var node0 = dungeon.ActivePlan.Nodes[0];
            if (node0.Kind != DungeonRoomKind.Special || (node0.ContentOption?.CompletionPoints ?? -1) != 0)
                return false;

            dungeon.MarkActiveNodeCleared();
            return dungeon.ActiveStats.BaseScore == 0 &&
                dungeon.ActiveStats.RoomsCleared == 1;
        }
        finally
        {
            dungeon.EndRun();
            dungeon.Free();
        }
    }

    private static int ContentPoints(string definitionPath, StringName contentId)
    {
        var definition = GD.Load<RoomTemplateDefinition>(definitionPath);
        var option = definition?.FindContentOption(contentId);
        return option?.CompletionPoints ?? -1;
    }

    // Assembles focused generation rules from the real room definitions, varying only the ordinary
    // kind weights and count so the entered nodes are predictable. The combat definition is always
    // present because the generator requires a usable combat fallback for any ordinary run.
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
