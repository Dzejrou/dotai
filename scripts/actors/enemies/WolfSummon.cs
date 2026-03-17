using Godot;

using System;

[GlobalClass]
public partial class WolfSummon : EnemyBase, IAttackable, ITargetable, ISummonedUnit
{
    private const float StuckProgressThreshold = 1.0f;
    private const float StuckTimeoutSeconds = 0.6f;

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
    public float SummonerRecoveryTolerance { get; set; } = 32.0f;

    public bool CanBeTargeted => !IsDead;
    public ISummoner Summoner { get; private set; }

    private readonly RandomNumberGenerator _randomNumberGenerator = new();
    private float _attackCooldownTimer;
    private bool _returningToSummonerAfterStuck;
    private bool _hasStuckProgressPosition;
    private Vector2 _lastStuckProgressPosition;
    private float _stuckTimer;

    public override void _Ready()
    {
        InitializeEnemy(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"),
            "WolfSummon");
        SetMovementSpeed(Speed);
        PlayIdleIfAvailable();
        AnimatedSprite.AnimationFinished += OnAnimationFinished;
        _randomNumberGenerator.Randomize();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Summoner != null && !HasValidSummoner())
        {
            QueueFree();
            return;
        }

        if (IsDead)
            return;

        base._PhysicsProcess(delta);
    }

    protected override void PrePhysicsProcess(double delta)
    {
        UpdateStuckRecovery((float)delta);
    }

    public void SetSummoner(ISummoner summoner)
    {
        Summoner = summoner;
    }

    public bool HasValidSummoner()
    {
        return Summoner != null &&
               GodotObject.IsInstanceValid(Summoner.SummonerNode) &&
               Summoner.IsSummonerActive;
    }

    protected override void AcquireTarget()
    {
        if (_returningToSummonerAfterStuck)
        {
            var summonerNode = GetSummonerNode();
            if (summonerNode != null &&
                GodotObject.IsInstanceValid(summonerNode) &&
                summonerNode.IsInsideTree() &&
                GlobalPosition.DistanceTo(summonerNode.GlobalPosition) > Math.Max(0.0f, SummonerRecoveryTolerance))
            {
                return;
            }

            _returningToSummonerAfterStuck = false;
        }

        base.AcquireTarget();
    }

    protected override bool HandleNoTarget(double delta)
    {
        if (_returningToSummonerAfterStuck)
        {
            var summonerNode = GetSummonerNode();
            if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode) || !summonerNode.IsInsideTree())
            {
                _returningToSummonerAfterStuck = false;
                return false;
            }

            if (GlobalPosition.DistanceTo(summonerNode.GlobalPosition) <= Math.Max(0.0f, SummonerRecoveryTolerance))
            {
                _returningToSummonerAfterStuck = false;
                SetCombatState(CombatUnitState.Idle);
                return false;
            }

            return TryMoveTowardDestination(summonerNode.GlobalPosition, 1.0f, CombatUnitState.Leashing, delta);
        }

        return base.HandleNoTarget(delta);
    }

    protected override bool CanAttackNow(Vector2 toTarget, double delta)
    {
        if (_attackCooldownTimer > 0.0f)
        {
            _attackCooldownTimer -= (float)delta;
            return false;
        }

        return toTarget.Length() <= AttackRange;
    }

    protected override bool ShouldStayEngaged(Vector2 toTarget, double delta)
    {
        return toTarget.Length() <= AttackRange;
    }

    protected override void StartAttack()
    {
        if (CurrentTarget == null || !IsInstanceValid(CurrentTarget) || !CurrentTarget.IsInsideTree())
        {
            ClearTarget();
            _attackCooldownTimer = 0.0f;
            return;
        }

        if (CurrentTarget is not IAttackable attackable ||
            CurrentTarget is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            ClearTarget();
            _attackCooldownTimer = 0.0f;
            return;
        }

        SetCombatState(CombatUnitState.Attacking);
        _attackCooldownTimer = AttackCooldown;

        if (CurrentTarget.GlobalPosition != Vector2.Zero)
            LastDirection = DirectionHelper.GetDirectionName(CurrentTarget.GlobalPosition - GlobalPosition);

        var attackAnimation = $"{AttackAnimation}_{LastDirection}";
        if (AnimatedSprite.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(attackAnimation) &&
            AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimation) > 0)
        {
            AnimatedSprite.Play(attackAnimation);
        }
        else
        {
            SetCombatState(CombatUnitState.PursuingTarget);
        }

        var maxDamage = Math.Max(MinAttackDamage, MaxAttackDamage);
        var damage = _randomNumberGenerator.RandiRange(Math.Min(MinAttackDamage, maxDamage), maxDamage);
        attackable.ApplyDamage(new DamageInfo(damage, this));
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyEnemyDamage(damageInfo, out var damage, out var died))
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        if (died)
            StartDeath();
    }

    private void UpdateStuckRecovery(float delta)
    {
        if (!ShouldCheckForStuckRecovery())
        {
            ResetStuckRecoveryTracking();
            return;
        }

        if (!_hasStuckProgressPosition)
        {
            _hasStuckProgressPosition = true;
            _lastStuckProgressPosition = GlobalPosition;
            _stuckTimer = 0.0f;
            return;
        }

        if (GlobalPosition.DistanceTo(_lastStuckProgressPosition) > StuckProgressThreshold)
        {
            _lastStuckProgressPosition = GlobalPosition;
            _stuckTimer = 0.0f;
            return;
        }

        _stuckTimer += Math.Max(0.0f, delta);
        if (_stuckTimer < StuckTimeoutSeconds)
            return;

        ClearTarget();
        _returningToSummonerAfterStuck = true;
        SetCombatState(CombatUnitState.Leashing);
        ResetStuckRecoveryTracking();
    }

    private bool ShouldCheckForStuckRecovery()
    {
        if (IsDead || Velocity == Vector2.Zero)
            return false;

        if (_returningToSummonerAfterStuck)
            return CurrentState == CombatUnitState.Leashing;

        return CurrentState == CombatUnitState.PursuingTarget && CurrentTarget != null;
    }

    private void ResetStuckRecoveryTracking()
    {
        _hasStuckProgressPosition = false;
        _lastStuckProgressPosition = Vector2.Zero;
        _stuckTimer = 0.0f;
    }

    private void OnAnimationFinished()
    {
        if (AnimatedSprite.Animation.ToString().StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
        {
            FinishAttackState();
            return;
        }

        TryFinalizeDeathAnimation();
    }

    private void StartDeath()
    {
        MarkDead();
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;
        _returningToSummonerAfterStuck = false;
        ResetStuckRecoveryTracking();
        TryPlayDeathAnimation();
    }

    private Node2D GetSummonerNode()
    {
        return Summoner?.SummonerNode;
    }

    protected override int MaxHealthValue => MaxHealth;
}
