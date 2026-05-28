using Godot;

using System;
using System.Collections.Generic;

public abstract partial class CombatCharacter : AnimatedCharacter, IFactionMember, IHealable
{
    [Signal]
    public delegate void DiedEventHandler();

    private int _level = 1;

    [Export]
    public int Level
    {
        get => _level;
        set
        {
            var clamped = Math.Max(1, value);
            if (_level == clamped)
                return;

            _level = clamped;
            OnLevelChanged(_level);
        }
    }

    protected virtual void OnLevelChanged(int newLevel) { }

    protected virtual int ResolveScaledBaseMaxHealth(int baseMaxHealth) => baseMaxHealth;

    protected virtual float ResolveScaledBasePower(float basePower) => basePower;

    public float ResolvedCritRate =>
        StatsNode != null
            ? Math.Clamp(StatsNode.ResolvedCritRate + GetEquipmentBonus(EquipmentStatIds.CritRate), 0.0f, 1.0f)
            : 0.0f;
    public float ResolvedCritDamage =>
        Math.Max(0.0f, (StatsNode?.ResolvedCritDamage ?? 0.0f) + GetEquipmentBonus(EquipmentStatIds.CritDamage));
    public float ResolvedPower =>
        Math.Max(0.0f, ResolveScaledBasePower(StatsNode?.ResolvedPower ?? 0.0f) + GetEquipmentBonus(EquipmentStatIds.Power));
    public int ResolvedMP5 =>
        Math.Max(0, (StatsNode?.ResolvedMP5 ?? 0) + GetEquipmentIntBonus(EquipmentStatIds.MP5));
    public int ResolvedHaste =>
        Math.Max(0, (StatsNode?.ResolvedHaste ?? 0) + GetEquipmentIntBonus(EquipmentStatIds.Haste));
    public float ResolvedHastePercent => ResolvedHaste / 2000.0f;
    public float ApplyHasteToDuration(float baseSeconds)
    {
        var clamped = Math.Max(0.0f, baseSeconds);
        if (clamped <= 0.0f)
            return 0.0f;

        return clamped / (1.0f + ResolvedHastePercent);
    }
    public float ResolveDamageBonus(DamageSchool school) =>
        (StatsNode?.ResolveDamageBonus(school) ?? 0.0f)
        + GetEquipmentBonus(EquipmentStatIds.DamageBonusFor(school))
        + GetEquipmentBonus(EquipmentStatIds.DamageBonus);
    public float ResolveResistance(DamageSchool school) =>
        (StatsNode?.ResolveResistance(school) ?? 0.0f) + GetEquipmentBonus(EquipmentStatIds.ResistanceFor(school));
    public int ResolvedMaxHealth =>
        Math.Max(1, ResolveScaledBaseMaxHealth(StatsNode?.ResolvedMaxHealth ?? 1) + GetEquipmentIntBonus(EquipmentStatIds.MaxHealth));
    public int ResolvedMaxMana =>
        Math.Max(0, (StatsNode?.ResolvedMaxMana ?? 0) + GetEquipmentIntBonus(EquipmentStatIds.MaxMana));

    public int BaseMaxHealth => StatsNode?.ResolvedMaxHealth ?? 1;
    public int BaseMaxMana => StatsNode?.ResolvedMaxMana ?? 0;
    public int BaseMP5 => StatsNode?.ResolvedMP5 ?? 0;
    public float BasePower => StatsNode?.ResolvedPower ?? 0.0f;
    public float BaseCritRate => StatsNode?.ResolvedCritRate ?? 0.0f;
    public float BaseCritDamage => StatsNode?.ResolvedCritDamage ?? 0.0f;
    public int BaseHaste => StatsNode?.ResolvedHaste ?? 0;
    public float BaseMovementSpeedMultiplier => StatsNode?.ResolvedMovementSpeedMultiplier ?? 1.0f;

    public float ResolvedGenericDamageBonus =>
        EquipmentControllerNode?.ResolveStatBonus(EquipmentStatIds.DamageBonus) ?? 0.0f;

    private bool _healthStateChangedBound;
    private bool _statusEffectsChangedBound;
    private bool _lastKnownIsDead;

    protected HealthState HealthStateNode { get; private set; }
    protected CombatState CombatStateNode { get; private set; }
    protected FactionState FactionStateNode { get; private set; }
    protected ManaState ManaStateNode { get; private set; }
    protected StatusEffectController StatusEffectControllerNode { get; private set; }
    protected Stats StatsNode { get; private set; }
    public EquipmentController EquipmentControllerNode { get; private set; }

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
    public virtual float MovementSpeedMultiplier
    {
        get
        {
            var statsMultiplier = StatsNode?.ResolvedMovementSpeedMultiplier ?? 1.0f;
            var equipmentBonus = GetEquipmentBonus(EquipmentStatIds.MovementSpeedMultiplier);
            var statusMultiplier = StatusEffectControllerNode?.GetMovementSpeedMultiplier() ?? 1.0f;
            return Math.Max(0.0f, (statsMultiplier + equipmentBonus) * statusMultiplier);
        }
    }
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

        EquipmentControllerNode = GetNodeOrNull<EquipmentController>("EquipmentController");

        HealthStateNode = GetNode<HealthState>("HealthState");
        HealthStateNode.Initialize(ResolvedMaxHealth);
        FactionStateNode = GetNode<FactionState>("FactionState");
        ManaStateNode = requireManaState
            ? GetNode<ManaState>("ManaState")
            : GetNodeOrNull<ManaState>("ManaState");
        ManaStateNode?.Initialize(ResolvedMaxMana);
        _lastKnownIsDead = IsDead;
        EnsureModelChangeSubscriptions();
    }

    private float GetEquipmentBonus(string statId)
    {
        return EquipmentControllerNode?.ResolveStatBonus(statId) ?? 0.0f;
    }

    private int GetEquipmentIntBonus(string statId)
    {
        return EquipmentControllerNode?.ResolveIntBonus(statId) ?? 0;
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

        if (GameSettings.GodMode && this is Player)
        {
            CombatLog.Debug($"God mode blocks damage to {Name}.");
            return false;
        }

        damageInfo.ResolveCritForReceiver(this);
        damageInfo.ApplyReceiverResistance(ResolveResistance(damageInfo.School));

        var preAbsorptionAmount = damageInfo.Amount;
        var remainingDamage = ResolveRemainingDamageAfterAbsorption(damageInfo, out var fullyAbsorbingAbsorber);
        damageInfo.RegisterHit(this, setReceiverTargetToSource);

        var oneHitKill = GameSettings.OneHitKill && damageInfo.Source is Player && this is not Player;
        if (oneHitKill)
            remainingDamage = Math.Max(remainingDamage, Math.Max(1, CurrentHealth));

        if (remainingDamage <= 0)
        {
            if (fullyAbsorbingAbsorber != null)
            {
                var origin = fullyAbsorbingAbsorber is Node2D absorberNode && GodotObject.IsInstanceValid(absorberNode)
                    ? absorberNode
                    : this;
                FloatingText.ShowNeutral("ABSORB", origin);
                CombatLog.Absorb(this, preAbsorptionAmount);
            }
            return false;
        }

        appliedDamage = HealthStateNode.ApplyDamage(remainingDamage);
        if (appliedDamage > 0)
        {
            if (oneHitKill)
                CombatLog.Debug($"One-hit kill downs {CombatLog.ResolveName(this)}.");
            CombatLog.Damage(this, damageInfo.Source, appliedDamage, damageInfo.IsCritical);
        }

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
