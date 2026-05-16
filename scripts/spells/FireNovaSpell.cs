using Godot;

[GlobalClass]
public partial class FireNovaSpell : NovaSpell
{
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
