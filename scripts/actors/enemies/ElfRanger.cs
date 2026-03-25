using Godot;

[GlobalClass]
public partial class ElfRanger : Actor, IAttackable, ITargetable
{
    [Export]
    public float Speed { get; set; } = 62.0f;

    [Export]
    public int Health { get; set; } = 18;

    [Export]
    public float MinimumRange { get; set; } = 70.0f;

    [Export]
    public float PreferredRange { get; set; } = 120.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.2f;

    [Export]
    public StringName AttackAnimation { get; set; } = "shooting-bow";

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

        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        SetPrimaryActionController(
            new RangedAttackController(
                MinimumRange,
                PreferredRange,
                AttackCooldown,
                AttackAnimation,
                ProjectileScene,
                ProjectileDamage,
                ProjectileSpeed,
                ProjectileLifetime,
                ProjectileMaxTravelDistance));
        ConfigureBehaviors(ActorBehaviorPresets.CreateSceneBackedHostileRangedPreset());
        PlayIdleIfAvailable();
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyIncomingDamage(damageInfo, out var damage, out var died))
            return;

        ShowFloatingDamageNumber(damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        if (died)
            StartDeath();
    }

    private void StartDeath()
    {
        SetIsDead(true);
        SpawnCorpseAndFree();
    }

    protected override int MaxHealthValue => Health;
}
