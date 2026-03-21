using Godot;

using System;

[GlobalClass]
public partial class Wolf : ActorBase, IAttackable, ITargetable, ISummonedUnit, IFactionAssignable
{
    private const int MaxFormationSlots = 4;

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
    public ISummoner Summoner => ResolveSummonState()?.Summoner;

    private Faction _faction = Factions.Enemies;
    private FollowSummonerBehavior _followSummonerBehavior;
    private SummonRoleComposer _summonRoleComposer;
    private SummonState _summonState;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        _summonState = ResolveSummonState();
        SetMovementSpeed(Speed);
        SetPrimaryActionController(new MeleeAttackController(AttackRange, AttackCooldown, AttackAnimation, MinAttackDamage, MaxAttackDamage));
        _summonRoleComposer = new SummonRoleComposer(
            _summonState,
            ConfigureBehaviors,
            CreateDefaultBehaviors,
            CreateSummonBehaviorPreset,
            isSummoned => ApplyFactionCombatGroup());
        RefreshSummonRoleComposition();
        PlayIdleIfAvailable();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Summoner != null && !HasValidSummoner())
        {
            PrepareForRemoval();
            QueueFree();
            return;
        }

        base._PhysicsProcess(delta);
    }

    public void SetSummoner(ISummoner summoner)
    {
        ResolveSummonState()?.SetSummoner(summoner, SetFaction);
        if (IsInsideTree())
            RefreshSummonRoleComposition();
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

    public bool HasValidSummoner()
    {
        return ResolveSummonState()?.HasValidSummoner() == true;
    }

    public bool IsOwnedBy(Node2D owner)
    {
        return ResolveSummonState()?.IsOwnedBy(owner) == true;
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
        ClearTarget();
        _followSummonerBehavior?.CancelRecovery();
        ResetPrimaryActionController();
        TryPlayDeathAnimation();
    }

    private IActorBehavior[] CreateDefaultBehaviors()
    {
        var preset = ActorBehaviorPresets.CreateSceneBackedHostileMeleePreset();
        return preset.Behaviors;
    }

    private void RefreshSummonRoleComposition()
    {
        _followSummonerBehavior = GetNodeOrNull<FollowSummonerBehavior>("Behaviors/Tier90_Recovery/FollowSummonerBehavior");
        _followSummonerBehavior = _summonRoleComposer?.Refresh();
    }

    private SummonBehaviorPreset CreateSummonBehaviorPreset()
    {
        return SummonBehaviorPresets.CreateSummonedMeleePreset(
            _followSummonerBehavior,
            stuckCondition: actor =>
                actor.CurrentState == CombatUnitState.PursuingTarget ||
                actor.CurrentState == CombatUnitState.FollowingOwner ||
                actor.CurrentState == CombatUnitState.Leashing);
    }

    private SummonState ResolveSummonState()
    {
        _summonState ??= GetNodeOrNull<SummonState>("SummonState");
        if (_summonState == null && IsInsideTree())
            GD.PushError($"{GetPath()}: missing required SummonState child.");

        return _summonState;
    }

    protected override int MaxHealthValue => Health;
}
