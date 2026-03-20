using Godot;

[GlobalClass]
public partial class Dryad : ActorBase, IAttackable, ITargetable
{
    private static readonly StringName HealAnimation = "slash";

    [Export]
    public float HealRange { get; set; } = 48.0f;

    [Export]
    public float HealAcquisitionRange { get; set; } = 96.0f;

    [Export]
    public float HealCooldown { get; set; } = 1.4f;

    [Export]
    public int HealAmount { get; set; } = 3;

    [Export]
    public int Health { get; set; } = 18;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => Factions.Neutral;

    public override void _Ready()
    {
        InitializeActor(
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"));
        ApplyFactionCombatGroup();
        SetPrimaryActionController(new HealActionController(HealRange, HealCooldown, HealAnimation, HealAmount));

        // TODO: Add wandering/support positioning once the first healer pass is stable.
        ConfigureBehaviors(new HealNearbyFactionBehavior(Factions.Allies, HealAcquisitionRange));
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
        MarkDead();
        Velocity = Vector2.Zero;
        ResetPrimaryActionController();
        TryPlayDeathAnimation();
    }

    protected override int MaxHealthValue => Health;
}
