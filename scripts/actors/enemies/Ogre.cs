using Godot;

using System;

[GlobalClass]
public partial class Ogre : ActorBase, IAttackable, ITargetable
{
    [Export]
    public float Speed { get; set; } = 64.0f;

    [Export]
    public float AttackRange { get; set; } = 18.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.2f;

    [Export]
    public StringName AttackAnimation { get; set; } = "cross-punch";

    [Export]
    public int MaxHealth { get; set; } = 40;

    [Export]
    public int MinAttackDamage { get; set; } = 1;

    [Export]
    public int MaxAttackDamage { get; set; } = 4;

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

    public override Faction Faction => Factions.Enemies;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        ApplyFactionCombatGroup();
        SetPrimaryActionController(new MeleeAttackController(AttackRange, AttackCooldown, AttackAnimation, MinAttackDamage, MaxAttackDamage));

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
                "Ogre",
                actor => !leashBehavior.IsReturningHome),
            new TargetCombatBehavior(),
            new ReturnHomeBehavior(actor => actor.HomePosition, actor => actor.IsAtHome()),
            new ReturnHomeRegenerationBehavior(ReturnHomeRegenerationFractionPerSecond),
            new IdleRegenerationBehavior(IdleRegenerationFractionPerSecond, IdleRegenerationIntervalSeconds));

        PlayIdleIfAvailable();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
            return;

        base._PhysicsProcess(delta);
    }

    public bool CanBeTargeted => !IsDead;

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyIncomingDamage(damageInfo, out var damage, out var died))
            return;

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

    protected override int MaxHealthValue => MaxHealth;
}
