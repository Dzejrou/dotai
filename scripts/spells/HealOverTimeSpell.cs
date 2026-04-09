using Godot;

using System;

[GlobalClass]
public partial class HealOverTimeSpell : Spell
{
    private HealOverTimeEffect _effectTemplate;

    [Export]
    public float Range { get; set; } = 256.0f;

    public override void _Ready()
    {
        Range = Math.Max(0.0f, Range);
        _effectTemplate = FindEffectTemplate();

        if (_effectTemplate == null)
            GD.PushError($"{GetPath()}: HealOverTimeSpell requires a HealOverTimeEffect child template.");
    }

    public override bool CanCast(ISpellCaster caster, SpellCastRequest request)
    {
        if (!base.CanCast(caster, request) || _effectTemplate == null)
            return false;

        return TryResolveTarget(
            caster,
            request,
            requireRangeCheck: true,
            requireMissingHealOverTime: true,
            out _,
            out _);
    }

    public override bool TryCast(ISpellCaster caster, SpellCastRequest request)
    {
        if (!base.CanCast(caster, request) || _effectTemplate == null)
            return false;

        if (!TryResolveTarget(
                caster,
                request,
                requireRangeCheck: false,
                requireMissingHealOverTime: false,
                out _,
                out var statusEffectController))
        {
            if (request == null || !request.TryResolveTargetNode(out _))
                return LogMissingCastRequestData("Heal-over-time spell requires a friendly target node.");

            return false;
        }

        if (!TrySpendCastMana(caster))
            return false;

        var effect = _effectTemplate.Duplicate() as HealOverTimeEffect;
        if (effect == null)
            return false;

        var source = caster.SpellOrigin;
        var sourceInstanceId = source != null && GodotObject.IsInstanceValid(source)
            ? source.GetInstanceId()
            : 0UL;

        statusEffectController.ApplyStatusEffect(effect, source, sourceInstanceId);
        StartCooldown();
        return true;
    }

    private HealOverTimeEffect FindEffectTemplate()
    {
        foreach (var child in GetChildren())
        {
            if (child is HealOverTimeEffect effect)
                return effect;
        }

        return null;
    }

    private bool TryResolveTarget(
        ISpellCaster caster,
        SpellCastRequest request,
        bool requireRangeCheck,
        bool requireMissingHealOverTime,
        out Node2D resolvedTarget,
        out StatusEffectController statusEffectController)
    {
        resolvedTarget = null;
        statusEffectController = null;

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

        if (target is not IHealable healable || !healable.CanReceiveHealing)
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

        statusEffectController = target.GetNodeOrNull<StatusEffectController>("StatusEffectController");
        if (statusEffectController == null)
            return false;

        if (requireMissingHealOverTime &&
            statusEffectController.HasStatus(HealOverTimeEffect.StatusKeyName))
        {
            return false;
        }

        resolvedTarget = target;
        return true;
    }
}
