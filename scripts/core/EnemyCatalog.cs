using Godot;

using System.Collections.Generic;
using System.Linq;

[GlobalClass]
public partial class EnemyCatalog : Resource
{
    [Export]
    public Godot.Collections.Array<EnemyCatalogEntry> Entries { get; set; } = new();

    public List<EnemyCatalogEntry> GetEnabledEntries()
    {
        return Entries
            .Where(entry => entry != null && entry.Enabled && !string.IsNullOrWhiteSpace(entry.Id) && entry.EnemyScene != null)
            .OrderBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.DisplayName)
            .ToList();
    }
}
