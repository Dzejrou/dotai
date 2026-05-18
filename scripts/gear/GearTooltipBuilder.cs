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

    private static string FormatModifier(GearStatModifier modifier)
    {
        if (modifier == null || string.IsNullOrEmpty(modifier.StatId))
            return string.Empty;

        var sign = modifier.Value >= 0 ? "+" : "";
        if (PercentStats.Contains(modifier.StatId))
        {
            var percent = modifier.Value * 100.0f;
            return $"{sign}{percent:0.##}% {modifier.StatId}";
        }

        return $"{sign}{modifier.Value:0.##} {modifier.StatId}";
    }
}
