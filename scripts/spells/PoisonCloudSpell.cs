using Godot;

[GlobalClass]
public partial class PoisonCloudSpell : GroundPlacedSpell
{
    public PoisonCloudSpell()
    {
        ManaCost = 8;
    }

    protected override bool TryResolvePlacementPosition(ISpellCaster caster, out Vector2 worldPosition)
    {
        var target = caster.SpellTarget;
        if (target != null && GodotObject.IsInstanceValid(target) && target.IsInsideTree())
        {
            worldPosition = target.GlobalPosition;
            return true;
        }

        worldPosition = default;
        return false;
    }
}
