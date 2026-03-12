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
    public float FollowDistance { get; set; } = 56.0f;

    [Export]
    public float FollowStopDistance { get; set; } = 36.0f;

    [Export]
    public float LeashDistance { get; set; } = 220.0f;

    [Export]
    public float LeashReturnDistance { get; set; } = 72.0f;

    [Export]
    public float LeashCatchupSpeedMultiplier { get; set; } = 1.35f;

    private readonly RandomNumberGenerator _randomNumberGenerator = new();
    private Node2D _owner;
    private float _attackCooldownTimer;
    private int _currentHealth;
    private bool _isDead;

    public bool CanBeTargeted => !_isDead;

    public override void _Ready()
    {
        _currentHealth = Math.Max(1, Health);
        InitializeCombatUnit(
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"));
        SetMovementSpeed(Speed);
        AddToGroup(CombatGroups.Allies);
        _owner = ResolveOwner();
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

        TryFinalizeDeathAnimation(DeathAnimation);
    }

    private void StartDeath()
    {
        _isDead = true;
        MarkDead();
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;
        TryPlayDeathAnimation(DeathAnimation, DisableCollisionOnDeath);
    }

    protected override bool HandleNoTarget(double delta)
    {
        if (_owner == null || !GodotObject.IsInstanceValid(_owner) || !_owner.IsInsideTree())
            _owner = ResolveOwner();

        if (_owner == null)
            return false;

        var toOwner = _owner.GlobalPosition - GlobalPosition;
        var distance = toOwner.Length();
        var startFollowDistance = Math.Max(FollowDistance, 0.0f);
        var stopFollowDistance = Math.Clamp(FollowStopDistance, 0.0f, startFollowDistance);
        var startLeashDistance = Math.Max(LeashDistance, startFollowDistance);
        var stopLeashDistance = Math.Clamp(LeashReturnDistance, 0.0f, startLeashDistance);

        if (CurrentState != CombatUnitState.Leashing && distance > startLeashDistance)
            SetCombatState(CombatUnitState.Leashing);

        if (CurrentState == CombatUnitState.Leashing && distance <= stopLeashDistance)
            SetCombatState(CombatUnitState.Idle);

        if (CurrentState == CombatUnitState.Leashing)
        {
            SetCombatState(CombatUnitState.Leashing);
            return MoveTowardOwner(toOwner, LeashCatchupSpeedMultiplier);
        }

        if (CurrentState != CombatUnitState.FollowingOwner)
        {
            if (distance <= startFollowDistance)
                return false;

            SetCombatState(CombatUnitState.FollowingOwner);
        }

        if (CurrentState == CombatUnitState.FollowingOwner && distance <= stopFollowDistance)
        {
            SetCombatState(CombatUnitState.Idle);
            return false;
        }

        SetCombatState(CombatUnitState.FollowingOwner);
        return MoveTowardOwner(toOwner, 1.0f);
    }

    private bool MoveTowardOwner(Vector2 toOwner, float speedMultiplier)
    {
        if (toOwner == Vector2.Zero)
            return false;

        LastDirection = DirectionHelper.GetDirectionName(toOwner);
        var walkAnimation = $"walk_{LastDirection}";
        if (AnimatedSprite?.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(walkAnimation) &&
            (!AnimatedSprite.IsPlaying() || AnimatedSprite.Animation != walkAnimation))
        {
            AnimatedSprite.Play(walkAnimation);
        }

        var movementMultiplier = Math.Max(0.0f, speedMultiplier);
        Velocity = toOwner.Normalized() * MovementSpeed * movementMultiplier;
        return true;
    }

    private bool ShouldPrioritizeLeashReturn()
    {
        if (_owner == null || !GodotObject.IsInstanceValid(_owner) || !_owner.IsInsideTree())
            _owner = ResolveOwner();

        if (_owner == null)
            return false;

        return GlobalPosition.DistanceTo(_owner.GlobalPosition) > Math.Max(LeashDistance, 0.0f);
    }

    private Node2D ResolveOwner()
    {
        if (!OwnerPath.IsEmpty && HasNode(OwnerPath))
            return GetNodeOrNull<Node2D>(OwnerPath);

        return GetParent()?.GetNodeOrNull<Node2D>("Player");
    }
}
