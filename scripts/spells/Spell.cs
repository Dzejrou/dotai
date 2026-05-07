using Godot;

using System;

[GlobalClass]
public abstract partial class Spell : Node
{
    [Export]
    public string SpellId { get; set; } = string.Empty;

    [Export]
    public StringName CastAction { get; set; }

    [Export]
    public string HudLabel { get; set; } = string.Empty;

    [Export]
    public int ManaCost { get; set; } = 0;

    [Export]
    public float Cooldown { get; set; } = 0.0f;

    private float _castTimeSeconds;
    private float _channelDurationSeconds;
    private float _cooldownRemaining;

    public string DisplayLabel => !string.IsNullOrWhiteSpace(HudLabel) ? HudLabel : Name;
    public virtual int DisplayManaCost => Math.Max(0, ManaCost);
    [Export]
    public float CastTimeSeconds
    {
        get => _castTimeSeconds;
        set => _castTimeSeconds = Math.Max(0.0f, value);
    }

    public virtual float CastTimeDuration => Math.Max(0.0f, CastTimeSeconds);
    [Export]
    public float ChannelDurationSeconds
    {
        get => _channelDurationSeconds;
        set => _channelDurationSeconds = Math.Max(0.0f, value);
    }

    public virtual float ChannelDuration => Math.Max(0.0f, ChannelDurationSeconds);
    public bool IsChanneled => ChannelDuration > 0.0f;
    public virtual float CooldownDuration => Math.Max(0.0f, Cooldown);
    public virtual float CooldownRemaining => Math.Max(0.0f, _cooldownRemaining);

    public override void _Ready()
    {
        if (string.IsNullOrWhiteSpace(SpellId))
            GD.PushWarning($"{GetPath()}: Spell is missing SpellId.");
    }

    public virtual bool CanCast(ISpellCaster caster, SpellCastRequest request)
    {
        var spellOrigin = caster?.SpellOrigin;
        if (caster == null ||
            !caster.CanCastSpells ||
            spellOrigin == null ||
            !GodotObject.IsInstanceValid(spellOrigin) ||
            IsOnCooldown)
            return false;

        var manaState = caster.ManaState;
        return manaState != null && manaState.Current >= Math.Max(0, ManaCost);
    }

    public override void _Process(double delta)
    {
        if (_cooldownRemaining > 0.0f)
            _cooldownRemaining = Math.Max(0.0f, _cooldownRemaining - (float)delta);
    }

    protected bool TrySpendCastMana(ISpellCaster caster)
    {
        return TrySpendCastMana(caster, ManaCost);
    }

    protected void StartCooldown()
    {
        _cooldownRemaining = Math.Max(0.0f, Cooldown);
    }

    protected bool IsOnCooldown => _cooldownRemaining > 0.0f;

    protected bool LogMissingCastRequestData(string message)
    {
        GD.PushWarning($"{GetPath()}: {message}");
        return false;
    }

    protected static bool TrySpendCastMana(ISpellCaster caster, int manaCost)
    {
        var manaState = caster?.ManaState;
        if (manaState == null)
            return false;

        var resolvedManaCost = Mathf.Max(0, manaCost);
        if (!manaState.TrySpend(resolvedManaCost))
            return false;

        manaState.ResetRegenerationDelay();
        if (resolvedManaCost > 0)
            caster.NotifyManaChanged();

        return true;
    }

    public virtual bool TryCast(ISpellCaster caster, SpellCastRequest request, out SpellCastResult result)
    {
        result = null;
        return TryCast(caster, request);
    }

    public abstract bool TryCast(ISpellCaster caster, SpellCastRequest request);
}
