using Godot;

[GlobalClass]
public partial class Wolf : ActorBase, IAttackable, ITargetable, IFactionAssignable
{
    [Export]
    public float Speed { get; set; } = 76.0f;

    [Export]
    public float AttackRange { get; set; } = 42.0f;

    [Export]
    public float AttackCooldown { get; set; } = 0.85f;

    [Export]
    public StringName AttackAnimation { get; set; } = "bark";

    [Export]
    public int Health { get; set; } = 12;

    [Export]
    public int MinAttackDamage { get; set; } = 1;

    [Export]
    public int MaxAttackDamage { get; set; } = 3;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => _faction;
    private Faction _faction = Factions.Enemies;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetFaction(_faction);
        SetMovementSpeed(Speed);
        SetPrimaryActionController(new MeleeAttackController(AttackRange, AttackCooldown, AttackAnimation, MinAttackDamage, MaxAttackDamage));
        ConfigureBehaviors(CreateDefaultBehaviors());
        PlayIdleIfAvailable();
    }

    public void SetFaction(Faction faction)
    {
        _faction = faction ?? Factions.Enemies;
        if (IsInsideTree())
        {
            ApplyFactionCombatGroup();
            RefreshHealthLabel();
        }
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
        ClearTarget();
        SpawnCorpseAndFree();
    }

    private IActorBehavior[] CreateDefaultBehaviors()
    {
        var preset = ActorBehaviorPresets.CreateSceneBackedHostileMeleePreset();
        return preset.Behaviors;
    }

    protected override int MaxHealthValue => Health;
}
