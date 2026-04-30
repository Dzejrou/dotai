using Godot;

using System.Collections.Generic;

public abstract partial class Content : Node2D
{
    [Export]
    public NodePath ObjectsPath { get; set; } = new("Objects");

    [Export]
    public NodePath ActorsPath { get; set; } = new("Actors");

    private Node _objectsRoot;
    private Node _actorsRoot;
    private bool _objectsRootResolved;
    private bool _actorsRootResolved;

    public bool IsEmpty => GetActiveChildCount() == 0;

    public override void _Ready()
    {
        Respawn();
    }

    public void Respawn()
    {
        foreach (var spawnPoint in GetActorSpawnPoints())
            spawnPoint.Respawn();
    }

    public void Restore()
    {
        foreach (var spawnPoint in GetActorSpawnPoints())
            spawnPoint.Restore();
    }

    public int GetActiveChildCount()
    {
        var activeChildCount = 0;
        foreach (var spawnPoint in GetActorSpawnPoints())
        {
            if (spawnPoint.IsOccupied())
                activeChildCount++;
        }

        return activeChildCount;
    }

    protected Node GetObjectsRoot()
    {
        if (_objectsRootResolved)
            return GodotObject.IsInstanceValid(_objectsRoot) ? _objectsRoot : null;

        _objectsRootResolved = true;
        _objectsRoot = ResolveRoot(ObjectsPath, nameof(ObjectsPath));
        return _objectsRoot;
    }

    protected Node GetActorsRoot()
    {
        if (_actorsRootResolved)
            return GodotObject.IsInstanceValid(_actorsRoot) ? _actorsRoot : null;

        _actorsRootResolved = true;
        _actorsRoot = ResolveRoot(ActorsPath, nameof(ActorsPath));
        return _actorsRoot;
    }

    private IEnumerable<ActorSpawnPoint> GetActorSpawnPoints()
    {
        var actorsRoot = GetActorsRoot();
        if (actorsRoot == null)
            yield break;

        foreach (var child in actorsRoot.GetChildren())
        {
            if (child is ActorSpawnPoint spawnPoint)
                yield return spawnPoint;
        }
    }

    private Node ResolveRoot(NodePath rootPath, string rootPathName)
    {
        if (rootPath.IsEmpty)
        {
            GD.PushError($"{nameof(Content)} '{Name}' has an empty {rootPathName}.");
            return null;
        }

        var root = GetNodeOrNull<Node>(rootPath);
        if (root == null)
            GD.PushError($"{nameof(Content)} '{Name}' could not resolve '{rootPath}'.");

        return root;
    }
}
