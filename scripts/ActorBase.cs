using Godot;

using System;

public abstract partial class ActorBase : CombatUnitBase, IFactionMember
{
    private enum RegenerationPhase
    {
        None,
        ReturningHome,
        Idle,
    }

    private static readonly Vector2 ActorHealthLabelOffset = new Vector2(-24.0f, -36.0f);
    private static readonly Vector2 ActorHealthLabelSize = new Vector2(48.0f, 16.0f);

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

    protected Vector2 HomePosition { get; private set; }
    protected int CurrentHealth { get; private set; }
    protected bool IsDead { get; private set; }
    protected int ResolvedMaxHealth => Math.Max(1, MaxHealthValue);
    public abstract Faction Faction { get; }

    private float _returnHomeRegenerationTimer;
    private RegenerationPhase _regenerationPhase;
    private Label _healthLabel;
    private ActorAI _actorAI;

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

    protected virtual void OnReachedHomeWithoutTarget() { }

    protected virtual void OnActorPrePhysicsProcess(double delta) { }

    protected virtual void OnActorExitTree() { }

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

    protected bool TryFinalizeDeathAnimation() => TryFinalizeDeathAnimation(DeathAnimation);

    protected bool TryPlayDeathAnimation() => TryPlayDeathAnimation(DeathAnimation, DisableCollisionOnDeath);

    protected abstract int MaxHealthValue { get; }

    protected override void PrePhysicsProcess(double delta)
    {
        _actorAI?.Update(delta);
        OnActorPrePhysicsProcess(delta);
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

        if (CurrentState == CombatUnitState.ReturningHome && EnableReturnHomeRegeneration)
            return RegenerationPhase.ReturningHome;

        if (CurrentState == CombatUnitState.Idle &&
            CurrentTarget == null &&
            EnableIdleRegeneration)
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
}
