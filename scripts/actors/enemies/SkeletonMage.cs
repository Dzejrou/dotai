using Godot;

[GlobalClass]
public partial class SkeletonMage : ActorBase, IAttackable, ITargetable
{
    private static readonly StringName CastSpellAnimation = "cast_spell";

    [Export]
    public float Speed { get; set; } = 58.0f;

    [Export]
    public int Health { get; set; } = 22;

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
    public StringName AttackAnimation { get; set; } = CastSpellAnimation;

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

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => Factions.Enemies;

    public override void _Ready()
    {
        AttackAnimation = CastSpellAnimation;
        if (ProjectileScene == null)
            ProjectileScene = GD.Load<PackedScene>("res://scenes/projectiles/projectile.tscn");

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

        var leashBehavior = new LeashBehavior(
            AggroLossRange,
            EvadeOnAggroLoss,
            IgnoreDamageWhileEvading,
            actor => actor.HomePosition,
            actor => actor.IsAtHome());
        ConfigureBehaviors(
            leashBehavior,
            new PursuitStuckRecoveryBehavior(
                1.0f,
                0.6f,
                8.0f,
                actor => actor.CurrentState == CombatUnitState.PursuingTarget && actor.CurrentTarget != null,
                actor => leashBehavior.BeginReturnHome(actor, true)),
            new AcquireHostileTargetBehavior(
                AggroAcquisitionRange,
                InitialTargetPath,
                "SkeletonMage",
                actor => !leashBehavior.IsReturningHome),
            new TargetCombatBehavior(),
            new ReturnHomeBehavior(actor => actor.HomePosition, actor => actor.IsAtHome()),
            new ReturnHomeRegenerationBehavior(ReturnHomeRegenerationFractionPerSecond),
            new IdleRegenerationBehavior(IdleRegenerationFractionPerSecond, IdleRegenerationIntervalSeconds));

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
