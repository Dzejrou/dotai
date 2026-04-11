using Godot;

[GlobalClass]
public partial class ImmobilizedEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "immobilized";

    public override StringName StatusKey => StatusKeyName;

    public override bool IsUniqueByStatusKey => true;

    public override bool PreventsMovement => true;
}
