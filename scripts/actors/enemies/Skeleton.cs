using Godot;

using System;

[GlobalClass]
public partial class Skeleton : EnemyBase, IAttackable, ITargetable
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
    public int Health { get; set; } = 24;

    private RandomNumberGenerator _randomNumberGenerator = new();
    private float _attackCooldownTimer;
    private int _currentHealth;
    private bool _isDead;

    public override void _Ready()
    {
        _currentHealth = Math.Max(1, Health);
        InitializeEnemy(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            "Skeleton");
        SetMovementSpeed(Speed);
        PlayIdleIfAvailable();
        AnimatedSprite.AnimationFinished += OnAnimationFinished;

        _randomNumberGenerator.Randomize();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        base._PhysicsProcess(delta);
    }

    protected override bool CanAttackNow(Vector2 toTarget, double delta)
    {
        if (_attackCooldownTimer > 0.0f)
        {
            _attackCooldownTimer -= (float)delta;
            return false;
        }

        return toTarget.Length() <= AttackRange;
    }

    protected override void StartAttack()
    {
        if (CurrentTarget == null || !IsInstanceValid(CurrentTarget) || !CurrentTarget.IsInsideTree())
        {
            ClearTarget();
            _attackCooldownTimer = 0.0f;
            return;
        }

        if (CurrentTarget is not IAttackable attackable || CurrentTarget is not ITargetable targetable || !targetable.CanBeTargeted)
        {
            ClearTarget();
            _attackCooldownTimer = 0.0f;
            return;
        }

        IsAttacking = true;
        _attackCooldownTimer = AttackCooldown;

        var attackAnimation = $"{AttackAnimation}_{LastDirection}";
        if (AnimatedSprite.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimation) == 0)
        {
            IsAttacking = false;
            attackable.ApplyDamage(new DamageInfo(_randomNumberGenerator.RandiRange(1, 5), this));
            return;
        }

        if (CurrentTarget != null && CurrentTarget.GlobalPosition != Vector2.Zero)
            LastDirection = DirectionHelper.GetDirectionName(CurrentTarget.GlobalPosition - GlobalPosition);

        AnimatedSprite.Play(attackAnimation);

        var damage = _randomNumberGenerator.RandiRange(1, 5);
        attackable.ApplyDamage(new DamageInfo(damage, this));
    }

    public bool CanBeTargeted => !_isDead;

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (_isDead)
            return;

        if (!TryReactToDamageSource(damageInfo))
            return;

        var damage = Math.Max(1, damageInfo.Amount);
        _currentHealth = Math.Max(0, _currentHealth - damage);
        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        GD.Print($"Skeleton health: {_currentHealth}/{Health} (took {damage})");
        if (_currentHealth <= 0)
        {
            _isDead = true;
            StartDeath();
        }
    }

    private void OnAnimationFinished()
    {
        var animationName = AnimatedSprite.Animation.ToString();

        if (animationName.StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
        {
            IsAttacking = false;
            return;
        }

        TryFinalizeDeathAnimation();
    }

    private void StartDeath()
    {
        IsAttacking = false;
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;

        TryPlayDeathAnimation();
    }

}
