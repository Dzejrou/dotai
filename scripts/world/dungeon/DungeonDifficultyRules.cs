using Godot;

using System;

// Centrally inspector-editable difficulty tuning for dungeon runs. Holds the data-driven
// option-to-reward tables (so the HUB never hardcodes option values or reward bonuses) plus the
// resolved-resistance cap applied to buffed actors. The shipped defaults live in
// resources/world/dungeon/dungeon_difficulty_rules.tres and are mirrored by CreateDefault() so
// verifiers and a missing-resource fallback agree with the authored resource.
//
// The first three starting-level tiers (10/20/30) are currently useful for testing and may be
// removed from the final game, which is exactly why the options stay data-driven here instead of
// being baked into the UI.
[GlobalClass]
public partial class DungeonDifficultyRules : Resource
{
    // Mutually exclusive starting room level choices. Value is the absolute level of the first room.
    [Export]
    public Godot.Collections.Array<DungeonDifficultyOption> StartingLevelOptions { get; set; } = new();

    // Mutually exclusive per-room level increase choices. Value is the level delta applied on every
    // progression edge.
    [Export]
    public Godot.Collections.Array<DungeonDifficultyOption> LevelIncreaseOptions { get; set; } = new();

    // Shared option table for the three independent enemy stat categories (Health/Power, Resistance,
    // Damage). Value is the additive-percent actor bonus (0.2 = +20%).
    [Export]
    public Godot.Collections.Array<DungeonDifficultyOption> EnemyStatOptions { get; set; } = new();

    // Maximum resolved resistance a buffed actor can reach. Default 1.0 keeps full immunity reachable
    // (100%) while preventing a stacked resistance bonus from exceeding it. Negative resistance is
    // never clamped here.
    [Export]
    public float MaxResistance { get; set; } = 1.0f;

    // Default selection is always the first option of each row, matching the shipped tables: starting
    // level 10, level increase +1, and 0% for all three stat categories.
    public const int DefaultStartingLevelIndex = 0;
    public const int DefaultLevelIncreaseIndex = 0;
    public const int DefaultEnemyStatIndex = 0;

    // Builds an instance with the shipped default tables. Mirrors dungeon_difficulty_rules.tres so a
    // missing/unassigned resource still produces correct behavior and verifiers can construct rules
    // without loading the resource.
    public static DungeonDifficultyRules CreateDefault()
    {
        var rules = new DungeonDifficultyRules { MaxResistance = 1.0f };

        rules.StartingLevelOptions = new Godot.Collections.Array<DungeonDifficultyOption>
        {
            new(10.0f, -0.75f),
            new(20.0f, -0.50f),
            new(30.0f, -0.25f),
            new(40.0f, 0.0f),
            new(50.0f, 0.25f),
            new(60.0f, 0.50f),
        };

        rules.LevelIncreaseOptions = new Godot.Collections.Array<DungeonDifficultyOption>
        {
            new(1.0f, 0.0f),
            new(2.0f, 0.25f),
            new(3.0f, 0.50f),
        };

        rules.EnemyStatOptions = new Godot.Collections.Array<DungeonDifficultyOption>
        {
            new(0.0f, 0.0f),
            new(0.2f, 0.25f),
            new(0.4f, 0.50f),
            new(0.6f, 0.75f),
        };

        return rules;
    }

    // Clamps an option index to the valid range for the given list, returning -1 only when the list
    // is empty. Callers use this so an out-of-range stored selection resolves to a usable option
    // rather than throwing.
    public static int ClampIndex(Godot.Collections.Array<DungeonDifficultyOption> options, int index)
    {
        if (options == null || options.Count == 0)
            return -1;

        return Math.Clamp(index, 0, options.Count - 1);
    }
}
