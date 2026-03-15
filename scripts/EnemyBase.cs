using Godot;

using System;

public abstract partial class EnemyBase : CombatUnitBase
{
    private const float PursuitStuckProgressThreshold = 1.0f;
    private const float PursuitStuckTimeout = 0.6f;
    private const float PursuitStuckWaypointDistance = 8.0f;

    [Export]
    public NodePath InitialTargetPath { get; set; } = new NodePath("../Player");

    [Export]
    public StringName DeathAnimation { get; set; } = "falling-back-death";

    [Export]
    public bool DisableCollisionOnDeath { get; set; } = true;

    [Export]
    public float AggroAcquisitionRange { get; set; } = 150.0f;

    [Export]
    public float AggroLossRange { get; set; } = 220.0f;

    [Export]
    public float HomeReturnTolerance { get; set; } = 4.0f;

    [Export]
    public bool EvadeOnAggroLoss { get; set; } = true;

    [Export]
    public bool IgnoreDamageWhileEvading { get; set; } = true;

    [Export]
    public bool EnableReturnHomeRegeneration { get; set; } = true;

    [Export]
    public float ReturnHomeRegenerationFractionPerSecond { get; set; } = 0.1f;

    protected Vector2 HomePosition { get; private set; }
    protected int CurrentHealth { get; private set; }
    protected bool IsDead { get; private set; }
    protected int ResolvedMaxHealth => Math.Max(1, MaxHealthValue);
    private bool _hasPursuitProgressPosition;
    private Vector2 _lastPursuitProgressPosition;
    private float _pursuitStuckTimer;
    private Node2D _trackedPursuitTarget;
    private bool _suppressTargetAcquisitionUntilHome;
    private float _returnHomeRegenerationTimer;

    protected void InitializeEnemy(AnimatedSprite2D animatedSprite, CollisionShape2D collisionShape, string enemyName)
    {
        InitializeEnemy(animatedSprite, collisionShape, null, enemyName);
    }

    protected void InitializeEnemy(
        AnimatedSprite2D animatedSprite,
        CollisionShape2D collisionShape,
        NavigationAgent2D navigationAgent,
        string enemyName)
    {
        InitializeCombatUnit(animatedSprite, collisionShape, navigationAgent);
        CurrentHealth = ResolvedMaxHealth;
        IsDead = false;
        AddToGroup(CombatGroups.Enemies);
        HomePosition = GlobalPosition;

        var resolvedTarget = CurrentTarget;
        if (resolvedTarget == null)
        {
            if (!InitialTargetPath.IsEmpty && HasNode(InitialTargetPath))
                resolvedTarget = GetNode<Node2D>(InitialTargetPath);
            else
                resolvedTarget = GetParent()?.GetNodeOrNull<Node2D>("Player");
        }

        if (resolvedTarget != null && CanAcquireTarget(resolvedTarget))
        {
            SetTarget(resolvedTarget);
        }
        else if (resolvedTarget != null && !CanAcquireTarget(resolvedTarget))
            GD.PrintErr($"{enemyName} did not acquire initial target (not in aggro range).");
    }

    protected override void AcquireTarget()
    {
        if (_suppressTargetAcquisitionUntilHome)
            return;

        var candidate = TargetingHelper.FindClosestTarget(
            this,
            CombatGroups.Allies,
            node => node is Node2D && node is IAttackable && node is ITargetable targetable && targetable.CanBeTargeted);

        if (candidate != null && CanAcquireTarget(candidate))
        {
            SetTarget(candidate);
            ResetPursuitStuckTracking();
        }
    }

    protected bool CanAcquireTarget(Node2D target)
    {
        return target is IAttackable && target is ITargetable targetable && targetable.CanBeTargeted &&
               IsTargetWithinAcquisitionRange(target);
    }

    protected override bool ShouldLoseCurrentTarget(Node2D target)
    {
        var shouldLoseTarget = !IsTargetWithinLossRange(target);
        if (shouldLoseTarget && EvadeOnAggroLoss)
            BeginEvadeReset(false);

        return shouldLoseTarget;
    }

    private bool IsTargetWithinAcquisitionRange(Node2D target)
    {
        return IsTargetWithinRange(target, Math.Max(0.0f, AggroAcquisitionRange));
    }

    protected bool IsTargetWithinLossRange(Node2D target)
    {
        return IsTargetWithinRange(target, Math.Max(AggroLossRange, AggroAcquisitionRange));
    }

    private bool IsTargetWithinRange(Node2D target, float range)
    {
        if (target == null)
            return false;

        return GlobalPosition.DistanceTo(target.GlobalPosition) <= range;
    }

    protected bool TryReactToDamageSource(DamageInfo damageInfo)
    {
        if (IsEvadingHomeReturn() && IgnoreDamageWhileEvading)
        {
            ShowFloatingDamageNumber("EVADE", new Color(1.0f, 1.0f, 1.0f, 1.0f));
            return false;
        }

        if (damageInfo.Source is not Node2D sourceNode)
            return true;

        if (!sourceNode.IsInGroup(CombatGroups.Allies))
            return true;

        if (sourceNode is not ITargetable targetable || !targetable.CanBeTargeted)
            return true;

        if (IsTargetWithinLossRange(sourceNode))
        {
            _suppressTargetAcquisitionUntilHome = false;
            SetTarget(sourceNode);
            return true;
        }

        ShowFloatingDamageNumber("EVADE", new Color(1.0f, 1.0f, 1.0f, 1.0f));
        return false;
    }

    protected void ShowFloatingDamageNumber(string text, Color color)
    {
        FloatingNumberHelper.ShowFloatingNumber(this, text, color);
    }

    protected bool IsAtHome()
    {
        return GlobalPosition.DistanceTo(HomePosition) <= Math.Max(0.0f, HomeReturnTolerance);
    }

    protected override bool HandleNoTarget(double delta)
    {
        if (IsAtHome())
        {
            _suppressTargetAcquisitionUntilHome = false;
            ResetPursuitStuckTracking();
            return false;
        }

        return TryMoveTowardDestination(HomePosition, 1.0f, CombatUnitState.ReturningHome, delta);
    }

    protected override void PrePhysicsProcess(double delta)
    {
        base.PrePhysicsProcess(delta);
        UpdatePursuitStuckEvade((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        UpdateReturnHomeRegeneration((float)delta);
    }

    protected bool TryApplyEnemyDamage(DamageInfo damageInfo, out int damage, out bool died)
    {
        damage = 0;
        died = false;

        if (IsDead)
            return false;

        if (!TryReactToDamageSource(damageInfo))
            return false;

        damage = Math.Max(1, damageInfo.Amount);
        CurrentHealth = Math.Max(0, CurrentHealth - damage);
        died = CurrentHealth <= 0;
        if (died)
            IsDead = true;

        return true;
    }

    private void UpdatePursuitStuckEvade(float delta)
    {
        if (_suppressTargetAcquisitionUntilHome)
        {
            ResetPursuitStuckTracking();
            return;
        }

        if (CurrentTarget == null ||
            CurrentState != CombatUnitState.PursuingTarget ||
            !IsUsingNavigationPath ||
            Velocity == Vector2.Zero)
        {
            ResetPursuitStuckTracking();
            return;
        }

        if (GlobalPosition.DistanceTo(LastNavigationPathPosition) <= PursuitStuckWaypointDistance)
        {
            ResetPursuitStuckTracking();
            return;
        }

        if (!ReferenceEquals(_trackedPursuitTarget, CurrentTarget))
        {
            _trackedPursuitTarget = CurrentTarget;
            _hasPursuitProgressPosition = true;
            _lastPursuitProgressPosition = GlobalPosition;
            _pursuitStuckTimer = 0.0f;
            return;
        }

        if (!_hasPursuitProgressPosition)
        {
            _hasPursuitProgressPosition = true;
            _lastPursuitProgressPosition = GlobalPosition;
            _pursuitStuckTimer = 0.0f;
            return;
        }

        if (GlobalPosition.DistanceTo(_lastPursuitProgressPosition) > PursuitStuckProgressThreshold)
        {
            _lastPursuitProgressPosition = GlobalPosition;
            _pursuitStuckTimer = 0.0f;
            return;
        }

        _pursuitStuckTimer += Math.Max(0.0f, delta);
        if (_pursuitStuckTimer < PursuitStuckTimeout)
            return;

        BeginEvadeReset(true);
    }

    private void ResetPursuitStuckTracking()
    {
        _hasPursuitProgressPosition = false;
        _lastPursuitProgressPosition = Vector2.Zero;
        _pursuitStuckTimer = 0.0f;
        _trackedPursuitTarget = null;
    }

    private void BeginEvadeReset(bool showEvadeText)
    {
        if (showEvadeText)
            ShowFloatingDamageNumber("EVADE", new Color(1.0f, 1.0f, 1.0f, 1.0f));

        _suppressTargetAcquisitionUntilHome = true;
        ClearTarget();
        ResetPursuitStuckTracking();
    }

    private bool IsEvadingHomeReturn()
    {
        return _suppressTargetAcquisitionUntilHome;
    }

    private void UpdateReturnHomeRegeneration(float delta)
    {
        if (!EnableReturnHomeRegeneration ||
            IsDead ||
            CurrentState != CombatUnitState.ReturningHome)
        {
            _returnHomeRegenerationTimer = 0.0f;
            return;
        }

        if (CurrentHealth >= ResolvedMaxHealth)
        {
            _returnHomeRegenerationTimer = 0.0f;
            return;
        }

        var regenerationRate = Math.Max(0.0f, ReturnHomeRegenerationFractionPerSecond);
        if (regenerationRate <= 0.0f)
            return;

        _returnHomeRegenerationTimer += Math.Max(0.0f, delta);
        var regenerationTicks = (int)MathF.Floor(_returnHomeRegenerationTimer);
        if (regenerationTicks <= 0)
            return;

        _returnHomeRegenerationTimer -= regenerationTicks;

        var regenerationPerTick = Math.Max(1, (int)MathF.Round(ResolvedMaxHealth * regenerationRate));
        var healAmount = Math.Min(regenerationTicks * regenerationPerTick, ResolvedMaxHealth - CurrentHealth);
        if (healAmount <= 0)
            return;

        CurrentHealth = Math.Min(ResolvedMaxHealth, CurrentHealth + healAmount);
        ShowFloatingHealingNumber(healAmount);
    }

    protected void ShowFloatingHealingNumber(int amount)
    {
        if (amount <= 0)
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, $"+{amount}", new Color(0.0f, 1.0f, 0.0f, 1.0f));
    }

    protected abstract int MaxHealthValue { get; }

    protected bool TryFinalizeDeathAnimation() => TryFinalizeDeathAnimation(DeathAnimation);

    protected bool TryPlayDeathAnimation() => TryPlayDeathAnimation(DeathAnimation, DisableCollisionOnDeath);
}
