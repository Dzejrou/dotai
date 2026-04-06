using Godot;

[GlobalClass]
public partial class PoisonedEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "poisoned";

    public PoisonedEffect()
    {
        DisplayName = "POISON";
        FloatingTextLabel = "POISON";
        Category = StatusCategory.Debuff;
    }

    public override StringName StatusKey => StatusKeyName;

    protected override void CopyConfigurationFrom(StatusEffect replacement)
    {
        base.CopyConfigurationFrom(replacement);
        CopyDamageTemplateFrom(replacement);
    }

    protected override void OnTick()
    {
        if (OwnerNode is not IAttackable attackable)
            return;

        var damage = DuplicateDamagePayload();
        if (damage != null)
            attackable.ApplyDamage(damage);
    }
}
