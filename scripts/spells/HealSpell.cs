using Godot;

using System;

[GlobalClass]
public partial class HealSpell : Spell
{
    [Export]
    public int HealAmount { get; set; } = 3;

    [Export]
    public float Range { get; set; } = 256.0f;

    public override void _Ready()
    {
        HealAmount = Math.Max(1, HealAmount);
        Range = Math.Max(0.0f, Range);
    }

    public override bool CanCast(ISpellCaster caster)
    {
        return CanCastOn(caster, caster?.SpellTarget);
    }

    public override bool TryCast(ISpellCaster caster)
    {
        return TryCastOn(caster, caster?.SpellTarget);
    }

    public bool CanCastOn(ISpellCaster caster, Node2D target)
    {
        if (!base.CanCast(caster))
            return false;

        return TryResolveTarget(caster, target, requireRangeCheck: true, out _, out _);
    }

    public bool TryCastOn(ISpellCaster caster, Node2D target)
    {
        if (!base.CanCast(caster))
            return false;

        if (!TryResolveTarget(caster, target, requireRangeCheck: false, out _, out var healable))
            return false;

        if (!TrySpendCastMana(caster))
            return false;

        healable.ApplyHealing(HealAmount);
        StartCooldown();
        return true;
    }

    private bool TryResolveTarget(
        ISpellCaster caster,
        Node2D target,
        bool requireRangeCheck,
        out Node2D resolvedTarget,
        out IHealable healable)
    {
        resolvedTarget = null;
        healable = null;

        if (caster == null ||
            caster.SpellOrigin == null ||
            !GodotObject.IsInstanceValid(caster.SpellOrigin) ||
            target == null ||
            !GodotObject.IsInstanceValid(target) ||
            !target.IsInsideTree())
        {
            return false;
        }

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        if (target is not IHealable targetHealable || !targetHealable.CanReceiveHealing)
            return false;

        if (caster.Faction == null ||
            target is not IFactionMember factionMember ||
            factionMember.Faction == null ||
            !caster.Faction.IsFriendlyTo(factionMember.Faction))
        {
            return false;
        }

        if (requireRangeCheck &&
            caster.SpellOrigin.GlobalPosition.DistanceTo(target.GlobalPosition) > Range)
        {
            return false;
        }

        resolvedTarget = target;
        healable = targetHealable;
        return true;
    }
}
