using Godot;

[GlobalClass]
public partial class DebugRoomCatalog : Resource
{
    [Export]
    public Godot.Collections.Array<DebugRoomCatalogEntry> Entries { get; set; } = new();
}
