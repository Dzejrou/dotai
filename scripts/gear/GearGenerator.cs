using Godot;

using System;
using System.Collections.Generic;

public static class GearGenerator
{
    public static GearInstance Generate(EquipmentSlot slot, ItemQuality quality, GearGenerationRules rules)
    {
        if (rules == null)
        {
            GD.PushError($"{nameof(GearGenerator)}: rules resource is null; cannot generate gear.");
            return null;
        }

        var slotRules = rules.GetSlotRules(slot);
        if (slotRules == null)
        {
            GD.PushError($"{nameof(GearGenerator)}: missing slot rules for {slot}.");
            return null;
        }

        var qualityRules = rules.GetQualityRules(quality);
        if (qualityRules == null)
        {
            GD.PushError($"{nameof(GearGenerator)}: missing quality rules for {quality}.");
            return null;
        }

        var mainStatIds = PickMainStats(slotRules);
        var mainStats = new List<GearStatModifier>(mainStatIds.Count);
        foreach (var statId in mainStatIds)
        {
            var maxValue = rules.GetMainStatMaxValue(statId, quality);
            var value = GearStatScaling.ComputeMainStatValue(statId, maxValue, level: 1);
            mainStats.Add(new GearStatModifier { StatId = statId, Value = value });
        }

        var substats = RollSubstats(rules, qualityRules, mainStatIds);

        var definition = SynthesizeDefinition(slot, quality, rules);
        if (definition == null)
            return null;

        return new GearInstance(definition, slot, quality, level: 1, mainStats, substats);
    }

    // Build a display-only GearDefinition for a (slot, quality) pair. Used by save/load
    // rehydration and by Generate(). Returns null if the rules resource is missing
    // entries for the requested slot.
    public static GearDefinition SynthesizeDefinition(EquipmentSlot slot, ItemQuality quality, GearGenerationRules rules)
    {
        if (rules == null)
        {
            GD.PushWarning($"{nameof(GearGenerator)}: rules resource is null; cannot synthesize gear definition.");
            return null;
        }

        var slotRules = rules.GetSlotRules(slot);
        if (slotRules == null)
        {
            GD.PushWarning($"{nameof(GearGenerator)}: missing slot rules for {slot}.");
            return null;
        }

        return new GearDefinition
        {
            Id = $"generated_{slot}_{quality}".ToLowerInvariant(),
            DisplayName = string.IsNullOrEmpty(slotRules.DisplayName)
                ? slot.ToString()
                : slotRules.DisplayName,
            Icon = slotRules.Icon,
            MaxStackSize = 1,
            Slot = slot,
            Quality = quality,
        };
    }

    private static List<string> PickMainStats(GearSlotRules slotRules)
    {
        var picks = new List<string>(2);
        var first = PickRandom(slotRules.MainStat1Pool);
        if (!string.IsNullOrEmpty(first))
            picks.Add(first);

        // Pull from pool 2 while avoiding the exact stat already chosen for slot 1.
        var pool2 = new List<string>(slotRules.MainStat2Pool.Count);
        foreach (var statId in slotRules.MainStat2Pool)
        {
            if (!string.IsNullOrEmpty(statId) && !picks.Contains(statId))
                pool2.Add(statId);
        }

        var second = PickRandom(pool2);
        if (!string.IsNullOrEmpty(second))
            picks.Add(second);

        return picks;
    }

    private static List<GearStatModifier> RollSubstats(
        GearGenerationRules rules,
        GearQualityRules qualityRules,
        IList<string> mainStatIds)
    {
        var available = new List<string>(rules.SubstatPool.Count);
        foreach (var statId in rules.SubstatPool)
        {
            if (string.IsNullOrEmpty(statId))
                continue;
            if (mainStatIds.Contains(statId))
                continue;
            available.Add(statId);
        }

        Shuffle(available);

        var count = Math.Min(qualityRules.SubstatCount, available.Count);
        var result = new List<GearStatModifier>(count);
        for (var i = 0; i < count; i++)
        {
            var statId = available[i];
            if (!rules.TryGetSubstatValue(qualityRules.Quality, statId, out var value))
                continue;

            result.Add(new GearStatModifier { StatId = statId, Value = value });
        }

        return result;
    }

    private static string PickRandom(IList<string> source)
    {
        if (source == null || source.Count == 0)
            return string.Empty;
        return source[(int)(GD.Randi() % (uint)source.Count)];
    }

    private static void Shuffle(IList<string> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = (int)(GD.Randi() % (uint)(i + 1));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
