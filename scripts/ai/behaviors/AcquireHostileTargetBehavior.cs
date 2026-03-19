using Godot;

using System;

public sealed class AcquireHostileTargetBehavior : IActorBehavior
{
    private readonly float _acquisitionRange;
    private readonly NodePath _initialTargetPath;
    private readonly string _actorName;
    private readonly Func<ActorBase, bool> _canAttemptAcquisition;
    private readonly Func<ActorBase, Node2D, bool> _additionalTargetFilter;
    private bool _initialTargetChecked;

    public AcquireHostileTargetBehavior(
        float acquisitionRange,
        NodePath initialTargetPath = default,
        string actorName = null,
        Func<ActorBase, bool> canAttemptAcquisition = null,
        Func<ActorBase, Node2D, bool> additionalTargetFilter = null)
    {
        _acquisitionRange = Math.Max(0.0f, acquisitionRange);
        _initialTargetPath = initialTargetPath;
        _actorName = actorName;
        _canAttemptAcquisition = canAttemptAcquisition;
        _additionalTargetFilter = additionalTargetFilter;
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor.CurrentTarget != null)
            return false;

        if (_canAttemptAcquisition != null && !_canAttemptAcquisition(actor))
            return false;

        if (!_initialTargetChecked)
        {
            _initialTargetChecked = true;
            var initialTarget = ResolveInitialTarget(actor);
            if (CanAcquireTarget(actor, initialTarget))
            {
                intent = ActorIntent.WithTarget(initialTarget);
                return true;
            }

            if (initialTarget != null && _actorName != null)
                GD.PrintErr($"{_actorName} did not acquire initial target (not in aggro range).");
        }

        var candidate = TargetingHelper.FindClosestHostileTarget(
            actor,
            actor.Faction,
            node => node is Node2D targetNode && CanAcquireTarget(actor, targetNode));
        if (candidate == null)
            return false;

        intent = ActorIntent.WithTarget(candidate);
        return true;
    }

    private Node2D ResolveInitialTarget(ActorBase actor)
    {
        if (_initialTargetPath == null || _initialTargetPath.IsEmpty)
            return null;

        if (actor.HasNode(_initialTargetPath))
            return actor.GetNodeOrNull<Node2D>(_initialTargetPath);

        return null;
    }

    private bool CanAcquireTarget(ActorBase actor, Node2D target)
    {
        if (target == null)
            return false;

        if (target is not IAttackable ||
            target is not ITargetable targetable ||
            !targetable.CanBeTargeted)
            return false;

        if (!actor.IsHostileTo(target))
            return false;

        if (actor.GlobalPosition.DistanceTo(target.GlobalPosition) > _acquisitionRange)
            return false;

        return _additionalTargetFilter == null || _additionalTargetFilter(actor, target);
    }
}
