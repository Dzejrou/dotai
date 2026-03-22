using Godot;

using System;

[GlobalClass]
public partial class SummonState : Node
{
    [Export]
    public bool OwnerCombatAssistAlliedSummonsOnly { get; set; } = true;

    [Export]
    public float MaxOwnerCombatTargetDistanceFromSummoner { get; set; } = -1.0f;

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

    [Export]
    public float RecoveryProgressThreshold { get; set; } = 1.0f;

    [Export]
    public float RecoveryTimeoutSeconds { get; set; } = 0.6f;

    [Export]
    public float RecoveryWaypointDistance { get; set; } = 8.0f;

    private ISummoner _summoner;
    private bool _recoveryRequested;
    private float _recoveryTimer;
    private bool _hasProgressPosition;
    private Vector2 _lastProgressPosition;
    private float _stuckTimer;
    private Node2D _trackedTarget;

    public ISummoner Summoner => _summoner;
    public Node2D SummonerNode => _summoner?.SummonerNode;
    public bool IsSummoned => _summoner != null;

    private float ResolvedStartLeashDistance => Math.Max(0.0f, StartLeashDistance);
    private float ResolvedStopLeashDistance => Math.Clamp(StopLeashDistance, 0.0f, ResolvedStartLeashDistance);
    private float ResolvedIdleAnchorTolerance => Math.Max(0.0f, IdleAnchorTolerance);
    private float ResolvedLeashSpeedMultiplier => Math.Max(0.0f, LeashSpeedMultiplier);
    private float ResolvedTeleportRecoveryTimeout => Math.Max(0.0f, TeleportRecoveryTimeout);
    private float ResolvedRecoveryProgressThreshold => Math.Max(0.0f, RecoveryProgressThreshold);
    private float ResolvedRecoveryTimeoutSeconds => Math.Max(0.0f, RecoveryTimeoutSeconds);
    private float ResolvedRecoveryWaypointDistance => Math.Max(0.0f, RecoveryWaypointDistance);

    public void SetSummoner(ISummoner summoner, Action<Faction> inheritFaction = null)
    {
        if (!ReferenceEquals(_summoner, summoner))
            ResetRuntimeState();

        _summoner = summoner;
        if (summoner is IFactionMember factionMember)
            inheritFaction?.Invoke(factionMember.Faction);
    }

    public bool HasValidSummoner()
    {
        return _summoner != null &&
               GodotObject.IsInstanceValid(_summoner.SummonerNode) &&
               _summoner.IsSummonerActive;
    }

    public bool IsOwnedBy(Node2D owner)
    {
        return owner != null && SummonerNode == owner;
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (!IsSummoned)
            return false;

        if (!HasValidSummoner())
        {
            intent = ActorIntent.Remove();
            return true;
        }

        var summonerNode = SummonerNode;
        if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode) || !summonerNode.IsInsideTree())
        {
            intent = ActorIntent.Remove();
            return true;
        }

        UpdateRecoveryTracking(actor, delta);

        if (TryCreateRecoveryOrLeashIntent(actor, summonerNode, delta, out intent))
            return true;

        if (actor.CurrentState == CombatUnitState.Attacking)
            return false;

        if (actor.Target != null &&
            TargetCombatBehavior.TryCreateCombatIntentForTarget(actor, actor.Target, out intent))
        {
            return true;
        }

        var ownerCombatTarget = ResolveOwnerCombatTarget(actor, summonerNode);
        if (ownerCombatTarget != null &&
            TargetCombatBehavior.TryCreateCombatIntentForTarget(
                actor,
                ownerCombatTarget,
                out intent,
                changeTarget: true))
        {
            return true;
        }

        if (TryCreateIdleFollowIntent(actor, out intent))
            return true;

        return false;
    }

    public static SummonState ResolveFor(Node node)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
            return null;

        return node.GetNodeOrNull<SummonState>("SummonState");
    }

    public static bool TryAssignToNode(Node node, ISummoner summoner)
    {
        var summonState = ResolveFor(node);
        if (summonState == null)
            return false;

        Action<Faction> inheritFaction = null;
        if (node is IFactionAssignable factionAssignable)
            inheritFaction = factionAssignable.SetFaction;

        summonState.SetSummoner(summoner, inheritFaction);
        return true;
    }

    public static bool IsOwnedByNode(Node node, Node2D owner)
    {
        return ResolveFor(node)?.IsOwnedBy(owner) == true;
    }

    public static bool HasValidSummonerForNode(Node node)
    {
        return ResolveFor(node)?.HasValidSummoner() == true;
    }

    private bool TryCreateRecoveryOrLeashIntent(ActorBase actor, Node2D summonerNode, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        var distanceToSummoner = actor.GlobalPosition.DistanceTo(summonerNode.GlobalPosition);
        if ((actor.Target != null && ShouldPrioritizeLeashReturn(actor, summonerNode)) || _recoveryRequested)
        {
            if (distanceToSummoner <= ResolvedStopLeashDistance)
            {
                _recoveryRequested = false;
                _recoveryTimer = 0.0f;
                return false;
            }

            if (_recoveryRequested && ResolvedTeleportRecoveryTimeout > 0.0f)
            {
                _recoveryTimer += Math.Max(0.0f, (float)delta);
                if (_recoveryTimer >= ResolvedTeleportRecoveryTimeout)
                {
                    _recoveryRequested = false;
                    _recoveryTimer = 0.0f;
                    intent = new ActorIntent
                    {
                        ChangeTarget = actor.Target != null,
                        Target = null,
                        TeleportDestination = GetAnchor(actor),
                        StopMovement = true,
                        State = CombatUnitState.Idle,
                    };
                    return true;
                }
            }

            intent = ActorIntent.ClearTargetAndMoveTo(
                summonerNode.GlobalPosition,
                LeashState,
                ResolvedLeashSpeedMultiplier);
            return true;
        }

        _recoveryTimer = 0.0f;
        return false;
    }

    private bool TryCreateIdleFollowIntent(ActorBase actor, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (!FollowWhenIdle || actor.Target != null)
            return false;

        var idleAnchor = GetAnchor(actor);
        if (actor.GlobalPosition.DistanceTo(idleAnchor) <= ResolvedIdleAnchorTolerance)
            return false;

        var facingDirection = idleAnchor - actor.GlobalPosition;
        intent = new ActorIntent
        {
            FacingDirection = facingDirection != Vector2.Zero ? facingDirection : (Vector2?)null,
            Destination = idleAnchor,
            SpeedMultiplier = 1.0f,
            State = FollowState,
        };
        return true;
    }

    private bool ShouldPrioritizeLeashReturn(ActorBase actor, Node2D summonerNode)
    {
        var distanceToSummoner = actor.GlobalPosition.DistanceTo(summonerNode.GlobalPosition);
        if (_recoveryRequested || actor.CurrentState == LeashState)
            return distanceToSummoner > ResolvedStopLeashDistance;

        return distanceToSummoner > ResolvedStartLeashDistance;
    }

    private void UpdateRecoveryTracking(ActorBase actor, double delta)
    {
        if (!ShouldTrackRecovery(actor))
        {
            ResetProgressTracking();
            return;
        }

        if (!ReferenceEquals(_trackedTarget, actor.Target))
        {
            _trackedTarget = actor.Target;
            _hasProgressPosition = true;
            _lastProgressPosition = actor.GlobalPosition;
            _stuckTimer = 0.0f;
            return;
        }

        if (!_hasProgressPosition)
        {
            _hasProgressPosition = true;
            _lastProgressPosition = actor.GlobalPosition;
            _stuckTimer = 0.0f;
            return;
        }

        if (actor.GlobalPosition.DistanceTo(_lastProgressPosition) > ResolvedRecoveryProgressThreshold)
        {
            _lastProgressPosition = actor.GlobalPosition;
            _stuckTimer = 0.0f;
            return;
        }

        _stuckTimer += Math.Max(0.0f, (float)delta);
        if (_stuckTimer < ResolvedRecoveryTimeoutSeconds)
            return;

        _recoveryRequested = true;
        ResetProgressTracking();
    }

    private bool ShouldTrackRecovery(ActorBase actor)
    {
        return actor.Target != null &&
               actor.IsUsingNavigationPath &&
               actor.Velocity != Vector2.Zero &&
               actor.GlobalPosition.DistanceTo(actor.LastNavigationPathPosition) > ResolvedRecoveryWaypointDistance &&
               (actor.CurrentState == CombatUnitState.PursuingTarget ||
                actor.CurrentState == CombatUnitState.FollowingOwner ||
                actor.CurrentState == CombatUnitState.Leashing);
    }

    private Node2D ResolveOwnerCombatTarget(ActorBase actor, Node2D summonerNode)
    {
        if (OwnerCombatAssistAlliedSummonsOnly && !ReferenceEquals(actor.Faction, Factions.Allies))
            return null;

        var ownerCombat = CombatState.ResolveFor(summonerNode);
        if (ownerCombat == null || !ownerCombat.InCombat)
            return null;

        var ownerCombatTarget = ownerCombat.Target;
        if (!ActorBase.IsStructurallyValidTarget(ownerCombatTarget) ||
            ownerCombatTarget is not IAttackable ||
            ownerCombatTarget is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            return null;
        }

        if (MaxOwnerCombatTargetDistanceFromSummoner < 0.0f)
            return ownerCombatTarget;

        return summonerNode.GlobalPosition.DistanceTo(ownerCombatTarget.GlobalPosition) <= MaxOwnerCombatTargetDistanceFromSummoner
            ? ownerCombatTarget
            : null;
    }

    private Vector2 GetAnchor(ActorBase actor)
    {
        if (!ActorBase.IsStructurallyValidTarget(SummonerNode))
            return actor.GlobalPosition;

        var summonSlot = GetSummonSlotIndex(actor);
        return SummonerNode.GlobalPosition + GetFormationOffset(summonSlot);
    }

    private int GetSummonSlotIndex(ActorBase actor)
    {
        var parent = actor.GetParent();
        if (parent == null)
            return 0;

        var clampedMaxSlots = Math.Max(1, MaxFormationSlots);
        var slot = 0;
        foreach (var node in parent.GetChildren())
        {
            if (node is not ActorBase siblingActor)
                continue;

            var siblingSummonState = ResolveFor(siblingActor);
            if (siblingSummonState == null || !ReferenceEquals(siblingSummonState.Summoner, _summoner))
                continue;

            if (node == actor)
                return Math.Min(slot, clampedMaxSlots - 1);

            slot++;
            if (slot >= clampedMaxSlots)
                return clampedMaxSlots - 1;
        }

        return 0;
    }

    private Vector2 GetFormationOffset(int slot)
    {
        return slot switch
        {
            0 => new Vector2(-FormationHorizontalOffset, -FormationVerticalOffset),
            1 => new Vector2(FormationHorizontalOffset, -FormationVerticalOffset),
            2 => new Vector2(-FormationHorizontalOffset, FormationVerticalOffset),
            3 => new Vector2(FormationHorizontalOffset, FormationVerticalOffset),
            _ => Vector2.Zero,
        };
    }

    private void ResetRuntimeState()
    {
        _recoveryRequested = false;
        _recoveryTimer = 0.0f;
        ResetProgressTracking();
    }

    private void ResetProgressTracking()
    {
        _hasProgressPosition = false;
        _lastProgressPosition = Vector2.Zero;
        _stuckTimer = 0.0f;
        _trackedTarget = null;
    }
}
