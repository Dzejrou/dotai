using System;

public static class ActorHudSettings
{
    private static bool _showNames = false;

    public static bool ShowNames => _showNames;

    public static event Action<bool> Changed;

    public static void SetShowNames(bool showNames)
    {
        if (_showNames == showNames)
            return;

        _showNames = showNames;
        Changed?.Invoke(_showNames);
    }
}
