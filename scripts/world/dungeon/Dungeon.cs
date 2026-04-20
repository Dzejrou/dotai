using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Dungeon : Node
{
    private sealed class EncounterTemplate
    {
        public PackedScene[] EnemyScenes { get; init; } = Array.Empty<PackedScene>();
        public int MinCount { get; init; }
        public int MaxCount { get; init; }
    }

    private const string CombatDungeonRoomScenePath = "res://scenes/world/rooms/combat_dungeon_room.tscn";
    private static readonly StringName DungeonCombatScreenId = "dungeon_combat";
    private static readonly StringName DungeonEntryExitId = "south_return";
    private static readonly StringName EntranceHallScreenId = "entrance_hall";
    private static readonly StringName EntranceHallReturnExitId = "north_center";

    private readonly RandomNumberGenerator _random = new();
    private readonly List<EncounterTemplate> _encounterTemplates = new();

    private PackedScene _combatDungeonRoomScene;
    private CombatDungeonRoom _activeDungeonRoom;
    private int _roomIndex;

    public override void _Ready()
    {
        _random.Randomize();
        _combatDungeonRoomScene = ResourceLoader.Load<PackedScene>(CombatDungeonRoomScenePath);
        if (_combatDungeonRoomScene == null)
            GD.PushError($"{nameof(Dungeon)} could not load combat room scene at '{CombatDungeonRoomScenePath}'.");

        BuildEncounterTemplates();
    }

    public bool TryCreateRoom(StringName screenId, RoomScreen currentRoom, RoomExit sourceExit, StringName entryExitId, out RoomScreen room)
    {
        room = null;
        if (screenId != DungeonCombatScreenId)
            return false;

        if (_combatDungeonRoomScene?.Instantiate<CombatDungeonRoom>() is not CombatDungeonRoom combatRoom)
        {
            GD.PushError($"{nameof(Dungeon)} could not instantiate a {nameof(CombatDungeonRoom)} for '{screenId}'.");
            return false;
        }

        if (currentRoom is CombatDungeonRoom && sourceExit != null)
            _roomIndex += 1;
        else
            StartNewRun();

        ConfigureCombatRoom(combatRoom);
        SetActiveDungeonRoom(combatRoom);
        room = combatRoom;
        return true;
    }

    public void OnTransitionCompleted(RoomScreen previousRoom, RoomExit usedExit, RoomScreen nextRoom)
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

        if (_encounterTemplates.Count == 0)
        {
            GD.PushError($"{nameof(Dungeon)} has no encounter templates configured.");
            room.FinalizeEncounterSetup();
            return;
        }

        var encounterTemplate = _encounterTemplates[_random.RandiRange(0, _encounterTemplates.Count - 1)];
        var markerIds = new List<StringName>(room.GetEncounterMarkerIds());
        Shuffle(markerIds);

        var encounterCount = Mathf.Clamp(
            _random.RandiRange(encounterTemplate.MinCount, encounterTemplate.MaxCount) + Math.Min(_roomIndex, 2),
            1,
            markerIds.Count);

        for (var i = 0; i < encounterCount; i++)
        {
            if (!room.TryGetEncounterMarkerGlobalPosition(markerIds[i], out var spawnPosition))
                continue;

            var enemyScene = encounterTemplate.EnemyScenes[_random.RandiRange(0, encounterTemplate.EnemyScenes.Length - 1)];
            if (enemyScene?.Instantiate<Node2D>() is not Node2D enemy)
                continue;

            enemy.GlobalPosition = spawnPosition;
            spawnRoot.AddChild(enemy);
            room.RegisterEncounterEnemy(enemy);
        }

        room.FinalizeEncounterSetup();
    }

    private void OnActiveRoomCleared()
    {
    }

    private void StartNewRun()
    {
        EndRun();
        _roomIndex = 0;
    }

    private void EndRun()
    {
        SetActiveDungeonRoom(null);
        _roomIndex = 0;
    }

    private void SetActiveDungeonRoom(CombatDungeonRoom room)
    {
        if (_activeDungeonRoom != null && GodotObject.IsInstanceValid(_activeDungeonRoom))
            _activeDungeonRoom.RoomCleared -= OnActiveRoomCleared;

        _activeDungeonRoom = room;

        if (_activeDungeonRoom != null)
            _activeDungeonRoom.RoomCleared += OnActiveRoomCleared;
    }

    private void BuildEncounterTemplates()
    {
        _encounterTemplates.Clear();

        var skeleton = LoadEnemyScene("res://scenes/actors/enemies/skeleton.tscn");
        var wolf = LoadEnemyScene("res://scenes/actors/enemies/wolf.tscn");
        var skeletonMage = LoadEnemyScene("res://scenes/actors/enemies/skeleton_mage.tscn");
        var elfRanger = LoadEnemyScene("res://scenes/actors/enemies/elf_ranger.tscn");

        AddEncounterTemplate(2, 3, skeleton, wolf);
        AddEncounterTemplate(3, 4, skeleton, wolf, skeletonMage);
        AddEncounterTemplate(3, 4, skeleton, wolf, elfRanger);
    }

    private void AddEncounterTemplate(int minCount, int maxCount, params PackedScene[] enemyScenes)
    {
        var validScenes = new List<PackedScene>();
        foreach (var enemyScene in enemyScenes)
        {
            if (enemyScene != null)
                validScenes.Add(enemyScene);
        }

        if (validScenes.Count == 0)
            return;

        _encounterTemplates.Add(new EncounterTemplate
        {
            EnemyScenes = validScenes.ToArray(),
            MinCount = minCount,
            MaxCount = maxCount,
        });
    }

    private static PackedScene LoadEnemyScene(string resourcePath)
    {
        return ResourceLoader.Load<PackedScene>(resourcePath);
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
