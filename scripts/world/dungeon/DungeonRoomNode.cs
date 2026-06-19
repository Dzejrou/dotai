using Godot;

using System;
using System.Collections.Generic;

// One pre-generated room in a run plan. Immutable once built: the generator resolves the
// definition, content option and level up front and stores them here, so entering a room
// later can never reroll the plan.
public sealed class DungeonRoomNode
{
    public DungeonRoomNode(
        StringName id,
        int index,
        DungeonRoomKind kind,
        RoomTemplateDefinition definition,
        RoomContentOption contentOption,
        int level,
        IReadOnlyList<DungeonRoomEdge> edges)
    {
        Id = id;
        Index = index;
        Kind = kind;
        Definition = definition;
        ContentOption = contentOption;
        Level = level;
        Edges = edges ?? Array.Empty<DungeonRoomEdge>();
    }

    // Stable identity, independent of position so future graph routes can reference nodes
    // without assuming a linear order.
    public StringName Id { get; }

    public int Index { get; }

    public DungeonRoomKind Kind { get; }

    // Selected room template; the room scene to instantiate when this node is entered.
    public RoomTemplateDefinition Definition { get; }

    // Selected content to inject. May be null for an intentionally empty room.
    public RoomContentOption ContentOption { get; }

    // Resolved difficulty/room level (starting level plus accumulated edge deltas).
    public int Level { get; }

    // Outgoing routes. Linear plan: combat nodes carry two edges to the same next node,
    // Timed/Special carry a single progression edge, and the terminal Boss node carries none.
    public IReadOnlyList<DungeonRoomEdge> Edges { get; }
}
