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
    public override void _Ready()
    {
        InitializeEnemy(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"),
            "Skeleton");
        SetMovementSpeed(Speed);
        PlayIdleIfAvailable();
        AnimatedSprite.AnimationFinished += OnAnimationFinished;

        _randomNumberGenerator.Randomize();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
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

    protected override bool ShouldStayEngaged(Vector2 toTarget, double delta)
    {
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

        SetCombatState(CombatUnitState.Attacking);
        _attackCooldownTimer = AttackCooldown;

        var attackAnimation = $"{AttackAnimation}_{LastDirection}";
        if (AnimatedSprite.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimation) == 0)
        {
            SetCombatState(CombatUnitState.PursuingTarget);
            attackable.ApplyDamage(new DamageInfo(_randomNumberGenerator.RandiRange(1, 5), this));
            return;
        }

        if (CurrentTarget != null && CurrentTarget.GlobalPosition != Vector2.Zero)
            LastDirection = DirectionHelper.GetDirectionName(CurrentTarget.GlobalPosition - GlobalPosition);

        AnimatedSprite.Play(attackAnimation);

        var damage = _randomNumberGenerator.RandiRange(1, 5);
        attackable.ApplyDamage(new DamageInfo(damage, this));
    }

    public bool CanBeTargeted => !IsDead;

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyEnemyDamage(damageInfo, out var damage, out var died))
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        GD.Print($"Skeleton health: {CurrentHealth}/{Health} (took {damage})");
        if (died)
            StartDeath();
    }

    private void OnAnimationFinished()
    {
        var animationName = AnimatedSprite.Animation.ToString();

        if (animationName.StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
        {
            FinishAttackState();
            return;
        }

        TryFinalizeDeathAnimation();
    }

    private void StartDeath()
    {
        MarkDead();
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;

        TryPlayDeathAnimation();
    }

    protected override int MaxHealthValue => Health;

}
