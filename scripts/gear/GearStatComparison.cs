using System;
using System.Collections.Generic;

// Stat aggregation and delta calculation for the Shift-held "hovered vs equipped"
// gear comparison. Kept separate from tooltip rendering so it can be reused by
// other comparison surfaces later.
public static class GearStatComparison
{
    // Differences at or below this are treated as equal and omitted.
    public const float Epsilon = 0.0001f;

    public readonly struct StatDelta
    {
        public StatDelta(string statId, float difference)
        {
            StatId = statId;
            Difference = difference;
        }

        public string StatId { get; }
        public float Difference { get; }
    }

    // Sums duplicate main-stat and substat modifiers by exact stat id.
    public static Dictionary<string, float> AggregateStatTotals(GearInstance gear)
    {
        var totals = new Dictionary<string, float>(StringComparer.Ordinal);
        if (gear == null)
            return totals;

        foreach (var modifier in gear.AllModifiers)
        {
            if (string.IsNullOrEmpty(modifier.StatId))
                continue;

            totals.TryGetValue(modifier.StatId, out var total);
            totals[modifier.StatId] = total + modifier.Value;
        }

        return totals;
    }

    // hovered total - equipped total over the union of stat ids on both items, so a
    // stat present only on the equipped item shows up as a loss. Gains sort before
    // losses; within each group the order is alphabetical by stat id so it's stable.
    public static List<StatDelta> ComputeDeltas(GearInstance hovered, GearInstance equipped)
    {
        var hoveredTotals = AggregateStatTotals(hovered);
        var equippedTotals = AggregateStatTotals(equipped);

        var statIds = new HashSet<string>(hoveredTotals.Keys, StringComparer.Ordinal);
        statIds.UnionWith(equippedTotals.Keys);

        var deltas = new List<StatDelta>();
        foreach (var statId in statIds)
        {
            hoveredTotals.TryGetValue(statId, out var hoveredTotal);
            equippedTotals.TryGetValue(statId, out var equippedTotal);

            var difference = hoveredTotal - equippedTotal;
            if (Math.Abs(difference) <= Epsilon)
                continue;

            deltas.Add(new StatDelta(statId, difference));
        }

        deltas.Sort(static (a, b) =>
        {
            var aIsGain = a.Difference > 0.0f;
            var bIsGain = b.Difference > 0.0f;
            if (aIsGain != bIsGain)
                return aIsGain ? -1 : 1;
            return string.CompareOrdinal(a.StatId, b.StatId);
        });

        return deltas;
    }
}
