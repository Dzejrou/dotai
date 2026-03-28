using Godot;

[GlobalClass]
public partial class SkeletonMage : Actor, IAttackable, ITargetable, ISpellCaster
{
    private ManaState _mana;

    [Export]
    public float Speed { get; set; } = 58.0f;

    public bool CanBeTargeted => !IsDead;
    public Node2D SpellOrigin => this;
    public string SpellDirectionName => LastDirection;
    public Vector2 SpellDirection => DirectionHelper.GetDirectionVector(LastDirection);
    public Node2D SpellTarget => Target;
    public ManaState ManaState => _mana;
    public bool CanCastSpells => !IsDead;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        _mana = GetNode<ManaState>("ManaState");
        _mana.Initialize();
        SetMovementSpeed(Speed);

        ConfigureBehaviors(ActorBehaviorPresets.CreateSceneBackedHostileRangedPreset());

        PlayIdleIfAvailable();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (!Combat.InCombat)
            _mana.Tick(delta);
    }

    public void NotifyManaChanged() { }

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
