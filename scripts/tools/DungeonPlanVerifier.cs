using Godot;

using System.Collections.Generic;
using System.Text;

// Headless developer tool that exercises DungeonRunPlanGenerator against the configured rules
// (plus a couple of in-memory edge cases) and prints PASS/FAIL lines, then quits with an exit
// code (0 = all passed, 1 = any failed). It is NOT part of normal runtime: it lives in its own
// scene so it never adds combat-log or startup-log spam to the game. Run it explicitly:
//
//   /Applications/Godot_mono.app/Contents/MacOS/Godot --headless \
//     --path /Users/jjindrak/Projects/Dotai \
//     --scene res://scenes/tools/dungeon_plan_verify.tscn
public partial class DungeonPlanVerifier : Node
{
    private const string RulesResourcePath = "res://resources/world/dungeon/dungeon_generation_rules.tres";

    [Export]
    public DungeonGenerationRules Rules { get; set; }

    private readonly DungeonRunPlanGenerator _generator = new();
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

        GD.Print("Dungeon run-plan verification:");
        RunChecks(rules);

        GD.Print(_failures == 0
            ? "All dungeon run-plan checks passed."
            : $"{_failures} dungeon run-plan check(s) failed.");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private void RunChecks(DungeonGenerationRules rules)
    {
        var planA = RequirePlan(rules, 12345);
        var planB = RequirePlan(rules, 12345);
        Check("same seed and inputs produce identical plans", planA != null && planB != null && PlansEqual(planA, planB));

        Check("different seeds can produce different ordinary sequences", DifferentSeedsDiffer(rules));

        Check("default plan has 12 nodes", planA != null && planA.Length == 12);

        Check("Pre-Boss is penultimate and Boss is terminal", PreBossAndBossTerminal(rules, planA));

        Check("Pre-Boss never appears in ordinary weighted Special slots", PreBossNeverOrdinary(rules));

        Check("both combat edges target the same next node and carry +1", CombatEdgesConsistent(planA, expectedDelta: 1));

        Check("levels progress from 1 by +1", LevelsProgress(planA, expectedStart: 1, expectedDelta: 1));

        Check("requested overrides change ordinary-room count", OverrideChangesCount(rules));

        Check("missing required content fails without a partial plan", MissingContentFailsCleanly(rules));

        Check("a rollable room kind without selectable content fails cleanly", RollableKindWithoutContentFailsCleanly(rules));
    }

    private DungeonRunPlan RequirePlan(DungeonGenerationRules rules, ulong seed)
    {
        var result = _generator.Generate(rules, seed);
        if (result.Succeeded)
            return result.Plan;

        GD.PrintErr($"  generation unexpectedly failed for seed {seed}: {result.Error}");
        return null;
    }

    private bool DifferentSeedsDiffer(DungeonGenerationRules rules)
    {
        var sequences = new HashSet<string>();
        for (ulong seed = 1; seed <= 16; seed++)
        {
            var result = _generator.Generate(rules, seed);
            if (!result.Succeeded)
                return false;

            sequences.Add(OrdinaryKindSequence(result.Plan));
        }

        return sequences.Count > 1;
    }

    private bool PreBossAndBossTerminal(DungeonGenerationRules rules, DungeonRunPlan plan)
    {
        if (plan == null || plan.Length < 2)
            return false;

        var preBoss = plan.Nodes[plan.Length - 2];
        var boss = plan.Nodes[plan.Length - 1];

        var preBossOk = preBoss.Kind == DungeonRoomKind.Special &&
            preBoss.ContentOption != null &&
            preBoss.ContentOption.Id == rules.PreBossContentId &&
            preBoss.Edges.Count == 1;

        var bossOk = boss.Kind == DungeonRoomKind.Boss &&
            boss.ContentOption != null &&
            boss.ContentOption.Id == rules.BossContentId &&
            boss.Edges.Count == 0;

        return preBossOk && bossOk;
    }

    private bool PreBossNeverOrdinary(DungeonGenerationRules rules)
    {
        for (ulong seed = 1; seed <= 32; seed++)
        {
            var result = _generator.Generate(rules, seed);
            if (!result.Succeeded)
                return false;

            var plan = result.Plan;
            // Ordinary nodes are everything except the last two (Pre-Boss, Boss).
            for (var i = 0; i < plan.Length - 2; i++)
            {
                var node = plan.Nodes[i];
                if (node.Kind == DungeonRoomKind.Special && node.ContentOption != null && node.ContentOption.Id == rules.PreBossContentId)
                    return false;
            }
        }

        return true;
    }

    private static bool CombatEdgesConsistent(DungeonRunPlan plan, int expectedDelta)
    {
        if (plan == null)
            return false;

        var sawCombat = false;
        foreach (var node in plan.Nodes)
        {
            if (node.Kind != DungeonRoomKind.Combat)
                continue;

            sawCombat = true;
            if (node.Edges.Count != 2)
                return false;

            var first = node.Edges[0];
            var second = node.Edges[1];
            if (first.DestinationNodeId != second.DestinationNodeId)
                return false;
            if (first.LevelDelta != expectedDelta || second.LevelDelta != expectedDelta)
                return false;
        }

        return sawCombat;
    }

    private static bool LevelsProgress(DungeonRunPlan plan, int expectedStart, int expectedDelta)
    {
        if (plan == null)
            return false;

        for (var i = 0; i < plan.Length; i++)
        {
            if (plan.Nodes[i].Level != expectedStart + (expectedDelta * i))
                return false;
        }

        return true;
    }

    private bool OverrideChangesCount(DungeonGenerationRules rules)
    {
        var result = _generator.Generate(rules, 7, ordinaryRoomCountOverride: 3, startingRoomLevelOverride: 5);
        if (!result.Succeeded)
            return false;

        var plan = result.Plan;
        // 3 ordinary + Pre-Boss + Boss, levels starting at 5.
        return plan.Length == 5 && plan.Nodes[0].Level == 5 && plan.Nodes[plan.Length - 1].Level == 9;
    }

    private bool MissingContentFailsCleanly(DungeonGenerationRules rules)
    {
        var broken = new DungeonGenerationRules
        {
            CombatRoomDefinitions = rules.CombatRoomDefinitions,
            TimedRoomDefinitions = rules.TimedRoomDefinitions,
            SpecialRoomDefinition = rules.SpecialRoomDefinition,
            BossRoomDefinition = rules.BossRoomDefinition,
            BossContentId = "definitely_missing_content",
        };

        var result = _generator.Generate(broken, 1);
        var bossCase = !result.Succeeded && result.Plan == null && !string.IsNullOrEmpty(result.Error);

        var empty = _generator.Generate(new DungeonGenerationRules(), 1);
        var emptyCase = !empty.Succeeded && empty.Plan == null && !string.IsNullOrEmpty(empty.Error);

        return bossCase && emptyCase;
    }

    private bool RollableKindWithoutContentFailsCleanly(DungeonGenerationRules rules)
    {
        // A combat definition with a room scene but no positive-weight content option must
        // fail generation rather than produce a plan with an empty combat encounter.
        PackedScene combatScene = null;
        if (rules.CombatRoomDefinitions != null && rules.CombatRoomDefinitions.Count > 0)
            combatScene = rules.CombatRoomDefinitions[0]?.RoomScene;

        var emptyCombatDefinition = new RoomTemplateDefinition { RoomScene = combatScene };
        var brokenRules = new DungeonGenerationRules
        {
            SpecialRoomDefinition = rules.SpecialRoomDefinition,
            BossRoomDefinition = rules.BossRoomDefinition,
            TimedRoomDefinitions = rules.TimedRoomDefinitions,
            CombatRoomDefinitions = new Godot.Collections.Array<RoomTemplateDefinition> { emptyCombatDefinition },
        };

        var result = _generator.Generate(brokenRules, 1);
        return !result.Succeeded && result.Plan == null && !string.IsNullOrEmpty(result.Error);
    }

    private static string OrdinaryKindSequence(DungeonRunPlan plan)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < plan.Length - 2; i++)
            builder.Append((int)plan.Nodes[i].Kind).Append(',');

        return builder.ToString();
    }

    private static bool PlansEqual(DungeonRunPlan a, DungeonRunPlan b)
    {
        if (a.Seed != b.Seed || a.Length != b.Length)
            return false;

        for (var i = 0; i < a.Length; i++)
        {
            var na = a.Nodes[i];
            var nb = b.Nodes[i];
            if (na.Id != nb.Id || na.Index != nb.Index || na.Kind != nb.Kind || na.Level != nb.Level)
                return false;
            if (na.Definition != nb.Definition || na.ContentOption != nb.ContentOption)
                return false;
            if (na.Edges.Count != nb.Edges.Count)
                return false;

            for (var e = 0; e < na.Edges.Count; e++)
            {
                var ea = na.Edges[e];
                var eb = nb.Edges[e];
                if (ea.SourceExitId != eb.SourceExitId || ea.DestinationNodeId != eb.DestinationNodeId || ea.LevelDelta != eb.LevelDelta)
                    return false;
            }
        }

        return true;
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
