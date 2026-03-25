using Godot;

[GlobalClass]
public partial class SkeletonMage : Actor, IAttackable, ITargetable
{
    [Export]
    public float Speed { get; set; } = 58.0f;

    public bool CanBeTargeted => !IsDead;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);

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
}
