using Godot;

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

    public static Faction ResolveForNode(Node node)
    {
        if (node is IFactionMember factionMember && factionMember.Faction != null)
            return factionMember.Faction;

        if (node != null && node.IsInGroup(CombatGroups.Allies))
            return Allies;

        if (node != null && node.IsInGroup(CombatGroups.Enemies))
            return Enemies;

        return null;
    }

    public static void ApplyCombatGroup(Node node, Faction faction)
    {
        if (node == null)
            return;

        node.RemoveFromGroup(CombatGroups.Allies);
        node.RemoveFromGroup(CombatGroups.Enemies);

        var group = GetCombatGroup(faction);
        if (!string.IsNullOrEmpty(group))
            node.AddToGroup(group);
    }

    public static string GetCombatGroup(Faction faction)
    {
        if (ReferenceEquals(faction, Allies))
            return CombatGroups.Allies;

        if (ReferenceEquals(faction, Enemies))
            return CombatGroups.Enemies;

        return null;
    }
}
