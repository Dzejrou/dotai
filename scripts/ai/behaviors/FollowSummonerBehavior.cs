using Godot;

using System;

public sealed class FollowSummonerBehavior : IActorBehavior
{
    private readonly Func<ActorBase, Node2D> _summonerGetter;
    private readonly Func<ActorBase, Vector2> _anchorGetter;
    private readonly float _startLeashDistance;
    private readonly float _stopLeashDistance;
    private readonly float _idleAnchorTolerance;
    private readonly float _leashSpeedMultiplier;
    private readonly bool _followWhenIdle;
    private readonly CombatUnitState _leashState;
    private readonly CombatUnitState _followState;
    private bool _recoveryRequested;

    public FollowSummonerBehavior(
        Func<ActorBase, Node2D> summonerGetter,
        Func<ActorBase, Vector2> anchorGetter,
        float startLeashDistance,
        float stopLeashDistance,
        float idleAnchorTolerance,
        float leashSpeedMultiplier,
        bool followWhenIdle,
        CombatUnitState leashState = CombatUnitState.Leashing,
        CombatUnitState followState = CombatUnitState.FollowingOwner)
    {
        _summonerGetter = summonerGetter ?? throw new ArgumentNullException(nameof(summonerGetter));
        _anchorGetter = anchorGetter ?? throw new ArgumentNullException(nameof(anchorGetter));
        _startLeashDistance = Math.Max(0.0f, startLeashDistance);
        _stopLeashDistance = Math.Clamp(stopLeashDistance, 0.0f, _startLeashDistance);
        _idleAnchorTolerance = Math.Max(0.0f, idleAnchorTolerance);
        _leashSpeedMultiplier = Math.Max(0.0f, leashSpeedMultiplier);
        _followWhenIdle = followWhenIdle;
        _leashState = leashState;
        _followState = followState;
    }

    public bool IsRecovering => _recoveryRequested;

    public void BeginRecovery()
    {
        _recoveryRequested = true;
    }

    public void CancelRecovery()
    {
        _recoveryRequested = false;
    }

    public bool ShouldPrioritizeLeashReturn(ActorBase actor)
    {
        var summonerNode = _summonerGetter(actor);
        if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode) || !summonerNode.IsInsideTree())
            return false;

        var distanceToSummoner = actor.GlobalPosition.DistanceTo(summonerNode.GlobalPosition);
        if (_recoveryRequested || actor.CurrentState == _leashState)
            return distanceToSummoner > _stopLeashDistance;

        return distanceToSummoner > _startLeashDistance;
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        var summonerNode = _summonerGetter(actor);
        if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode) || !summonerNode.IsInsideTree())
            return false;

        var distanceToSummoner = actor.GlobalPosition.DistanceTo(summonerNode.GlobalPosition);
        if ((actor.CurrentTarget != null && ShouldPrioritizeLeashReturn(actor)) || _recoveryRequested)
        {
            if (distanceToSummoner <= _stopLeashDistance)
            {
                _recoveryRequested = false;
                intent = ActorIntent.Hold(CombatUnitState.Idle);
                return true;
            }

            intent = new ActorIntent
            {
                ChangeTarget = actor.CurrentTarget != null,
                Target = null,
                Destination = summonerNode.GlobalPosition,
                SpeedMultiplier = _leashSpeedMultiplier,
                State = _leashState,
            };
            return true;
        }

        if (!_followWhenIdle || actor.CurrentTarget != null)
            return false;

        var idleAnchor = _anchorGetter(actor);
        if (actor.GlobalPosition.DistanceTo(idleAnchor) <= _idleAnchorTolerance)
        {
            intent = ActorIntent.Hold(CombatUnitState.Idle);
            return true;
        }

        intent = ActorIntent.MoveTo(idleAnchor, _followState, 1.0f);
        return true;
    }
}
