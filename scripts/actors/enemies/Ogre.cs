using Godot;

[GlobalClass]
public partial class Ogre : Actor, IAttackable, ITargetable
{
    [Export]
    public float Speed { get; set; } = 64.0f;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);

        ConfigureBehaviors(ActorBehaviorPresets.CreateSceneBackedHostileMeleePreset());

        PlayIdleIfAvailable();
    }

    public bool CanBeTargeted => !IsDead;

    public void ApplyDamage(Damage damageInfo)
    {
        if (!TryApplyIncomingDamage(damageInfo, out var damage, out var died))
            return;

        if (died)
            StartDeath();
    }

    private void StartDeath()
    {
        SetIsDead(true);
        SpawnCorpseAndFree();
    }
}
