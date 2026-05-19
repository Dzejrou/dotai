using Godot;

using System;
using System.Collections.Generic;

public static class GearSaveSerializer
{
    public static GearInstanceSaveData Serialize(GearInstance gear)
    {
        if (gear == null)
            return null;

        var data = new GearInstanceSaveData
        {
            Slot = gear.Slot.ToString(),
            Quality = gear.Quality.ToString(),
            Level = gear.Level,
        };

        foreach (var modifier in gear.MainStats)
            data.MainStats.Add(SerializeModifier(modifier));

        foreach (var modifier in gear.Substats)
            data.Substats.Add(SerializeModifier(modifier));

        return data;
    }

    public static GearInstance Rehydrate(GearInstanceSaveData data, GearGenerationRules rules)
    {
        if (data == null)
            return null;

        if (!Enum.TryParse<EquipmentSlot>(data.Slot, out var slot))
        {
            GD.PushWarning($"{nameof(GearSaveSerializer)}: unknown EquipmentSlot '{data.Slot}'.");
            return null;
        }

        if (!Enum.TryParse<GearQuality>(data.Quality, out var quality))
        {
            GD.PushWarning($"{nameof(GearSaveSerializer)}: unknown GearQuality '{data.Quality}'.");
            return null;
        }

        var definition = GearGenerator.SynthesizeDefinition(slot, quality, rules);
        if (definition == null)
            return null;

        var mainStats = RehydrateModifiers(data.MainStats);
        var substats = RehydrateModifiers(data.Substats);
        var level = Math.Max(1, data.Level);

        return new GearInstance(definition, slot, quality, level, mainStats, substats);
    }

    private static GearStatModifierSaveData SerializeModifier(GearStatModifier modifier)
    {
        return new GearStatModifierSaveData
        {
            StatId = modifier?.StatId ?? string.Empty,
            Value = modifier?.Value ?? 0.0f,
        };
    }

    private static List<GearStatModifier> RehydrateModifiers(List<GearStatModifierSaveData> source)
    {
        var result = new List<GearStatModifier>(source?.Count ?? 0);
        if (source == null)
            return result;

        foreach (var entry in source)
        {
            if (entry == null || string.IsNullOrEmpty(entry.StatId))
                continue;

            result.Add(new GearStatModifier
            {
                StatId = entry.StatId,
                Value = entry.Value,
            });
        }

        return result;
    }
}
