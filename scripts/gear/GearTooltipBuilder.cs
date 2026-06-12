using System.Collections.Generic;
using System.Text;

public static class GearTooltipBuilder
{
    private static readonly HashSet<string> PercentStats = new(System.StringComparer.Ordinal)
    {
        EquipmentStatIds.CritRate,
        EquipmentStatIds.CritDamage,
        EquipmentStatIds.MovementSpeedMultiplier,
        EquipmentStatIds.DamageBonus,
        EquipmentStatIds.PhysicalDamageBonus,
        EquipmentStatIds.FireDamageBonus,
        EquipmentStatIds.IceDamageBonus,
        EquipmentStatIds.PoisonDamageBonus,
        EquipmentStatIds.ArcaneDamageBonus,
        EquipmentStatIds.PhysicalResistance,
        EquipmentStatIds.FireResistance,
        EquipmentStatIds.IceResistance,
        EquipmentStatIds.PoisonResistance,
        EquipmentStatIds.ArcaneResistance,
    };

    public static string Build(GearInstance gear)
    {
        if (gear == null)
            return string.Empty;

        var builder = new StringBuilder();
        var displayName = gear.Definition?.DisplayName ?? string.Empty;
        if (!string.IsNullOrEmpty(displayName))
            builder.AppendLine(displayName);

        builder.AppendLine($"Quality: {gear.Quality}");
        builder.AppendLine($"Slot: {gear.Slot}");
        builder.AppendLine($"Level: {gear.Level}");

        if (gear.MainStats.Count > 0)
        {
            builder.AppendLine("Main:");
            foreach (var modifier in gear.MainStats)
                builder.AppendLine($"  {FormatModifier(modifier)}");
        }

        if (gear.Substats.Count > 0)
        {
            builder.AppendLine("Substats:");
            foreach (var modifier in gear.Substats)
                builder.AppendLine($"  {FormatModifier(modifier)}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatModifier(GearStatModifier modifier)
    {
        if (modifier == null || string.IsNullOrEmpty(modifier.StatId))
            return string.Empty;

        return FormatStatValue(modifier.StatId, modifier.Value);
    }

    // Shared by gear stat lines and comparison-delta lines so both use the same
    // display names, percent handling, and value formatting.
    public static string FormatStatValue(string statId, float value)
    {
        var sign = value >= 0 ? "+" : "";
        var name = GetDisplayName(statId);
        if (PercentStats.Contains(statId))
        {
            var percent = value * 100.0f;
            return $"{sign}{percent:0.##}% {name}";
        }

        return $"{sign}{value:0.##} {name}";
    }

    public static string GetDisplayName(string statId) => statId switch
    {
        EquipmentStatIds.MaxHealth => "Health",
        EquipmentStatIds.MaxMana => "Mana",
        EquipmentStatIds.MP5 => "MP5",
        EquipmentStatIds.Power => "Power",
        EquipmentStatIds.Haste => "Haste",
        EquipmentStatIds.MovementSpeedMultiplier => "Speed",
        EquipmentStatIds.CritRate => "Crit Rate",
        EquipmentStatIds.CritDamage => "Crit DMG",
        EquipmentStatIds.DamageBonus => "DMG",
        EquipmentStatIds.PhysicalDamageBonus => "Physical DMG",
        EquipmentStatIds.FireDamageBonus => "Fire DMG",
        EquipmentStatIds.IceDamageBonus => "Ice DMG",
        EquipmentStatIds.PoisonDamageBonus => "Poison DMG",
        EquipmentStatIds.ArcaneDamageBonus => "Arcane DMG",
        EquipmentStatIds.PhysicalResistance => "Physical Resist",
        EquipmentStatIds.FireResistance => "Fire Resist",
        EquipmentStatIds.IceResistance => "Ice Resist",
        EquipmentStatIds.PoisonResistance => "Poison Resist",
        EquipmentStatIds.ArcaneResistance => "Arcane Resist",
        _ => statId,
    };
}
