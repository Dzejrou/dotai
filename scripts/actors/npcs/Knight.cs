using Godot;

[GlobalClass]
public partial class Knight : Actor, IAttackable, ITargetable
{
    [Export]
    public float Speed { get; set; } = 48.0f;

    public bool CanBeTargeted => !IsDead;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<OmniSprite>("OmniSprite"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        ConfigureBehaviors();

        PlayIdleIfAvailable();
    }

    public void ApplyDamage(Damage damageInfo)
    {
        if (!TryApplyIncomingDamage(damageInfo, out var damage, out var died))
            return;

        FloatingText.ShowDamage(damage, damageInfo.IsCritical, this);
        if (died)
            StartDeath();
    }

    private void StartDeath()
    {
        SetIsDead(true);
        ClearTarget();
        SpawnCorpseAndFree();
    }
}
