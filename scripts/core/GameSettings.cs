using System;

using Godot;

public static class GameSettings
{
    public const bool DefaultShowActorNames = false;
    public const bool DefaultShowFloatingText = true;
    public const bool DefaultShowCombatLogDebugMessages = false;
    public const bool DefaultShowCombatLog = false;
    public const bool DefaultLockCombatLogPosition = true;
    public static readonly Vector2 DefaultCombatLogPosition = Vector2.Zero;

    private static bool _showActorNames = DefaultShowActorNames;
    private static bool _showFloatingText = DefaultShowFloatingText;
    private static bool _showCombatLogDebugMessages = DefaultShowCombatLogDebugMessages;
    private static bool _showCombatLog = DefaultShowCombatLog;
    private static bool _lockCombatLogPosition = DefaultLockCombatLogPosition;
    private static Vector2 _combatLogPosition = DefaultCombatLogPosition;
    private static bool _combatLogPositionCustomized;

    public static bool ShowActorNames => _showActorNames;
    public static bool ShowFloatingText => _showFloatingText;
    public static bool ShowCombatLogDebugMessages => _showCombatLogDebugMessages;
    public static bool ShowCombatLog => _showCombatLog;
    public static bool LockCombatLogPosition => _lockCombatLogPosition;
    public static Vector2 CombatLogPosition => _combatLogPosition;
    public static bool CombatLogPositionCustomized => _combatLogPositionCustomized;

    public static event Action<bool> ShowActorNamesChanged;
    public static event Action<bool> ShowFloatingTextChanged;
    public static event Action<bool> ShowCombatLogDebugMessagesChanged;
    public static event Action<bool> ShowCombatLogChanged;
    public static event Action<bool> LockCombatLogPositionChanged;
    public static event Action<Vector2> CombatLogPositionChanged;

    public static void SetShowActorNames(bool value)
    {
        if (_showActorNames == value)
            return;

        _showActorNames = value;
        ShowActorNamesChanged?.Invoke(value);
    }

    public static void SetShowFloatingText(bool value)
    {
        if (_showFloatingText == value)
            return;

        _showFloatingText = value;
        ShowFloatingTextChanged?.Invoke(value);
    }

    public static void SetShowCombatLogDebugMessages(bool value)
    {
        if (_showCombatLogDebugMessages == value)
            return;

        _showCombatLogDebugMessages = value;
        ShowCombatLogDebugMessagesChanged?.Invoke(value);
    }

    public static void SetShowCombatLog(bool value)
    {
        if (_showCombatLog == value)
            return;

        _showCombatLog = value;
        ShowCombatLogChanged?.Invoke(value);
    }

    public static void SetLockCombatLogPosition(bool value)
    {
        if (_lockCombatLogPosition == value)
            return;

        _lockCombatLogPosition = value;
        LockCombatLogPositionChanged?.Invoke(value);
    }

    public static void SetCombatLogPosition(Vector2 value, bool customized)
    {
        var changed = _combatLogPosition != value || _combatLogPositionCustomized != customized;
        _combatLogPosition = value;
        _combatLogPositionCustomized = customized;

        if (changed)
            CombatLogPositionChanged?.Invoke(value);
    }
}
