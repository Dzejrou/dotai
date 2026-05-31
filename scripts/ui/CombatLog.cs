using System;
using System.Collections.Generic;

using Godot;

public enum CombatLogEntryKind
{
    Info,
    Damage,
    Heal,
    Absorb,
    Debug,
}

// Filter category used by the fullscreen Log page. Drives which messages are
// visible per filter; Kind continues to drive text color.
public enum CombatLogCategory
{
    Damage,
    Healing,
    Loot,
    System,
    Debug,
}

public readonly struct CombatLogEntry
{
    public CombatLogEntry(CombatLogEntryKind kind, CombatLogCategory category, string text)
    {
        Kind = kind;
        Category = category;
        Text = text;
    }

    public CombatLogEntryKind Kind { get; }
    public CombatLogCategory Category { get; }
    public string Text { get; }
}

// Central, UI-agnostic combat log API. Systems call CombatLog.Damage/Heal/Absorb/Info/Debug
// without needing to know about the HUD panel. The HUD panel subscribes to Emitted.
public static class CombatLog
{
    private const int HistoryCapacity = 500;

    private static readonly Queue<CombatLogEntry> History = new();

    public static event Action<CombatLogEntry> Emitted;

    public static IReadOnlyCollection<CombatLogEntry> Recent => History;

    public static void Info(string text)
    {
        Publish(CombatLogEntryKind.Info, CombatLogCategory.System, text);
    }

    public static void System(string text)
    {
        Publish(CombatLogEntryKind.Info, CombatLogCategory.System, text);
    }

    public static void Loot(string text)
    {
        Publish(CombatLogEntryKind.Info, CombatLogCategory.Loot, text);
    }

    public static void Healing(string text)
    {
        Publish(CombatLogEntryKind.Heal, CombatLogCategory.Healing, text);
    }

    public static void Damage(Node target, Node source, int amount, bool isCritical)
    {
        if (amount <= 0)
            return;

        var targetName = ResolveDisplayName(target, "Unknown");
        var sourceName = ResolveDisplayName(source, null);

        string text;
        if (string.IsNullOrEmpty(sourceName))
            text = isCritical
                ? $"{targetName} takes {amount} damage (crit)."
                : $"{targetName} takes {amount} damage.";
        else
            text = isCritical
                ? $"{sourceName} crits {targetName} for {amount}."
                : $"{sourceName} hits {targetName} for {amount}.";

        Publish(CombatLogEntryKind.Damage, CombatLogCategory.Damage, text);
    }

    public static void Heal(Node target, int amount)
    {
        if (amount <= 0)
            return;

        var targetName = ResolveDisplayName(target, "Unknown");
        Publish(CombatLogEntryKind.Heal, CombatLogCategory.Healing, $"{targetName} healed for {amount}.");
    }

    public static void Absorb(Node target, int amount)
    {
        var targetName = ResolveDisplayName(target, "Unknown");
        var text = amount > 0
            ? $"{targetName} absorbs {amount} damage."
            : $"{targetName} absorbs the hit.";
        Publish(CombatLogEntryKind.Absorb, CombatLogCategory.Damage, text);
    }

    public static void Debug(string text)
    {
        if (!GameSettings.ShowCombatLogDebugMessages)
            return;

        Publish(CombatLogEntryKind.Debug, CombatLogCategory.Debug, text);
    }

    private static void Publish(CombatLogEntryKind kind, CombatLogCategory category, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var entry = new CombatLogEntry(kind, category, text);

        while (History.Count >= HistoryCapacity)
            History.Dequeue();
        History.Enqueue(entry);

        Emitted?.Invoke(entry);
    }

    public static string ResolveName(Node node)
    {
        return ResolveDisplayName(node, string.Empty);
    }

    private static string ResolveDisplayName(Node node, string fallback)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
            return fallback;

        var hud = node.GetNodeOrNull<ActorHUD>("ActorHUD");
        if (hud != null)
        {
            var resolved = hud.ResolvedDisplayName;
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }

        var name = node.Name.ToString();
        return string.IsNullOrEmpty(name) ? fallback : name;
    }
}
