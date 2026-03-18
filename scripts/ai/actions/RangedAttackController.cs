using Godot;

using System;

public sealed class RangedAttackController : ICombatActionController
{
    private readonly StringName _attackAnimation;
    private readonly PackedScene _projectileScene;
    private readonly int _projectileDamage;
    private readonly float _projectileSpeed;
    private readonly float _projectileLifetime;
    private readonly float _projectileMaxTravelDistance;
    private readonly string _projectileTargetGroup;
    private float _cooldownTimer;
    private bool _hasPendingProjectileShot;
    private Vector2 _pendingProjectileDirection;

    public RangedAttackController(
        float minimumRange,
        float preferredRange,
        float attackCooldown,
        StringName attackAnimation,
        PackedScene projectileScene,
        int projectileDamage,
        float projectileSpeed,
        float projectileLifetime,
        float projectileMaxTravelDistance,
        string projectileTargetGroup)
    {
        MinimumRange = Math.Max(0.0f, minimumRange);
        PreferredRange = Math.Max(MinimumRange, preferredRange);
        AttackCooldown = Math.Max(0.0f, attackCooldown);
        _attackAnimation = attackAnimation;
        _projectileScene = projectileScene;
        _projectileDamage = projectileDamage;
        _projectileSpeed = projectileSpeed;
        _projectileLifetime = projectileLifetime;
        _projectileMaxTravelDistance = projectileMaxTravelDistance;
        _projectileTargetGroup = projectileTargetGroup;
    }

    public float MinimumRange { get; }
    public float PreferredRange { get; }
    public float AttackCooldown { get; }

    public void Update(ActorBase actor, double delta)
    {
        if (_cooldownTimer > 0.0f)
            _cooldownTimer -= (float)delta;
    }

    public bool CanStartAction(ActorBase actor, Node2D target)
    {
        if (_cooldownTimer > 0.0f || _projectileScene == null)
            return false;

        if (target == null || !ActorBase.IsStructurallyValidTarget(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        var distance = actor.GlobalPosition.DistanceTo(target.GlobalPosition);
        return distance >= MinimumRange && distance <= PreferredRange;
    }

    public void StartAction(ActorBase actor, Node2D target)
    {
        if (!CanStartAction(actor, target))
        {
            if (target == null || !ActorBase.IsStructurallyValidTarget(target))
                actor.ClearTarget();
            return;
        }

        ClearPendingProjectileShot();

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        actor.SetState(CombatUnitState.Attacking);
        _cooldownTimer = AttackCooldown;

        var projectileDirection = toTarget != Vector2.Zero ? toTarget.Normalized() : DirectionHelper.GetDirectionVector(actor.LastDirection);
        var attackAnimationName = $"{_attackAnimation}_{actor.LastDirection}";
        if (actor.AnimatedSprite?.SpriteFrames != null &&
            actor.AnimatedSprite.SpriteFrames.HasAnimation(attackAnimationName) &&
            actor.AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimationName) > 0)
        {
            _hasPendingProjectileShot = true;
            _pendingProjectileDirection = projectileDirection;
            actor.AnimatedSprite.Play(attackAnimationName);
            return;
        }

        actor.SetState(CombatUnitState.PursuingTarget);
        LaunchProjectile(actor, projectileDirection);
    }

    public bool HandleAnimationFinished(ActorBase actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(_attackAnimation.ToString(), StringComparison.Ordinal))
            return false;

        if (_hasPendingProjectileShot)
        {
            LaunchProjectile(actor, _pendingProjectileDirection);
            ClearPendingProjectileShot();
        }

        actor.FinishAttackState();
        return true;
    }

    public void Cancel(ActorBase actor)
    {
        _cooldownTimer = 0.0f;
        ClearPendingProjectileShot();
    }

    private void ClearPendingProjectileShot()
    {
        _hasPendingProjectileShot = false;
        _pendingProjectileDirection = Vector2.Zero;
    }

    private void LaunchProjectile(ActorBase actor, Vector2 direction)
    {
        var projectile = _projectileScene?.Instantiate<Projectile>();
        if (projectile == null)
            return;

        var parent = actor.GetParent();
        if (parent == null)
            return;

        projectile.GlobalPosition = actor.GlobalPosition;
        parent.AddChild(projectile);
        projectile.Initialize(
            direction,
            actor,
            _projectileDamage,
            _projectileSpeed,
            _projectileLifetime,
            _projectileMaxTravelDistance,
            _projectileTargetGroup);
    }
}
