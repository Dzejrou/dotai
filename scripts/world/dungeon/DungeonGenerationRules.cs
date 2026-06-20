using Godot;

using System;

// Inspector-editable configuration for deterministic dungeon run-plan generation. The
// generator reads its defaults from here; the future Dungeon HUB may override ordinary-room
// count and starting level per run, which is why the generator accepts those as arguments
// while everything else comes from this resource.
[GlobalClass]
public partial class DungeonGenerationRules : Resource
{
    // Default number of ordinary rooms (Combat/Timed/Special) before the guaranteed Pre-Boss
    // and terminal Boss. The HUB may override this when starting a run.
    [Export]
    public int OrdinaryRoomCount
    {
        get => _ordinaryRoomCount;
        set => _ordinaryRoomCount = Math.Max(0, value);
    }

    // Level of the first room. The HUB may override this when starting a run.
    [Export]
    public int StartingRoomLevel
    {
        get => _startingRoomLevel;
        set => _startingRoomLevel = Math.Max(1, value);
    }

    // Level increase applied per progression edge / room step.
    [Export]
    public int LevelIncreasePerRoom
    {
        get => _levelIncreasePerRoom;
        set => _levelIncreasePerRoom = Math.Max(0, value);
    }

    // Weighted selection for ordinary room kinds. Weights are relative; all-zero falls back
    // to Combat.
    [Export(PropertyHint.Range, "0,100,0.1,or_greater")]
    public float CombatWeight
    {
        get => _combatWeight;
        set => _combatWeight = NonNegative(value);
    }

    [Export(PropertyHint.Range, "0,100,0.1,or_greater")]
    public float TimedWeight
    {
        get => _timedWeight;
        set => _timedWeight = NonNegative(value);
    }

    [Export(PropertyHint.Range, "0,100,0.1,or_greater")]
    public float SpecialWeight
    {
        get => _specialWeight;
        set => _specialWeight = NonNegative(value);
    }

    // Pity: force a Special ordinary room once this many consecutive non-Special ordinary
    // rooms have been generated. 0 disables the pity rule.
    [Export(PropertyHint.Range, "0,20,1")]
    public int SpecialRoomPity
    {
        get => _specialRoomPity;
        set => _specialRoomPity = Math.Max(0, value);
    }

    [Export]
    public Godot.Collections.Array<RoomTemplateDefinition> CombatRoomDefinitions { get; set; } = new();

    [Export]
    public Godot.Collections.Array<RoomTemplateDefinition> TimedRoomDefinitions { get; set; } = new();

    [Export]
    public RoomTemplateDefinition SpecialRoomDefinition { get; set; }

    [Export]
    public RoomTemplateDefinition BossRoomDefinition { get; set; }

    // Content option id resolved explicitly (not by weight) for the guaranteed penultimate
    // Pre-Boss room, so a zero-weight Pre-Boss option is still placed there.
    [Export]
    public StringName PreBossContentId { get; set; } = "pre_boss";

    // Content option id resolved explicitly for the guaranteed terminal Boss room.
    [Export]
    public StringName BossContentId { get; set; } = "demon_boss_content";

    private int _ordinaryRoomCount = 10;
    private int _startingRoomLevel = 1;
    private int _levelIncreasePerRoom = 1;
    private float _combatWeight = 75.0f;
    private float _timedWeight = 15.0f;
    private float _specialWeight = 10.0f;
    private int _specialRoomPity = 5;

    private static float NonNegative(float value)
    {
        return float.IsFinite(value) ? Math.Max(0.0f, value) : 0.0f;
    }
}
