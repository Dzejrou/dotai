using Godot;

using System;

[GlobalClass]
public partial class RangedAttackController : Node, ICombatActionController
{
    private const string DefaultProjectileScenePath = "res://scenes/projectiles/projectile.tscn";
    private static readonly StringName DefaultProjectileAnimationName = "default";
    private float _cooldownTimer;
    private bool _hasPendingProjectileShot;
    private Vector2 _pendingProjectileDirection;

    [Export]
    public float MinimumRange { get; set; } = 70.0f;

    [Export]
    public float PreferredRange { get; set; } = 120.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.2f;

    [Export]
    public StringName AttackAnimation { get; set; } = "attack";

    [Export]
    public PackedScene ProjectileScene { get; set; }

    [Export]
    public float ProjectileSpeed { get; set; } = 280.0f;

    [Export]
    public float ProjectileLifetime { get; set; } = 2.5f;

    [Export]
    public float ProjectileMaxTravelDistance { get; set; } = 320.0f;

    [Export]
    public float ProjectileCollisionRadius { get; set; } = 4.0f;

    [Export]
    public float ProjectileVisualScale { get; set; } = 1.0f;

    [Export]
    public SpriteFrames ProjectileVisualFrames { get; set; }

    [Export]
    public DirectionalTextureSet ProjectileDirectionalTextures { get; set; }

    [Export]
    public StringName ProjectileAnimationName { get; set; } = DefaultProjectileAnimationName;

    [Export]
    public float AnimationSpeedMultiplier { get; set; } = 2.0f;

    public override void _Ready()
    {
        MinimumRange = Math.Max(0.0f, MinimumRange);
        PreferredRange = Math.Max(MinimumRange, PreferredRange);
        AttackCooldown = Math.Max(0.0f, AttackCooldown);
        ProjectileSpeed = Math.Max(0.0f, ProjectileSpeed);
        ProjectileLifetime = Math.Max(0.0f, ProjectileLifetime);
        ProjectileMaxTravelDistance = Math.Max(0.0f, ProjectileMaxTravelDistance);
        ProjectileCollisionRadius = Math.Max(0.0f, ProjectileCollisionRadius);
        ProjectileVisualScale = Math.Max(0.01f, ProjectileVisualScale);
        AnimationSpeedMultiplier = Math.Max(0.0f, AnimationSpeedMultiplier);

        if (ProjectileScene == null)
            ProjectileScene = GD.Load<PackedScene>(DefaultProjectileScenePath);
    }

    public void Update(Actor actor, double delta)
    {
        if (_cooldownTimer > 0.0f)
            _cooldownTimer -= (float)delta * Math.Max(0.0f, actor.AttackSpeedMultiplier);
    }

    public bool CanStartAction(Actor actor, Node2D target)
    {
        if (_cooldownTimer > 0.0f || ProjectileScene == null)
            return false;

        if (target == null || !Actor.IsStructurallyValidTarget(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        var targetFactionState = FactionState.ResolveFor(target);
        if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(actor.Faction))
            return false;

        var distance = actor.GlobalPosition.DistanceTo(target.GlobalPosition);
        return distance >= MinimumRange && distance <= PreferredRange;
    }

    public void StartAction(Actor actor, Node2D target)
    {
        if (!CanStartAction(actor, target))
        {
            if (target == null || !Actor.IsStructurallyValidTarget(target))
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
        if (actor.TryPlayDirectionalAnimation(AttackAnimation.ToString(), AnimationSpeedMultiplier * Math.Max(0.0f, actor.AttackSpeedMultiplier)))
        {
            _hasPendingProjectileShot = true;
            _pendingProjectileDirection = projectileDirection;
            return;
        }

        actor.SetState(CombatUnitState.PursuingTarget);
        LaunchProjectile(actor, projectileDirection);
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
            return false;

        if (_hasPendingProjectileShot)
        {
            LaunchProjectile(actor, _pendingProjectileDirection);
            ClearPendingProjectileShot();
        }

        actor.FinishAttackState();
        return true;
    }

    public void Cancel(Actor actor)
    {
        _cooldownTimer = 0.0f;
        ClearPendingProjectileShot();
    }

    private void ClearPendingProjectileShot()
    {
        _hasPendingProjectileShot = false;
        _pendingProjectileDirection = Vector2.Zero;
    }

    private void LaunchProjectile(Actor actor, Vector2 direction)
    {
        var projectile = ProjectileScene?.Instantiate<Projectile>();
        if (projectile == null)
            return;

        var parent = actor.GetParent();
        if (parent == null)
            return;

        parent.AddChild(projectile);
        projectile.GlobalPosition = actor.GlobalPosition;

        var damagePayload = Damage.DuplicateFrom(this);
        damagePayload?.InitializeRuntime(actor, damagePayload.ResolveAmount());
        projectile.Initialize(
            direction,
            actor,
            damagePayload,
            overrideVisualFrames: ProjectileVisualFrames,
            overrideDirectionalTextures: ProjectileDirectionalTextures,
            overrideAnimationName: ProjectileAnimationName.ToString(),
            overrideSpeed: ProjectileSpeed,
            overrideLifetime: ProjectileLifetime,
            overrideMaxTravelDistance: ProjectileMaxTravelDistance,
            overrideCollisionRadius: ProjectileCollisionRadius,
            overrideVisualScale: ProjectileVisualScale);
    }
}
