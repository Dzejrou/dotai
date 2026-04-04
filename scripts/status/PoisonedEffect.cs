using Godot;

using System;

[GlobalClass]
public partial class PoisonedEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "poisoned";

    public PoisonedEffect()
    {
        DisplayName = "POISON";
        Category = StatusCategory.Debuff;
    }

    [Export]
    public int DamagePerTick { get; set; } = 2;

    public override StringName StatusKey => StatusKeyName;

    protected override void CopyConfigurationFrom(StatusEffect replacement)
    {
        base.CopyConfigurationFrom(replacement);

        if (replacement is PoisonedEffect poisonedEffect)
            DamagePerTick = poisonedEffect.DamagePerTick;
    }

    protected override void OnTick()
    {
        if (OwnerNode is not IAttackable attackable)
            return;

        var damage = Math.Max(1, DamagePerTick);
        var damageSource = Source != null && GodotObject.IsInstanceValid(Source) ? Source : null;
        attackable.ApplyDamage(new DamageInfo(damage, (Node)damageSource));
    }
}
