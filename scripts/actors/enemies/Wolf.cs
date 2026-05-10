using Godot;

[GlobalClass]
public partial class Wolf : Actor, IAttackable, ITargetable
{
    [Export]
    public float Speed { get; set; } = 76.0f;

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

        FloatingText.ShowBad(damage.ToString(), this);
        if (died)
        {
            TryGrantExperienceToKiller(damageInfo);
            StartDeath();
        }
    }

    private void StartDeath()
    {
        SetIsDead(true);
        ClearTarget();
        SpawnCorpseAndFree();
    }

    private IActorBehavior[] CreateDefaultBehaviors()
    {
        return ActorBehaviorPresets.CreateSceneBackedHostileMeleePreset();
    }
}
