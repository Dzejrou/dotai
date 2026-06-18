using Godot;

using System;
using System.Collections.Generic;

// Editor-configurable behavior container that gates a set of child behaviors on the
// actor's current phase. It is collected by Actor like any behavior (IActorBehavior/
// IActorTickBehavior) but, being an IActorBehaviorContainer, its children are not also
// collected directly - this group exclusively owns forwarding to them.
//
// While active it forwards intent resolution and tick updates to its children in scene
// order, preserving the ordering and target-change semantics Actor applies to
// top-level behaviors. While inactive it is a no-op, so the grouped behaviors neither
// resolve intents nor tick.
[GlobalClass]
public partial class PhaseGroup : Node, IActorBehavior, IActorTickBehavior, IActorBehaviorContainer
{
    // Phases in which this group is active. Empty means active in every phase.
    [Export]
    public int[] ActivePhases { get; set; } = Array.Empty<int>();

    private readonly List<IActorBehavior> _behaviors = new();
    private readonly List<IActorTickBehavior> _tickBehaviors = new();
    private bool _resolved;

    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;
        EnsureResolved();
        if (!IsActive(actor))
            return false;

        // Mirror Actor.TryResolveBehaviorIntent: a child may emit a target change with
        // no execution directive; apply it and keep going so later children act on the
        // updated target. Only an intent with an execution directive ends resolution.
        foreach (var behavior in _behaviors)
        {
            if (!behavior.TryCreateIntent(actor, delta, out var candidate))
                continue;

            if (candidate.ChangeTarget)
            {
                if (candidate.Target == null)
                    actor.ClearTarget();
                else
                    actor.SetTarget(candidate.Target);
            }

            if (!candidate.HasExecutionDirective)
                continue;

            intent = candidate;
            return true;
        }

        return false;
    }

    public void Update(Actor actor, double delta)
    {
        EnsureResolved();
        if (!IsActive(actor))
            return;

        foreach (var tickBehavior in _tickBehaviors)
            tickBehavior.Update(actor, delta);
    }

    private bool IsActive(Actor actor)
    {
        if (actor == null)
            return false;

        // A transition owns the actor; grouped behaviors stay inactive even though
        // CurrentPhase still reports the pre-transition phase until completion.
        if (actor.IsTransitioning)
            return false;

        var phases = ActivePhases;
        if (phases == null || phases.Length == 0)
            return true;

        return Array.IndexOf(phases, actor.CurrentPhase) >= 0;
    }

    private void EnsureResolved()
    {
        if (_resolved)
            return;

        _resolved = true;
        CollectChildBehaviors(this);
    }

    // Discovers child behaviors in scene order. A nested behavior container is
    // collected as a single behavior and not descended into (it owns and forwards to
    // its own children), so nested groups compose without double-collecting children.
    private void CollectChildBehaviors(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is not Node childNode)
                continue;

            if (childNode is IActorBehavior behavior)
            {
                _behaviors.Add(behavior);
                if (behavior is IActorTickBehavior tickBehavior)
                    _tickBehaviors.Add(tickBehavior);
            }

            if (childNode is IActorBehaviorContainer)
                continue;

            CollectChildBehaviors(childNode);
        }
    }
}
