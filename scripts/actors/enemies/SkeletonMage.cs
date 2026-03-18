using Godot;

using System;

[GlobalClass]
public partial class SkeletonMage : RangedEnemyBase, IAttackable, ITargetable
{
    private readonly ActorAI _actorAI = new AggressiveRangedActorAI();
    private static readonly StringName cast_spell_animation = "cast_spell";

    [Export]
    public float Speed { get; set; } = 58.0f;

    [Export]
    public int Health { get; set; } = 22;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => Factions.Enemies;

    public override void _Ready()
    {
        AttackAnimation = cast_spell_animation;
        SetActorAI(_actorAI);
        EnsureProjectileScene("res://scenes/projectiles/projectile.tscn");
        InitializeAggressiveActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"),
            "SkeletonMage");
        SetMovementSpeed(Speed);

        PlayIdleIfAvailable();

        AnimatedSprite.AnimationFinished += OnAnimationFinished;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
            return;

        base._PhysicsProcess(delta);
    }

    protected override void AcquireTarget()
    {
        TryAcquireTargetWithAI();
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyAggressiveActorDamage(damageInfo, out var damage, out var died))
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        if (died)
            StartDeath();
    }

    private void OnAnimationFinished()
    {
        if (TryHandleRangedAttackAnimationFinished())
            return;

        TryFinalizeDeathAnimation();
    }

    private void StartDeath()
    {
        MarkDead();
        Velocity = Vector2.Zero;
        ResetRangedAttackCooldown();
        TryPlayDeathAnimation();
    }

    protected override int MaxHealthValue => Health;
}
