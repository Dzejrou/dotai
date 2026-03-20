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

    [Export]
    public float AggroAcquisitionRange { get; set; } = 150.0f;

    [Export]
    public float AggroLossRange { get; set; } = 220.0f;

    [Export]
    public bool EvadeOnAggroLoss { get; set; } = true;

    [Export]
    public bool IgnoreDamageWhileEvading { get; set; } = true;

    [Export]
    public float SummonerRecoveryTolerance { get; set; } = 220.0f;

    [Export]
    public float FormationHorizontalOffset { get; set; } = 28.0f;

    [Export]
    public float FormationVerticalOffset { get; set; } = 18.0f;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => _faction;
    public ISummoner Summoner => _summonRole.Summoner;

    private Faction _faction = Factions.Enemies;
    private readonly SummonRoleState _summonRole = new();
    private FollowSummonerBehavior _followSummonerBehavior;
    private SummonRoleComposer _summonRoleComposer;

    public override void _Ready()
    {
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        SetPrimaryActionController(new MeleeAttackController(AttackRange, AttackCooldown, AttackAnimation, MinAttackDamage, MaxAttackDamage));
        _summonRoleComposer = new SummonRoleComposer(
            _summonRole,
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
            QueueFree();
            return;
        }

        base._PhysicsProcess(delta);
    }

    public void SetSummoner(ISummoner summoner)
    {
        _summonRole.SetSummoner(summoner, SetFaction);
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

    private IActorBehavior[] CreateDefaultBehaviors()
    {
        var preset = ActorBehaviorPresets.CreateHostileMeleePreset(
            AggroAcquisitionRange,
            null,
            nameof(Wolf),
            AggroLossRange,
            EvadeOnAggroLoss,
            IgnoreDamageWhileEvading);
        return preset.Behaviors;
    }

    private void RefreshSummonRoleComposition()
    {
        _followSummonerBehavior = _summonRoleComposer?.Refresh();
    }

    private SummonBehaviorPreset CreateSummonBehaviorPreset()
    {
        var summonLeashDistance = Math.Max(0.0f, SummonerRecoveryTolerance);
        var summonReturnDistance = Math.Min(summonLeashDistance, 18.0f);
        var summonIdleTolerance = Math.Min(summonLeashDistance, 12.0f);
        return SummonBehaviorPresets.CreateSummonedMeleePreset(
            actor => GetSummonerNode(),
            actor => GetSummonerAnchor(),
            summonLeashDistance,
            summonReturnDistance,
            summonIdleTolerance,
            1.0f,
            followWhenIdle: true,
            ownerCombatAssistTargetGetter: actor => SummonBehaviorPresets.GetOwnerCombatAssistTarget(actor, _summonRole, IsValidAssistTarget),
            canAttemptAcquisition: actor => _followSummonerBehavior == null || !_followSummonerBehavior.IsRecovering,
            additionalTargetFilter: (actor, target) => CanAcquireTarget(target),
            shouldDropTarget: (actor, target) => _followSummonerBehavior != null && _followSummonerBehavior.ShouldPrioritizeLeashReturn(actor));
    }

    private Node2D GetSummonerNode()
    {
        return _summonRole.SummonerNode;
    }

    private Vector2 GetSummonerAnchor()
    {
        return SummonBehaviorPresets.GetFormationAnchor(
            this,
            _summonRole,
            FormationHorizontalOffset,
            FormationVerticalOffset,
            MaxFormationSlots);
    }

    private bool CanAcquireTarget(Node2D target)
    {
        return target != null && IsHostileTo(target);
    }

    private static bool IsValidAssistTarget(Node2D target)
    {
        return ActorBase.IsStructurallyValidTarget(target) &&
               target is IAttackable &&
               target is ITargetable targetable &&
               targetable.CanBeTargeted;
    }

    protected override int MaxHealthValue => Health;
}
