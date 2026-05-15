using Godot;

using System;

[GlobalClass]
public partial class FrostNovaSpell : NovaSpell
{
    private readonly RandomNumberGenerator _random = new();

    [Export]
    public float FreezeChance { get; set; } = 1.0f;

    public override void _Ready()
    {
        _random.Randomize();
    }

    protected override int ResolveDamage(Damage damageTemplate, Node target)
    {
        return damageTemplate?.ResolveAmount() ?? 0;
    }

    protected override void OnTargetHit(ISpellCaster caster, Node target, IAttackable attackable)
    {
        var controller = ResolveStatusEffectController(target);
        if (controller == null)
            return;

        var shouldFreeze = _random.Randf() < Math.Clamp(FreezeChance, 0.0f, 1.0f);
        var templateName = shouldFreeze ? "FrozenEffect" : "SlowedEffect";
        var statusTemplate = GetNodeOrNull<StatusEffect>(templateName);
        var statusEffect = statusTemplate?.Duplicate() as StatusEffect;
        if (statusEffect == null)
            return;

        var source = caster.SpellOrigin;
        controller.ApplyStatusEffect(statusEffect, source, ResolveSourceInstanceId(source));
    }
}
