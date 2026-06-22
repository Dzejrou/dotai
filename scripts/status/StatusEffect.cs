using Godot;

using System;

public enum StatusCategory
{
    Buff,
    Debuff,
}

// Explicit lifetime model. Timed effects expire once their duration elapses through the
// physics tick; Permanent effects never expire that way and only end through forced
// removal (actor reset, encounter teardown, death cleanup) or refresh-driven replacement.
public enum StatusLifetime
{
    Timed,
    Permanent,
}

[GlobalClass]
public abstract partial class StatusEffect : Node
{
    [Export]
    public StatusLifetime Lifetime { get; set; } = StatusLifetime.Timed;

    // Whether a future-facing dispel attempt may remove this effect. Undispellable effects
    // (e.g. boss Enrage) survive dispels but are still removed by forced cleanup.
    [Export]
    public bool Dispellable { get; set; } = true;

    [Export]
    public float DurationSeconds { get; set; } = 5.0f;

    [Export]
    public float TickIntervalSeconds { get; set; } = 1.0f;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public string FloatingTextLabel { get; set; } = string.Empty;

    [Export]
    public StatusCategory Category { get; set; } = StatusCategory.Debuff;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ApplyChance { get; set; } = 1.0f;

    public float ResolvedApplyChance => Math.Clamp(ApplyChance, 0.0f, 1.0f);

    public bool IsPermanent => Lifetime == StatusLifetime.Permanent;

    public virtual bool IsUniqueByStatusKey => false;
    public virtual bool PreventsMovement => false;
    public virtual float MovementSpeedMultiplier => 1.0f;
    public virtual float AttackSpeedMultiplier => 1.0f;
    public virtual float CastSpeedMultiplier => 1.0f;

    // Stat-modifier hooks. Default to no-op so ordinary statuses contribute nothing;
    // StatModifierEffect overrides these and StatusEffectController aggregates them into
    // CombatCharacter's resolved stats (without mutating base Stats). Percent values are
    // additive and summed across active effects before being applied once; flat values are
    // summed directly.
    public virtual float MaxHealthPercentModifier => 0.0f;
    public virtual float PowerPercentModifier => 0.0f;
    public virtual int HasteFlatModifier => 0;
    public virtual float DamageBonusFlatModifier => 0.0f;
    public virtual float ResolveResistanceFlatModifier(DamageSchool school) => 0.0f;

    public Node2D OwnerNode { get; private set; }
    public Node2D Source { get; private set; }
    public ulong SourceInstanceId { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public float NextTickSeconds { get; private set; }
    public bool IsActive { get; private set; }

    private float _expiresAtSeconds;

    public abstract StringName StatusKey { get; }

    internal void Start(Node2D owner, Node2D source, ulong sourceInstanceId)
    {
        OwnerNode = owner;
        Source = source;
        SourceInstanceId = sourceInstanceId;
        IsActive = true;
        ResetTiming();
        OnApplied();
    }

    internal void Refresh(StatusEffect replacement, Node2D source, ulong sourceInstanceId)
    {
        var previousTickInterval = TickIntervalSeconds;
        var previousNextTickSeconds = NextTickSeconds;
        var currentTime = ElapsedSeconds;

        CopyConfigurationFrom(replacement);
        Source = source;
        SourceInstanceId = sourceInstanceId;

        // Honor the (possibly just-copied) lifetime: a refreshed permanent effect must not
        // start counting toward an expiry.
        _expiresAtSeconds = IsPermanent
            ? float.PositiveInfinity
            : currentTime + Math.Max(0.0f, DurationSeconds);
        NextTickSeconds = CalculateRefreshedNextTickSeconds(
            currentTime,
            previousTickInterval,
            previousNextTickSeconds);
        OnRefreshed(replacement);
    }

    internal bool Tick(double delta)
    {
        if (!IsActive)
            return true;

        var deltaSeconds = Math.Max(0.0f, (float)delta);
        ElapsedSeconds += deltaSeconds;

        if (TickIntervalSeconds > 0.0f)
        {
            while (NextTickSeconds > 0.0f &&
                   ElapsedSeconds >= NextTickSeconds &&
                   NextTickSeconds <= _expiresAtSeconds)
            {
                OnTick();
                NextTickSeconds += TickIntervalSeconds;
            }
        }

        return _expiresAtSeconds <= 0.0f || ElapsedSeconds >= _expiresAtSeconds;
    }

    internal void Stop(bool expired)
    {
        if (!IsActive)
            return;

        IsActive = false;
        OnRemoved(expired);
        OwnerNode = null;
        Source = null;
        SourceInstanceId = 0UL;
    }

    public virtual void ApplyVisualEffect(OmniSprite omniSprite, bool active)
    {
    }

    protected virtual void CopyConfigurationFrom(StatusEffect replacement)
    {
        Lifetime = replacement.Lifetime;
        Dispellable = replacement.Dispellable;
        DurationSeconds = replacement.DurationSeconds;
        TickIntervalSeconds = replacement.TickIntervalSeconds;
        DisplayName = replacement.DisplayName;
        FloatingTextLabel = replacement.FloatingTextLabel;
        Category = replacement.Category;
        ApplyChance = replacement.ApplyChance;
    }

    protected virtual void OnApplied()
    {
    }

    protected virtual void OnRefreshed(StatusEffect replacement)
    {
    }

    protected virtual void OnTick()
    {
    }

    protected virtual void OnRemoved(bool expired)
    {
    }

    protected Damage DuplicateDamagePayload()
    {
        var damage = Damage.DuplicateFrom(this);
        if (damage == null)
            return null;

        // TODO: Status-effect/DoT damage is hard non-critting for now. Future buffs may make
        // this configurable per status; until then, force CanCrit off regardless of template.
        damage.CanCrit = false;

        var damageSource = Source != null && GodotObject.IsInstanceValid(Source) ? (Node)Source : null;
        damage.InitializeRuntime(damageSource);
        return damage;
    }

    protected Healing DuplicateHealingPayload()
    {
        var healing = Healing.DuplicateFrom(this);
        if (healing == null)
            return null;

        var healingSource = Source != null && GodotObject.IsInstanceValid(Source) ? (Node)Source : null;
        healing.InitializeRuntime(healingSource, healing.ResolveAmount());
        return healing;
    }

    protected void CopyDamageTemplateFrom(StatusEffect replacement)
    {
        if (replacement == null)
            return;

        var replacementDamage = replacement.GetNodeOrNull<Damage>("Damage");
        var damage = GetNodeOrNull<Damage>("Damage");
        if (replacementDamage == null || damage == null)
            return;

        damage.FlatDamage = replacementDamage.FlatDamage;
        damage.PowerScale = replacementDamage.PowerScale;
        damage.MinDamageMultiplier = replacementDamage.MinDamageMultiplier;
        damage.MaxDamageMultiplier = replacementDamage.MaxDamageMultiplier;
        damage.School = replacementDamage.School;
        damage.CanCrit = replacementDamage.CanCrit;
    }

    protected void CopyHealingTemplateFrom(StatusEffect replacement)
    {
        if (replacement == null)
            return;

        var replacementHealing = replacement.GetNodeOrNull<Healing>("Healing");
        var healing = GetNodeOrNull<Healing>("Healing");
        if (replacementHealing == null || healing == null)
            return;

        healing.MinimumHealing = replacementHealing.MinimumHealing;
        healing.MaximumHealing = replacementHealing.MaximumHealing;
    }

    private void ResetTiming()
    {
        ElapsedSeconds = 0.0f;
        NextTickSeconds = TickIntervalSeconds > 0.0f ? TickIntervalSeconds : float.PositiveInfinity;

        // Permanent effects never expire through the tick: an infinite expiry keeps Tick
        // from ever reporting completion while still letting any periodic ticks run.
        _expiresAtSeconds = IsPermanent
            ? float.PositiveInfinity
            : Math.Max(0.0f, DurationSeconds);
    }

    private float CalculateRefreshedNextTickSeconds(float currentTime, float previousTickInterval, float previousNextTickSeconds)
    {
        if (TickIntervalSeconds <= 0.0f)
            return float.PositiveInfinity;

        if (previousTickInterval > 0.0f && !float.IsInfinity(previousNextTickSeconds))
        {
            var previousRemainingSeconds = Math.Max(0.0f, previousNextTickSeconds - currentTime);
            var previousProgress = 1.0f - Math.Clamp(previousRemainingSeconds / previousTickInterval, 0.0f, 1.0f);
            return currentTime + Math.Max(0.0f, TickIntervalSeconds * (1.0f - previousProgress));
        }

        return currentTime + TickIntervalSeconds;
    }
}
