using System;
using System.Collections.Generic;

// Runtime gear identity. Owns the rolled main/substat modifiers per-pickup.
// Definition is an in-memory display shell (icon, name) — for generated gear the
// Definition is synthesized by GearGenerator and is not persisted as a .tres.
public sealed class GearInstance
{
    public GearInstance(
        GearDefinition definition,
        EquipmentSlot slot,
        ItemQuality quality,
        int level,
        IReadOnlyList<GearStatModifier> mainStats,
        IReadOnlyList<GearStatModifier> substats,
        int currentXp = 0)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Slot = slot;
        Quality = quality;
        Level = level;
        MainStats = mainStats ?? Array.Empty<GearStatModifier>();
        _substats = substats != null
            ? new List<GearStatModifier>(substats)
            : new List<GearStatModifier>();
        CurrentXp = Math.Max(0, currentXp);
    }

    private readonly List<GearStatModifier> _substats;

    public GearDefinition Definition { get; }
    public EquipmentSlot Slot { get; }
    public ItemQuality Quality { get; }
    public int Level { get; set; }
    public int CurrentXp { get; set; }
    public IReadOnlyList<GearStatModifier> MainStats { get; }
    public IReadOnlyList<GearStatModifier> Substats => _substats;

    // Used by GearSubstatProgression when a milestone roll adds a brand-new substat
    // (i.e. the gear had fewer substats than its quality's intended count).
    public void AddSubstat(GearStatModifier modifier)
    {
        if (modifier == null || string.IsNullOrEmpty(modifier.StatId))
            return;
        _substats.Add(modifier);
    }

    public IEnumerable<GearStatModifier> AllModifiers
    {
        get
        {
            foreach (var modifier in MainStats)
            {
                if (modifier != null)
                    yield return modifier;
            }
            foreach (var modifier in Substats)
            {
                if (modifier != null)
                    yield return modifier;
            }
        }
    }

    // Recomputes each main stat's Value from the rules: maxValue * level / 20, with the
    // integer-stat floor of 1 preserved (same rule GearGenerator applies at generation time).
    public void RecalculateMainStatsForLevel(GearGenerationRules rules)
    {
        if (rules == null)
            return;

        foreach (var modifier in MainStats)
        {
            if (modifier == null || string.IsNullOrEmpty(modifier.StatId))
                continue;

            var maxValue = rules.GetMainStatMaxValue(modifier.StatId, Quality);
            modifier.Value = GearStatScaling.ComputeMainStatValue(modifier.StatId, maxValue, Level);
        }

    }
}
