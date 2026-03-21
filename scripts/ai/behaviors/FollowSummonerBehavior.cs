using Godot;

using System;

[GlobalClass]
public partial class FollowSummonerBehavior : Node, IActorBehavior
{
    [Export]
    public float StartLeashDistance { get; set; } = 220.0f;

    [Export]
    public float StopLeashDistance { get; set; } = 72.0f;

    [Export]
    public float IdleAnchorTolerance { get; set; } = 10.0f;

    [Export]
    public float LeashSpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public bool FollowWhenIdle { get; set; } = true;

    [Export]
    public CombatUnitState LeashState { get; set; } = CombatUnitState.Leashing;

    [Export]
    public CombatUnitState FollowState { get; set; } = CombatUnitState.FollowingOwner;

    [Export]
    public float TeleportRecoveryTimeout { get; set; } = 0.0f;

    [Export]
    public float FormationHorizontalOffset { get; set; } = 24.0f;

    [Export]
    public float FormationVerticalOffset { get; set; } = 24.0f;

    [Export]
    public int MaxFormationSlots { get; set; } = 4;

    private bool _recoveryRequested;
    private float _recoveryTimer;

    public bool IsRecovering => _recoveryRequested;

    private float ResolvedStartLeashDistance => Math.Max(0.0f, StartLeashDistance);
    private float ResolvedStopLeashDistance => Math.Clamp(StopLeashDistance, 0.0f, ResolvedStartLeashDistance);
    private float ResolvedIdleAnchorTolerance => Math.Max(0.0f, IdleAnchorTolerance);
    private float ResolvedLeashSpeedMultiplier => Math.Max(0.0f, LeashSpeedMultiplier);
    private float ResolvedTeleportRecoveryTimeout => Math.Max(0.0f, TeleportRecoveryTimeout);

    public void BeginRecovery()
    {
        _recoveryRequested = true;
        _recoveryTimer = 0.0f;
    }

    public void CancelRecovery()
    {
        _recoveryRequested = false;
        _recoveryTimer = 0.0f;
    }

    public bool ShouldPrioritizeLeashReturn(ActorBase actor)
    {
        var summonerNode = SummonState.ResolveFor(actor)?.SummonerNode;
        if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode) || !summonerNode.IsInsideTree())
            return false;

        var distanceToSummoner = actor.GlobalPosition.DistanceTo(summonerNode.GlobalPosition);
        if (_recoveryRequested || actor.CurrentState == LeashState)
            return distanceToSummoner > ResolvedStopLeashDistance;

        return distanceToSummoner > ResolvedStartLeashDistance;
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        var summonerNode = SummonState.ResolveFor(actor)?.SummonerNode;
        if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode) || !summonerNode.IsInsideTree())
            return false;

        var distanceToSummoner = actor.GlobalPosition.DistanceTo(summonerNode.GlobalPosition);
        if ((actor.Target != null && ShouldPrioritizeLeashReturn(actor)) || _recoveryRequested)
        {
            if (distanceToSummoner <= ResolvedStopLeashDistance)
            {
                _recoveryRequested = false;
                _recoveryTimer = 0.0f;
                intent = ActorIntent.Hold(CombatUnitState.Idle);
                return true;
            }

            if (_recoveryRequested &&
                ResolvedTeleportRecoveryTimeout > 0.0f &&
                !actor.IsDead)
            {
                _recoveryTimer += Math.Max(0.0f, (float)delta);
                if (_recoveryTimer >= ResolvedTeleportRecoveryTimeout)
                {
                    actor.TeleportTo(GetAnchor(actor));
                    _recoveryRequested = false;
                    _recoveryTimer = 0.0f;
                    intent = ActorIntent.Hold(CombatUnitState.Idle);
                    return true;
                }
            }

            intent = new ActorIntent
            {
                ChangeTarget = actor.Target != null,
                Target = null,
                Destination = summonerNode.GlobalPosition,
                SpeedMultiplier = ResolvedLeashSpeedMultiplier,
                State = LeashState,
            };
            return true;
        }

        _recoveryTimer = 0.0f;

        if (!FollowWhenIdle || actor.Target != null)
            return false;

        var idleAnchor = GetAnchor(actor);
        if (actor.GlobalPosition.DistanceTo(idleAnchor) <= ResolvedIdleAnchorTolerance)
        {
            intent = ActorIntent.Hold(CombatUnitState.Idle);
            return true;
        }

        intent = ActorIntent.MoveTo(idleAnchor, FollowState, 1.0f);
        return true;
    }

    private Vector2 GetAnchor(ActorBase actor)
    {
        return SummonBehaviorPresets.GetFormationAnchor(
            actor,
            FormationHorizontalOffset,
            FormationVerticalOffset,
            MaxFormationSlots);
    }
}
