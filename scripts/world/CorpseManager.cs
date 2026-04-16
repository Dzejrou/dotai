using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class CorpseManager : Node
{
    private const int DefaultCorpseLimit = 20;

    [Export(PropertyHint.Range, "0,200,1")]
    public int CorpseLimit { get; set; } = DefaultCorpseLimit;

    private readonly List<Corpse> _trackedCorpses = new();
    private readonly Dictionary<ulong, Callable> _treeExitCallablesByCorpseId = new();

    public void Register(Corpse corpse)
    {
        if (!IsTrackable(corpse))
            return;

        Unregister(corpse);

        _trackedCorpses.Add(corpse);
        var corpseId = corpse.GetInstanceId();
        var treeExitCallable = Callable.From(() => OnTrackedCorpseTreeExited(corpse));
        _treeExitCallablesByCorpseId[corpseId] = treeExitCallable;
        corpse.Connect(Node.SignalName.TreeExited, treeExitCallable, (uint)ConnectFlags.OneShot);

        EnforceLimit();
    }

    public void Unregister(Corpse corpse)
    {
        if (corpse == null)
            return;

        _trackedCorpses.Remove(corpse);
        DisconnectTrackedCorpse(corpse);
    }

    public override void _ExitTree()
    {
        for (var i = 0; i < _trackedCorpses.Count; i++)
            DisconnectTrackedCorpse(_trackedCorpses[i]);

        _trackedCorpses.Clear();
        _treeExitCallablesByCorpseId.Clear();
    }

    private void EnforceLimit()
    {
        var maxTrackedCorpses = Math.Max(0, CorpseLimit);
        while (_trackedCorpses.Count > maxTrackedCorpses)
        {
            var oldestCorpse = _trackedCorpses[0];
            _trackedCorpses.RemoveAt(0);
            DisconnectTrackedCorpse(oldestCorpse);

            if (GodotObject.IsInstanceValid(oldestCorpse))
                oldestCorpse.QueueFree();
        }
    }

    private void OnTrackedCorpseTreeExited(Corpse corpse)
    {
        Unregister(corpse);
    }

    private void DisconnectTrackedCorpse(Corpse corpse)
    {
        if (corpse == null)
            return;

        var corpseId = corpse.GetInstanceId();
        if (!_treeExitCallablesByCorpseId.TryGetValue(corpseId, out var treeExitCallable))
            return;

        _treeExitCallablesByCorpseId.Remove(corpseId);
        if (GodotObject.IsInstanceValid(corpse) && corpse.IsConnected(Node.SignalName.TreeExited, treeExitCallable))
            corpse.Disconnect(Node.SignalName.TreeExited, treeExitCallable);
    }

    private static bool IsTrackable(Corpse corpse)
    {
        return corpse != null && GodotObject.IsInstanceValid(corpse);
    }
}
