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

    private float _attackCooldownTimer;
    private int _currentHealth;
    private bool _isDead;

    public bool CanBeTargeted => !_isDead;

    public override void _Ready()
    {
        _currentHealth = Math.Max(1, Health);
        if (ProjectileScene == null)
            ProjectileScene = GD.Load<PackedScene>("res://scenes/projectiles/projectile.tscn");
        InitializeEnemy(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            "SkeletonMage");
        SetMovementSpeed(Speed);
        AnimatedSprite.SpriteFrames = BuildSpriteFrames();

        if (AnimatedSprite.SpriteFrames.HasAnimation("walk_south"))
        {
            AnimatedSprite.Animation = "walk_south";
            AnimatedSprite.Play();
        }

        AnimatedSprite.AnimationFinished += OnAnimationFinished;
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

        var distance = toTarget.Length();
        return distance >= MinimumRange && distance <= PreferredRange;
    }

    protected override Vector2 GetDesiredMovementDirection(Vector2 toTarget, double delta)
    {
        var distance = toTarget.Length();
        if (toTarget == Vector2.Zero || (distance >= MinimumRange && distance <= PreferredRange))
            return Vector2.Zero;

        if (distance > PreferredRange)
            return toTarget.Normalized();

        return -toTarget.Normalized();
    }

    protected override void StartAttack()
    {
        if (_isDead || ProjectileScene == null)
            return;

        if (CurrentTarget == null || !IsInstanceValid(CurrentTarget) || !CurrentTarget.IsInsideTree())
        {
            ClearTarget();
            _attackCooldownTimer = 0.0f;
            return;
        }

        if (CurrentTarget is not ITargetable targetable || !targetable.CanBeTargeted)
        {
            ClearTarget();
            _attackCooldownTimer = 0.0f;
            return;
        }

        IsAttacking = true;
        _attackCooldownTimer = AttackCooldown;

        var toTarget = CurrentTarget.GlobalPosition - GlobalPosition;
        var projectileDirection = toTarget != Vector2.Zero ? toTarget.Normalized() : DirectionHelper.GetDirectionVector(LastDirection);

        var spawnAttackAnimation = $"{AttackAnimation}_{LastDirection}";
        if (AnimatedSprite.SpriteFrames == null ||
            !AnimatedSprite.SpriteFrames.HasAnimation(spawnAttackAnimation) ||
            AnimatedSprite.SpriteFrames.GetFrameCount(spawnAttackAnimation) == 0)
        {
            IsAttacking = false;
            LaunchProjectile(projectileDirection);
            return;
        }

        AnimatedSprite.Play(spawnAttackAnimation);
        LaunchProjectile(projectileDirection);
    }

    public void ApplyDamage(int amount)
    {
        if (_isDead)
            return;

        var damage = Math.Max(1, amount);
        _currentHealth = Math.Max(0, _currentHealth - damage);
        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));

        GD.Print($"SkeletonMage health: {_currentHealth}/{Health} (took {damage})");
        if (_currentHealth <= 0)
        {
            _isDead = true;
            StartDeath();
        }
    }

    private void OnAnimationFinished()
    {
        if (AnimatedSprite.Animation.ToString().StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
        {
            IsAttacking = false;
            return;
        }

        TryFinalizeDeathAnimation();
    }

    private SpriteFrames BuildSpriteFrames()
    {
        var spriteFrames = new SpriteFrames();
        foreach (var direction in DirectionHelper.GetCardinalDirections())
        {
            RuntimeSpriteLoader.AddAnimationFrames(
                spriteFrames,
                $"walk_{direction}",
                "assets/skeleton_mage/animations",
                "walk",
                direction,
                true,
                "SkeletonMage",
                false);
            RuntimeSpriteLoader.AddAnimationFrames(
                spriteFrames,
                $"{AttackAnimation}_{direction}",
                "assets/skeleton_mage/animations",
                "fireball",
                direction,
                false,
                "SkeletonMage",
                false);
            RuntimeSpriteLoader.AddAnimationFrames(
                spriteFrames,
                $"{DeathAnimation}_{direction}",
                "assets/skeleton_mage/animations",
                "falling-back-death",
                direction,
                false,
                "SkeletonMage",
                false);
        }

        return spriteFrames;
    }

    private void StartDeath()
    {
        IsAttacking = false;
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;
        TryPlayDeathAnimation();
    }

    private void LaunchProjectile(Vector2 direction)
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
            CombatGroups.Allies);
    }
}
