using Godot;

[GlobalClass]
public partial class Dryad : Actor, IAttackable, ITargetable
{
    [Export]
    public float HealAcquisitionRange { get; set; } = 256.0f;

    [Export]
    public float Speed { get; set; } = 44.0f;

    public bool CanBeTargeted => !IsDead;

    public override void _Ready()
    {
        InitializeActor(
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D"));
        SetMovementSpeed(Speed);

        ConfigureBehaviors(new HealLowestHealthFriendlyBehavior(HealAcquisitionRange));
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
