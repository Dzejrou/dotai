using Godot;

using System;
using System.Collections.Generic;

// Opt-in arbiter that lets a single actor choose between several existing
// ICombatActionController implementations while remaining the actor's only
// PrimaryActionController. Children keep their own cooldowns and behavior; the
// composite only decides which one acts each time the actor wants to attack.
[GlobalClass]
public partial class CompositeActionController : Node, ICombatActionController
{
    private readonly struct ResolvedEntry
    {
        public ResolvedEntry(ICombatActionController controller, int priority, float weight, int[] activePhases)
        {
            Controller = controller;
            Priority = priority;
            Weight = weight;
            ActivePhases = activePhases;
        }

        public ICombatActionController Controller { get; }
        public int Priority { get; }
        public float Weight { get; }

        // Phases in which this entry can be selected. Null/empty means every phase.
        public int[] ActivePhases { get; }
    }

    private readonly RandomNumberGenerator _random = new();
    private readonly List<ICombatActionController> _updateChildren = new();
    private readonly List<ResolvedEntry> _entries = new();
    private readonly List<ResolvedEntry> _selectionScratch = new();

    private ICombatActionController _fallbackMovementController;
    private ICombatActionController _selectedController;
    private ICombatActionController _activeController;
    private bool _resolved;

    [Export]
    public Godot.Collections.Array<CompositeActionEntry> Entries { get; set; } = new();

    // Range source used while no child action can currently start: the actor keeps
    // closing to this controller's range instead of waiting indefinitely.
    [Export]
    public NodePath FallbackMovementController { get; set; }

    public float MinimumRange => ResolveRangeSource()?.MinimumRange ?? 0.0f;
    public float PreferredRange => ResolveRangeSource()?.PreferredRange ?? 0.0f;

    // The composite is busy while its active child owns an in-flight operation.
    public bool IsBusy => _activeController?.IsBusy ?? false;

    public override void _Ready()
    {
        ResolveConfiguration();
    }

    public void Update(Actor actor, double delta)
    {
        EnsureResolved();

        // Every child ticks so their independent cooldowns continue advancing even
        // when another child is the one acting this frame.
        foreach (var child in _updateChildren)
            child.Update(actor, delta);

        // A timer-driven cast can finish inside a child's Update without any
        // animation-finished callback (e.g. the Demon's missing 'cast' release
        // art). Release ownership as soon as the active child is no longer busy
        // so a finished cast cannot leave the composite permanently active.
        if (_activeController != null && !_activeController.IsBusy)
            _activeController = null;
    }

    public bool CanStartAction(Actor actor, Node2D target)
    {
        EnsureResolved();
        _selectedController = SelectController(actor, target);
        return _selectedController != null;
    }

    public void StartAction(Actor actor, Node2D target)
    {
        EnsureResolved();

        // Use the controller cached by the preceding CanStartAction so the two calls
        // cannot disagree. Fall back to a fresh selection only if StartAction was
        // invoked without a successful CanStartAction (defensive).
        var controller = _selectedController ?? SelectController(actor, target);
        _selectedController = null;
        if (controller == null)
            return;

        _activeController = controller;
        controller.StartAction(actor, target);

        // When the child completed instantly (no lasting operation began) it never
        // becomes busy, so no animation-finished callback will arrive. Release
        // ownership now so a missing/failed action animation cannot leave the
        // composite permanently active. Casts that began a windup, or instant casts
        // whose release art is playing, stay busy and retain ownership.
        if (!_activeController.IsBusy)
            _activeController = null;
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        var controller = _activeController;
        if (controller == null)
            return false;

        var handled = controller.HandleAnimationFinished(actor, animationName);
        if (handled)
            _activeController = null;

        return handled;
    }

    public void Cancel(Actor actor)
    {
        _activeController?.Cancel(actor);
        if (_selectedController != null && !ReferenceEquals(_selectedController, _activeController))
            _selectedController.Cancel(actor);

        _selectedController = null;
        _activeController = null;
    }

    private ICombatActionController ResolveRangeSource()
    {
        EnsureResolved();
        return _selectedController ?? _fallbackMovementController;
    }

    private ICombatActionController SelectController(Actor actor, Node2D target)
    {
        if (actor == null || target == null)
            return null;

        // Keep only entries whose controller can act right now, tracking the highest
        // priority seen among them.
        _selectionScratch.Clear();
        var currentPhase = actor.CurrentPhase;
        var highestPriority = int.MinValue;
        foreach (var entry in _entries)
        {
            if (entry.Controller == null || !(entry.Weight > 0.0f))
                continue;

            // An entry inactive for the actor's current phase is never selected, even
            // though its controller keeps ticking its own cooldowns via _updateChildren.
            if (!IsActiveForPhase(entry.ActivePhases, currentPhase))
                continue;

            if (!entry.Controller.CanStartAction(actor, target))
                continue;

            if (entry.Priority > highestPriority)
                highestPriority = entry.Priority;

            _selectionScratch.Add(entry);
        }

        var totalWeight = 0.0f;
        foreach (var entry in _selectionScratch)
        {
            if (entry.Priority == highestPriority)
                totalWeight += entry.Weight;
        }

        if (!(totalWeight > 0.0f))
            return null;

        // Weighted random choice between the equal-priority winners.
        var roll = _random.Randf() * totalWeight;
        var cumulativeWeight = 0.0f;
        ICombatActionController chosen = null;
        foreach (var entry in _selectionScratch)
        {
            if (entry.Priority != highestPriority)
                continue;

            cumulativeWeight += entry.Weight;
            chosen = entry.Controller;
            if (roll < cumulativeWeight)
                return chosen;
        }

        return chosen;
    }

    // Empty/null phase list means the entry is selectable in every phase, so entries
    // authored before phase filtering existed keep their original behavior.
    private static bool IsActiveForPhase(int[] activePhases, int currentPhase)
    {
        if (activePhases == null || activePhases.Length == 0)
            return true;

        return Array.IndexOf(activePhases, currentPhase) >= 0;
    }

    private void EnsureResolved()
    {
        if (!_resolved)
            ResolveConfiguration();
    }

    private void ResolveConfiguration()
    {
        _resolved = true;
        _updateChildren.Clear();
        _entries.Clear();

        // Every direct child controller ticks, regardless of whether it appears in an
        // entry, so configured cooldowns keep advancing.
        foreach (var child in GetChildren())
        {
            if (child is ICombatActionController controller)
                _updateChildren.Add(controller);
        }

        if (Entries != null)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry == null)
                    continue;

                var controller = ResolveController(entry.Controller);
                if (controller == null)
                {
                    GD.PushWarning($"{GetPath()}: composite action entry #{i} could not resolve a controller at '{entry.Controller}'.");
                    continue;
                }

                if (!_updateChildren.Contains(controller))
                    _updateChildren.Add(controller);

                _entries.Add(new ResolvedEntry(controller, entry.Priority, Math.Max(0.0f, entry.Weight), entry.ActivePhases));
            }
        }

        _fallbackMovementController = ResolveController(FallbackMovementController);
        if (_fallbackMovementController == null)
            GD.PushWarning($"{GetPath()}: composite action fallback movement controller could not be resolved at '{FallbackMovementController}'.");
    }

    private ICombatActionController ResolveController(NodePath path)
    {
        if (path == null || path.IsEmpty)
            return null;

        var node = GetNodeOrNull(path);
        if (ReferenceEquals(node, this))
            return null;

        return node as ICombatActionController;
    }
}
