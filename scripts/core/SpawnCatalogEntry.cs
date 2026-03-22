using Godot;

[GlobalClass]
public partial class SpawnCatalogEntry : Resource
{
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
