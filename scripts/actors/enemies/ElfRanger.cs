using Godot;

using System;

[GlobalClass]
public partial class ElfRanger : RangedEnemyBase, IAttackable, ITargetable, ISummoner, IFactionMember
{
    private const string DefaultWolfSummonScenePath = "res://scenes/actors/enemies/wolf_summon.tscn";
    private readonly ActorAI _actorAI = new AggressiveRangedActorAI();

    [Export]
    public float Speed { get; set; } = 62.0f;

    [Export]
    public int Health { get; set; } = 18;

    [Export]
    public PackedScene WolfSummonScene { get; set; }

    [Export]
    public float WolfSummonSpawnOffset { get; set; } = 28.0f;

    [Export]
    public float WolfSummonTriggerRange { get; set; } = 180.0f;

    [Export]
    public float WolfResummonDelaySeconds { get; set; } = 10.0f;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => Factions.Enemies;
    public Node2D SummonerNode => this;
    public bool IsSummonerActive => !IsDead && IsInsideTree();

    private WolfSummon _summonedWolf;
    private float _wolfResummonCooldownTimer;

    public override void _Ready()
    {
        SetActorAI(_actorAI);
        EnsureProjectileScene("res://scenes/projectiles/projectile.tscn");
        if (WolfSummonScene == null)
            WolfSummonScene = GD.Load<PackedScene>(DefaultWolfSummonScenePath);

        InitializeEnemy(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"),
            "ElfRanger");
        SetMovementSpeed(Speed);
        PlayIdleIfAvailable();
        AnimatedSprite.AnimationFinished += OnAnimationFinished;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
            return;

        if (_wolfResummonCooldownTimer > 0.0f)
            _wolfResummonCooldownTimer -= (float)delta;

        base._PhysicsProcess(delta);
        UpdateWolfSummonState();
        TrySummonWolf();
    }

    protected override void AcquireTarget()
    {
        TryAcquireTargetWithAI();
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyEnemyDamage(damageInfo, out var damage, out var died))
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        if (died)
            StartDeath();
    }

    private void OnAnimationFinished()
    {
        if (TryHandleRangedAttackAnimationFinished())
            return;

        TryFinalizeDeathAnimation();
    }

    private void StartDeath()
    {
        MarkDead();
        Velocity = Vector2.Zero;
        ResetRangedAttackCooldown();
        TryPlayDeathAnimation();
    }

    private void TrySummonWolf()
    {
        if (IsDead ||
            CurrentState == CombatUnitState.Attacking ||
            _wolfResummonCooldownTimer > 0.0f ||
            HasActiveWolfSummon())
            return;

        if (CurrentTarget == null || !IsInstanceValid(CurrentTarget) || !CurrentTarget.IsInsideTree())
            return;

        if (CurrentTarget is not ITargetable targetable || !targetable.CanBeTargeted)
            return;

        if (GlobalPosition.DistanceTo(CurrentTarget.GlobalPosition) > Math.Max(0.0f, WolfSummonTriggerRange))
            return;

        var parent = GetParent();
        if (parent == null || WolfSummonScene == null)
            return;

        var summonedWolf = WolfSummonScene.Instantiate<WolfSummon>();
        if (summonedWolf == null)
            return;

        var summonDirection = DirectionHelper.GetDirectionVector(LastDirection);
        if (summonDirection == Vector2.Zero && CurrentTarget.GlobalPosition != GlobalPosition)
            summonDirection = (CurrentTarget.GlobalPosition - GlobalPosition).Normalized();
        if (summonDirection == Vector2.Zero)
            summonDirection = Vector2.Right;

        summonedWolf.GlobalPosition = GlobalPosition + summonDirection.Normalized() * Math.Max(0.0f, WolfSummonSpawnOffset);
        summonedWolf.SetSummoner(this);
        parent.AddChild(summonedWolf);
        _summonedWolf = summonedWolf;
    }

    private bool HasActiveWolfSummon()
    {
        return IsActiveWolfSummon(_summonedWolf);
    }

    private void UpdateWolfSummonState()
    {
        if (IsActiveWolfSummon(_summonedWolf))
            return;

        if (_summonedWolf == null)
            return;

        _summonedWolf = null;
        _wolfResummonCooldownTimer = Math.Max(_wolfResummonCooldownTimer, Math.Max(0.0f, WolfResummonDelaySeconds));
    }

    private bool IsActiveWolfSummon(WolfSummon summonedWolf)
    {
        if (summonedWolf == null || !IsInstanceValid(summonedWolf) || !summonedWolf.IsInsideTree())
            return false;

        if (summonedWolf is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        return summonedWolf.HasValidSummoner();
    }

    protected override int MaxHealthValue => Health;
}
