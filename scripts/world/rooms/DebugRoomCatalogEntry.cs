using Godot;

[GlobalClass]
public partial class DebugRoomCatalogEntry : Resource
{
    [Export]
    public RoomTemplateDefinition Definition { get; set; }

    // Rooms without a standardized ContentRoot (e.g. the Special Dungeon Room)
    // ship their content baked into the scene; no external injection is attempted.
    [Export]
    public bool UsesBuiltInContent { get; set; }

    public bool IsConfigured => Definition?.RoomScene != null;
}
