using Godot;

using System;

public abstract partial class RangedEnemyBase : ActorBase, IAggressiveRangedActorAIHost
{
    private float _rangedAttackCooldownTimer;
    private bool _hasPendingProjectileShot;
    private Vector2 _pendingProjectileDirection;

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
    public PackedScene ProjectileScene { get; set; }

    [Export]
    public float ProjectileSpeed { get; set; } = 280.0f;

    [Export]
    public int ProjectileDamage { get; set; } = 4;

    [Export]
    public float ProjectileLifetime { get; set; } = 2.5f;

    [Export]
    public float ProjectileMaxTravelDistance { get; set; } = 320.0f;

    [Export]
    public string ProjectileTargetGroup { get; set; } = CombatGroups.Allies;

    protected override bool ShouldUseAggressiveCombatSupport() => true;

    protected override bool CanAttackNow(Vector2 toTarget, double delta)
    {
        if (_rangedAttackCooldownTimer > 0.0f)
        {
            _rangedAttackCooldownTimer -= (float)delta;
            return false;
        }

        var resolvedMinimumRange = Math.Max(0.0f, MinimumRange);
        var resolvedMaximumRange = Math.Max(resolvedMinimumRange, PreferredRange);
        var distance = toTarget.Length();
        return distance >= resolvedMinimumRange && distance <= resolvedMaximumRange;
    }

    protected override void StartAttack()
    {
        if (IsDead || ProjectileScene == null)
            return;

        ClearPendingProjectileShot();

        if (CurrentTarget == null || !IsInstanceValid(CurrentTarget) || !CurrentTarget.IsInsideTree())
        {
            ClearTarget();
            ResetRangedAttackCooldown();
            return;
        }

        if (CurrentTarget is not ITargetable targetable || !targetable.CanBeTargeted)
        {
            ClearTarget();
            ResetRangedAttackCooldown();
            return;
        }

        var toTarget = CurrentTarget.GlobalPosition - GlobalPosition;
        if (toTarget != Vector2.Zero)
            LastDirection = DirectionHelper.GetDirectionName(toTarget);

        SetCombatState(CombatUnitState.Attacking);
        _rangedAttackCooldownTimer = Math.Max(0.0f, AttackCooldown);

        var projectileDirection = toTarget != Vector2.Zero ? toTarget.Normalized() : DirectionHelper.GetDirectionVector(LastDirection);
        var attackAnimationName = $"{AttackAnimation}_{LastDirection}";
        if (AnimatedSprite?.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(attackAnimationName) &&
            AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimationName) > 0)
        {
            _hasPendingProjectileShot = true;
            _pendingProjectileDirection = projectileDirection;
            AnimatedSprite.Play(attackAnimationName);
        }
        else
        {
            SetCombatState(CombatUnitState.PursuingTarget);
            LaunchRangedProjectile(projectileDirection);
        }
    }

    protected void ResetRangedAttackCooldown()
    {
        _rangedAttackCooldownTimer = 0.0f;
        ClearPendingProjectileShot();
    }

    protected void EnsureProjectileScene(string defaultProjectileScenePath)
    {
        if (ProjectileScene == null && !string.IsNullOrWhiteSpace(defaultProjectileScenePath))
            ProjectileScene = GD.Load<PackedScene>(defaultProjectileScenePath);
    }

    protected bool TryHandleRangedAttackAnimationFinished()
    {
        if (AnimatedSprite == null)
            return false;

        var animationName = AnimatedSprite.Animation.ToString();
        if (!animationName.StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
            return false;

        if (_hasPendingProjectileShot)
        {
            LaunchRangedProjectile(_pendingProjectileDirection);
            ClearPendingProjectileShot();
        }

        FinishAttackState();
        return true;
    }

    private void ClearPendingProjectileShot()
    {
        _hasPendingProjectileShot = false;
        _pendingProjectileDirection = Vector2.Zero;
    }

    private void LaunchRangedProjectile(Vector2 direction)
    {
        var projectile = ProjectileScene?.Instantiate<Projectile>();
        if (projectile == null)
            return;

        var parent = GetParent();
        if (parent == null)
            return;

        projectile.GlobalPosition = GlobalPosition;
        parent.AddChild(projectile);
        projectile.Initialize(
            direction,
            this,
            ProjectileDamage,
            ProjectileSpeed,
            ProjectileLifetime,
            ProjectileMaxTravelDistance,
            ProjectileTargetGroup);
    }
}
