using Godot;

using System.Collections.Generic;
using System.Linq;

[GlobalClass]
public partial class SpawnCatalog : Resource
{
    [Export]
    public Godot.Collections.Array<SpawnCatalogEntry> Entries { get; set; } = new();

    public List<SpawnCatalogEntry> GetEnabledEntries()
    {
        return Entries
            .Where(entry => entry != null && entry.Enabled && !string.IsNullOrWhiteSpace(entry.Id) && entry.SpawnScene != null)
            .OrderBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.DisplayName)
            .ToList();
    }
}
