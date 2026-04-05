using Godot;

using System;

[GlobalClass]
public partial class BurningEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "burning";

    [Export]
    public int DamagePerTick { get; set; } = 3;

    public BurningEffect()
    {
        DisplayName = "BURNING";
        Category = StatusCategory.Debuff;
        DurationSeconds = 6.0f;
        TickIntervalSeconds = 2.0f;
    }

    public override StringName StatusKey => StatusKeyName;

    protected override void CopyConfigurationFrom(StatusEffect replacement)
    {
        base.CopyConfigurationFrom(replacement);

        if (replacement is BurningEffect burningEffect)
            DamagePerTick = burningEffect.DamagePerTick;
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
