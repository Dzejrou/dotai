using Godot;

[GlobalClass]
public partial class Skeleton : Actor, IAttackable, ITargetable
{
    [Export]
    public float Speed { get; set; } = 52.0f;

    public bool CanBeTargeted => !IsDead;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<OmniSprite>("OmniSprite"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        ConfigureBehaviors(CreateDefaultBehaviors());

        PlayIdleIfAvailable();
    }

    public void ApplyDamage(Damage damageInfo)
    {
        if (!TryApplyIncomingDamage(damageInfo, out var damage, out var died))
            return;

        FloatingText.ShowDamage(damage, damageInfo.IsCritical, this);
        if (died)
        {
            TryGrantExperienceToKiller(damageInfo);
            StartDeath();
        }
    }

    private IActorBehavior[] CreateDefaultBehaviors()
    {
        return ActorBehaviorPresets.CreateSceneBackedHostileMeleePreset();
    }

    private void StartDeath()
    {
        SetIsDead(true);
        ClearTarget();
        SpawnCorpseAndFree();
    }
}
