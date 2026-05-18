using Godot;

public enum SpawnCatalogEntryKind
{
    Character = 0,
    Drop = 1,
    Gear = 2,
}

[GlobalClass]
public partial class SpawnCatalogEntry : Resource
{
    [Export]
    public SpawnCatalogEntryKind EntryKind { get; set; } = SpawnCatalogEntryKind.Character;

    [Export]
    public string Id { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public string Category { get; set; } = "General";

    [Export]
    public PackedScene SpawnScene { get; set; }

    [Export]
    public bool Enabled { get; set; } = true;

    [Export]
    public int SortOrder { get; set; } = 0;
}
