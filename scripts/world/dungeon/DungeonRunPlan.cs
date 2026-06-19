using Godot;

using System;
using System.Collections.Generic;

// An immutable, fully pre-generated dungeon run: ordered nodes from the first room to the
// terminal Boss, addressable by stable id. Built by DungeonRunPlanGenerator from a seed, so
// the same seed and rules always reproduce the same plan. Runtime progression will consume
// this in a later slice; nothing mutates it after construction.
public sealed class DungeonRunPlan
{
    private readonly List<DungeonRoomNode> _nodes;
    private readonly Dictionary<StringName, DungeonRoomNode> _nodesById = new();

    public DungeonRunPlan(ulong seed, IReadOnlyList<DungeonRoomNode> nodes)
    {
        Seed = seed;
        // Defensive copy behind a genuinely read-only wrapper so the plan cannot be mutated
        // through Nodes (e.g. by downcasting to List) after construction.
        _nodes = nodes != null ? new List<DungeonRoomNode>(nodes) : new List<DungeonRoomNode>();
        Nodes = _nodes.AsReadOnly();

        foreach (var node in _nodes)
        {
            if (node?.Id != null && !node.Id.IsEmpty)
                _nodesById[node.Id] = node;
        }
    }

    // Seed the plan was generated from; enough (with the rules) to reproduce it.
    public ulong Seed { get; }

    public IReadOnlyList<DungeonRoomNode> Nodes { get; }

    public int Length => _nodes.Count;

    public DungeonRoomNode GetNodeById(StringName id)
    {
        if (id == null || id.IsEmpty)
            return null;

        return _nodesById.TryGetValue(id, out var node) ? node : null;
    }
}
