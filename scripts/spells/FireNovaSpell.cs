using Godot;

using System;

[GlobalClass]
public partial class FireNovaSpell : NovaSpell
{
    private readonly RandomNumberGenerator _random = new();

    [Export]
    public int MinimumDamage { get; set; } = 6;

    [Export]
    public int MaximumDamage { get; set; } = 10;

    public FireNovaSpell()
    {
        ManaCost = 20;
    }

    public override void _Ready()
    {
        base._Ready();
        _random.Randomize();
    }

    protected override int ResolveDamage(Node target)
    {
        var maximumDamage = Math.Max(MinimumDamage, MaximumDamage);
        return _random.RandiRange(Math.Min(MinimumDamage, maximumDamage), maximumDamage);
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
