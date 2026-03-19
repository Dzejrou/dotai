using Godot;

[GlobalClass]
public partial class ElfRanger : ActorBase, IAttackable, ITargetable, ISummoner, IFactionMember
{
    private const string DefaultWolfSummonScenePath = "res://scenes/actors/enemies/wolf_summon.tscn";

    [Export]
    public float Speed { get; set; } = 62.0f;

    [Export]
    public int Health { get; set; } = 18;

    [Export]
    public PackedScene WolfSummonScene { get; set; }

    [Export]
    public float WolfSummonSpawnOffset { get; set; } = 28.0f;

    [Export]
    public float WolfSummonTriggerRange { get; set; } = 180.0f;

    [Export]
    public float WolfResummonDelaySeconds { get; set; } = 10.0f;

    [Export]
    public NodePath InitialTargetPath { get; set; } = new NodePath("../Player");

    [Export]
    public float AggroAcquisitionRange { get; set; } = 150.0f;

    [Export]
    public float AggroLossRange { get; set; } = 220.0f;

    [Export]
    public bool EvadeOnAggroLoss { get; set; } = true;

    [Export]
    public bool IgnoreDamageWhileEvading { get; set; } = true;

    [Export]
    public float ReturnHomeRegenerationFractionPerSecond { get; set; } = 0.1f;

    [Export]
    public float IdleRegenerationFractionPerSecond { get; set; } = 0.01f;

    [Export]
    public float IdleRegenerationIntervalSeconds { get; set; } = 5.0f;

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
        if (WolfSummonScene == null)
            WolfSummonScene = GD.Load<PackedScene>(DefaultWolfSummonScenePath);

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

        var preset = ActorBehaviorPresets.CreateHostileRangedPreset(
            AggroAcquisitionRange,
            InitialTargetPath,
            "ElfRanger",
            AggroLossRange,
            EvadeOnAggroLoss,
            IgnoreDamageWhileEvading,
            ReturnHomeRegenerationFractionPerSecond,
            IdleRegenerationFractionPerSecond,
            IdleRegenerationIntervalSeconds,
            extraBehaviors: new SingleOwnedSummonBehavior(
                WolfSummonScene,
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

        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        if (died)
            StartDeath();
    }

    private void StartDeath()
    {
        SetIsDead(true);
        MarkDead();
        Velocity = Vector2.Zero;
        ResetPrimaryActionController();
        TryPlayDeathAnimation();
    }

    protected override int MaxHealthValue => Health;
}
