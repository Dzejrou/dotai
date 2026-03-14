using Godot;

using System;

[GlobalClass]
public partial class SummonedSkeleton : CombatUnitBase, IAttackable, ITargetable
{
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
    private Node2D _owner;
    private float _attackCooldownTimer;
    private int _currentHealth;
    private bool _isDead;
    private bool _ownerCollisionExceptionApplied;
    private bool _deathFallbackQueued;
    private const float DeathFallbackDelay = 2.0f;
    private const int MaxFormationSlots = 4;

    public bool CanBeTargeted => !_isDead;

    public override void _Ready()
    {
        _currentHealth = Math.Max(1, Health);
        InitializeCombatUnit(
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        AddToGroup(CombatGroups.Allies);
        _owner = ResolveOwner();
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

    public override void _ExitTree()
    {
        _deathFallbackQueued = false;
        ClearAllyCollisionExceptions();
    }

    public bool IsOwnedBy(Node2D owner)
    {
        return owner != null && _owner == owner;
    }

    public void SetOwner(Node2D owner)
    {
        if (_owner == owner)
        {
            if (IsInsideTree())
                ApplyAllyCollisionExceptions();
            return;
        }

        _owner = owner;
        _ownerCollisionExceptionApplied = false;
        if (IsInsideTree())
            ApplyAllyCollisionExceptions();
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

    protected override void AcquireTarget()
    {
        if (CurrentState == CombatUnitState.Leashing)
            return;

        if (ShouldPrioritizeLeashReturn())
            return;

        var candidate = TargetingHelper.FindClosestTarget(
            this,
            CombatGroups.Enemies,
            node => node is IAttackable && node is ITargetable targetable && targetable.CanBeTargeted);
        if (candidate != null)
            SetTarget(candidate);
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
        if (_owner == null || !GodotObject.IsInstanceValid(_owner) || !_owner.IsInsideTree())
        {
            _owner = ResolveOwner();
            _ownerCollisionExceptionApplied = false;
            ApplyAllyCollisionExceptions();
        }

        if (_owner == null)
            return false;

        var distance = (_owner.GlobalPosition - GlobalPosition).Length();
        var startLeashDistance = Math.Max(LeashDistance, 0.0f);
        var stopLeashDistance = Math.Clamp(LeashReturnDistance, 0.0f, startLeashDistance);

        if (CurrentState != CombatUnitState.Leashing && distance > startLeashDistance)
            SetCombatState(CombatUnitState.Leashing);

        if (CurrentState == CombatUnitState.Leashing && distance <= stopLeashDistance)
            SetCombatState(CombatUnitState.Idle);

        if (CurrentState == CombatUnitState.Leashing)
        {
            return TryMoveTowardDestination(_owner.GlobalPosition, LeashCatchupSpeedMultiplier, CombatUnitState.Leashing, delta);
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
        if (_owner == null || !GodotObject.IsInstanceValid(_owner))
            return GlobalPosition;

        return _owner.GlobalPosition + GetSummonSpreadOffset();
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
        if (_owner == null || !GodotObject.IsInstanceValid(_owner))
            return 0;

        var parent = GetParent();
        if (parent == null)
            return 0;

        var slot = 0;
        foreach (var node in parent.GetChildren())
        {
            if (node is not SummonedSkeleton summon)
                continue;

            var summonOwner = summon._owner;
            if (!GodotObject.IsInstanceValid(summonOwner))
                summonOwner = summon.ResolveOwner();

            if (!GodotObject.IsInstanceValid(summonOwner) || summonOwner != _owner)
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
        if (_owner == null || !GodotObject.IsInstanceValid(_owner) || !_owner.IsInsideTree())
            _owner = ResolveOwner();

        if (_owner == null)
            return false;

        var distanceToOwner = GlobalPosition.DistanceTo(_owner.GlobalPosition);
        if (CurrentState == CombatUnitState.Leashing)
            return distanceToOwner > Math.Max(LeashReturnDistance, 0.0f);

        return distanceToOwner > Math.Max(LeashDistance, 0.0f);
    }

    private Node2D ResolveOwner()
    {
        if (!OwnerPath.IsEmpty && HasNode(OwnerPath))
            return GetNodeOrNull<Node2D>(OwnerPath);

        return GetParent()?.GetNodeOrNull<Node2D>("Player");
    }

    private void ApplyAllyCollisionExceptions()
    {
        if (_ownerCollisionExceptionApplied)
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

        _ownerCollisionExceptionApplied = true;
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

        _ownerCollisionExceptionApplied = false;
    }
}
