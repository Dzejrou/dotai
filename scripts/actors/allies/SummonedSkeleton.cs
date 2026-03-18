using Godot;

using System;

[GlobalClass]
public partial class SummonedSkeleton : ActorBase, IAttackable, ITargetable, ISummonedUnit, IFactionMember, IOffensiveSummon
{
    private const float DeathFallbackDelay = 2.0f;
    private const int MaxFormationSlots = 4;

    [Export]
    public float Speed { get; set; } = 52.0f;

    [Export]
    public float AttackRange { get; set; } = 18.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.1f;

    [Export]
    public StringName AttackAnimation { get; set; } = "cross-punch";

    [Export]
    public int Health { get; set; } = 20;

    [Export]
    public int MinAttackDamage { get; set; } = 1;

    [Export]
    public int MaxAttackDamage { get; set; } = 5;

    [Export]
    public NodePath OwnerPath { get; set; } = new NodePath("../Player");

    [Export]
    public float LeashDistance { get; set; } = 220.0f;

    [Export]
    public float LeashReturnDistance { get; set; } = 72.0f;

    [Export]
    public float LeashCatchupSpeedMultiplier { get; set; } = 1.35f;

    [Export]
    public float IdleAnchorTolerance { get; set; } = 10.0f;

    [Export]
    public float FormationHorizontalOffset { get; set; } = 24.0f;

    [Export]
    public float FormationVerticalOffset { get; set; } = 42.0f;

    private Faction _faction = Factions.Allies;
    private ISummoner _summoner;
    private Node2D _summonerNode;
    private bool _summonerCollisionExceptionApplied;
    private bool _deathFallbackQueued;
    private Node2D _commandedTarget;
    private FollowSummonerBehavior _followSummonerBehavior;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => _faction;
    public ISummoner Summoner => _summoner;

    public override void _Ready()
    {
        InitializeActor(
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        ApplyFactionGroup();
        RefreshSummonerReference();
        ApplyAllyCollisionExceptions();
        SetPrimaryActionController(new MeleeAttackController(AttackRange, AttackCooldown, AttackAnimation, MinAttackDamage, MaxAttackDamage));

        _followSummonerBehavior = new FollowSummonerBehavior(
            actor => GetSummonerNode(),
            actor => GetIdleAnchor(),
            LeashDistance,
            LeashReturnDistance,
            IdleAnchorTolerance,
            LeashCatchupSpeedMultiplier,
            followWhenIdle: true);

        ConfigureBehaviors(
            new CommandedTargetBehavior(actor => GetCommandedTarget()),
            new AcquireHostileTargetBehavior(
                float.MaxValue,
                canAttemptAcquisition: actor =>
                    !_followSummonerBehavior.IsRecovering &&
                    actor.CurrentState != CombatUnitState.Leashing &&
                    !_followSummonerBehavior.ShouldPrioritizeLeashReturn(actor),
                additionalTargetFilter: (actor, target) => CanAcquireTarget(target)),
            new PursuitStuckRecoveryBehavior(
                1.0f,
                0.6f,
                8.0f,
                actor =>
                    actor.CurrentState == CombatUnitState.PursuingTarget ||
                    actor.CurrentState == CombatUnitState.FollowingOwner ||
                    actor.CurrentState == CombatUnitState.Leashing,
                actor =>
                {
                    actor.ClearTarget();
                    _followSummonerBehavior.BeginRecovery();
                }),
            new TargetCombatBehavior((actor, target) => _followSummonerBehavior.ShouldPrioritizeLeashReturn(actor)),
            _followSummonerBehavior);
        PlayIdleIfAvailable();
    }

    protected override void OnActorExitTree()
    {
        _deathFallbackQueued = false;
        ClearAllyCollisionExceptions();
    }

    protected override void OnDeathAnimationFinalized()
    {
        ClearAllyCollisionExceptions();
        QueueFree();
    }

    public bool IsOwnedBy(Node2D owner)
    {
        return owner != null && _summonerNode == owner;
    }

    public void SetSummoner(ISummoner summoner)
    {
        var summonerNode = summoner?.SummonerNode;
        if (_summonerNode == summonerNode)
        {
            _summoner = summoner;
            if (summoner is IFactionMember factionMember)
                SetFaction(factionMember.Faction);
            if (IsInsideTree())
                ApplyAllyCollisionExceptions();
            return;
        }

        _summoner = summoner;
        _summonerNode = summonerNode;
        if (summoner is IFactionMember inheritedFactionMember)
            SetFaction(inheritedFactionMember.Faction);
        _summonerCollisionExceptionApplied = false;
        if (IsInsideTree())
            ApplyAllyCollisionExceptions();
    }

    public void SetFaction(Faction faction)
    {
        _faction = faction ?? Factions.Allies;
        if (!IsInsideTree())
            return;

        ClearAllyCollisionExceptions();
        ApplyFactionGroup();
        ApplyAllyCollisionExceptions();
        RefreshHealthLabel();
    }

    public bool HasValidSummoner()
    {
        return _summoner != null &&
               GodotObject.IsInstanceValid(_summoner.SummonerNode) &&
               _summoner.IsSummonerActive;
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyIncomingDamage(damageInfo, out var damage, out var died))
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        if (died)
            StartDeath();
    }

    public void CommandAttackTarget(Node2D target, bool forceRetarget = false)
    {
        if (!IsValidCommandedTarget(target))
            return;

        _commandedTarget = target;
        _followSummonerBehavior.CancelRecovery();

        if (forceRetarget || !HasUsableCurrentTarget())
            SetTarget(target);
    }

    private void StartDeath()
    {
        SetIsDead(true);
        MarkDead();
        Velocity = Vector2.Zero;
        _commandedTarget = null;
        _followSummonerBehavior?.CancelRecovery();
        ClearTarget();
        ResetPrimaryActionController();
        ClearAllyCollisionExceptions();
        if (NavigationAgent != null)
            NavigationAgent.SetPhysicsProcess(false);
        TryPlayDeathAnimation(queueFreeOnMissingAnimation: true);
        ScheduleDeathCleanupFallback();
    }

    private void ScheduleDeathCleanupFallback()
    {
        if (_deathFallbackQueued || GetTree() == null || !IsInsideTree())
            return;

        _deathFallbackQueued = true;
        var timer = GetTree().CreateTimer(DeathFallbackDelay);
        timer.Timeout += OnDeathCleanupTimeout;
    }

    private void OnDeathCleanupTimeout()
    {
        if (!IsInstanceValid(this) || IsQueuedForDeletion())
            return;

        QueueFree();
    }

    private bool HasUsableCurrentTarget()
    {
        return CurrentTarget != null &&
               IsStructurallyValidTarget(CurrentTarget) &&
               CurrentTarget is ITargetable targetable &&
               targetable.CanBeTargeted;
    }

    private bool CanAcquireTarget(Node2D target)
    {
        if (target == null)
            return false;

        if (_summonerNode == null || !GodotObject.IsInstanceValid(_summonerNode) || !_summonerNode.IsInsideTree())
            return true;

        return _summonerNode.GlobalPosition.DistanceTo(target.GlobalPosition) <= Math.Max(LeashDistance, 0.0f);
    }

    private Node2D GetCommandedTarget()
    {
        if (!IsValidCommandedTarget(_commandedTarget))
        {
            _commandedTarget = null;
            return null;
        }

        return _commandedTarget;
    }

    private bool IsValidCommandedTarget(Node2D target)
    {
        if (!IsStructurallyValidTarget(target))
            return false;

        if (target is not IAttackable || target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        return CanAcquireTarget(target);
    }

    private Vector2 GetIdleAnchor()
    {
        if (_summonerNode == null || !GodotObject.IsInstanceValid(_summonerNode))
            return GlobalPosition;

        return _summonerNode.GlobalPosition + GetSummonSpreadOffset();
    }

    private Vector2 GetSummonSpreadOffset()
    {
        var summonSlot = GetSummonSlotIndex();
        if (summonSlot < 0)
            return Vector2.Zero;

        summonSlot = Math.Min(summonSlot, MaxFormationSlots - 1);
        return GetSlotOffsetForIndex(summonSlot);
    }

    private Vector2 GetSlotOffsetForIndex(int slotIndex)
    {
        if (slotIndex < 0)
            return Vector2.Zero;

        return slotIndex switch
        {
            0 => new Vector2(-FormationHorizontalOffset, -FormationVerticalOffset),
            1 => new Vector2(FormationHorizontalOffset, -FormationVerticalOffset),
            2 => new Vector2(-FormationHorizontalOffset, FormationVerticalOffset),
            3 => new Vector2(FormationHorizontalOffset, FormationVerticalOffset),
            _ => Vector2.Zero,
        };
    }

    private int GetSummonSlotIndex()
    {
        if (_summonerNode == null || !GodotObject.IsInstanceValid(_summonerNode))
            return 0;

        var parent = GetParent();
        if (parent == null)
            return 0;

        var slot = 0;
        foreach (var node in parent.GetChildren())
        {
            if (node is not SummonedSkeleton summon)
                continue;

            var summonOwner = summon.GetSummonerNode();
            if (!GodotObject.IsInstanceValid(summonOwner))
                summon.RefreshSummonerReference();

            summonOwner = summon.GetSummonerNode();
            if (!GodotObject.IsInstanceValid(summonOwner) || summonOwner != _summonerNode)
                continue;

            if (summon == this)
                return slot;

            slot++;
            if (slot >= MaxFormationSlots)
                return MaxFormationSlots - 1;
        }

        return 0;
    }

    private void RefreshSummonerReference()
    {
        if (HasValidSummoner())
        {
            _summonerNode = _summoner.SummonerNode;
            return;
        }

        _summoner = ResolveSummoner();
        _summonerNode = _summoner?.SummonerNode;
    }

    private Node2D GetSummonerNode()
    {
        return _summonerNode;
    }

    private ISummoner ResolveSummoner()
    {
        if (!OwnerPath.IsEmpty && HasNode(OwnerPath))
            return GetNodeOrNull<Node>(OwnerPath) as ISummoner;

        return null;
    }

    private void ApplyAllyCollisionExceptions()
    {
        if (_summonerCollisionExceptionApplied)
            return;

        if (!IsInsideTree() || GetTree() == null)
            return;

        if (this is not PhysicsBody2D summonPhysicsBody)
            return;

        var ownGroup = Factions.GetCombatGroup(Faction);
        if (string.IsNullOrEmpty(ownGroup))
            return;

        foreach (var node in GetTree().GetNodesInGroup(ownGroup))
        {
            if (node == this ||
                node is not PhysicsBody2D allyPhysicsBody ||
                !GodotObject.IsInstanceValid(allyPhysicsBody) ||
                !allyPhysicsBody.IsInsideTree())
                continue;

            summonPhysicsBody.AddCollisionExceptionWith(allyPhysicsBody);
            allyPhysicsBody.AddCollisionExceptionWith(summonPhysicsBody);
        }

        _summonerCollisionExceptionApplied = true;
    }

    private void ClearAllyCollisionExceptions()
    {
        if (this is not PhysicsBody2D summonPhysicsBody)
            return;

        var tree = GetTree();
        if (tree == null)
            return;

        var ownGroup = Factions.GetCombatGroup(Faction);
        if (string.IsNullOrEmpty(ownGroup))
            return;

        foreach (var node in tree.GetNodesInGroup(ownGroup))
        {
            if (node == this ||
                node is not PhysicsBody2D allyPhysicsBody ||
                !GodotObject.IsInstanceValid(allyPhysicsBody) ||
                !allyPhysicsBody.IsInsideTree())
                continue;

            summonPhysicsBody.RemoveCollisionExceptionWith(allyPhysicsBody);
            allyPhysicsBody.RemoveCollisionExceptionWith(summonPhysicsBody);
        }

        _summonerCollisionExceptionApplied = false;
    }

    private void ApplyFactionGroup()
    {
        ApplyFactionCombatGroup();
    }

    protected override int MaxHealthValue => Health;
}
