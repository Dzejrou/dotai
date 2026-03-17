using System.Collections.Generic;

public sealed class Faction
{
    private readonly HashSet<Faction> _hostileFactions = new();

    internal Faction(string key)
    {
        Key = key;
    }

    public string Key { get; }

    public bool IsHostileTo(Faction other)
    {
        return other != null && _hostileFactions.Contains(other);
    }

    internal void SetHostileFactions(params Faction[] hostileFactions)
    {
        _hostileFactions.Clear();
        if (hostileFactions == null)
            return;

        foreach (var hostileFaction in hostileFactions)
        {
            if (hostileFaction == null || ReferenceEquals(hostileFaction, this))
                continue;

            _hostileFactions.Add(hostileFaction);
        }
    }

    public override string ToString()
    {
        return Key;
    }
}
