using Godot;

// One outgoing route from a room node to another node in the run plan.
//
// The model is a graph from the very first slice: a node owns zero or more edges, so a
// future branching generator can give a combat room's two doors different destinations and
// surface per-door difficulty/destination previews. The first (linear) generator simply
// points both combat doors at the same next node, and gives every edge the same default
// level delta.
public sealed class DungeonRoomEdge
{
    public DungeonRoomEdge(StringName sourceExitId, StringName destinationNodeId, int levelDelta)
    {
        SourceExitId = sourceExitId;
        DestinationNodeId = destinationNodeId;
        LevelDelta = levelDelta;
    }

    // Door/exit this edge leaves through (e.g. north_west, north_east, north_center). Matches
    // the exit ids the room scenes expose so a later runtime can map a used door to its edge.
    public StringName SourceExitId { get; }

    // Stable id of the node this edge leads to.
    public StringName DestinationNodeId { get; }

    // Difficulty increase applied when crossing this edge. Stored explicitly so per-door
    // previews and future branching can vary it independently; the linear generator uses the
    // rules' default increase for every edge.
    public int LevelDelta { get; }
}
