using Godot;

using System;

[GlobalClass]
public abstract partial class NovaSpell : Spell
{
    [Export]
    public PackedScene VfxScene { get; set; }

    [Export]
    public float Range { get; set; } = 72.0f;

    public override bool CanCast(ISpellCaster caster)
    {
        return base.CanCast(caster) && VfxScene != null && caster?.Faction != null;
    }

    public override bool TryCast(ISpellCaster caster)
    {
        if (!CanCast(caster))
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

    protected abstract int ResolveDamage(Node target);

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

        foreach (var node in TargetingHelper.EnumerateCandidateTargets(source))
        {
            if (node is not IAttackable attackable)
                continue;

            var targetFactionState = FactionState.ResolveFor(node);
            if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(sourceFaction))
                continue;

            if (source.GlobalPosition.DistanceTo(node.GlobalPosition) > range)
                continue;

            attackable.ApplyDamage(new DamageInfo(Math.Max(1, ResolveDamage(node)), source));
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
