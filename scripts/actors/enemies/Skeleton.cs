using Godot;

using System;

[GlobalClass]
public partial class Skeleton : ActorBase, IAttackable, ITargetable, ISummonedUnit, IOffensiveSummon, IFactionAssignable
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
    public int Health { get; set; } = 24;

    [Export]
    public int SummonedHealth { get; set; } = 20;

    [Export]
    public int MinAttackDamage { get; set; } = 1;

    [Export]
    public int MaxAttackDamage { get; set; } = 5;

    [Export]
    public NodePath InitialTargetPath { get; set; } = new NodePath("../Player");

    [Export]
    public float AggroAcquisitionRange { get; set; } = 150.0f;

    [Export]
    public float AggroLossRange { get; set; } = 220.0f;

    [Export]
    public bool EvadeOnAggroLoss { get; set; } = true;

    [Export]
    public bool IgnoreDamageWhileEvading { get; set; } = true;

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

    [Export]
    public float RecoveryTeleportTimeoutSeconds { get; set; } = 2.5f;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => _faction;
    public ISummoner Summoner => _summonRole.Summoner;

    private Faction _faction = Factions.Enemies;
    private readonly SummonRoleState _summonRole = new();
    private bool _sameFactionCollisionExceptionApplied;
    private bool _deathFallbackQueued;
    private FollowSummonerBehavior _followSummonerBehavior;
    private SummonRoleComposer _summonRoleComposer;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        SetPrimaryActionController(new MeleeAttackController(AttackRange, AttackCooldown, AttackAnimation, MinAttackDamage, MaxAttackDamage));

        _summonRoleComposer = new SummonRoleComposer(
            _summonRole,
            ConfigureBehaviors,
            CreateDefaultBehaviors,
            CreateSummonBehaviorPreset,
            OnSummonRoleModeChanged);
        RefreshSummonRoleComposition();

        PlayIdleIfAvailable();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Summoner != null && !HasValidSummoner())
        {
            QueueFree();
            return;
        }

        if (IsDead)
            return;

        base._PhysicsProcess(delta);
    }

    protected override void OnActorExitTree()
    {
        _deathFallbackQueued = false;
        ClearSameFactionCollisionExceptions();
    }

    protected override void OnDeathAnimationFinalized()
    {
        if (!_summonRole.IsSummoned)
            return;

        ClearSameFactionCollisionExceptions();
        QueueFree();
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyIncomingDamage(damageInfo, out var damage, out var died))
            return;

        ShowFloatingDamageNumber(damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        if (died)
            StartDeath();
    }

    public bool IsOwnedBy(Node2D owner)
    {
        return _summonRole.IsOwnedBy(owner);
    }

    public void SetSummoner(ISummoner summoner)
    {
        _summonRole.SetSummoner(summoner, SetFaction);
        if (IsInsideTree())
            RefreshSummonRoleComposition();
    }

    public void SetFaction(Faction faction)
    {
        _faction = faction ?? Factions.Enemies;
        if (!IsInsideTree())
            return;

        ClearSameFactionCollisionExceptions();
        ApplyFactionCombatGroup();
        if (_summonRole.IsSummoned)
            ApplySameFactionCollisionExceptions();
        RefreshHealthLabel();
    }

    public bool HasValidSummoner()
    {
        return _summonRole.HasValidSummoner();
    }

    public void CommandAttackTarget(Node2D target, bool forceRetarget = false)
    {
        if (!_summonRole.IsSummoned || !IsValidCommandedTarget(target))
            return;

        _summonRole.SetCommandedTarget(target);
        _followSummonerBehavior?.CancelRecovery();

        if (forceRetarget || !HasUsableCurrentTarget())
            SetTarget(target);
    }

    private IActorBehavior[] CreateDefaultBehaviors()
    {
        var preset = ActorBehaviorPresets.CreateHostileMeleePreset(
            AggroAcquisitionRange,
            InitialTargetPath,
            "Skeleton",
            AggroLossRange,
            EvadeOnAggroLoss,
            IgnoreDamageWhileEvading);
        return preset.Behaviors;
    }

    private void RefreshSummonRoleComposition()
    {
        _followSummonerBehavior = _summonRoleComposer?.Refresh();
    }

    private SummonBehaviorPreset CreateSummonBehaviorPreset()
    {
        return SummonBehaviorPresets.CreateSummonedMeleePreset(
            actor => GetSummonerNode(),
            actor => GetIdleAnchor(),
            LeashDistance,
            LeashReturnDistance,
            IdleAnchorTolerance,
            LeashCatchupSpeedMultiplier,
            followWhenIdle: true,
            ownerCombatAssistTargetGetter: actor => SummonBehaviorPresets.GetOwnerCombatAssistTarget(actor, _summonRole, IsValidCommandedTarget),
            commandedTargetGetter: actor => GetCommandedTarget(),
            canAttemptAcquisition: actor =>
                actor.CurrentState != CombatUnitState.Leashing &&
                (_followSummonerBehavior == null || !_followSummonerBehavior.IsRecovering) &&
                (_followSummonerBehavior == null || !_followSummonerBehavior.ShouldPrioritizeLeashReturn(actor)),
            additionalTargetFilter: (actor, target) => CanAcquireTargetAsSummon(target),
            shouldDropTarget: (actor, target) => _followSummonerBehavior != null && _followSummonerBehavior.ShouldPrioritizeLeashReturn(actor),
            teleportDestinationGetter: actor => GetIdleAnchor(),
            teleportRecoveryTimeout: RecoveryTeleportTimeoutSeconds);
    }

    private void OnSummonRoleModeChanged(bool isSummoned)
    {
        ClearSameFactionCollisionExceptions();
        ApplyFactionCombatGroup();
        if (isSummoned)
            ApplySameFactionCollisionExceptions();
    }

    private void StartDeath()
    {
        SetIsDead(true);
        MarkDead();
        Velocity = Vector2.Zero;
        _summonRole.ClearCommandedTarget();
        _followSummonerBehavior?.CancelRecovery();
        ClearTarget();
        ResetPrimaryActionController();

        if (_summonRole.IsSummoned)
        {
            ClearSameFactionCollisionExceptions();
            if (NavigationAgent != null)
                NavigationAgent.SetPhysicsProcess(false);
            TryPlayDeathAnimation(queueFreeOnMissingAnimation: true);
            ScheduleDeathCleanupFallback();
            return;
        }

        TryPlayDeathAnimation();
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

    private bool CanAcquireTargetAsSummon(Node2D target)
    {
        if (target == null)
            return false;

        var summonerNode = GetSummonerNode();
        if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode) || !summonerNode.IsInsideTree())
            return true;

        return summonerNode.GlobalPosition.DistanceTo(target.GlobalPosition) <= Math.Max(LeashDistance, 0.0f);
    }

    private Node2D GetCommandedTarget()
    {
        return _summonRole.GetCommandedTarget(IsValidCommandedTarget);
    }

    private bool IsValidCommandedTarget(Node2D target)
    {
        if (!IsStructurallyValidTarget(target))
            return false;

        if (target is not IAttackable || target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        return CanAcquireTargetAsSummon(target);
    }

    private Vector2 GetIdleAnchor()
    {
        return SummonBehaviorPresets.GetFormationAnchor(
            this,
            _summonRole,
            FormationHorizontalOffset,
            FormationVerticalOffset,
            MaxFormationSlots);
    }

    private Node2D GetSummonerNode()
    {
        return _summonRole.SummonerNode;
    }

    private void ApplySameFactionCollisionExceptions()
    {
        if (_sameFactionCollisionExceptionApplied)
            return;

        if (!IsInsideTree() || GetTree() == null || this is not PhysicsBody2D skeletonPhysicsBody)
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
            {
                continue;
            }

            skeletonPhysicsBody.AddCollisionExceptionWith(allyPhysicsBody);
            allyPhysicsBody.AddCollisionExceptionWith(skeletonPhysicsBody);
        }

        _sameFactionCollisionExceptionApplied = true;
    }

    private void ClearSameFactionCollisionExceptions()
    {
        if (this is not PhysicsBody2D skeletonPhysicsBody)
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
            {
                continue;
            }

            skeletonPhysicsBody.RemoveCollisionExceptionWith(allyPhysicsBody);
            allyPhysicsBody.RemoveCollisionExceptionWith(skeletonPhysicsBody);
        }

        _sameFactionCollisionExceptionApplied = false;
    }

    protected override int MaxHealthValue => _summonRole.IsSummoned ? SummonedHealth : Health;
}
