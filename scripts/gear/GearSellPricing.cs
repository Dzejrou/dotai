using System;

// Computes the merchant sell price for a gear instance using the per-quality
// BaseSellPrice on GearQualityRules. The formula is:
//   sellPrice = baseQualitySellPrice * (1 + (level - 1) / (maxLevel - 1))
// Qualities with MaxLevel <= 1 always use a flat 1.0 multiplier. Results are
// rounded to the nearest int and clamped to >= 0.
public static class GearSellPricing
{
    public static int GetSellPrice(GearInstance gear, GearGenerationRules rules)
    {
        if (gear == null || rules == null)
            return 0;

        var qualityRules = rules.GetQualityRules(gear.Quality);
        if (qualityRules == null)
            return 0;

        var basePrice = Math.Max(0, qualityRules.BaseSellPrice);
        if (basePrice == 0)
            return 0;

        var multiplier = GetLevelMultiplier(gear.Level, qualityRules.MaxLevel);
        var price = (int)Math.Round(basePrice * multiplier, MidpointRounding.AwayFromZero);
        return Math.Max(0, price);
    }

    private static float GetLevelMultiplier(int level, int maxLevel)
    {
        if (maxLevel <= 1)
            return 1.0f;

        var clampedLevel = Math.Clamp(level, 1, maxLevel);
        var normalized = (clampedLevel - 1f) / (maxLevel - 1f);
        return 1.0f + normalized;
    }
}
