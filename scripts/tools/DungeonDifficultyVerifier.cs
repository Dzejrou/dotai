using Godot;

using System;

// Headless developer tool that exercises the dungeon difficulty slice end to end: the shipped
// option-to-reward tables, the additive difficulty multiplier (including its 0.25x/4.25x bounds),
// that the selected starting level and per-room increase drive generated plans and are snapshotted
// immutably on the run, score finalization/rounding with a non-1.0 multiplier, the history save
// round-trip (and legacy defaults), and the actor stat buff (uniqueness/non-stacking, resolved
// modifiers, full boosted spawn health, identical modifiers across initial and later spawns, and the
// resolved-resistance clamp with full immunity reachable). It prints PASS/FAIL lines and quits with
// an exit code (0 = all passed). Run it:
//
//   /Applications/Godot_mono.app/Contents/MacOS/Godot --headless \
//     --path /Users/jjindrak/Projects/Dotai \
//     --scene res://scenes/tools/dungeon_difficulty_verify.tscn
public partial class DungeonDifficultyVerifier : Node
{
    private const string DifficultyRulesPath = "res://resources/world/dungeon/dungeon_difficulty_rules.tres";
    private const string GenerationRulesPath = "res://resources/world/dungeon/dungeon_generation_rules.tres";
    private const string TargetDummyScenePath = "res://scenes/actors/enemies/target_dummy.tscn";

    private const float Tolerance = 0.0005f;

    private int _failures;

    public override void _Ready()
    {
        GD.Print("Dungeon difficulty verification:");

        Check("shipped option-to-reward tables match the spec", OptionTablesMatchSpec());
        Check("difficulty multiplier is additive (+25% and +50% give +75% / 1.75x)", MultiplierIsAdditive());
        Check("shipped multiplier bounds are 0.25x (min) and 4.25x (max)", MultiplierBounds());
        Check("selected starting level and level increase drive the plan and snapshot", SelectionsDrivePlanAndSnapshot());
        Check("score finalizes with the run multiplier, rounds away from zero, and is immutable", ScoreFinalizationRoundsAndIsImmutable());
        Check("history save round-trips the difficulty fields", SaveRoundTripsDifficulty());
        Check("legacy records without difficulty load with unknown defaults", LegacyRecordDefaults());

        Check("the dungeon stat buff is unique and never stacks", BuffIsUniqueNonStacking());
        Check("a buffed actor resolves boosted health, power, resistance and damage", BuffResolvesModifiers());
        Check("a buffed actor begins at full boosted health without a combat free heal", BuffSpawnHealthVsNoFreeHeal());
        Check("initial and later spawned actors receive identical modifiers", SpawnerActorsReceiveSameModifiers());
        Check("resolved resistance clamps to the configured maximum, immunity reachable, negatives kept", ResistanceClampImmunityAndNegative());

        GD.Print(_failures == 0
            ? "All dungeon difficulty checks passed."
            : $"{_failures} dungeon difficulty check(s) failed.");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    // Pure-logic checks ----------------------------------------------------------------------------

    private bool OptionTablesMatchSpec()
    {
        var rules = GD.Load<DungeonDifficultyRules>(DifficultyRulesPath);
        if (rules == null)
            return false;

        return OptionsMatch(rules.StartingLevelOptions, new[]
            {
                (10.0f, -0.75f), (20.0f, -0.50f), (30.0f, -0.25f),
                (40.0f, 0.0f), (50.0f, 0.25f), (60.0f, 0.50f),
            }) &&
            OptionsMatch(rules.LevelIncreaseOptions, new[]
            {
                (1.0f, 0.0f), (2.0f, 0.25f), (3.0f, 0.50f),
            }) &&
            OptionsMatch(rules.EnemyStatOptions, new[]
            {
                (0.0f, 0.0f), (0.2f, 0.25f), (0.4f, 0.50f), (0.6f, 0.75f),
            }) &&
            Approx(rules.MaxResistance, 1.0f);
    }

    private bool MultiplierIsAdditive()
    {
        var rules = GD.Load<DungeonDifficultyRules>(DifficultyRulesPath);
        if (rules == null)
            return false;

        // Starting level 40 (0%), level increase +2 (+25%), Health/Power +40% (+50%); rest neutral.
        var selection = DungeonDifficultySelection.FromIndices(rules, 3, 1, 2, 0, 0);

        // Additive: +25% + +50% = +75% -> 1.75x. Multiplicative would be 1.25 * 1.5 = 1.875x.
        return Approx(selection.TotalRewardAdjustment, 0.75f) &&
            Approx(selection.DifficultyMultiplier, 1.75f);
    }

    private bool MultiplierBounds()
    {
        var rules = GD.Load<DungeonDifficultyRules>(DifficultyRulesPath);
        if (rules == null)
            return false;

        // All-minimum: level 10 (-75%), +1 (0%), 0% stats -> 0.25x.
        var minimum = DungeonDifficultySelection.FromIndices(rules, 0, 0, 0, 0, 0);
        // All-maximum: level 60 (+50%), +3 (+50%), +60% on all three stats (+75% each) -> 4.25x.
        var maximum = DungeonDifficultySelection.FromIndices(rules, 5, 2, 3, 3, 3);

        return Approx(minimum.DifficultyMultiplier, 0.25f) &&
            Approx(maximum.DifficultyMultiplier, 4.25f);
    }

    private bool SelectionsDrivePlanAndSnapshot()
    {
        var generationRules = GD.Load<DungeonGenerationRules>(GenerationRulesPath);
        var difficultyRules = GD.Load<DungeonDifficultyRules>(DifficultyRulesPath);
        if (generationRules == null || difficultyRules == null)
            return false;

        // Starting level 50, +2 per room.
        var selection = DungeonDifficultySelection.FromIndices(difficultyRules, 4, 1, 0, 0, 0);
        if (selection.StartingRoomLevel != 50 || selection.LevelIncreasePerRoom != 2)
            return false;

        var dungeon = new Dungeon { GenerationRules = generationRules };
        try
        {
            if (!dungeon.TryStartRun(7UL, ordinaryRoomCount: 3, selection, out var error))
            {
                GD.PrintErr($"  unexpected start failure: {error}");
                return false;
            }

            var plan = dungeon.ActivePlan;
            if (plan == null || plan.Length != 5)
                return false;

            // First room uses the selected starting level; every edge applies the selected increase.
            for (var i = 0; i < plan.Length; i++)
            {
                if (plan.Nodes[i].Level != 50 + (2 * i))
                    return false;
            }

            // The immutable snapshot is stored on the run exactly as supplied.
            var stats = dungeon.ActiveStats;
            return stats != null &&
                stats.StartingRoomLevel == 50 &&
                ReferenceEquals(stats.Difficulty, selection);
        }
        finally
        {
            dungeon.EndRun();
            dungeon.Free();
        }
    }

    private bool ScoreFinalizationRoundsAndIsImmutable()
    {
        // A single +25% selection -> 1.25x, chosen so a base score of 10 lands on a .5 boundary
        // (12.5) and proves midpoint-away-from-zero rounding (-> 13).
        var selection = new DungeonDifficultySelection(40, 2, 0.0f, 0.0f, 0.0f, 0.0f, 0.25f, 0.0f, 0.0f, 0.0f);
        if (!Approx(selection.DifficultyMultiplier, 1.25f))
            return false;

        var stats = new DungeonRunStats(11UL, 40, 5, selection);
        stats.AddScore(10);

        var record = new DungeonRunRecord(stats, DungeonRunOutcome.Completed, DateTimeOffset.Now);

        // Mutating the live stats after the snapshot must not change the record.
        stats.AddScore(999);

        return record.BaseScore == 10 &&
            record.DifficultyMultiplier.HasValue && Approx(record.DifficultyMultiplier.Value, 1.25f) &&
            record.FinalScore == 13 &&
            record.LevelIncreasePerRoom == 2 &&
            record.HealthPowerBonus.HasValue && Approx(record.HealthPowerBonus.Value, 0.0f) &&
            DungeonRunRecord.ComputeFinalScore(10, 1.25f) == 13;
    }

    private bool SaveRoundTripsDifficulty()
    {
        var selection = new DungeonDifficultySelection(50, 2, 0.4f, 0.6f, 0.2f, 0.25f, 0.25f, 0.5f, 0.75f, 0.25f);
        var stats = new DungeonRunStats(21UL, 50, 6, selection);
        stats.AddScore(200);
        stats.RecordRoomReached(3, 54);

        var record = new DungeonRunRecord(stats, DungeonRunOutcome.GaveUp, DateTimeOffset.Now);

        var saveData = DungeonRunRecordSaveData.FromRecord(record);
        if (!saveData.TryToRecord(out var restored))
            return false;

        return restored.StartingRoomLevel == 50 &&
            restored.LevelIncreasePerRoom == 2 &&
            restored.HealthPowerBonus.HasValue && Approx(restored.HealthPowerBonus.Value, 0.4f) &&
            restored.ResistanceBonus.HasValue && Approx(restored.ResistanceBonus.Value, 0.6f) &&
            restored.DamageBonus.HasValue && Approx(restored.DamageBonus.Value, 0.2f) &&
            restored.DifficultyMultiplier.HasValue && Approx(restored.DifficultyMultiplier.Value, record.DifficultyMultiplier.Value) &&
            restored.FinalScore == record.FinalScore;
    }

    private bool LegacyRecordDefaults()
    {
        // A record saved before difficulty existed: identity/progress present, no difficulty fields.
        var legacy = new DungeonRunRecordSaveData
        {
            Outcome = nameof(DungeonRunOutcome.Completed),
            Seed = 5UL,
            StartingRoomLevel = 3,
            PlannedRunLength = 6,
            RoomsCleared = 2,
            FurthestRoomIndex = 2,
            FurthestRoomLevel = 4,
            // Score present, difficulty absent.
            BaseScore = 100,
            DifficultyMultiplier = 1.0f,
            FinalScore = 100,
        };

        if (!legacy.TryToRecord(out var record))
            return false;

        // Difficulty fields load as unknown (null) without dropping the record or its score.
        return record.LevelIncreasePerRoom == null &&
            record.HealthPowerBonus == null &&
            record.ResistanceBonus == null &&
            record.DamageBonus == null &&
            record.BaseScore == 100 &&
            record.FinalScore == 100 &&
            record.StartingRoomLevel == 3;
    }

    // Actor buff checks ----------------------------------------------------------------------------

    private bool BuffIsUniqueNonStacking()
    {
        var dummy = InstantiateDummy();
        if (dummy == null)
            return false;

        try
        {
            var controller = dummy.GetNodeOrNull<StatusEffectController>("StatusEffectController");
            if (controller == null)
                return false;

            var buff = new DungeonActorBuff(0.4f, 0.0f, 0.0f);
            buff.ApplyTo(dummy, initializeHealth: true);
            var afterFirst = dummy.ResolvedMaxHealth;

            // Re-applying (e.g. room restoration / reconciliation) must not stack a second copy.
            buff.ApplyTo(dummy, initializeHealth: true);

            return controller.GetStatusCount(DungeonActorBuff.StatusKey) == 1 &&
                dummy.ResolvedMaxHealth == afterFirst;
        }
        finally
        {
            dummy.QueueFree();
        }
    }

    private bool BuffResolvesModifiers()
    {
        var dummy = InstantiateDummy();
        if (dummy == null)
            return false;

        try
        {
            var stats = dummy.GetNodeOrNull<Stats>("Stats");
            if (stats == null)
                return false;

            stats.Power = 100.0f;
            var baseMaxHealth = dummy.ResolvedMaxHealth;

            new DungeonActorBuff(0.4f, 0.6f, 5.0f).ApplyTo(dummy, initializeHealth: true);

            return dummy.ResolvedMaxHealth == (int)Math.Round(baseMaxHealth * 1.4) &&
                Approx(dummy.ResolvedPower, 140.0f) &&
                Approx(dummy.ResolveResistance(DamageSchool.Physical), 0.6f) &&
                Approx(dummy.ResolveResistance(DamageSchool.Arcane), 0.6f) &&
                Approx(dummy.ResolveDamageBonus(DamageSchool.Fire), 5.0f) &&
                Approx(dummy.ResolveDamageBonus(DamageSchool.Poison), 5.0f);
        }
        finally
        {
            dummy.QueueFree();
        }
    }

    private bool BuffSpawnHealthVsNoFreeHeal()
    {
        // Spawn-initialization: the actor begins at full boosted Max Health.
        var spawnDummy = InstantiateDummy();
        if (spawnDummy == null)
            return false;

        var spawnOk = false;
        try
        {
            new DungeonActorBuff(0.4f, 0.0f, 0.0f).ApplyTo(spawnDummy, initializeHealth: true);
            spawnOk = spawnDummy.CurrentHealth == spawnDummy.ResolvedMaxHealth &&
                spawnDummy.ResolvedMaxHealth > spawnDummy.MaxHealableHealth - 1 &&
                spawnDummy.ResolvedMaxHealth > 999;
        }
        finally
        {
            spawnDummy.QueueFree();
        }

        // Combat-time application (initializeHealth: false) preserves the no-free-heal behavior:
        // current health is unchanged while Max grows.
        var combatDummy = InstantiateDummy();
        if (combatDummy == null)
            return false;

        try
        {
            var health = combatDummy.GetNodeOrNull<HealthState>("HealthState");
            if (health == null)
                return false;

            health.SetCurrent(400);
            new DungeonActorBuff(0.4f, 0.0f, 0.0f).ApplyTo(combatDummy, initializeHealth: false);

            var noFreeHeal = combatDummy.CurrentHealth == 400 &&
                combatDummy.MaxHealthValue == (int)Math.Round(999 * 1.4);

            return spawnOk && noFreeHeal;
        }
        finally
        {
            combatDummy.QueueFree();
        }
    }

    private bool SpawnerActorsReceiveSameModifiers()
    {
        var dummyScene = GD.Load<PackedScene>(TargetDummyScenePath);
        if (dummyScene == null)
            return false;

        var spawner = new ActorSpawner();
        spawner.Options.Add(new RandomActorSpawnOption { ActorScene = dummyScene, Weight = 1.0f });
        spawner.MinLevel = 1;
        spawner.MaxLevel = 1;
        spawner.SetDungeonActorBuff(new DungeonActorBuff(0.4f, 0.6f, 5.0f));
        AddChild(spawner);

        try
        {
            // Initial spawn.
            spawner.Respawn();
            if (spawner.CurrentSpawnedActor is not CombatCharacter first)
                return false;

            var firstController = first.GetNodeOrNull<StatusEffectController>("StatusEffectController");
            var initialOk = firstController != null &&
                firstController.HasStatus(DungeonActorBuff.StatusKey) &&
                first.CurrentHealth == first.ResolvedMaxHealth;
            var firstMaxHealth = first.ResolvedMaxHealth;
            var firstResistance = first.ResolveResistance(DamageSchool.Ice);
            var firstDamage = first.ResolveDamageBonus(DamageSchool.Ice);

            // Later spawn through the same authoritative spawn point.
            spawner.Respawn();
            if (spawner.CurrentSpawnedActor is not CombatCharacter later)
                return false;

            var laterController = later.GetNodeOrNull<StatusEffectController>("StatusEffectController");
            var laterOk = laterController != null && laterController.HasStatus(DungeonActorBuff.StatusKey);

            return initialOk && laterOk &&
                later.ResolvedMaxHealth == firstMaxHealth &&
                Approx(later.ResolveResistance(DamageSchool.Ice), firstResistance) &&
                Approx(later.ResolveDamageBonus(DamageSchool.Ice), firstDamage);
        }
        finally
        {
            spawner.QueueFree();
        }
    }

    private bool ResistanceClampImmunityAndNegative()
    {
        var previousMax = CombatCharacter.MaxResolvedResistance;
        var dummy = InstantiateDummy();
        var negativeDummy = InstantiateDummy();
        if (dummy == null || negativeDummy == null)
        {
            dummy?.QueueFree();
            negativeDummy?.QueueFree();
            CombatCharacter.MaxResolvedResistance = previousMax;
            return false;
        }

        try
        {
            var stats = dummy.GetNodeOrNull<Stats>("Stats");
            if (stats == null)
                return false;

            stats.PhysicalResistance = 0.6f;
            new DungeonActorBuff(0.0f, 0.6f, 0.0f).ApplyTo(dummy, initializeHealth: true);

            // Summed resistance is 1.2; a configurable cap clamps it down.
            CombatCharacter.MaxResolvedResistance = 0.75f;
            var clampedTo75 = Approx(dummy.ResolveResistance(DamageSchool.Physical), 0.75f);

            // Default cap of 1.0 keeps full immunity (100%) reachable.
            CombatCharacter.MaxResolvedResistance = 1.0f;
            var immunityReachable = Approx(dummy.ResolveResistance(DamageSchool.Physical), 1.0f);

            // Negative resistance is never clamped up.
            var negativeStats = negativeDummy.GetNodeOrNull<Stats>("Stats");
            if (negativeStats == null)
                return false;

            negativeStats.IceResistance = -0.5f;
            var negativeKept = Approx(negativeDummy.ResolveResistance(DamageSchool.Ice), -0.5f);

            return clampedTo75 && immunityReachable && negativeKept;
        }
        finally
        {
            dummy.QueueFree();
            negativeDummy.QueueFree();
            CombatCharacter.MaxResolvedResistance = previousMax;
        }
    }

    // Helpers --------------------------------------------------------------------------------------

    private CombatCharacter InstantiateDummy()
    {
        var scene = GD.Load<PackedScene>(TargetDummyScenePath);
        if (scene?.Instantiate() is not CombatCharacter dummy)
        {
            GD.PrintErr($"  could not instantiate a {nameof(CombatCharacter)} from '{TargetDummyScenePath}'.");
            return null;
        }

        AddChild(dummy);
        return dummy;
    }

    private bool OptionsMatch(Godot.Collections.Array<DungeonDifficultyOption> options, (float Value, float Reward)[] expected)
    {
        if (options == null || options.Count != expected.Length)
            return false;

        for (var i = 0; i < expected.Length; i++)
        {
            var option = options[i];
            if (option == null || !Approx(option.Value, expected[i].Value) || !Approx(option.RewardAdjustment, expected[i].Reward))
                return false;
        }

        return true;
    }

    private static bool Approx(float a, float b)
    {
        return Math.Abs(a - b) <= Tolerance;
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
