using Godot;

[GlobalClass]
public partial class PoisonedEffect : StatusEffect
{
    private static readonly Color PoisonTintColor = new(0.7f, 1.0f, 0.7f, 1.0f);

    public static readonly StringName StatusKeyName = "poisoned";

    public override StringName StatusKey => StatusKeyName;

    public override void ApplyVisualEffect(OmniSprite omniSprite, bool active)
    {
        if (omniSprite == null)
            return;

        if (active)
            omniSprite.SetStatusTint(StatusKeyName, PoisonTintColor);
        else
            omniSprite.ClearStatusTint(StatusKeyName);
    }

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
