using Godot;

using System;

[GlobalClass]
public partial class RejuvenationEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "rejuvenation";

    [Export]
    public int HealPerTick { get; set; } = 3;

    public RejuvenationEffect()
    {
        DisplayName = "REJUVENATION";
        Category = StatusCategory.Buff;
        DurationSeconds = 16.0f;
        TickIntervalSeconds = 2.0f;
    }

    public override StringName StatusKey => StatusKeyName;

    protected override void CopyConfigurationFrom(StatusEffect replacement)
    {
        base.CopyConfigurationFrom(replacement);

        if (replacement is RejuvenationEffect rejuvenationEffect)
            HealPerTick = rejuvenationEffect.HealPerTick;
    }

    protected override void OnTick()
    {
        if (OwnerNode is not IHealable healable || !healable.CanReceiveHealing)
            return;

        healable.ApplyHealing(Math.Max(1, HealPerTick));
    }
}
