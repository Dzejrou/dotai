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
    public Godot.Collections.Array<DungeonEncounterDefinition> EncounterPool { get; set; } = new();

    [Export]
    public Godot.Collections.Array<PackedScene> CombatRoomTemplates { get; set; } = new();

    [Export]
    public Godot.Collections.Array<PackedScene> SpecialRoomTemplates { get; set; } = new();

    [Export]
    public Godot.Collections.Array<PackedScene> TimedRoomTemplates { get; set; } = new();

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SpecialRoomChance { get; set; } = 0.2f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float TimedRoomChance { get; set; } = 0.15f;

    [Export(PropertyHint.Range, "0,20,1")]
    public int SpecialRoomPity { get; set; } = 3;

    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<StringName, DungeonRoomDescriptor> _activeProgressionDoors = new();

    private RoomScreen _activeDungeonRoom;
    private int _consecutiveNonSpecialRooms;

    public override void _Ready()
    {
        _random.Randomize();
    }

    public bool TryCreateRoom(StringName screenId, RoomScreen currentRoom, RoomTransition sourceTransition, StringName entryExitId, out RoomScreen room)
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

    public void OnTransitionCompleted(RoomScreen previousRoom, RoomTransition usedTransition, RoomScreen nextRoom)
    {
        if (IsDungeonRoom(previousRoom) && !IsDungeonRoom(nextRoom))
            EndRun();
    }

    private void ConfigureDungeonRoom(RoomScreen room, DungeonRoomKind roomKind)
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
        room.PrepareEncounter();
        SpawnEncounter(room);
    }

    private void ConfigureSpecialRoom(SpecialDungeonRoom room)
    {
        room.ConfigureProgressionDoor(DungeonRuntimeScreenId, DungeonEntryExitId);
        room.ConfigureReturnDoor(EntranceHallScreenId, EntranceHallReturnExitId);
        SetProgressionDoor(SpecialTopExitId, DungeonRoomKind.Combat);
    }

    private void ConfigureTimedRoom(TimedDungeonRoom room)
    {
        room.ConfigureProgressionDoor(EntranceHallScreenId, EntranceHallReturnExitId);
    }

    private void SpawnEncounter(CombatDungeonRoom room)
    {
        var spawnRoot = room.GetUnscaledRoot();
        if (spawnRoot == null)
        {
            GD.PushError($"{nameof(Dungeon)} could not resolve the unscaled root for {nameof(CombatDungeonRoom)}.");
            room.FinalizeEncounterSetup();
            return;
        }

        var encounterDefinition = ChooseEncounterDefinition();
        if (encounterDefinition == null)
        {
            GD.PushError($"{nameof(Dungeon)} has no valid encounter definitions configured.");
            room.FinalizeEncounterSetup();
            return;
        }

        var markerIds = new List<StringName>(room.GetEncounterMarkerIds());
        if (markerIds.Count == 0)
        {
            GD.PushError($"{nameof(CombatDungeonRoom)} does not define any encounter spawn markers.");
            room.FinalizeEncounterSetup();
            return;
        }

        Shuffle(markerIds);

        var encounterCount = Mathf.Clamp(
            _random.RandiRange(encounterDefinition.GetResolvedMinSpawnCount(), encounterDefinition.GetResolvedMaxSpawnCount()),
            1,
            markerIds.Count);

        for (var i = 0; i < encounterCount; i++)
        {
            if (!room.TryGetEncounterMarkerGlobalPosition(markerIds[i], out var spawnPosition))
                continue;

            var enemyScene = encounterDefinition.RollEnemyScene(_random);
            if (enemyScene?.Instantiate<Node2D>() is not Node2D enemy)
                continue;

            enemy.GlobalPosition = spawnPosition;
            spawnRoot.AddChild(enemy);
            room.RegisterEncounterEnemy(enemy);
        }

        room.FinalizeEncounterSetup();
    }

    private DungeonEncounterDefinition ChooseEncounterDefinition()
    {
        var validDefinitions = new List<DungeonEncounterDefinition>();
        if (EncounterPool != null)
        {
            foreach (var encounterDefinition in EncounterPool)
            {
                if (encounterDefinition?.IsConfigured == true)
                    validDefinitions.Add(encounterDefinition);
            }
        }

        if (validDefinitions.Count == 0)
            return null;

        return validDefinitions[_random.RandiRange(0, validDefinitions.Count - 1)];
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

    private void SetActiveDungeonRoom(RoomScreen room)
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

    private DungeonRoomKind ResolveRequestedRoomKind(RoomScreen currentRoom, RoomTransition sourceTransition)
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

    private bool TryInstantiateDungeonRoom(DungeonRoomKind roomKind, out RoomScreen room)
    {
        room = null;

        var template = ChooseRoomTemplate(roomKind);
        if (template == null)
        {
            GD.PushError($"{nameof(Dungeon)} has no configured {roomKind} room templates.");
            return false;
        }

        room = template.Instantiate<RoomScreen>();
        if (room == null)
        {
            GD.PushError($"{nameof(Dungeon)} could not instantiate a dungeon room for {roomKind}.");
            return false;
        }

        return true;
    }

    private PackedScene ChooseRoomTemplate(DungeonRoomKind roomKind)
    {
        var templates = roomKind switch
        {
            DungeonRoomKind.Combat => CombatRoomTemplates,
            DungeonRoomKind.Special => SpecialRoomTemplates,
            DungeonRoomKind.Timed => TimedRoomTemplates,
            _ => null,
        };

        var validTemplates = new List<PackedScene>();
        if (templates != null)
        {
            foreach (var template in templates)
            {
                if (template != null)
                    validTemplates.Add(template);
            }
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

    private static bool IsDungeonRoom(RoomScreen room)
    {
        return room is CombatDungeonRoom || room is SpecialDungeonRoom || room is TimedDungeonRoom;
    }

    private static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }

    private void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var swapIndex = _random.RandiRange(0, i);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }
}
