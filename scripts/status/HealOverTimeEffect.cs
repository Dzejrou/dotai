using Godot;

using System;

[GlobalClass]
public partial class HealOverTimeEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "heal_over_time";

    [Export]
    public int HealPerTick { get; set; } = 3;

    public HealOverTimeEffect()
    {
        DisplayName = "Heal Over Time";
        Category = StatusCategory.Buff;
        DurationSeconds = 16.0f;
        TickIntervalSeconds = 2.0f;
    }

    public override StringName StatusKey => StatusKeyName;

    protected override void CopyConfigurationFrom(StatusEffect replacement)
    {
        base.CopyConfigurationFrom(replacement);

        if (replacement is HealOverTimeEffect healOverTimeEffect)
            HealPerTick = healOverTimeEffect.HealPerTick;
    }

    protected override void OnTick()
    {
        if (OwnerNode is not IHealable healable || !healable.CanReceiveHealing)
            return;

        healable.ApplyHealing(Math.Max(1, HealPerTick));
    }
}
