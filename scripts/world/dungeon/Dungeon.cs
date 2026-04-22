using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class Dungeon : Node
{
    private const string CombatDungeonRoomScenePath = "res://scenes/world/rooms/combat_dungeon_room.tscn";
    private static readonly StringName DungeonCombatScreenId = "dungeon_combat";
    private static readonly StringName DungeonEntryExitId = "south_return";
    private static readonly StringName EntranceHallScreenId = "entrance_hall";
    private static readonly StringName EntranceHallReturnExitId = "north_center";

    [Export]
    public Godot.Collections.Array<DungeonEncounterDefinition> EncounterPool { get; set; } = new();

    private readonly RandomNumberGenerator _random = new();

    private PackedScene _combatDungeonRoomScene;
    private CombatDungeonRoom _activeDungeonRoom;

    public override void _Ready()
    {
        _random.Randomize();
        _combatDungeonRoomScene = ResourceLoader.Load<PackedScene>(CombatDungeonRoomScenePath);
        if (_combatDungeonRoomScene == null)
            GD.PushError($"{nameof(Dungeon)} could not load combat room scene at '{CombatDungeonRoomScenePath}'.");
    }

    public bool TryCreateRoom(StringName screenId, RoomScreen currentRoom, Door sourceDoor, StringName entryExitId, out RoomScreen room)
    {
        room = null;
        if (screenId != DungeonCombatScreenId)
            return false;

        if (_combatDungeonRoomScene?.Instantiate<CombatDungeonRoom>() is not CombatDungeonRoom combatRoom)
        {
            GD.PushError($"{nameof(Dungeon)} could not instantiate a {nameof(CombatDungeonRoom)} for '{screenId}'.");
            return false;
        }

        if (currentRoom is not CombatDungeonRoom)
            StartNewRun();

        ConfigureCombatRoom(combatRoom);
        SetActiveDungeonRoom(combatRoom);
        room = combatRoom;
        return true;
    }

    public void OnTransitionCompleted(RoomScreen previousRoom, Door usedDoor, RoomScreen nextRoom)
    {
        if (previousRoom is CombatDungeonRoom && nextRoom is not CombatDungeonRoom)
            EndRun();
    }

    private void ConfigureCombatRoom(CombatDungeonRoom room)
    {
        room.ConfigureProgressionDoors(DungeonCombatScreenId, DungeonEntryExitId);
        room.ConfigureReturnDoor(EntranceHallScreenId, EntranceHallReturnExitId);
        room.PrepareEncounter();
        SpawnEncounter(room);
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
    }

    private void StartNewRun()
    {
        EndRun();
    }

    private void EndRun()
    {
        SetActiveDungeonRoom(null);
    }

    private void SetActiveDungeonRoom(CombatDungeonRoom room)
    {
        if (_activeDungeonRoom != null && GodotObject.IsInstanceValid(_activeDungeonRoom))
            _activeDungeonRoom.RoomCleared -= OnActiveRoomCleared;

        _activeDungeonRoom = room;

        if (_activeDungeonRoom != null)
            _activeDungeonRoom.RoomCleared += OnActiveRoomCleared;
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
