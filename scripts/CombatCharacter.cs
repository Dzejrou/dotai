using Godot;

public abstract partial class CombatCharacter : AnimatedCharacter, IFactionMember, IHealable
{
    private bool _healthStateChangedBound;
    private bool _statusEffectsChangedBound;

    protected HealthState HealthStateNode { get; private set; }
    protected CombatState CombatStateNode { get; private set; }
    protected FactionState FactionStateNode { get; private set; }
    protected ManaState ManaStateNode { get; private set; }
    protected StatusEffectController StatusEffectControllerNode { get; private set; }

    public CombatState Combat => CombatStateNode;
    public bool InCombat => CombatStateNode?.InCombat ?? false;
    public Faction Faction => FactionStateNode?.Current;
    public FactionState FactionState => FactionStateNode;
    public ManaState ManaState => ManaStateNode;
    public int CurrentHealth => HealthStateNode?.Current ?? 0;
    public int MaxHealthValue => HealthStateNode?.Max ?? 0;
    public int MaxHealableHealth => MaxHealthValue;
    public int CurrentMana => ManaStateNode?.Current ?? 0;
    public int MaxManaValue => ManaStateNode?.Max ?? 0;
    public bool IsDead => HealthStateNode?.IsDead ?? false;
    public bool CanReceiveHealing => !IsDead && CurrentHealth < MaxHealableHealth;
    public virtual bool CanMove => StatusEffectControllerNode?.CanMove() ?? true;
    public virtual float MovementSpeedMultiplier => StatusEffectControllerNode?.GetMovementSpeedMultiplier() ?? 1.0f;
    public virtual float AttackSpeedMultiplier => StatusEffectControllerNode?.GetAttackSpeedMultiplier() ?? 1.0f;
    public virtual float CastSpeedMultiplier => StatusEffectControllerNode?.GetCastSpeedMultiplier() ?? 1.0f;

    public override void _EnterTree()
    {
        EnsureModelChangeSubscriptions();
    }

    public override void _ExitTree()
    {
        DisconnectModelChangeSubscriptions();
    }

    protected void InitializeCombatCharacter(bool requireManaState = false)
    {
        CombatStateNode = GetNode<CombatState>("CombatState");
        HealthStateNode = GetNode<HealthState>("HealthState");
        HealthStateNode.Initialize();
        FactionStateNode = GetNode<FactionState>("FactionState");
        ManaStateNode = requireManaState
            ? GetNode<ManaState>("ManaState")
            : GetNodeOrNull<ManaState>("ManaState");
        ManaStateNode?.Initialize();
        EnsureModelChangeSubscriptions();
    }

    protected void ResetCombatState()
    {
        CombatStateNode?.ClearTarget();
        CombatStateNode?.ExitCombat();
    }

    protected void SetStatusEffectController(StatusEffectController statusEffectController)
    {
        if (!ReferenceEquals(StatusEffectControllerNode, statusEffectController))
            DisconnectStatusEffectsChanged();

        StatusEffectControllerNode = statusEffectController;
        EnsureStatusEffectsChangedConnected();
    }

    public virtual void RestoreCombatState(bool clearStatusEffects = true)
    {
        HealthStateNode?.RestoreToFull();

        if (clearStatusEffects)
            StatusEffectControllerNode?.ClearAllEffects();
    }

    protected virtual void OnHealthStateChanged() { }

    protected virtual void OnStatusEffectsChanged() { }

    public abstract void ApplyHealing(Healing healing);

    private void EnsureModelChangeSubscriptions()
    {
        EnsureHealthStateChangedConnected();
        EnsureStatusEffectsChangedConnected();
    }

    private void DisconnectModelChangeSubscriptions()
    {
        DisconnectHealthStateChanged();
        DisconnectStatusEffectsChanged();
    }

    private void EnsureHealthStateChangedConnected()
    {
        if (_healthStateChangedBound || HealthStateNode == null)
            return;

        HealthStateNode.Connect(HealthState.SignalName.Changed, new Callable(this, nameof(HandleHealthStateChanged)));
        _healthStateChangedBound = true;
    }

    private void DisconnectHealthStateChanged()
    {
        if (!_healthStateChangedBound || HealthStateNode == null || !GodotObject.IsInstanceValid(HealthStateNode))
            return;

        var callable = new Callable(this, nameof(HandleHealthStateChanged));
        if (HealthStateNode.IsConnected(HealthState.SignalName.Changed, callable))
            HealthStateNode.Disconnect(HealthState.SignalName.Changed, callable);

        _healthStateChangedBound = false;
    }

    private void EnsureStatusEffectsChangedConnected()
    {
        if (_statusEffectsChangedBound || StatusEffectControllerNode == null)
            return;

        StatusEffectControllerNode.Connect(
            StatusEffectController.SignalName.Changed,
            new Callable(this, nameof(HandleStatusEffectsChanged)));
        _statusEffectsChangedBound = true;
    }

    private void DisconnectStatusEffectsChanged()
    {
        if (!_statusEffectsChangedBound ||
            StatusEffectControllerNode == null ||
            !GodotObject.IsInstanceValid(StatusEffectControllerNode))
        {
            return;
        }

        var callable = new Callable(this, nameof(HandleStatusEffectsChanged));
        if (StatusEffectControllerNode.IsConnected(StatusEffectController.SignalName.Changed, callable))
            StatusEffectControllerNode.Disconnect(StatusEffectController.SignalName.Changed, callable);

        _statusEffectsChangedBound = false;
    }

    private void HandleHealthStateChanged()
    {
        OnHealthStateChanged();
    }

    private void HandleStatusEffectsChanged()
    {
        OnStatusEffectsChanged();
    }
}
