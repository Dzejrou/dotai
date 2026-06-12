using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class Dungeon : Node
{
    private enum DungeonRoomKind
    {
        Combat,
        Special,
        Timed,
    }

    private sealed class DungeonRoomDescriptor
    {
        public DungeonRoomDescriptor(DungeonRoomKind kind)
        {
            Kind = kind;
        }

        public DungeonRoomKind Kind { get; }
    }

    private static readonly StringName DungeonRuntimeScreenId = "dungeon_runtime";
    private static readonly StringName DungeonEntryExitId = "south_return";
    private static readonly StringName EntranceHallScreenId = "entrance_hall";
    private static readonly StringName EntranceHallReturnExitId = "north_center";
    private static readonly StringName CombatTopLeftExitId = "north_west";
    private static readonly StringName CombatTopRightExitId = "north_east";
    private static readonly StringName SpecialTopExitId = "north_center";

    [Export]
    public Godot.Collections.Array<RoomTemplateDefinition> CombatRoomDefinitions { get; set; } = new();

    [Export]
    public Godot.Collections.Array<PackedScene> SpecialRoomTemplates { get; set; } = new();

    [Export]
    public Godot.Collections.Array<RoomTemplateDefinition> TimedRoomDefinitions { get; set; } = new();

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SpecialRoomChance { get; set; } = 0.2f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float TimedRoomChance { get; set; } = 0.15f;

    [Export(PropertyHint.Range, "0,20,1")]
    public int SpecialRoomPity { get; set; } = 3;

    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<StringName, DungeonRoomDescriptor> _activeProgressionDoors = new();

    private Room _activeDungeonRoom;
    private int _consecutiveNonSpecialRooms;

    public override void _Ready()
    {
        _random.Randomize();
    }

    public bool TryCreateRoom(StringName screenId, Room currentRoom, RoomTransition sourceTransition, StringName entryExitId, out Room room)
    {
        room = null;
        if (screenId != DungeonRuntimeScreenId)
            return false;

        var roomKind = ResolveRequestedRoomKind(currentRoom, sourceTransition);
        if (!TryInstantiateDungeonRoom(roomKind, out room))
        {
            return false;
        }

        if (!IsDungeonRoom(currentRoom))
            StartNewRun();

        RegisterEnteredDungeonRoom(roomKind);
        SetActiveDungeonRoom(room);
        ConfigureDungeonRoom(room, roomKind);
        return true;
    }

    public void OnTransitionCompleted(Room previousRoom, RoomTransition usedTransition, Room nextRoom)
    {
        if (IsDungeonRoom(previousRoom) && !IsDungeonRoom(nextRoom))
            EndRun();
    }

    private void ConfigureDungeonRoom(Room room, DungeonRoomKind roomKind)
    {
        _activeProgressionDoors.Clear();

        switch (roomKind)
        {
            case DungeonRoomKind.Combat:
                ConfigureCombatRoom((CombatDungeonRoom)room);
                break;
            case DungeonRoomKind.Special:
                ConfigureSpecialRoom((SpecialDungeonRoom)room);
                break;
            case DungeonRoomKind.Timed:
                ConfigureTimedRoom((TimedDungeonRoom)room);
                break;
        }
    }

    private void ConfigureCombatRoom(CombatDungeonRoom room)
    {
        room.ConfigureProgressionDoors(DungeonRuntimeScreenId, DungeonEntryExitId);
        room.ConfigureReturnDoor(EntranceHallScreenId, EntranceHallReturnExitId);
    }

    private void ConfigureSpecialRoom(SpecialDungeonRoom room)
    {
        room.ConfigureProgressionDoor(DungeonRuntimeScreenId, DungeonEntryExitId);
        room.ConfigureReturnDoor(EntranceHallScreenId, EntranceHallReturnExitId);
        SetProgressionDoor(SpecialTopExitId, DungeonRoomKind.Combat);
    }

    private void ConfigureTimedRoom(TimedDungeonRoom room)
    {
        room.ConfigureProgressionDoor(DungeonRuntimeScreenId, DungeonEntryExitId);
    }

    private void OnActiveRoomCleared()
    {
        if (_activeDungeonRoom is not CombatDungeonRoom)
            return;

        if (TryResolveForcedCombatProgressionKind(out var forcedRoomKind))
        {
            SetProgressionDoor(CombatTopLeftExitId, forcedRoomKind);
            SetProgressionDoor(CombatTopRightExitId, forcedRoomKind);
            return;
        }

        SetProgressionDoor(CombatTopLeftExitId, RollCombatProgressionRoomKind());
        SetProgressionDoor(CombatTopRightExitId, RollCombatProgressionRoomKind());
    }

    private void StartNewRun()
    {
        EndRun();
    }

    private void EndRun()
    {
        _activeProgressionDoors.Clear();
        _consecutiveNonSpecialRooms = 0;
        SetActiveDungeonRoom(null);
    }

    private void SetActiveDungeonRoom(Room room)
    {
        if (_activeDungeonRoom is CombatDungeonRoom previousCombatRoom &&
            GodotObject.IsInstanceValid(previousCombatRoom))
        {
            previousCombatRoom.RoomCleared -= OnActiveRoomCleared;
        }

        _activeDungeonRoom = room;

        if (_activeDungeonRoom is CombatDungeonRoom activeCombatRoom)
            activeCombatRoom.RoomCleared += OnActiveRoomCleared;
    }

    private DungeonRoomKind ResolveRequestedRoomKind(Room currentRoom, RoomTransition sourceTransition)
    {
        if (!IsDungeonRoom(currentRoom))
            return DungeonRoomKind.Combat;

        if (sourceTransition != null &&
            HasValue(sourceTransition.ExitId) &&
            _activeProgressionDoors.TryGetValue(sourceTransition.ExitId, out var descriptor))
        {
            return descriptor.Kind;
        }

        return currentRoom is SpecialDungeonRoom
            ? DungeonRoomKind.Combat
            : RollCombatProgressionRoomKind();
    }

    private bool TryInstantiateDungeonRoom(DungeonRoomKind roomKind, out Room room)
    {
        return roomKind == DungeonRoomKind.Special
            ? TryInstantiateSpecialDungeonRoom(out room)
            : TryInstantiateDefinedDungeonRoom(roomKind, out room);
    }

    private bool TryInstantiateSpecialDungeonRoom(out Room room)
    {
        room = null;

        var template = ChooseSpecialRoomTemplate();
        if (template == null)
        {
            GD.PushError($"{nameof(Dungeon)} has no configured {DungeonRoomKind.Special} room templates.");
            return false;
        }

        room = template.Instantiate<Room>();
        if (room == null)
        {
            GD.PushError($"{nameof(Dungeon)} could not instantiate a dungeon room for {DungeonRoomKind.Special}.");
            return false;
        }

        return true;
    }

    private bool TryInstantiateDefinedDungeonRoom(DungeonRoomKind roomKind, out Room room)
    {
        room = null;

        var definition = ChooseRoomDefinition(roomKind);
        if (definition == null)
        {
            GD.PushError($"{nameof(Dungeon)} has no configured {roomKind} room definitions.");
            return false;
        }

        var roomInstance = definition.RoomScene.Instantiate();
        if (roomInstance is not Room definedRoom)
        {
            GD.PushError($"{nameof(Dungeon)} room definition '{definition.GetLabel()}' did not instantiate a {nameof(Room)} root for {roomKind}.");
            roomInstance?.QueueFree();
            return false;
        }

        var contentOption = definition.PickContentOption(_random);
        definedRoom.TryInjectContent(contentOption?.ContentScene);
        room = definedRoom;
        return true;
    }

    private RoomTemplateDefinition ChooseRoomDefinition(DungeonRoomKind roomKind)
    {
        var definitions = roomKind switch
        {
            DungeonRoomKind.Combat => CombatRoomDefinitions,
            DungeonRoomKind.Timed => TimedRoomDefinitions,
            _ => null,
        };

        var validDefinitions = new List<RoomTemplateDefinition>();
        if (definitions != null)
        {
            foreach (var definition in definitions)
            {
                if (definition?.RoomScene != null)
                    validDefinitions.Add(definition);
            }
        }

        if (validDefinitions.Count == 0)
            return null;

        return validDefinitions[_random.RandiRange(0, validDefinitions.Count - 1)];
    }

    private PackedScene ChooseSpecialRoomTemplate()
    {
        var validTemplates = new List<PackedScene>();
        foreach (var template in SpecialRoomTemplates)
        {
            if (template != null)
                validTemplates.Add(template);
        }

        if (validTemplates.Count == 0)
            return null;

        return validTemplates[_random.RandiRange(0, validTemplates.Count - 1)];
    }

    private DungeonRoomKind RollCombatProgressionRoomKind()
    {
        var timedChance = Mathf.Clamp(TimedRoomChance, 0.0f, 1.0f);
        if (_random.Randf() < timedChance)
            return DungeonRoomKind.Timed;

        var specialChance = Mathf.Clamp(SpecialRoomChance, 0.0f, 1.0f);
        return _random.Randf() < specialChance
            ? DungeonRoomKind.Special
            : DungeonRoomKind.Combat;
    }

    private void RegisterEnteredDungeonRoom(DungeonRoomKind roomKind)
    {
        _consecutiveNonSpecialRooms = roomKind == DungeonRoomKind.Special
            ? 0
            : _consecutiveNonSpecialRooms + 1;
    }

    private bool TryResolveForcedCombatProgressionKind(out DungeonRoomKind forcedRoomKind)
    {
        forcedRoomKind = default;

        if (SpecialRoomPity <= 0)
            return false;

        var pityThreshold = Mathf.Max(1, SpecialRoomPity);
        if (_consecutiveNonSpecialRooms < pityThreshold)
            return false;

        forcedRoomKind = DungeonRoomKind.Special;
        return true;
    }

    private void SetProgressionDoor(StringName exitId, DungeonRoomKind roomKind)
    {
        _activeProgressionDoors[exitId] = new DungeonRoomDescriptor(roomKind);
    }

    private static bool IsDungeonRoom(Room room)
    {
        return room is CombatDungeonRoom || room is SpecialDungeonRoom || room is TimedDungeonRoom;
    }

    private static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }

}
