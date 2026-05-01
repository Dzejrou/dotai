using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Room : Node2D
{
    private const string EphemeralNodeName = "Ephemeral";

    [Export]
    public StringName ScreenId { get; set; } = default;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public StringName Theme { get; set; } = default;

    [Export]
    public StringName RoomType { get; set; } = default;

    [Export]
    public StringName RoomSize { get; set; } = default;

    [Export]
    public bool PersistInstance { get; set; }

    [Export]
    public NodePath ExitsPath { get; set; } = new NodePath("Scaled/Exits");

    [Export]
    public NodePath MarkersPath { get; set; } = new NodePath("Scaled/Markers");

    [Export]
    public NodePath PlayerStartPath { get; set; } = new NodePath("Scaled/Markers/PlayerStart");

    [Export]
    public NodePath CameraAnchorPath { get; set; } = new NodePath("Scaled/Markers/CameraAnchor");

    [Export]
    public NodePath UnscaledPath { get; set; } = new NodePath("Unscaled");

    [Export]
    public NodePath ScaledPath { get; set; } = new NodePath("Scaled");

    [Export]
    public Rect2 CameraBoundsRect { get; set; } = new Rect2(0.0f, 0.0f, 400.0f, 400.0f);

    public event Action<RoomTransition> TransitionTriggered;

    private readonly Dictionary<StringName, RoomTransition> _transitionsById = new();
    private Node _attachedContentInstance;

    public override void _EnterTree()
    {
        EnsureTransitionsCached();
    }

    public override void _ExitTree()
    {
        foreach (var transition in _transitionsById.Values)
            transition.TransitionRequested -= OnTransitionRequested;

        _transitionsById.Clear();
    }

    public RoomTransition GetTransition(StringName exitId)
    {
        EnsureTransitionsCached();
        return HasValue(exitId) && _transitionsById.TryGetValue(exitId, out var transition)
            ? transition
            : null;
    }

    public Door GetDoor(StringName exitId)
    {
        return GetTransition(exitId) as Door;
    }

    public virtual void OnEnter()
    {
    }

    public virtual void OnExit()
    {
        ClearEphemeralChildren(GetScaledEphemeralRoot());
        ClearEphemeralChildren(GetUnscaledEphemeralRoot());
    }

    public bool TryGetSpawnMarker(StringName exitId, out Marker2D marker)
    {
        EnsureTransitionsCached();
        marker = null;

        if (HasValue(exitId))
        {
            var entryTransition = GetTransition(exitId);
            marker = entryTransition?.GetSpawnPoint();
            if (marker != null)
                return true;
        }

        marker = GetNodeOrNull<Marker2D>(PlayerStartPath);
        return marker != null;
    }

    public Marker2D GetCameraAnchor()
    {
        if (CameraAnchorPath.IsEmpty)
            return null;

        return GetNodeOrNull<Marker2D>(CameraAnchorPath);
    }

    public Rect2 GetWorldCameraBounds()
    {
        var localBounds = CameraBoundsRect.Size.X > 0.0f && CameraBoundsRect.Size.Y > 0.0f
            ? CameraBoundsRect
            : new Rect2(0.0f, 0.0f, 400.0f, 400.0f);

        var transformRoot = GetNodeOrNull<Node2D>(ScaledPath) ?? this;
        var transform = transformRoot.GlobalTransform;

        var topLeft = transform * localBounds.Position;
        var topRight = transform * new Vector2(localBounds.End.X, localBounds.Position.Y);
        var bottomRight = transform * localBounds.End;
        var bottomLeft = transform * new Vector2(localBounds.Position.X, localBounds.End.Y);

        var minX = Mathf.Min(Mathf.Min(topLeft.X, topRight.X), Mathf.Min(bottomRight.X, bottomLeft.X));
        var minY = Mathf.Min(Mathf.Min(topLeft.Y, topRight.Y), Mathf.Min(bottomRight.Y, bottomLeft.Y));
        var maxX = Mathf.Max(Mathf.Max(topLeft.X, topRight.X), Mathf.Max(bottomRight.X, bottomLeft.X));
        var maxY = Mathf.Max(Mathf.Max(topLeft.Y, topRight.Y), Mathf.Max(bottomRight.Y, bottomLeft.Y));

        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }

    public Node2D GetUnscaledRoot()
    {
        if (UnscaledPath.IsEmpty)
            return null;

        return GetNodeOrNull<Node2D>(UnscaledPath);
    }

    public Node GetScaledEphemeralRoot()
    {
        return ResolveEphemeralRoot(ScaledPath, nameof(ScaledPath));
    }

    public Node GetUnscaledEphemeralRoot()
    {
        return ResolveEphemeralRoot(UnscaledPath, nameof(UnscaledPath));
    }

    public bool TryAttachContent(PackedScene contentScene, bool replaceExisting = false)
    {
        if (contentScene == null)
        {
            GD.PushError($"{nameof(Room)} '{Name}' cannot attach a null content scene.");
            return false;
        }

        var unscaledRoot = GetUnscaledRoot();
        if (unscaledRoot == null)
        {
            GD.PushError($"{nameof(Room)} '{Name}' could not resolve unscaled root '{UnscaledPath}' for content attachment.");
            return false;
        }

        if (GodotObject.IsInstanceValid(_attachedContentInstance))
        {
            if (!replaceExisting)
            {
                GD.PushError($"{nameof(Room)} '{Name}' already has attached runtime content.");
                return false;
            }

            ClearAttachedContent();
        }

        var contentInstance = contentScene.Instantiate();
        if (contentInstance == null)
        {
            GD.PushError($"{nameof(Room)} '{Name}' failed to instantiate content scene '{contentScene.ResourcePath}'.");
            return false;
        }

        unscaledRoot.AddChild(contentInstance);
        _attachedContentInstance = contentInstance;
        return true;
    }

    public void ClearAttachedContent()
    {
        if (!GodotObject.IsInstanceValid(_attachedContentInstance))
        {
            _attachedContentInstance = null;
            return;
        }

        var parent = _attachedContentInstance.GetParent();
        if (parent != null)
            parent.RemoveChild(_attachedContentInstance);

        _attachedContentInstance.QueueFree();
        _attachedContentInstance = null;
    }

    private void CacheTransitions()
    {
        _transitionsById.Clear();

        var exitsRoot = GetNodeOrNull<Node>(ExitsPath);
        if (exitsRoot == null)
            return;

        foreach (Node child in exitsRoot.GetChildren())
        {
            if (child is not RoomTransition transition || !HasValue(transition.ExitId))
                continue;

            _transitionsById[transition.ExitId] = transition;
            transition.TransitionRequested += OnTransitionRequested;
        }
    }

    private void EnsureTransitionsCached()
    {
        if (_transitionsById.Count > 0)
            return;

        CacheTransitions();
    }

    private void ClearEphemeralChildren(Node ephemeralRoot)
    {
        if (ephemeralRoot == null)
            return;

        foreach (Node child in ephemeralRoot.GetChildren())
        {
            ephemeralRoot.RemoveChild(child);
            child.QueueFree();
        }
    }

    private Node ResolveEphemeralRoot(NodePath parentPath, string parentPathName)
    {
        if (parentPath.IsEmpty)
        {
            GD.PushError($"{nameof(Room)} '{Name}' has an empty {parentPathName} and cannot resolve '{EphemeralNodeName}'.");
            return null;
        }

        var parentRoot = GetNodeOrNull<Node>(parentPath);
        if (parentRoot == null)
        {
            GD.PushError($"{nameof(Room)} '{Name}' could not resolve '{parentPath}' and cannot attach '{EphemeralNodeName}'.");
            return null;
        }

        var ephemeralRoot = parentRoot.GetNodeOrNull<Node>(EphemeralNodeName);
        if (ephemeralRoot != null)
            return ephemeralRoot;

        ephemeralRoot = new Node
        {
            Name = EphemeralNodeName,
        };
        parentRoot.AddChild(ephemeralRoot);
        GD.PushWarning($"{nameof(Room)} '{Name}' was missing '{parentPath}/{EphemeralNodeName}'. Created it at runtime.");
        return ephemeralRoot;
    }

    private void OnTransitionRequested(RoomTransition transition)
    {
        TransitionTriggered?.Invoke(transition);
    }

    private static bool HasValue(StringName value)
    {
        return value != null && !value.IsEmpty;
    }
}
