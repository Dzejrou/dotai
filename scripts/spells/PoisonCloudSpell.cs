using Godot;

using System;

[GlobalClass]
public partial class PoisonCloudSpell : Spell
{
    private PoisonCloudArea _cloudTemplate;

    public PoisonCloudSpell()
    {
        ManaCost = 8;
    }

    public override void _Ready()
    {
        _cloudTemplate = FindCloudTemplate();

        if (_cloudTemplate == null)
            GD.PushError($"{GetPath()}: PoisonCloudSpell requires a PoisonCloudArea child template.");
    }

    public override bool CanCast(ISpellCaster caster)
    {
        if (!base.CanCast(caster) || _cloudTemplate == null)
            return false;

        var sourceFaction = caster.Faction;
        var target = caster.SpellTarget;
        return sourceFaction != null &&
               target != null &&
               GodotObject.IsInstanceValid(target) &&
               target.IsInsideTree();
    }

    public override bool TryCast(ISpellCaster caster)
    {
        if (!CanCast(caster))
            return false;

        var target = caster.SpellTarget;
        if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
            return false;

        var parent = caster.SpellOrigin.GetParent();
        if (parent == null)
            return false;

        if (!TrySpendCastMana(caster))
            return false;

        // TODO: run a broader dead-code analysis pass once spell-owned templates have settled.
        var poisonCloud = _cloudTemplate.Duplicate() as PoisonCloudArea;
        if (poisonCloud == null)
            return false;

        parent.AddChild(poisonCloud);
        poisonCloud.GlobalPosition = target.GlobalPosition;
        poisonCloud.Initialize(caster.SpellOrigin, caster.Faction);

        StartCooldown();
        return true;
    }

    private PoisonCloudArea FindCloudTemplate()
    {
        foreach (var child in GetChildren())
        {
            if (child is PoisonCloudArea poisonCloudArea)
                return poisonCloudArea;
        }

        return null;
    }
}
