using Godot;

using System;

[GlobalClass]
public partial class SummonedSkeleton : CombatUnitBase, IAttackable, ITargetable, ISummonedUnit
{
    private const float StuckProgressThreshold = 1.0f;
    private const float StuckTimeoutSeconds = 0.6f;
    private const float StuckWaypointDistance = 8.0f;

    [Export]
    public float Speed { get; set; } = 52.0f;

    [Export]
    public float AttackRange { get; set; } = 18.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.1f;

    [Export]
    public StringName AttackAnimation { get; set; } = "cross-punch";

    [Export]
    public StringName DeathAnimation { get; set; } = "falling-back-death";

    [Export]
    public int Health { get; set; } = 20;

    [Export]
    public int MinAttackDamage { get; set; } = 1;

    [Export]
    public int MaxAttackDamage { get; set; } = 5;

    [Export]
    public bool DisableCollisionOnDeath { get; set; } = true;

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

    private readonly RandomNumberGenerator _randomNumberGenerator = new();
    private ISummoner _summoner;
    private Node2D _summonerNode;
    private float _attackCooldownTimer;
    private int _currentHealth;
    private bool _isDead;
    private bool _summonerCollisionExceptionApplied;
    private bool _deathFallbackQueued;
    private bool _hasStuckProgressPosition;
    private Vector2 _lastStuckProgressPosition;
    private float _stuckTimer;
    private bool _returningToSummonerAfterStuck;
    private const float DeathFallbackDelay = 2.0f;
    private const int MaxFormationSlots = 4;

    public bool CanBeTargeted => !_isDead;
    public ISummoner Summoner => _summoner;

    public override void _Ready()
    {
        _currentHealth = Math.Max(1, Health);
        InitializeCombatUnit(
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        AddToGroup(CombatGroups.Allies);
        RefreshSummonerReference();
        ApplyAllyCollisionExceptions();
        PlayIdleIfAvailable();

        if (AnimatedSprite != null)
            AnimatedSprite.AnimationFinished += OnAnimationFinished;

        _randomNumberGenerator.Randomize();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        base._PhysicsProcess(delta);
    }

    protected override void PrePhysicsProcess(double delta)
    {
        UpdateStuckRecovery((float)delta);
    }

    public override void _ExitTree()
    {
        _deathFallbackQueued = false;
        ClearAllyCollisionExceptions();
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
            if (IsInsideTree())
                ApplyAllyCollisionExceptions();
            return;
        }

        _summoner = summoner;
        _summonerNode = summonerNode;
        _summonerCollisionExceptionApplied = false;
        if (IsInsideTree())
            ApplyAllyCollisionExceptions();
    }

    public bool HasValidSummoner()
    {
        return _summoner != null &&
               GodotObject.IsInstanceValid(_summoner.SummonerNode) &&
               _summoner.IsSummonerActive;
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (_isDead)
            return;

        var damage = Math.Max(1, damageInfo.Amount);
        _currentHealth = Math.Max(0, _currentHealth - damage);
        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        GD.Print($"SummonedSkeleton health: {_currentHealth}/{Health} (took {damage})");

        if (_currentHealth <= 0)
            StartDeath();
    }

    private void UpdateStuckRecovery(float delta)
    {
        if (!ShouldCheckForStuckRecovery())
        {
            ResetStuckRecoveryTracking();
            return;
        }

        if (!_hasStuckProgressPosition)
        {
            _hasStuckProgressPosition = true;
            _lastStuckProgressPosition = GlobalPosition;
            _stuckTimer = 0.0f;
            return;
        }

        if (GlobalPosition.DistanceTo(_lastStuckProgressPosition) > StuckProgressThreshold)
        {
            _lastStuckProgressPosition = GlobalPosition;
            _stuckTimer = 0.0f;
            return;
        }

        _stuckTimer += Math.Max(0.0f, delta);
        if (_stuckTimer < StuckTimeoutSeconds)
            return;

        ClearTarget();
        _returningToSummonerAfterStuck = true;
        SetCombatState(CombatUnitState.Leashing);
        ResetStuckRecoveryTracking();
    }

    private bool ShouldCheckForStuckRecovery()
    {
        if (_isDead || Velocity == Vector2.Zero)
            return false;

        if (!IsUsingNavigationPath)
            return false;

        if (GlobalPosition.DistanceTo(LastNavigationPathPosition) <= StuckWaypointDistance)
            return false;

        return CurrentState == CombatUnitState.PursuingTarget ||
               CurrentState == CombatUnitState.FollowingOwner ||
               CurrentState == CombatUnitState.Leashing;
    }

    private void ResetStuckRecoveryTracking()
    {
        _hasStuckProgressPosition = false;
        _lastStuckProgressPosition = Vector2.Zero;
        _stuckTimer = 0.0f;
    }

    protected override void AcquireTarget()
    {
        if (_returningToSummonerAfterStuck)
        {
            if (_summonerNode != null &&
                GodotObject.IsInstanceValid(_summonerNode) &&
                _summonerNode.IsInsideTree() &&
                GlobalPosition.DistanceTo(_summonerNode.GlobalPosition) > Math.Max(LeashReturnDistance, 0.0f))
            {
                return;
            }

            _returningToSummonerAfterStuck = false;
        }

        if (CurrentState == CombatUnitState.Leashing)
            return;

        if (ShouldPrioritizeLeashReturn())
            return;

        var candidate = TargetingHelper.FindClosestTarget(
            this,
            CombatGroups.Enemies,
            node => node is IAttackable &&
                    node is ITargetable targetable &&
                    targetable.CanBeTargeted &&
                    node is Node2D targetNode &&
                    CanAcquireTarget(targetNode));
        if (candidate != null)
            SetTarget(candidate);
    }

    private bool CanAcquireTarget(Node2D target)
    {
        if (target == null)
            return false;

        if (_summonerNode == null || !GodotObject.IsInstanceValid(_summonerNode) || !_summonerNode.IsInsideTree())
            return true;

        return _summonerNode.GlobalPosition.DistanceTo(target.GlobalPosition) <= Math.Max(LeashDistance, 0.0f);
    }

    protected override bool ShouldLoseCurrentTarget(Node2D target)
    {
        if (!ShouldPrioritizeLeashReturn())
            return false;

        SetCombatState(CombatUnitState.Leashing);
        return true;
    }

    protected override bool CanAttackNow(Vector2 toTarget, double delta)
    {
        if (_attackCooldownTimer > 0.0f)
            _attackCooldownTimer -= (float)delta;

        return _attackCooldownTimer <= 0.0f && toTarget.Length() <= AttackRange;
    }

    protected override bool ShouldStayEngaged(Vector2 toTarget, double delta)
    {
        return toTarget.Length() <= AttackRange;
    }

    protected override void StartAttack()
    {
        if (_isDead ||
            CurrentTarget is not IAttackable attackable ||
            CurrentTarget is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            ClearTarget();
            _attackCooldownTimer = 0.0f;
            return;
        }

        SetCombatState(CombatUnitState.Attacking);
        _attackCooldownTimer = AttackCooldown;

        var toTarget = CurrentTarget.GlobalPosition - GlobalPosition;
        if (toTarget != Vector2.Zero)
            LastDirection = DirectionHelper.GetDirectionName(toTarget);

        var attackAnimation = $"{AttackAnimation}_{LastDirection}";
        if (AnimatedSprite?.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(attackAnimation) &&
            AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimation) > 0)
        {
            AnimatedSprite.Play(attackAnimation);
        }
        else
        {
            SetCombatState(CombatUnitState.PursuingTarget);
        }

        var maxDamage = Math.Max(MinAttackDamage, MaxAttackDamage);
        var damage = _randomNumberGenerator.RandiRange(Math.Min(MinAttackDamage, maxDamage), maxDamage);
        attackable.ApplyDamage(new DamageInfo(damage, this));
    }

    private void OnAnimationFinished()
    {
        if (AnimatedSprite == null)
            return;

        var animationName = AnimatedSprite.Animation.ToString();
        if (animationName.StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
        {
            FinishAttackState();
            return;
        }

        if (TryFinalizeDeathAnimation(DeathAnimation))
        {
            ClearAllyCollisionExceptions();
            QueueFree();
        }
    }

    private void StartDeath()
    {
        _isDead = true;
        MarkDead();
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;
        _returningToSummonerAfterStuck = false;
        ResetStuckRecoveryTracking();
        ClearAllyCollisionExceptions();
        if (NavigationAgent != null)
            NavigationAgent.SetPhysicsProcess(false);
        TryPlayDeathAnimation(DeathAnimation, DisableCollisionOnDeath, queueFreeOnMissingAnimation: true);
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

    protected override bool HandleNoTarget(double delta)
    {
        if (_summonerNode == null || !GodotObject.IsInstanceValid(_summonerNode) || !_summonerNode.IsInsideTree())
        {
            RefreshSummonerReference();
            _summonerCollisionExceptionApplied = false;
            ApplyAllyCollisionExceptions();
        }

        if (_summonerNode == null)
            return false;

        var distance = (_summonerNode.GlobalPosition - GlobalPosition).Length();
        var startLeashDistance = Math.Max(LeashDistance, 0.0f);
        var stopLeashDistance = Math.Clamp(LeashReturnDistance, 0.0f, startLeashDistance);

        if (CurrentState != CombatUnitState.Leashing && distance > startLeashDistance)
            SetCombatState(CombatUnitState.Leashing);

        if (CurrentState == CombatUnitState.Leashing && distance <= stopLeashDistance)
        {
            SetCombatState(CombatUnitState.Idle);
            _returningToSummonerAfterStuck = false;
        }

        if (CurrentState == CombatUnitState.Leashing)
        {
            return TryMoveTowardDestination(_summonerNode.GlobalPosition, LeashCatchupSpeedMultiplier, CombatUnitState.Leashing, delta);
        }

        var idleAnchor = GetIdleAnchor();
        var toAnchor = idleAnchor - GlobalPosition;
        var anchorDistance = toAnchor.Length();

        if (anchorDistance <= Math.Max(0.0f, IdleAnchorTolerance))
        {
            SetCombatState(CombatUnitState.Idle);
            return false;
        }

        return TryMoveTowardDestination(idleAnchor, 1.0f, CombatUnitState.FollowingOwner, delta);
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
        var localSlot = GetSlotOffsetForIndex(summonSlot);
        return localSlot;
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

    private bool ShouldPrioritizeLeashReturn()
    {
        if (_summonerNode == null || !GodotObject.IsInstanceValid(_summonerNode) || !_summonerNode.IsInsideTree())
            RefreshSummonerReference();

        if (_summonerNode == null)
            return false;

        var distanceToSummoner = GlobalPosition.DistanceTo(_summonerNode.GlobalPosition);
        if (CurrentState == CombatUnitState.Leashing)
            return distanceToSummoner > Math.Max(LeashReturnDistance, 0.0f);

        return distanceToSummoner > Math.Max(LeashDistance, 0.0f);
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

        foreach (var node in GetTree().GetNodesInGroup(CombatGroups.Allies))
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

        foreach (var node in tree.GetNodesInGroup(CombatGroups.Allies))
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
}
