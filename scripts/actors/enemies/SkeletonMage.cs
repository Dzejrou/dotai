using Godot;

using System;

[GlobalClass]
public partial class SkeletonMage : EnemyBase, IAttackable, ITargetable
{
    [Export]
    public float Speed { get; set; } = 58.0f;

    [Export]
    public float AttackRange { get; set; } = 150.0f;

    [Export]
    public float MinimumRange { get; set; } = 70.0f;

    [Export]
    public float PreferredRange { get; set; } = 120.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.2f;

    [Export]
    public StringName AttackAnimation { get; set; } = "fireball";

    [Export]
    public int Health { get; set; } = 22;

    [Export]
    public PackedScene ProjectileScene { get; set; }

    [Export]
    public float ProjectileSpeed { get; set; } = 280.0f;

    [Export]
    public int ProjectileDamage { get; set; } = 4;

    [Export]
    public float ProjectileLifetime { get; set; } = 2.5f;

    [Export]
    public float ProjectileMaxTravelDistance { get; set; } = 320.0f;

    public bool CanBeTargeted => !IsDead;

    public override void _Ready()
    {
        if (ProjectileScene == null)
            ProjectileScene = GD.Load<PackedScene>("res://scenes/projectiles/projectile.tscn");
        InitializeEnemy(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"),
            "SkeletonMage");
        SetMovementSpeed(Speed);

        PlayIdleIfAvailable();

        AnimatedSprite.AnimationFinished += OnAnimationFinished;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
            return;

        base._PhysicsProcess(delta);
    }

    protected override bool CanAttackNow(Vector2 toTarget, double delta)
    {
        return CanUseRangedAttack(toTarget, delta, AttackCooldown, MinimumRange, PreferredRange);
    }

    protected override Vector2 GetDesiredMovementTarget(Vector2 targetPosition, double delta)
    {
        var toTarget = targetPosition - GlobalPosition;
        var distance = toTarget.Length();
        if (toTarget == Vector2.Zero || (distance >= MinimumRange && distance <= PreferredRange))
            return GlobalPosition;

        if (distance > PreferredRange)
            return targetPosition;

        var retreatDirection = toTarget == Vector2.Zero ? Vector2.Zero : -toTarget.Normalized();
        return GlobalPosition + retreatDirection * Math.Max(PreferredRange, 0.0f);
    }

    protected override void StartAttack()
    {
        if (IsDead)
            return;

        TryStartRangedProjectileAttack(
            ProjectileScene,
            AttackAnimation,
            AttackCooldown,
            ProjectileDamage,
            ProjectileSpeed,
            ProjectileLifetime,
            ProjectileMaxTravelDistance,
            CombatGroups.Allies);
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyEnemyDamage(damageInfo, out var damage, out var died))
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        if (died)
            StartDeath();
    }

    private void OnAnimationFinished()
    {
        if (AnimatedSprite.Animation.ToString().StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
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
        ResetRangedAttackCooldown();
        TryPlayDeathAnimation();
    }

    protected override int MaxHealthValue => Health;
}
