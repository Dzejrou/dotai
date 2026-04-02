using Godot;

using System;

[GlobalClass]
public partial class PoisonedEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "poisoned";

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
        attackable.ApplyDamage(new DamageInfo(damage, Source ?? OwnerNode));
    }
}
