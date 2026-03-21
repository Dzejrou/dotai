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

        var preset = ActorBehaviorPresets.CreateSceneBackedHostileMeleePreset();
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

        if (died)
            StartDeath();
    }

    private void StartDeath()
    {
        SetIsDead(true);
        MarkDead();
        SpawnCorpseAndFree();
    }

    protected override int MaxHealthValue => MaxHealth;
}
