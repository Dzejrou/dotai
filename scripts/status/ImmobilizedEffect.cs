using Godot;

[GlobalClass]
public partial class ImmobilizedEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "immobilized";

    public ImmobilizedEffect()
    {
        DisplayName = "IMMOBILIZED";
        Category = StatusCategory.Debuff;
        DurationSeconds = 2.5f;
        TickIntervalSeconds = 0.0f;
    }

    public override StringName StatusKey => StatusKeyName;

    public override bool IsUniqueByStatusKey => true;

    public override bool PreventsMovement => true;
}
