using System;
using System.Collections.Generic;

public static class Factions
{
    private static readonly Dictionary<string, Faction> ByKey;

    static Factions()
    {
        Allies = new Faction("allies");
        Enemies = new Faction("enemies");
        Neutral = new Faction("neutral");

        Allies.SetHostileFactions(Enemies);
        Enemies.SetHostileFactions(Allies);
        Neutral.SetHostileFactions();

        ByKey = new Dictionary<string, Faction>(StringComparer.OrdinalIgnoreCase)
        {
            [Allies.Key] = Allies,
            [Enemies.Key] = Enemies,
            [Neutral.Key] = Neutral,
        };
    }

    public static Faction Allies { get; }
    public static Faction Enemies { get; }
    public static Faction Neutral { get; }

    public static Faction Get(string key)
    {
        return key != null && ByKey.TryGetValue(key, out var faction) ? faction : null;
    }
}
