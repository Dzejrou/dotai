using System;

public static class NavigationDebugSettings
{
    private static bool _enabled;

    public static bool Enabled => _enabled;

    public static event Action<bool> Changed;

    public static bool Toggle()
    {
        SetEnabled(!_enabled);
        return _enabled;
    }

    public static void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
            return;

        _enabled = enabled;
        Changed?.Invoke(_enabled);
    }
}
