using Godot;

[GlobalClass]
public abstract partial class Spell : Node
{
    [Export]
    public StringName CastAction { get; set; }

    public abstract bool TryCast(ISpellCaster caster);
}
