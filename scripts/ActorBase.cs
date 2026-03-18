using Godot;

using System;

public abstract partial class ActorBase : CombatUnitBase, IFactionMember, IAggressiveCombatActorAIHost
{
    private enum RegenerationPhase
    {
        None,
        ReturningHome,
        Idle,
    }

    private static readonly Vector2 ActorHealthLabelOffset = new Vector2(-24.0f, -36.0f);
    private static readonly Vector2 ActorHealthLabelSize = new Vector2(48.0f, 16.0f);
    private const float PursuitStuckProgressThreshold = 1.0f;
    private const float PursuitStuckTimeout = 0.6f;
    private const float PursuitStuckWaypointDistance = 8.0f;

    [Export]
    public StringName DeathAnimation { get; set; } = "falling-back-death";

    [Export]
    public bool DisableCollisionOnDeath { get; set; } = true;

    [Export]
    public float HomeReturnTolerance { get; set; } = 4.0f;

    [Export]
    public bool EnableReturnHomeRegeneration { get; set; } = true;

    [Export]
    public float ReturnHomeRegenerationFractionPerSecond { get; set; } = 0.1f;

    [Export]
    public bool EnableIdleRegeneration { get; set; } = true;

    [Export]
    public float IdleRegenerationFractionPerSecond { get; set; } = 0.01f;

    [Export]
    public float IdleRegenerationIntervalSeconds { get; set; } = 5.0f;

    [Export]
    public NodePath InitialTargetPath { get; set; } = new NodePath("../Player");

    [Export]
    public float AggroAcquisitionRange { get; set; } = 150.0f;

    [Export]
    public float AggroLossRange { get; set; } = 220.0f;

    [Export]
    public bool EvadeOnAggroLoss { get; set; } = true;

    [Export]
    public bool IgnoreDamageWhileEvading { get; set; } = true;

    protected Vector2 HomePosition { get; private set; }
    protected int CurrentHealth { get; private set; }
    protected bool IsDead { get; private set; }
    protected int ResolvedMaxHealth => Math.Max(1, MaxHealthValue);
    public abstract Faction Faction { get; }

    private float _returnHomeRegenerationTimer;
    private RegenerationPhase _regenerationPhase;
    private Label _healthLabel;
    private ActorAI _actorAI;
    private bool _hasPursuitProgressPosition;
    private Vector2 _lastPursuitProgressPosition;
    private float _pursuitStuckTimer;
    private Node2D _trackedPursuitTarget;
    private bool _suppressTargetAcquisitionUntilHome;

    protected void InitializeActor(
        AnimatedSprite2D animatedSprite,
        CollisionShape2D collisionShape,
        NavigationAgent2D navigationAgent = null)
    {
        InitializeCombatUnit(animatedSprite, collisionShape, navigationAgent);
        CurrentHealth = ResolvedMaxHealth;
        IsDead = false;
        HomePosition = GlobalPosition;
        EnsureHealthLabel();
        UpdateHealthLabel();
    }

    protected void InitializeAggressiveActor(
        AnimatedSprite2D animatedSprite,
        CollisionShape2D collisionShape,
        NavigationAgent2D navigationAgent = null,
        string actorName = null)
    {
        InitializeActor(animatedSprite, collisionShape, navigationAgent);
        ApplyFactionCombatGroup();
        TryAcquireInitialAggressiveTarget(actorName);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        UpdateReturnHomeRegeneration((float)delta);
    }

    public override void _ExitTree()
    {
        _actorAI?.Shutdown();
        OnActorExitTree();
    }

    protected bool IsAtHome()
    {
        return GlobalPosition.DistanceTo(HomePosition) <= Math.Max(0.0f, HomeReturnTolerance);
    }

    protected override bool HandleNoTarget(double delta)
    {
        if (IsAtHome())
        {
            OnReachedHomeWithoutTarget();
            return false;
        }

        return TryMoveTowardDestination(HomePosition, 1.0f, CombatUnitState.ReturningHome, delta);
    }

    protected virtual void OnReachedHomeWithoutTarget()
    {
        if (!ShouldUseAggressiveCombatSupport())
            return;

        _suppressTargetAcquisitionUntilHome = false;
        ResetAggressivePursuitStuckTracking();
    }

    protected virtual void OnActorPrePhysicsProcess(double delta) { }

    protected virtual void OnActorExitTree() { }

    protected virtual bool ShouldUseAggressiveCombatSupport() => false;

    protected void SetActorAI(ActorAI actorAI)
    {
        if (ReferenceEquals(_actorAI, actorAI))
            return;

        _actorAI?.Shutdown();
        _actorAI = actorAI;
        _actorAI?.Initialize(this);
    }

    protected bool TryAcquireTargetWithAI()
    {
        return _actorAI != null && _actorAI.TryAcquireTarget();
    }

    protected bool TryHandleNoTargetWithAI(double delta)
    {
        return _actorAI != null && _actorAI.TryHandleNoTarget(delta);
    }

    protected override Vector2 GetDesiredMovementTarget(Vector2 targetPosition, double delta)
    {
        if (_actorAI != null &&
            _actorAI.TryGetDesiredMovementTarget(targetPosition, delta, out var desiredMovementTarget))
        {
            return desiredMovementTarget;
        }

        return GetActorDesiredMovementTarget(targetPosition, delta);
    }

    protected void SetCurrentHealth(int value)
    {
        CurrentHealth = Math.Clamp(value, 0, ResolvedMaxHealth);
        UpdateHealthLabel();
    }

    protected void SetIsDead(bool value)
    {
        IsDead = value;
    }

    protected void ShowFloatingHealingNumber(int amount)
    {
        if (amount <= 0)
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, $"+{amount}", new Color(0.0f, 1.0f, 0.0f, 1.0f));
    }

    protected void ShowFloatingDamageNumber(string text, Color color)
    {
        FloatingNumberHelper.ShowFloatingNumber(this, text, color);
    }

    protected bool TryFinalizeDeathAnimation() => TryFinalizeDeathAnimation(DeathAnimation);

    protected bool TryPlayDeathAnimation() => TryPlayDeathAnimation(DeathAnimation, DisableCollisionOnDeath);

    protected void ApplyFactionCombatGroup()
    {
        Factions.ApplyCombatGroup(this, Faction);
    }

    protected abstract int MaxHealthValue { get; }

    protected virtual Vector2 GetActorDesiredMovementTarget(Vector2 targetPosition, double delta) => targetPosition;

    protected virtual bool ShouldUseReturnHomeRegeneration() => EnableReturnHomeRegeneration;

    protected virtual bool ShouldUseIdleRegeneration() => EnableIdleRegeneration;

    protected override void PrePhysicsProcess(double delta)
    {
        _actorAI?.Update(delta);
        UpdateAggressivePursuitStuckEvade((float)delta);
        OnActorPrePhysicsProcess(delta);
    }

    protected override bool ShouldLoseCurrentTarget(Node2D target)
    {
        if (!ShouldUseAggressiveCombatSupport())
            return base.ShouldLoseCurrentTarget(target);

        var shouldLoseTarget = !IsTargetWithinLossRange(target);
        if (shouldLoseTarget && EvadeOnAggroLoss)
            BeginEvadeReset(false);

        return shouldLoseTarget;
    }

    protected bool CanAcquireHostileTarget(Node2D target)
    {
        return target is IAttackable &&
               target is ITargetable targetable &&
               targetable.CanBeTargeted &&
               IsHostileTarget(target) &&
               IsTargetWithinAcquisitionRange(target);
    }

    protected bool IsTargetWithinLossRange(Node2D target)
    {
        return IsTargetWithinRange(target, Math.Max(AggroLossRange, AggroAcquisitionRange));
    }

    protected bool TryApplyAggressiveActorDamage(DamageInfo damageInfo, out int damage, out bool died)
    {
        damage = 0;
        died = false;

        if (IsDead)
            return false;

        if (!TryReactToAggressiveDamageSource(damageInfo))
            return false;

        damage = Math.Max(1, damageInfo.Amount);
        SetCurrentHealth(Math.Max(0, CurrentHealth - damage));
        died = CurrentHealth <= 0;
        if (died)
            SetIsDead(true);

        return true;
    }

    public bool ShouldAttemptAggressiveTargetAcquisition()
    {
        return ShouldUseAggressiveCombatSupport() && !_suppressTargetAcquisitionUntilHome && !IsDead;
    }

    public Node2D SelectAggressiveTargetCandidate()
    {
        if (!ShouldUseAggressiveCombatSupport())
            return null;

        return TargetingHelper.FindClosestHostileTarget(
            this,
            Faction,
            node => node is Node2D targetNode && CanAcquireHostileTarget(targetNode));
    }

    public void ApplyAggressiveTargetCandidate(Node2D target)
    {
        if (!ShouldUseAggressiveCombatSupport() || target == null || !CanAcquireHostileTarget(target))
            return;

        SetTarget(target);
        ResetAggressivePursuitStuckTracking();
    }

    private void UpdateReturnHomeRegeneration(float delta)
    {
        var regenerationPhase = GetRegenerationPhase();
        if (regenerationPhase == RegenerationPhase.None || IsDead)
        {
            _returnHomeRegenerationTimer = 0.0f;
            _regenerationPhase = RegenerationPhase.None;
            return;
        }

        if (_regenerationPhase != regenerationPhase)
        {
            _returnHomeRegenerationTimer = 0.0f;
            _regenerationPhase = regenerationPhase;
        }

        if (CurrentHealth >= ResolvedMaxHealth)
        {
            _returnHomeRegenerationTimer = 0.0f;
            return;
        }

        var regenerationRate = regenerationPhase == RegenerationPhase.ReturningHome
            ? Math.Max(0.0f, ReturnHomeRegenerationFractionPerSecond)
            : Math.Max(0.0f, IdleRegenerationFractionPerSecond);
        if (regenerationRate <= 0.0f)
            return;

        var regenerationInterval = regenerationPhase == RegenerationPhase.ReturningHome
            ? 1.0f
            : Math.Max(0.01f, IdleRegenerationIntervalSeconds);

        _returnHomeRegenerationTimer += Math.Max(0.0f, delta);
        var regenerationTicks = (int)MathF.Floor(_returnHomeRegenerationTimer / regenerationInterval);
        if (regenerationTicks <= 0)
            return;

        _returnHomeRegenerationTimer -= regenerationTicks * regenerationInterval;

        var regenerationPerTick = Math.Max(1, (int)MathF.Round(ResolvedMaxHealth * regenerationRate));
        var healAmount = Math.Min(regenerationTicks * regenerationPerTick, ResolvedMaxHealth - CurrentHealth);
        if (healAmount <= 0)
            return;

        SetCurrentHealth(Math.Min(ResolvedMaxHealth, CurrentHealth + healAmount));
        ShowFloatingHealingNumber(healAmount);
    }

    private RegenerationPhase GetRegenerationPhase()
    {
        if (IsDead)
            return RegenerationPhase.None;

        if (CurrentState == CombatUnitState.ReturningHome && ShouldUseReturnHomeRegeneration())
            return RegenerationPhase.ReturningHome;

        if (CurrentState == CombatUnitState.Idle &&
            CurrentTarget == null &&
            ShouldUseIdleRegeneration())
        {
            return RegenerationPhase.Idle;
        }

        return RegenerationPhase.None;
    }

    private void EnsureHealthLabel()
    {
        if (_healthLabel != null)
            return;

        _healthLabel = new Label
        {
            Name = "HealthLabel",
            Position = ActorHealthLabelOffset,
            Size = ActorHealthLabelSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 10
        };
        _healthLabel.AddThemeFontSizeOverride("font_size", 12);
        _healthLabel.AddThemeColorOverride("font_color", Colors.White);
        _healthLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _healthLabel.AddThemeConstantOverride("outline_size", 2);
        AddChild(_healthLabel);
    }

    private void UpdateHealthLabel()
    {
        if (_healthLabel == null)
            return;

        _healthLabel.Text = $"{CurrentHealth}/{ResolvedMaxHealth}";
        _healthLabel.AddThemeColorOverride("font_color", FactionColors.Resolve(Faction));
    }

    private void TryAcquireInitialAggressiveTarget(string actorName)
    {
        if (!ShouldUseAggressiveCombatSupport())
            return;

        var resolvedTarget = CurrentTarget;
        if (resolvedTarget == null)
        {
            if (!InitialTargetPath.IsEmpty && HasNode(InitialTargetPath))
                resolvedTarget = GetNode<Node2D>(InitialTargetPath);
            else
                resolvedTarget = GetParent()?.GetNodeOrNull<Node2D>("Player");
        }

        if (resolvedTarget != null && CanAcquireHostileTarget(resolvedTarget))
        {
            SetTarget(resolvedTarget);
            return;
        }

        if (resolvedTarget != null && actorName != null)
            GD.PrintErr($"{actorName} did not acquire initial target (not in aggro range).");
    }

    private bool IsTargetWithinAcquisitionRange(Node2D target)
    {
        return IsTargetWithinRange(target, Math.Max(0.0f, AggroAcquisitionRange));
    }

    private bool IsTargetWithinRange(Node2D target, float range)
    {
        if (target == null)
            return false;

        return GlobalPosition.DistanceTo(target.GlobalPosition) <= range;
    }

    private bool IsHostileTarget(Node target)
    {
        return Faction != null && Faction.IsHostileTo(Factions.ResolveForNode(target));
    }

    private bool TryReactToAggressiveDamageSource(DamageInfo damageInfo)
    {
        if (ShouldUseAggressiveCombatSupport() && IsEvadingHomeReturn() && IgnoreDamageWhileEvading)
        {
            ShowFloatingDamageNumber("EVADE", new Color(1.0f, 1.0f, 1.0f, 1.0f));
            return false;
        }

        if (damageInfo.Source is not Node2D sourceNode)
            return true;

        if (!ShouldUseAggressiveCombatSupport())
            return true;

        if (!IsHostileTarget(sourceNode))
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

    private void UpdateAggressivePursuitStuckEvade(float delta)
    {
        if (!ShouldUseAggressiveCombatSupport())
            return;

        if (_suppressTargetAcquisitionUntilHome)
        {
            ResetAggressivePursuitStuckTracking();
            return;
        }

        if (CurrentTarget == null ||
            CurrentState != CombatUnitState.PursuingTarget ||
            !IsUsingNavigationPath ||
            Velocity == Vector2.Zero)
        {
            ResetAggressivePursuitStuckTracking();
            return;
        }

        if (GlobalPosition.DistanceTo(LastNavigationPathPosition) <= PursuitStuckWaypointDistance)
        {
            ResetAggressivePursuitStuckTracking();
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

    private void ResetAggressivePursuitStuckTracking()
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
        ResetAggressivePursuitStuckTracking();
    }

    private bool IsEvadingHomeReturn()
    {
        return _suppressTargetAcquisitionUntilHome;
    }
}
