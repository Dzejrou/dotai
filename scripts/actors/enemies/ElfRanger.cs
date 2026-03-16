using Godot;

using System;

[GlobalClass]
public partial class ElfRanger : RangedEnemyBase, IAttackable, ITargetable
{
    [Export]
    public float Speed { get; set; } = 62.0f;

    [Export]
    public int Health { get; set; } = 18;

    public bool CanBeTargeted => !IsDead;

    public override void _Ready()
    {
        EnsureProjectileScene("res://scenes/projectiles/projectile.tscn");
        InitializeEnemy(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"),
            "ElfRanger");
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
