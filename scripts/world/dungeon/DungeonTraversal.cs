using Godot;

// Pure, runtime-independent traversal helpers shared by the live Dungeon and headless
// verification. Resolving a used door to its plan edge and then to a destination node has no
// Godot scene-tree dependency, so it can be exercised directly in tooling.
public static class DungeonTraversal
{
    // Finds the outgoing edge a node exposes for the given door/exit id. Edges are matched
    // independently, so a future branching node can expose a different destination per door
    // without changing this lookup.
    public static DungeonRoomEdge FindEdge(DungeonRoomNode node, StringName exitId)
    {
        if (node == null || exitId == null || exitId.IsEmpty)
            return null;

        foreach (var edge in node.Edges)
        {
            if (edge != null && edge.SourceExitId == exitId)
                return edge;
        }

        return null;
    }

    // Resolves the destination node reached by leaving `node` through `exitId`, looking the
    // edge's stable destination id up in `plan`. Returns null (with a null `edge`) when the
    // exit has no edge or the destination id is not present in the plan.
    public static DungeonRoomNode ResolveDestination(DungeonRunPlan plan, DungeonRoomNode node, StringName exitId, out DungeonRoomEdge edge)
    {
        edge = FindEdge(node, exitId);
        if (edge == null || plan == null)
            return null;

        return plan.GetNodeById(edge.DestinationNodeId);
    }
}
