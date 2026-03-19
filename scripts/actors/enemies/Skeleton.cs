using Godot;

using System;

[GlobalClass]
public partial class Skeleton : ActorBase, IAttackable, ITargetable
{
    [Export]
    public float Speed { get; set; } = 52.0f;

    [Export]
    public float AttackRange { get; set; } = 18.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.1f;

    [Export]
    public StringName AttackAnimation { get; set; } = "cross-punch";

    [Export]
    public int Health { get; set; } = 24;

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

    public override Faction Faction => Factions.Enemies;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        ApplyFactionCombatGroup();
        SetPrimaryActionController(new MeleeAttackController(AttackRange, AttackCooldown, AttackAnimation, 1, 5));

        var preset = ActorBehaviorPresets.CreateHostileMeleePreset(
            AggroAcquisitionRange,
            InitialTargetPath,
            "Skeleton",
            AggroLossRange,
            EvadeOnAggroLoss,
            IgnoreDamageWhileEvading);
        ConfigureBehaviors(preset.Behaviors);
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

        ShowFloatingDamageNumber(damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
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
