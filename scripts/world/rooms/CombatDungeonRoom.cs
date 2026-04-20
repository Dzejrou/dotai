using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class CombatDungeonRoom : RoomScreen
{
    private static readonly StringName TopLeftExitId = "north_west";
    private static readonly StringName TopRightExitId = "north_east";
    private static readonly StringName BottomReturnExitId = "south_return";
    private static readonly Color LockedIndicatorColor = new(0.88f, 0.24f, 0.24f, 1.0f);
    private static readonly Color UnlockedIndicatorColor = new(0.30f, 0.86f, 0.34f, 1.0f);

    [Signal]
    public delegate void RoomClearedEventHandler();

    [Export]
    public NodePath EncounterMarkersPath { get; set; } = new NodePath("Unscaled/EncounterMarkers");

    [Export]
    public NodePath TopLeftIndicatorPath { get; set; } = new NodePath("Scaled/Visual/LockIndicators/NorthWestIndicator");

    [Export]
    public NodePath TopRightIndicatorPath { get; set; } = new NodePath("Scaled/Visual/LockIndicators/NorthEastIndicator");

    private readonly Dictionary<StringName, Marker2D> _encounterMarkersById = new();
    private readonly Dictionary<Node, Callable> _enemyExitCallables = new();
    private bool _isCleared;

    public override void _Ready()
    {
        base._Ready();
        CacheEncounterMarkers();
        SetTopDoorsLocked(true);
    }

    public override void _ExitTree()
    {
        DisconnectTrackedEnemies();
        _encounterMarkersById.Clear();
        base._ExitTree();
    }

    public IReadOnlyList<StringName> GetEncounterMarkerIds()
    {
        CacheEncounterMarkers();
        return new List<StringName>(_encounterMarkersById.Keys);
    }

    public bool TryGetEncounterMarkerGlobalPosition(StringName markerId, out Vector2 globalPosition)
    {
        CacheEncounterMarkers();
        if (_encounterMarkersById.TryGetValue(markerId, out var marker))
        {
            globalPosition = marker.GlobalPosition;
            return true;
        }

        globalPosition = Vector2.Zero;
        return false;
    }

    public void ConfigureProgressionDoors(StringName targetScreenId, StringName targetExitId)
    {
        ConfigureExit(TopLeftExitId, targetScreenId, targetExitId);
        ConfigureExit(TopRightExitId, targetScreenId, targetExitId);
    }

    public void ConfigureReturnDoor(StringName targetScreenId, StringName targetExitId)
    {
        ConfigureExit(BottomReturnExitId, targetScreenId, targetExitId);
    }

    public void PrepareEncounter()
    {
        _isCleared = false;
        DisconnectTrackedEnemies();
        SetTopDoorsLocked(true);
    }

    public void RegisterEncounterEnemy(Node enemy)
    {
        if (enemy == null || _enemyExitCallables.ContainsKey(enemy))
            return;

        var callable = Callable.From(() => OnTrackedEnemyExited(enemy));
        _enemyExitCallables[enemy] = callable;
        enemy.Connect(Node.SignalName.TreeExited, callable, (uint)ConnectFlags.OneShot);
    }

    public void FinalizeEncounterSetup()
    {
        EvaluateEncounterState();
    }

    private void ConfigureExit(StringName exitId, StringName targetScreenId, StringName targetExitId)
    {
        var exit = GetExit(exitId);
        if (exit == null)
            return;

        exit.TargetScreenId = targetScreenId;
        exit.TargetExitId = targetExitId;
    }

    private void CacheEncounterMarkers()
    {
        if (_encounterMarkersById.Count > 0)
            return;

        var markersRoot = GetNodeOrNull<Node>(EncounterMarkersPath);
        if (markersRoot == null)
            return;

        foreach (Node child in markersRoot.GetChildren())
        {
            if (child is Marker2D marker)
                _encounterMarkersById[marker.Name] = marker;
        }
    }

    private void DisconnectTrackedEnemies()
    {
        foreach (var entry in _enemyExitCallables)
        {
            var enemy = entry.Key;
            var callable = entry.Value;
            if (GodotObject.IsInstanceValid(enemy) && enemy.IsConnected(Node.SignalName.TreeExited, callable))
                enemy.Disconnect(Node.SignalName.TreeExited, callable);
        }

        _enemyExitCallables.Clear();
    }

    private void OnTrackedEnemyExited(Node enemy)
    {
        _enemyExitCallables.Remove(enemy);
        EvaluateEncounterState();
    }

    private void EvaluateEncounterState()
    {
        if (_isCleared || _enemyExitCallables.Count > 0)
            return;

        _isCleared = true;
        SetTopDoorsLocked(false);
        EmitSignal(SignalName.RoomCleared);
    }

    private void SetTopDoorsLocked(bool isLocked)
    {
        SetExitLockState(TopLeftExitId, isLocked);
        SetExitLockState(TopRightExitId, isLocked);
        SetIndicatorColor(GetNodeOrNull<Polygon2D>(TopLeftIndicatorPath), isLocked ? LockedIndicatorColor : UnlockedIndicatorColor);
        SetIndicatorColor(GetNodeOrNull<Polygon2D>(TopRightIndicatorPath), isLocked ? LockedIndicatorColor : UnlockedIndicatorColor);
    }

    private void SetExitLockState(StringName exitId, bool isLocked)
    {
        var exit = GetExit(exitId);
        if (exit != null)
            exit.IsLocked = isLocked;
    }

    private static void SetIndicatorColor(Polygon2D indicator, Color color)
    {
        if (indicator != null)
            indicator.Color = color;
    }
}
