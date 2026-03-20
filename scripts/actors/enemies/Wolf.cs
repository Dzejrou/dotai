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

    [Export]
    public float AggroAcquisitionRange { get; set; } = 150.0f;

    [Export]
    public float AggroLossRange { get; set; } = 220.0f;

    [Export]
    public bool EvadeOnAggroLoss { get; set; } = true;

    [Export]
    public bool IgnoreDamageWhileEvading { get; set; } = true;

    [Export]
    public float SummonerRecoveryTolerance { get; set; } = 32.0f;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => _faction;
    public ISummoner Summoner => _summonRole.Summoner;

    private Faction _faction = Factions.Enemies;
    private readonly SummonRoleState _summonRole = new();
    private FollowSummonerBehavior _followSummonerBehavior;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        SetPrimaryActionController(new MeleeAttackController(AttackRange, AttackCooldown, AttackAnimation, MinAttackDamage, MaxAttackDamage));
        ConfigureBehaviorRole();
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
        _summonRole.SetSummoner(summoner, SetFaction);
        if (IsInsideTree())
            ConfigureBehaviorRole();
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
        return _summonRole.HasValidSummoner();
    }

    public bool IsOwnedBy(Node2D owner)
    {
        return _summonRole.IsOwnedBy(owner);
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

    private void ConfigureBehaviorRole()
    {
        if (IsSummonedRole)
            ConfigureSummonedRole();
        else
            ConfigureHostileRole();
    }

    private void ConfigureHostileRole()
    {
        _followSummonerBehavior = null;
        ApplyFactionCombatGroup();

        var preset = ActorBehaviorPresets.CreateHostileMeleePreset(
            AggroAcquisitionRange,
            null,
            nameof(Wolf),
            AggroLossRange,
            EvadeOnAggroLoss,
            IgnoreDamageWhileEvading);
        ConfigureBehaviors(preset.Behaviors);
    }

    private void ConfigureSummonedRole()
    {
        ApplyFactionCombatGroup();

        var summonLeashDistance = Math.Max(0.0f, SummonerRecoveryTolerance);
        var summonReturnDistance = Math.Min(summonLeashDistance, 18.0f);
        var summonIdleTolerance = Math.Min(summonLeashDistance, 12.0f);
        var preset = SummonBehaviorPresets.CreateSummonedMeleePreset(
            actor => GetSummonerNode(),
            actor => GetSummonerAnchor(),
            summonLeashDistance,
            summonReturnDistance,
            summonIdleTolerance,
            1.0f,
            followWhenIdle: true,
            canAttemptAcquisition: actor => _followSummonerBehavior == null || !_followSummonerBehavior.IsRecovering,
            additionalTargetFilter: (actor, target) => CanAcquireTarget(target),
            shouldDropTarget: (actor, target) => _followSummonerBehavior != null && _followSummonerBehavior.ShouldPrioritizeLeashReturn(actor));
        _followSummonerBehavior = preset.FollowSummonerBehavior;
        ConfigureBehaviors(preset.Behaviors);
    }

    private Node2D GetSummonerNode()
    {
        return _summonRole.SummonerNode;
    }

    private Vector2 GetSummonerAnchor()
    {
        var summonerNode = GetSummonerNode();
        if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode))
            return GlobalPosition;

        return summonerNode.GlobalPosition;
    }

    private bool CanAcquireTarget(Node2D target)
    {
        return target != null && IsHostileTo(target);
    }

    protected override int MaxHealthValue => Health;

    private bool IsSummonedRole => _summonRole.IsSummoned;
}
