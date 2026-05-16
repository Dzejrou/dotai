using Godot;

[GlobalClass]
public partial class SkeletonMage : Actor, IAttackable, ITargetable, ISpellCaster
{
    [Export]
    public float Speed { get; set; } = 58.0f;

    public bool CanBeTargeted => !IsDead;
    public Node2D SpellOrigin => this;
    public bool CanCastSpells => !IsDead;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<OmniSprite>("OmniSprite"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);

        ConfigureBehaviors(ActorBehaviorPresets.CreateSceneBackedHostileRangedPreset());

        PlayIdleIfAvailable();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (!Combat.InCombat)
            ManaState?.Tick(delta, ResolvedMP5);
    }

    public void NotifyManaChanged() { }

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

    private void StartDeath()
    {
        SetIsDead(true);
        SpawnCorpseAndFree();
    }
}
