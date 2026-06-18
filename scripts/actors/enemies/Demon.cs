using Godot;

// TODO(fire-barrage-spell): Fire Barrage currently exists only as phase-transition
// infrastructure (a ChannelSpellTransitionAction repeatedly casting a nested FireBallSpell),
// not as a standalone castable spell. Once a real Fire Barrage spell exists, replace
// Ring of Fire with Fire Barrage in the Demon boss's phase-3 combat action pool
// (the RingOfFireCast entry in demon_boss.tscn).
[GlobalClass]
public partial class Demon : Actor, IAttackable, ITargetable, ISpellCaster
{
    [Export]
    public float Speed { get; set; } = 55.0f;

    public bool CanBeTargeted => !IsDead;
    public Node2D SpellOrigin => this;
    public bool CanCastSpells => !IsDead;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<OmniSprite>("OmniSprite"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);

        ConfigureBehaviors(ActorBehaviorPresets.CreateSceneBackedHostileMeleePreset());

        PlayIdleIfAvailable();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        // Regenerate mana while fighting too so the composite can keep choosing
        // Ring of Fire during sustained combat.
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
        ClearTarget();
        SpawnCorpseAndFree();
    }
}
