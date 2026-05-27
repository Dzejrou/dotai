using System;

using Godot;

public enum CombatLogEntryKind
{
    Info,
    Damage,
    Heal,
    Absorb,
    Debug,
}

public readonly struct CombatLogEntry
{
    public CombatLogEntry(CombatLogEntryKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    public CombatLogEntryKind Kind { get; }
    public string Text { get; }
}

// Central, UI-agnostic combat log API. Systems call CombatLog.Damage/Heal/Absorb/Info/Debug
// without needing to know about the HUD panel. The HUD panel subscribes to Emitted.
public static class CombatLog
{
    public static event Action<CombatLogEntry> Emitted;

    public static void Info(string text)
    {
        Publish(CombatLogEntryKind.Info, text);
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

        Publish(CombatLogEntryKind.Damage, text);
    }

    public static void Heal(Node target, int amount)
    {
        if (amount <= 0)
            return;

        var targetName = ResolveDisplayName(target, "Unknown");
        Publish(CombatLogEntryKind.Heal, $"{targetName} healed for {amount}.");
    }

    public static void Absorb(Node target, int amount)
    {
        var targetName = ResolveDisplayName(target, "Unknown");
        var text = amount > 0
            ? $"{targetName} absorbs {amount} damage."
            : $"{targetName} absorbs the hit.";
        Publish(CombatLogEntryKind.Absorb, text);
    }

    public static void Debug(string text)
    {
        if (!GameSettings.ShowCombatLogDebugMessages)
            return;

        Publish(CombatLogEntryKind.Debug, text);
    }

    private static void Publish(CombatLogEntryKind kind, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Emitted?.Invoke(new CombatLogEntry(kind, text));
    }

    private static string ResolveDisplayName(Node node, string fallback)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
            return fallback;

        var name = node.Name.ToString();
        return string.IsNullOrEmpty(name) ? fallback : name;
    }
}
