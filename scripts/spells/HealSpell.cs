using Godot;

using System;

[GlobalClass]
public partial class HealSpell : Spell
{
    [Export]
    public float Range { get; set; } = 256.0f;

    public override void _Ready()
    {
        Range = Math.Max(0.0f, Range);
    }

    public override bool CanCast(ISpellCaster caster, SpellCastRequest request)
    {
        if (!base.CanCast(caster, request))
            return false;

        return TryResolveTarget(caster, request, requireRangeCheck: true, out _, out _);
    }

    public override bool TryCast(ISpellCaster caster, SpellCastRequest request)
    {
        if (!base.CanCast(caster, request))
            return false;

        if (!TryResolveTarget(caster, request, requireRangeCheck: false, out _, out var healable))
        {
            if (request == null || !request.TryResolveTargetNode(out _))
                return LogMissingCastRequestData("Heal spell requires a friendly target node.");

            return false;
        }

        if (!TrySpendCastMana(caster))
            return false;

        if (Healing.DuplicateFrom(this) is Healing healing)
        {
            healing.InitializeRuntime((Node)caster.SpellOrigin, healing.ResolveAmount());
            healable.ApplyHealing(healing);
        }

        StartCooldown();
        return true;
    }

    private bool TryResolveTarget(
        ISpellCaster caster,
        SpellCastRequest request,
        bool requireRangeCheck,
        out Node2D resolvedTarget,
        out IHealable healable)
    {
        resolvedTarget = null;
        healable = null;

        if (request == null || !request.TryResolveTargetNode(out var target))
            return false;

        if (caster == null ||
            caster.SpellOrigin == null ||
            !GodotObject.IsInstanceValid(caster.SpellOrigin) ||
            target == null)
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
