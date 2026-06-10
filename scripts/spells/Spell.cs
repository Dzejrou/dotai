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
    public Texture2D Icon { get; set; }

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    // Authored display school. Used directly by utility spells without Damage nodes
    // (e.g. Blink, Ice Shield) and as the fallback when attached Damage descendants
    // disagree with each other.
    [Export]
    public DamageSchool School { get; set; } = DamageSchool.Physical;

    [Export]
    public int ManaCost { get; set; } = 0;

    [Export]
    public float Cooldown { get; set; } = 0.0f;

    private float _castTimeSeconds;
    private float _channelDurationSeconds;
    private float _cooldownRemaining;
    private DamageSchool? _displaySchool;

    public string DisplayLabel => !string.IsNullOrWhiteSpace(HudLabel) ? HudLabel : Name;

    // School shown by UI (e.g. tooltip name color). Resolved from attached Damage
    // descendants when they all agree on one school; otherwise the exported School
    // is used. The exported value itself is never mutated. Resolution is cached on
    // first use because some subclasses override _Ready without calling base.
    public DamageSchool DisplaySchool => _displaySchool ??= ResolveDisplaySchool();
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
    public virtual bool ShouldFaceCastRequest => true;
    public virtual float CooldownDuration => Math.Max(0.0f, Cooldown);
    public virtual float CooldownRemaining => Math.Max(0.0f, _cooldownRemaining);

    public override void _Ready()
    {
        if (string.IsNullOrWhiteSpace(SpellId))
            GD.PushWarning($"{GetPath()}: Spell is missing SpellId.");

        _displaySchool ??= ResolveDisplaySchool();
    }

    private DamageSchool ResolveDisplaySchool()
    {
        DamageSchool? uniformSchool = null;
        return TryResolveUniformDamageSchool(this, ref uniformSchool)
            ? uniformSchool ?? School
            : School;
    }

    private static bool TryResolveUniformDamageSchool(Node node, ref DamageSchool? uniformSchool)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Damage damage)
            {
                if (uniformSchool == null)
                    uniformSchool = damage.School;
                else if (uniformSchool.Value != damage.School)
                    return false;
            }

            if (!TryResolveUniformDamageSchool(child, ref uniformSchool))
                return false;
        }

        return true;
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
