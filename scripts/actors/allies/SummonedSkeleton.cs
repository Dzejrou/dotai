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

    private readonly RandomNumberGenerator _randomNumberGenerator = new();
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
        var candidate = TargetingHelper.FindClosestTarget(
            this,
            CombatGroups.Enemies,
            node => node is IAttackable && node is ITargetable targetable && targetable.CanBeTargeted);
        if (candidate != null)
            SetTarget(candidate);
    }

    protected override bool CanAttackNow(Vector2 toTarget, double delta)
    {
        if (_attackCooldownTimer > 0.0f)
            _attackCooldownTimer -= (float)delta;

        return _attackCooldownTimer <= 0.0f && toTarget.Length() <= AttackRange;
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

        IsAttacking = true;
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
            IsAttacking = false;
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
            IsAttacking = false;
            return;
        }

        TryFinalizeDeathAnimation(DeathAnimation);
    }

    private void StartDeath()
    {
        _isDead = true;
        IsAttacking = false;
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;
        TryPlayDeathAnimation(DeathAnimation, DisableCollisionOnDeath);
    }
}
