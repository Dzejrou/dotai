using System;

public static class GameSettings
{
    public const bool DefaultShowActorNames = false;
    public const bool DefaultShowFloatingText = true;
    public const bool DefaultShowCombatLogDebugMessages = false;

    private static bool _showActorNames = DefaultShowActorNames;
    private static bool _showFloatingText = DefaultShowFloatingText;
    private static bool _showCombatLogDebugMessages = DefaultShowCombatLogDebugMessages;

    public static bool ShowActorNames => _showActorNames;
    public static bool ShowFloatingText => _showFloatingText;
    public static bool ShowCombatLogDebugMessages => _showCombatLogDebugMessages;

    public static event Action<bool> ShowActorNamesChanged;
    public static event Action<bool> ShowFloatingTextChanged;
    public static event Action<bool> ShowCombatLogDebugMessagesChanged;

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
}
