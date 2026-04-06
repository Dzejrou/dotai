using Godot;

using System;

[GlobalClass]
public partial class FrostNovaSpell : NovaSpell
{
    private readonly RandomNumberGenerator _random = new();

    [Export]
    public float ImmobilizeChance { get; set; } = 0.33f;

    public FrostNovaSpell()
    {
        ManaCost = 20;
        Cooldown = 15.0f;
    }

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

        var shouldImmobilize = _random.Randf() < Math.Clamp(ImmobilizeChance, 0.0f, 1.0f);
        var templateName = shouldImmobilize ? "ImmobilizedEffect" : "SlowedEffect";
        var statusTemplate = GetNodeOrNull<StatusEffect>(templateName);
        var statusEffect = statusTemplate?.Duplicate() as StatusEffect;
        if (statusEffect == null)
            return;

        var source = caster.SpellOrigin;
        controller.ApplyStatusEffect(statusEffect, source, ResolveSourceInstanceId(source));
    }
}
