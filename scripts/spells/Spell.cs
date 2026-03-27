using Godot;

[GlobalClass]
public abstract partial class Spell : Node
{
    [Export]
    public StringName CastAction { get; set; }

    public virtual bool CanCast(ISpellCaster caster)
    {
        return caster != null && caster.CanCastSpells && caster.SpellOrigin != null;
    }

    public abstract bool TryCast(ISpellCaster caster);
}
