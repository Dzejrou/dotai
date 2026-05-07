using Godot;

using System;

[GlobalClass]
public abstract partial class NovaSpell : Spell
{
    [Export]
    public PackedScene VfxScene { get; set; }

    [Export]
    public float Range { get; set; } = 72.0f;

    public override bool ShouldFaceCastRequest => false;

    public override bool CanCast(ISpellCaster caster, SpellCastRequest request)
    {
        return base.CanCast(caster, request) && VfxScene != null && caster?.Faction != null;
    }

    public override bool TryCast(ISpellCaster caster, SpellCastRequest request)
    {
        if (!CanCast(caster, request))
            return false;

        if (!TrySpendCastMana(caster))
            return false;

        var resolvedRange = Math.Max(0.0f, Range);
        if (resolvedRange > 0.0f)
        {
            SpawnVfx(caster.SpellOrigin, resolvedRange);
            ApplyNovaEffects(caster, resolvedRange);
        }

        StartCooldown();
        return true;
    }

    protected virtual int ResolveDamage(Damage damageTemplate, Node target)
    {
        return damageTemplate?.ResolveAmount() ?? 0;
    }

    protected virtual void OnTargetHit(ISpellCaster caster, Node target, IAttackable attackable)
    {
    }

    protected virtual void SpawnVfx(Node2D spellOrigin, float range)
    {
        if (VfxScene?.Instantiate<NovaSpellVfx>() is not NovaSpellVfx novaSpellVfx)
            return;

        var parent = spellOrigin.GetParent();
        if (parent == null)
        {
            novaSpellVfx.QueueFree();
            return;
        }

        parent.AddChild(novaSpellVfx);
        novaSpellVfx.GlobalPosition = spellOrigin.GlobalPosition;
        novaSpellVfx.Play(range);
    }

    private void ApplyNovaEffects(ISpellCaster caster, float range)
    {
        var sourceFaction = caster.Faction;
        var source = caster.SpellOrigin;
        var damageTemplate = GetNodeOrNull<Damage>("Damage");

        foreach (var node in TargetingHelper.EnumerateCandidateTargets(source))
        {
            if (node is not IAttackable attackable)
                continue;

            var targetFactionState = FactionState.ResolveFor(node);
            if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(sourceFaction))
                continue;

            if (source.GlobalPosition.DistanceTo(node.GlobalPosition) > range)
                continue;

            if (damageTemplate?.Duplicate() is Damage damagePayload)
            {
                damagePayload.InitializeRuntime(source, Math.Max(1, ResolveDamage(damageTemplate, node)));
                attackable.ApplyDamage(damagePayload);
            }

            OnTargetHit(caster, node, attackable);
        }
    }

    protected static StatusEffectController ResolveStatusEffectController(Node target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
            return null;

        return target.GetNodeOrNull<StatusEffectController>("StatusEffectController");
    }

    protected static ulong ResolveSourceInstanceId(Node2D source)
    {
        return source != null && GodotObject.IsInstanceValid(source) ? source.GetInstanceId() : 0UL;
    }
}
