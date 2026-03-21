using Godot;

[GlobalClass]
public partial class ElfRanger : ActorBase, IAttackable, ITargetable, ISummoner, IFactionMember
{
    private const string DefaultWolfScenePath = "res://scenes/actors/enemies/wolf.tscn";

    [Export]
    public float Speed { get; set; } = 62.0f;

    [Export]
    public int Health { get; set; } = 18;

    [Export]
    public PackedScene WolfScene { get; set; }

    [Export]
    public float WolfSummonSpawnOffset { get; set; } = 28.0f;

    [Export]
    public float WolfSummonTriggerRange { get; set; } = 180.0f;

    [Export]
    public float WolfResummonDelaySeconds { get; set; } = 10.0f;

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

    [Export]
    public string ProjectileTargetGroup { get; set; } = string.Empty;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => Factions.Enemies;
    public Node2D SummonerNode => this;
    public bool IsSummonerActive => !IsDead && IsInsideTree();

    public override void _Ready()
    {
        if (ProjectileScene == null)
            ProjectileScene = GD.Load<PackedScene>("res://scenes/projectiles/projectile.tscn");
        if (WolfScene == null)
            WolfScene = GD.Load<PackedScene>(DefaultWolfScenePath);

        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        ApplyFactionCombatGroup();
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
                ProjectileMaxTravelDistance,
                ProjectileTargetGroup));

        var preset = ActorBehaviorPresets.CreateSceneBackedHostileRangedPreset(
            extraBehaviors: new SingleOwnedSummonBehavior(
                WolfScene,
                WolfSummonSpawnOffset,
                WolfSummonTriggerRange,
                WolfResummonDelaySeconds,
                actor => actor as ISummoner));
        ConfigureBehaviors(preset.Behaviors);
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
