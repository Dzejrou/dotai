using Godot;

using System;

[GlobalClass]
public partial class WolfSummon : ActorBase, IAttackable, ITargetable, ISummonedUnit, IFactionMember
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
    public int MaxHealth { get; set; } = 12;

    [Export]
    public int MinAttackDamage { get; set; } = 1;

    [Export]
    public int MaxAttackDamage { get; set; } = 3;

    [Export]
    public float AggroAcquisitionRange { get; set; } = 150.0f;

    [Export]
    public float AggroLossRange { get; set; } = 220.0f;

    [Export]
    public bool EvadeOnAggroLoss { get; set; } = true;

    [Export]
    public bool IgnoreDamageWhileEvading { get; set; } = true;

    [Export]
    public float ReturnHomeRegenerationFractionPerSecond { get; set; } = 0.1f;

    [Export]
    public float IdleRegenerationFractionPerSecond { get; set; } = 0.01f;

    [Export]
    public float IdleRegenerationIntervalSeconds { get; set; } = 5.0f;

    [Export]
    public float SummonerRecoveryTolerance { get; set; } = 32.0f;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => _faction;
    public ISummoner Summoner { get; private set; }

    private Faction _faction = Factions.Enemies;
    private FollowSummonerBehavior _followSummonerBehavior;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        ApplyFactionGroup();
        SetPrimaryActionController(new MeleeAttackController(AttackRange, AttackCooldown, AttackAnimation, MinAttackDamage, MaxAttackDamage));

        var leashBehavior = new LeashBehavior(
            AggroLossRange,
            EvadeOnAggroLoss,
            IgnoreDamageWhileEvading,
            actor => actor.HomePosition,
            actor => actor.IsAtHome());
        _followSummonerBehavior = new FollowSummonerBehavior(
            actor => GetSummonerNode(),
            actor => actor.GlobalPosition,
            SummonerRecoveryTolerance,
            SummonerRecoveryTolerance,
            0.0f,
            1.0f,
            followWhenIdle: false);

        ConfigureBehaviors(
            leashBehavior,
            new PursuitStuckRecoveryBehavior(
                1.0f,
                0.6f,
                8.0f,
                actor => actor.CurrentState == CombatUnitState.PursuingTarget && actor.CurrentTarget != null,
                actor =>
                {
                    actor.ClearTarget();
                    _followSummonerBehavior.BeginRecovery();
                }),
            new AcquireHostileTargetBehavior(
                AggroAcquisitionRange,
                null,
                "WolfSummon",
                actor => !leashBehavior.IsReturningHome && !_followSummonerBehavior.IsRecovering,
                additionalTargetFilter: (actor, target) => CanAcquireTarget(target)),
            new TargetCombatBehavior(),
            _followSummonerBehavior,
            new ReturnHomeBehavior(actor => actor.HomePosition, actor => actor.IsAtHome()),
            new ReturnHomeRegenerationBehavior(ReturnHomeRegenerationFractionPerSecond),
            new IdleRegenerationBehavior(IdleRegenerationFractionPerSecond, IdleRegenerationIntervalSeconds));
        PlayIdleIfAvailable();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Summoner != null && !HasValidSummoner())
        {
            QueueFree();
            return;
        }

        base._PhysicsProcess(delta);
    }

    public void SetSummoner(ISummoner summoner)
    {
        Summoner = summoner;
        if (summoner is IFactionMember factionMember)
            SetFaction(factionMember.Faction);
    }

    public void SetFaction(Faction faction)
    {
        _faction = faction ?? Factions.Enemies;
        if (IsInsideTree())
            ApplyFactionGroup();
    }

    public bool HasValidSummoner()
    {
        return Summoner != null &&
               GodotObject.IsInstanceValid(Summoner.SummonerNode) &&
               Summoner.IsSummonerActive;
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyIncomingDamage(damageInfo, out var damage, out var died))
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        if (died)
            StartDeath();
    }

    private void StartDeath()
    {
        SetIsDead(true);
        MarkDead();
        Velocity = Vector2.Zero;
        _followSummonerBehavior?.CancelRecovery();
        ResetPrimaryActionController();
        TryPlayDeathAnimation();
    }

    private Node2D GetSummonerNode()
    {
        return Summoner?.SummonerNode;
    }

    private bool CanAcquireTarget(Node2D target)
    {
        return target != null && IsHostileTo(target);
    }

    private void ApplyFactionGroup()
    {
        ApplyFactionCombatGroup();
    }

    protected override int MaxHealthValue => MaxHealth;
}
