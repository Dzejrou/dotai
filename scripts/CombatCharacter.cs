using Godot;

using System;
using System.Collections.Generic;

public abstract partial class CombatCharacter : AnimatedCharacter, IFactionMember, IHealable
{
    [Signal]
    public delegate void DiedEventHandler();

    public float ResolvedCritRate => StatsNode != null ? Math.Clamp(StatsNode.ResolvedCritRate, 0.0f, 1.0f) : 0.0f;
    public float ResolvedCritDamage => StatsNode?.ResolvedCritDamage ?? 0.0f;
    public float ResolvedPower => StatsNode?.ResolvedPower ?? 0.0f;
    public int ResolvedMP5 => StatsNode?.ResolvedMP5 ?? 0;
    public int ResolvedHaste => StatsNode?.ResolvedHaste ?? 0;
    public float ResolvedHastePercent => StatsNode?.ResolvedHastePercent ?? 0.0f;
    public float ApplyHasteToDuration(float baseSeconds) =>
        StatsNode?.ApplyHasteToDuration(baseSeconds) ?? Math.Max(0.0f, baseSeconds);
    public float ResolveDamageBonus(DamageSchool school) => StatsNode?.ResolveDamageBonus(school) ?? 0.0f;
    public float ResolveResistance(DamageSchool school) => StatsNode?.ResolveResistance(school) ?? 0.0f;

    private bool _healthStateChangedBound;
    private bool _statusEffectsChangedBound;
    private bool _lastKnownIsDead;

    protected HealthState HealthStateNode { get; private set; }
    protected CombatState CombatStateNode { get; private set; }
    protected FactionState FactionStateNode { get; private set; }
    protected ManaState ManaStateNode { get; private set; }
    protected StatusEffectController StatusEffectControllerNode { get; private set; }
    protected Stats StatsNode { get; private set; }

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
        StatsNode = GetNodeOrNull<Stats>("Stats");
        if (StatsNode == null)
            GD.PushWarning($"{GetPath()}: missing Stats child; falling back to defaults (MaxHealth=1, Power=0).");

        HealthStateNode = GetNode<HealthState>("HealthState");
        HealthStateNode.Initialize(StatsNode?.ResolvedMaxHealth ?? 1);
        FactionStateNode = GetNode<FactionState>("FactionState");
        ManaStateNode = requireManaState
            ? GetNode<ManaState>("ManaState")
            : GetNodeOrNull<ManaState>("ManaState");
        ManaStateNode?.Initialize(StatsNode?.ResolvedMaxMana ?? 0);
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

        damageInfo.ResolveCritForReceiver(this);
        damageInfo.ApplyReceiverResistance(ResolveResistance(damageInfo.School));

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
