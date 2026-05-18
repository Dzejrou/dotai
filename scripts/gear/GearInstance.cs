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
        GearQuality quality,
        int level,
        IReadOnlyList<GearStatModifier> mainStats,
        IReadOnlyList<GearStatModifier> substats)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Slot = slot;
        Quality = quality;
        Level = level;
        MainStats = mainStats ?? Array.Empty<GearStatModifier>();
        Substats = substats ?? Array.Empty<GearStatModifier>();
    }

    public GearDefinition Definition { get; }
    public EquipmentSlot Slot { get; }
    public GearQuality Quality { get; }
    public int Level { get; set; }
    public IReadOnlyList<GearStatModifier> MainStats { get; }
    public IReadOnlyList<GearStatModifier> Substats { get; }

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
}
