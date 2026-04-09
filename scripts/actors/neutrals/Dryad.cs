using Godot;

[GlobalClass]
public partial class Dryad : Actor, IAttackable, ITargetable, ISpellCaster
{
    [Export]
    public float HealAcquisitionRange { get; set; } = 256.0f;

    [Export]
    public float Speed { get; set; } = 44.0f;

    public bool CanBeTargeted => !IsDead;
    public Node2D SpellOrigin => this;
    public bool CanCastSpells => !IsDead;

    public override void _Ready()
    {
        InitializeActor(
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D"));
        SetMovementSpeed(Speed);

        ConfigureBehaviors(new HealLowestHealthFriendlyBehavior(HealAcquisitionRange));
        PlayIdleIfAvailable();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (!Combat.InCombat)
            ManaState?.Tick(delta);
    }

    public void NotifyManaChanged() { }

    public void ApplyDamage(Damage damageInfo)
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
