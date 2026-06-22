using Godot;

using System;
using System.Collections.Generic;

// Deterministic, seeded generator for an immutable dungeon run plan.
//
// Given the same rules, seed and requested overrides it always produces the same ordering,
// content choices, levels and edges. All randomness comes from a single RandomNumberGenerator
// initialized from the supplied seed (never Randomize()), and every selection is made and
// stored at generation time so entering rooms later cannot reroll anything.
//
// Generation is all-or-nothing: required definitions/content are validated up front and any
// problem returns a Failure with actionable errors and no partial plan.
public sealed class DungeonRunPlanGenerator
{
    private static readonly StringName CombatTopLeftExitId = "north_west";
    private static readonly StringName CombatTopRightExitId = "north_east";
    private static readonly StringName ProgressionExitId = "north_center";

    public DungeonRunPlanResult Generate(
        DungeonGenerationRules rules,
        ulong seed,
        int? ordinaryRoomCountOverride = null,
        int? startingRoomLevelOverride = null,
        int? levelIncreasePerRoomOverride = null)
    {
        if (rules == null)
            return DungeonRunPlanResult.Failure($"{nameof(DungeonGenerationRules)} is null.");

        var ordinaryRoomCount = Math.Max(0, ordinaryRoomCountOverride ?? rules.OrdinaryRoomCount);
        var startingLevel = Math.Max(1, startingRoomLevelOverride ?? rules.StartingRoomLevel);
        var levelDelta = Math.Max(0, levelIncreasePerRoomOverride ?? rules.LevelIncreasePerRoom);

        var errors = new List<string>();
        ValidatePreBossRequirements(rules, errors);
        ValidateBossRequirements(rules, errors);
        ValidateOrdinaryRequirements(rules, ordinaryRoomCount, errors);
        if (errors.Count > 0)
            return DungeonRunPlanResult.Failure(string.Join(" ", errors));

        // Build into a local list so any mid-generation failure discards everything (no
        // partial plan ever escapes).
        var nodes = new List<DungeonRoomNode>();
        var rng = new RandomNumberGenerator { Seed = seed };
        var consecutiveNonSpecial = 0;
        // Content id of the immediately preceding node when it was Special; null otherwise. It
        // is excluded from the next adjacent Special node's weighted draw so identical Special
        // content never lands in two neighbouring rooms.
        StringName previousSpecialContentId = null;

        for (var index = 0; index < ordinaryRoomCount; index++)
        {
            var kind = ResolveOrdinaryKind(rng, rules, consecutiveNonSpecial);
            consecutiveNonSpecial = kind == DungeonRoomKind.Special ? 0 : consecutiveNonSpecial + 1;

            if (!TryBuildOrdinaryNode(rng, rules, index, kind, startingLevel, levelDelta, previousSpecialContentId, out var node, out var error))
                return DungeonRunPlanResult.Failure(error);

            nodes.Add(node);
            previousSpecialContentId = kind == DungeonRoomKind.Special ? node.ContentOption?.Id : null;
        }

        var preBossIndex = ordinaryRoomCount;
        if (!TryBuildPreBossNode(rules, preBossIndex, startingLevel, levelDelta, out var preBossNode, out var preBossError))
            return DungeonRunPlanResult.Failure(preBossError);
        nodes.Add(preBossNode);

        var bossIndex = preBossIndex + 1;
        if (!TryBuildBossNode(rules, bossIndex, startingLevel, levelDelta, out var bossNode, out var bossError))
            return DungeonRunPlanResult.Failure(bossError);
        nodes.Add(bossNode);

        return DungeonRunPlanResult.Success(new DungeonRunPlan(seed, nodes));
    }

    private static DungeonRoomKind ResolveOrdinaryKind(RandomNumberGenerator rng, DungeonGenerationRules rules, int consecutiveNonSpecial)
    {
        // Pity forces a Special once the configured run of non-Special rooms is reached.
        if (rules.SpecialRoomPity > 0 && consecutiveNonSpecial >= rules.SpecialRoomPity)
            return DungeonRoomKind.Special;

        return RollOrdinaryKind(rng, rules);
    }

    private static DungeonRoomKind RollOrdinaryKind(RandomNumberGenerator rng, DungeonGenerationRules rules)
    {
        var combat = Math.Max(0.0f, rules.CombatWeight);
        var timed = Math.Max(0.0f, rules.TimedWeight);
        var special = Math.Max(0.0f, rules.SpecialWeight);
        var total = combat + timed + special;
        if (total <= 0.0f)
            return DungeonRoomKind.Combat;

        var roll = rng.Randf() * total;
        if (roll < combat)
            return DungeonRoomKind.Combat;

        roll -= combat;
        return roll < timed ? DungeonRoomKind.Timed : DungeonRoomKind.Special;
    }

    private bool TryBuildOrdinaryNode(
        RandomNumberGenerator rng,
        DungeonGenerationRules rules,
        int index,
        DungeonRoomKind kind,
        int startingLevel,
        int levelDelta,
        StringName previousSpecialContentId,
        out DungeonRoomNode node,
        out string error)
    {
        node = null;
        error = null;

        var level = ResolveLevel(startingLevel, levelDelta, index);
        var nextNodeId = NodeId(index + 1);

        switch (kind)
        {
            case DungeonRoomKind.Combat:
            {
                var definition = PickDefinition(rng, rules.CombatRoomDefinitions);
                if (definition == null)
                {
                    error = $"Combat room at index {index} has no valid room definition.";
                    return false;
                }

                var content = definition.PickContentOption(rng);
                if (content == null)
                {
                    error = $"Combat room at index {index} has no positive-weight content option.";
                    return false;
                }

                var edges = new List<DungeonRoomEdge>
                {
                    new(CombatTopLeftExitId, nextNodeId, levelDelta),
                    new(CombatTopRightExitId, nextNodeId, levelDelta),
                };
                node = new DungeonRoomNode(NodeId(index), index, kind, definition, content, level, edges);
                return true;
            }

            case DungeonRoomKind.Timed:
            {
                var definition = PickDefinition(rng, rules.TimedRoomDefinitions);
                if (definition == null)
                {
                    error = $"Timed room at index {index} has no valid room definition.";
                    return false;
                }

                var content = definition.PickContentOption(rng);
                if (content == null)
                {
                    error = $"Timed room at index {index} has no positive-weight content option.";
                    return false;
                }

                node = new DungeonRoomNode(NodeId(index), index, kind, definition, content, level, SingleProgressionEdge(nextNodeId, levelDelta));
                return true;
            }

            case DungeonRoomKind.Special:
            {
                var definition = rules.SpecialRoomDefinition;
                if (definition?.RoomScene == null)
                {
                    error = $"Special room at index {index} has no valid room definition.";
                    return false;
                }

                // Weighted selection draws only positive-weight options, so the zero-weight
                // Pre-Boss option can never land in an ordinary Special slot. Excluding the
                // previous adjacent Special's content keeps neighbouring Special rooms distinct
                // while still allowing a sole option when no alternative exists.
                var content = definition.PickContentOption(rng, previousSpecialContentId);
                if (content == null)
                {
                    error = $"Special room at index {index} has no positive-weight content option.";
                    return false;
                }

                node = new DungeonRoomNode(NodeId(index), index, kind, definition, content, level, SingleProgressionEdge(nextNodeId, levelDelta));
                return true;
            }

            default:
                error = $"Unsupported ordinary room kind '{kind}' at index {index}.";
                return false;
        }
    }

    private bool TryBuildPreBossNode(
        DungeonGenerationRules rules,
        int index,
        int startingLevel,
        int levelDelta,
        out DungeonRoomNode node,
        out string error)
    {
        node = null;
        error = null;

        var definition = rules.SpecialRoomDefinition;
        var content = definition?.FindContentOption(rules.PreBossContentId);
        if (content == null || !content.IsConfigured)
        {
            error = $"Pre-Boss content '{rules.PreBossContentId}' is missing or has no scene on the Special room definition.";
            return false;
        }

        var level = ResolveLevel(startingLevel, levelDelta, index);
        var bossNodeId = NodeId(index + 1);
        node = new DungeonRoomNode(NodeId(index), index, DungeonRoomKind.Special, definition, content, level, SingleProgressionEdge(bossNodeId, levelDelta));
        return true;
    }

    private bool TryBuildBossNode(
        DungeonGenerationRules rules,
        int index,
        int startingLevel,
        int levelDelta,
        out DungeonRoomNode node,
        out string error)
    {
        node = null;
        error = null;

        var definition = rules.BossRoomDefinition;
        var content = definition?.FindContentOption(rules.BossContentId);
        if (content == null || !content.IsConfigured)
        {
            error = $"Boss content '{rules.BossContentId}' is missing or has no scene on the Boss room definition.";
            return false;
        }

        var level = ResolveLevel(startingLevel, levelDelta, index);
        // Terminal node: no progression edge.
        node = new DungeonRoomNode(NodeId(index), index, DungeonRoomKind.Boss, definition, content, level, Array.Empty<DungeonRoomEdge>());
        return true;
    }

    private static IReadOnlyList<DungeonRoomEdge> SingleProgressionEdge(StringName destinationNodeId, int levelDelta)
    {
        return new List<DungeonRoomEdge> { new(ProgressionExitId, destinationNodeId, levelDelta) };
    }

    private static RoomTemplateDefinition PickDefinition(RandomNumberGenerator rng, Godot.Collections.Array<RoomTemplateDefinition> definitions)
    {
        var valid = new List<RoomTemplateDefinition>();
        if (definitions != null)
        {
            foreach (var definition in definitions)
            {
                // Only draw definitions that can actually yield a populated room, so a node
                // can never end up with null content.
                if (IsFullyUsableDefinition(definition))
                    valid.Add(definition);
            }
        }

        if (valid.Count == 0)
            return null;

        return valid[rng.RandiRange(0, valid.Count - 1)];
    }

    private static int ResolveLevel(int startingLevel, int levelDelta, int index)
    {
        return Math.Max(1, startingLevel + (levelDelta * index));
    }

    private static StringName NodeId(int index)
    {
        return new StringName($"node_{index}");
    }

    private static void ValidatePreBossRequirements(DungeonGenerationRules rules, List<string> errors)
    {
        if (rules.SpecialRoomDefinition?.RoomScene == null)
        {
            errors.Add("Special room definition is missing or has no room scene.");
            return;
        }

        var preBoss = rules.SpecialRoomDefinition.FindContentOption(rules.PreBossContentId);
        if (preBoss == null)
            errors.Add($"Special room definition has no Pre-Boss content option with id '{rules.PreBossContentId}'.");
        else if (!preBoss.IsConfigured)
            errors.Add($"Pre-Boss content option '{rules.PreBossContentId}' has no scene.");
    }

    private static void ValidateBossRequirements(DungeonGenerationRules rules, List<string> errors)
    {
        if (rules.BossRoomDefinition?.RoomScene == null)
        {
            errors.Add("Boss room definition is missing or has no room scene.");
            return;
        }

        var boss = rules.BossRoomDefinition.FindContentOption(rules.BossContentId);
        if (boss == null)
            errors.Add($"Boss room definition has no content option with id '{rules.BossContentId}'.");
        else if (!boss.IsConfigured)
            errors.Add($"Boss content option '{rules.BossContentId}' has no scene.");
    }

    private static void ValidateOrdinaryRequirements(DungeonGenerationRules rules, int ordinaryRoomCount, List<string> errors)
    {
        if (ordinaryRoomCount <= 0)
            return;

        // Combat is the base/fallback ordinary kind, so it must always be satisfiable with a
        // room scene and at least one positive-weight content option.
        if (!HasFullyUsableDefinition(rules.CombatRoomDefinitions))
            errors.Add("No combat room definition with a room scene and a positive-weight content option is configured for ordinary rooms.");

        if (Math.Max(0.0f, rules.TimedWeight) > 0.0f && !HasFullyUsableDefinition(rules.TimedRoomDefinitions))
            errors.Add("Timed room weight is positive but no timed room definition with a room scene and a positive-weight content option is configured.");

        // An ordinary Special can occur either by weight or because pity forces one within
        // this run length; if so the Special definition needs a positive-weight option.
        var specialPossible = Math.Max(0.0f, rules.SpecialWeight) > 0.0f ||
            (rules.SpecialRoomPity > 0 && ordinaryRoomCount > rules.SpecialRoomPity);
        if (specialPossible && rules.SpecialRoomDefinition != null && !HasRandomlySelectableOption(rules.SpecialRoomDefinition))
            errors.Add("Ordinary Special rooms can occur but the Special room definition has no positive-weight content option.");
    }

    // A definition the generator can actually produce a populated room from: it has a room
    // scene and at least one positive-weight (randomly selectable) content option.
    private static bool IsFullyUsableDefinition(RoomTemplateDefinition definition)
    {
        return definition?.RoomScene != null && HasRandomlySelectableOption(definition);
    }

    private static bool HasFullyUsableDefinition(Godot.Collections.Array<RoomTemplateDefinition> definitions)
    {
        if (definitions == null)
            return false;

        foreach (var definition in definitions)
        {
            if (IsFullyUsableDefinition(definition))
                return true;
        }

        return false;
    }

    private static bool HasRandomlySelectableOption(RoomTemplateDefinition definition)
    {
        if (definition?.ContentOptions == null)
            return false;

        foreach (var option in definition.ContentOptions)
        {
            if (option?.IsRandomlySelectable == true)
                return true;
        }

        return false;
    }
}
