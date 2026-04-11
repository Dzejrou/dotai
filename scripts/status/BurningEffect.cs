using Godot;

[GlobalClass]
public partial class BurningEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "burning";

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
