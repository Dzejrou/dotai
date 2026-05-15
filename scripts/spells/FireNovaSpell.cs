using Godot;

[GlobalClass]
public partial class FireNovaSpell : NovaSpell
{
    private readonly RandomNumberGenerator _random = new();

    public override void _Ready()
    {
        base._Ready();
        _random.Randomize();
    }

    protected override int ResolveDamage(Damage damageTemplate, Node target)
    {
        return damageTemplate?.ResolveAmount(_random) ?? 0;
    }

    protected override void OnTargetHit(ISpellCaster caster, Node target, IAttackable attackable)
    {
        var controller = ResolveStatusEffectController(target);
        if (controller == null)
            return;

        var burningTemplate = GetNodeOrNull<StatusEffect>("BurningEffect");
        var burningEffect = burningTemplate?.Duplicate() as StatusEffect;
        if (burningEffect == null)
            return;

        var source = caster.SpellOrigin;
        controller.ApplyStatusEffect(burningEffect, source, ResolveSourceInstanceId(source));
    }
}
