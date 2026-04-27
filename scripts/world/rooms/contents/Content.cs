using Godot;

using System.Collections.Generic;

public abstract partial class Content : Node2D
{
    private readonly List<Node> _initialChildTemplates = new();
    private bool _initialChildSnapshotCaptured;

    public bool IsEmpty => GetActiveChildCount() == 0;

    public override void _Ready()
    {
        CaptureInitialChildSnapshot();
    }

    public void Respawn()
    {
        ClearCurrentChildren();
        RecreateInitialChildren();
    }

    public int GetActiveChildCount()
    {
        var activeChildCount = 0;
        foreach (var child in GetChildren())
        {
            if (child is Node node &&
                GodotObject.IsInstanceValid(node) &&
                !node.IsQueuedForDeletion())
            {
                activeChildCount++;
            }
        }

        return activeChildCount;
    }

    private void CaptureInitialChildSnapshot()
    {
        if (_initialChildSnapshotCaptured)
            return;

        _initialChildTemplates.Clear();
        foreach (var child in GetChildren())
        {
            if (child is not Node node || !GodotObject.IsInstanceValid(node))
                continue;

            if (node.Duplicate() is not Node template)
            {
                GD.PushWarning($"{nameof(Content)} '{Name}' could not snapshot child '{node.Name}'.");
                continue;
            }

            _initialChildTemplates.Add(template);
        }

        _initialChildSnapshotCaptured = true;
    }

    private void ClearCurrentChildren()
    {
        foreach (var child in GetChildren())
        {
            if (child is not Node node || !GodotObject.IsInstanceValid(node))
                continue;

            RemoveChild(node);
            node.QueueFree();
        }
    }

    private void RecreateInitialChildren()
    {
        foreach (var template in _initialChildTemplates)
        {
            if (!GodotObject.IsInstanceValid(template))
            {
                GD.PushWarning($"{nameof(Content)} '{Name}' is missing a valid child template during respawn.");
                continue;
            }

            if (template.Duplicate() is not Node childInstance)
            {
                GD.PushWarning($"{nameof(Content)} '{Name}' could not recreate child '{template.Name}' during respawn.");
                continue;
            }

            AddChild(childInstance);
        }
    }
}
