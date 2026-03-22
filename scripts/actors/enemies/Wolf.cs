using Godot;

using System;

[GlobalClass]
public partial class Wolf : ActorBase, IAttackable, ITargetable, ISummonedUnit, IFactionAssignable
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
    public ISummoner Summoner => ResolveSummonState().Summoner;

    private Faction _faction = Factions.Enemies;
    private SummonState _summon;

    public override void _Ready()
    {
        _summon = GetNode<SummonState>("SummonState");
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
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

    public void SetSummoner(ISummoner summoner)
    {
        ResolveSummonState().SetSummoner(summoner, SetFaction);
    }

    public bool HasValidSummoner()
    {
        return ResolveSummonState().HasValidSummoner();
    }

    public bool IsOwnedBy(Node2D owner)
    {
        return ResolveSummonState().IsOwnedBy(owner);
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

    private SummonState ResolveSummonState()
    {
        _summon ??= GetNode<SummonState>("SummonState");
        return _summon;
    }
}
