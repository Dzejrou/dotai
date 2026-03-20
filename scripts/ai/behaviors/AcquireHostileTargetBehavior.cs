using Godot;

using System;

[GlobalClass]
public partial class AcquireHostileTargetBehavior : Node, IActorBehavior
{
    [Export]
    public float AcquisitionRange { get; set; } = 150.0f;

    [Export]
    public NodePath InitialTargetPath { get; set; } = new NodePath();

    [Export]
    public string DebugActorName { get; set; }

    [Export]
    public bool SuppressWhileSummonRecovering { get; set; } = false;

    [Export]
    public bool SuppressWhileSummonerNeedsLeashReturn { get; set; } = false;

    [Export]
    public float MaxSummonTargetDistanceFromSummoner { get; set; } = -1.0f;

    private readonly Func<ActorBase, bool> _canAttemptAcquisition;
    private readonly Func<ActorBase, Node2D, bool> _additionalTargetFilter;
    private bool _initialTargetChecked;

    public AcquireHostileTargetBehavior() { }

    public AcquireHostileTargetBehavior(
        float acquisitionRange,
        NodePath initialTargetPath = default,
        string actorName = null,
        Func<ActorBase, bool> canAttemptAcquisition = null,
        Func<ActorBase, Node2D, bool> additionalTargetFilter = null)
    {
        AcquisitionRange = Math.Max(0.0f, acquisitionRange);
        InitialTargetPath = initialTargetPath;
        DebugActorName = actorName;
        _canAttemptAcquisition = canAttemptAcquisition;
        _additionalTargetFilter = additionalTargetFilter;
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor.CurrentTarget != null)
            return false;

        if (_canAttemptAcquisition != null)
        {
            if (!_canAttemptAcquisition(actor))
                return false;
        }
        else if (!CanAttemptNodeDrivenAcquisition(actor))
        {
            return false;
        }

        if (!_initialTargetChecked)
        {
            _initialTargetChecked = true;
            var initialTarget = ResolveInitialTarget(actor);
            if (CanAcquireTarget(actor, initialTarget))
            {
                intent = ActorIntent.WithTarget(initialTarget);
                return true;
            }

            if (initialTarget != null && !string.IsNullOrEmpty(DebugActorName))
                GD.PrintErr($"{DebugActorName} did not acquire initial target (not in aggro range).");
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

    private bool CanAttemptNodeDrivenAcquisition(ActorBase actor)
    {
        var followSummonerBehavior = ResolveFollowSummonerBehavior(actor);
        if (followSummonerBehavior == null)
            return true;

        if (SuppressWhileSummonRecovering && followSummonerBehavior.IsRecovering)
            return false;

        if (SuppressWhileSummonerNeedsLeashReturn && followSummonerBehavior.ShouldPrioritizeLeashReturn(actor))
            return false;

        return true;
    }

    private Node2D ResolveInitialTarget(ActorBase actor)
    {
        if (InitialTargetPath == null || InitialTargetPath.IsEmpty)
            return null;

        if (actor.HasNode(InitialTargetPath))
            return actor.GetNodeOrNull<Node2D>(InitialTargetPath);

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

        if (actor.GlobalPosition.DistanceTo(target.GlobalPosition) > Math.Max(0.0f, AcquisitionRange))
            return false;

        if (!PassesSummonTargetFilter(actor, target))
            return false;

        return _additionalTargetFilter == null || _additionalTargetFilter(actor, target);
    }

    private bool PassesSummonTargetFilter(ActorBase actor, Node2D target)
    {
        if (MaxSummonTargetDistanceFromSummoner < 0.0f)
            return true;

        var summonState = SummonState.ResolveFor(actor);
        if (summonState?.SummonerNode == null || !summonState.IsSummoned)
            return true;

        var summonerNode = summonState.SummonerNode;
        if (!GodotObject.IsInstanceValid(summonerNode) || !summonerNode.IsInsideTree())
            return false;

        return summonerNode.GlobalPosition.DistanceTo(target.GlobalPosition) <= MaxSummonTargetDistanceFromSummoner;
    }

    private static FollowSummonerBehavior ResolveFollowSummonerBehavior(ActorBase actor)
    {
        return actor?.GetNodeOrNull<FollowSummonerBehavior>("Behaviors/Tier90_Recovery/FollowSummonerBehavior");
    }
}
