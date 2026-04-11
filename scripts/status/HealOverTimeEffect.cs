using Godot;

using System;

[GlobalClass]
public partial class HealOverTimeEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "heal_over_time";

    public override StringName StatusKey => StatusKeyName;

    protected override void CopyConfigurationFrom(StatusEffect replacement)
    {
        base.CopyConfigurationFrom(replacement);
        CopyHealingTemplateFrom(replacement);
    }

    protected override void OnTick()
    {
        if (OwnerNode is not IHealable healable || !healable.CanReceiveHealing)
            return;

        var healing = DuplicateHealingPayload();
        if (healing != null)
            healable.ApplyHealing(healing);
    }
}
