using Godot;

using System;

[GlobalClass]
public abstract partial class Spell : Node
{
    [Export]
    public StringName CastAction { get; set; }

    [Export]
    public string HudLabel { get; set; } = string.Empty;

    public string DisplayLabel => !string.IsNullOrWhiteSpace(HudLabel) ? HudLabel : Name;
    public virtual int DisplayManaCost => 0;
    public virtual float CooldownDuration => 0.0f;
    public virtual float CooldownRemaining => 0.0f;

    public virtual bool CanCast(ISpellCaster caster)
    {
        return caster != null && caster.CanCastSpells && caster.SpellOrigin != null;
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

    public abstract bool TryCast(ISpellCaster caster);
}
