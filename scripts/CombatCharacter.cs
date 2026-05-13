using Godot;

using System;
using System.Collections.Generic;

public abstract partial class CombatCharacter : AnimatedCharacter, IFactionMember, IHealable
{
    [Signal]
    public delegate void DiedEventHandler();

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float CritRate { get; set; } = 0.50f;

    [Export(PropertyHint.Range, "0,10,0.05")]
    public float CritDamage { get; set; } = 1.0f;

    public float ResolvedCritRate => Math.Clamp(CritRate, 0.0f, 1.0f);
    public float ResolvedCritDamage => Math.Max(0.0f, CritDamage);

    private bool _healthStateChangedBound;
    private bool _statusEffectsChangedBound;
    private bool _lastKnownIsDead;

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
        _lastKnownIsDead = IsDead;
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

    protected bool TryApplyDamageToHealth(Damage damageInfo, bool setReceiverTargetToSource, out int appliedDamage)
    {
        appliedDamage = 0;
        if (damageInfo == null || IsDead)
            return false;

        var remainingDamage = ResolveRemainingDamageAfterAbsorption(damageInfo, out var fullyAbsorbingAbsorber);
        damageInfo.RegisterHit(this, setReceiverTargetToSource);
        if (remainingDamage <= 0)
        {
            if (fullyAbsorbingAbsorber != null)
            {
                var origin = fullyAbsorbingAbsorber is Node2D absorberNode && GodotObject.IsInstanceValid(absorberNode)
                    ? absorberNode
                    : this;
                FloatingText.ShowNeutral("ABSORB", origin);
            }
            return false;
        }

        appliedDamage = HealthStateNode.ApplyDamage(remainingDamage);
        return appliedDamage > 0;
    }

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
        var wasDead = _lastKnownIsDead;
        _lastKnownIsDead = IsDead;

        OnHealthStateChanged();

        if (!wasDead && _lastKnownIsDead)
            EmitSignal(SignalName.Died);
    }

    private void HandleStatusEffectsChanged()
    {
        OnStatusEffectsChanged();
    }

    private int ResolveRemainingDamageAfterAbsorption(Damage damageInfo, out IDamageAbsorber fullyAbsorbingAbsorber)
    {
        fullyAbsorbingAbsorber = null;
        var remainingDamage = Math.Max(0, damageInfo?.Amount ?? 0);
        if (remainingDamage <= 0)
            return 0;

        var absorbers = new List<IDamageAbsorber>();
        CollectDamageAbsorbers(this, absorbers);
        foreach (var absorber in absorbers)
        {
            remainingDamage = Math.Clamp(absorber.AbsorbDamage(remainingDamage), 0, remainingDamage);
            if (remainingDamage <= 0)
            {
                fullyAbsorbingAbsorber = absorber;
                return 0;
            }
        }

        return remainingDamage;
    }

    private static void CollectDamageAbsorbers(Node node, List<IDamageAbsorber> absorbers)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is not Node childNode)
                continue;

            if (childNode is IDamageAbsorber absorber)
                absorbers.Add(absorber);

            CollectDamageAbsorbers(childNode, absorbers);
        }
    }
}
