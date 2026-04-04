using Godot;

using System;

[GlobalClass]
public partial class SlowedEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "slowed";

    [Export]
    public float MovementSpeedMultiplierValue { get; set; } = 0.5f;

    [Export]
    public float AttackSpeedMultiplierValue { get; set; } = 0.33f;

    [Export]
    public float CastSpeedMultiplierValue { get; set; } = 0.2f;

    public SlowedEffect()
    {
        DisplayName = "SLOWED";
        Category = StatusCategory.Debuff;
        DurationSeconds = 6.0f;
        TickIntervalSeconds = 0.0f;
    }

    public override StringName StatusKey => StatusKeyName;

    public override bool IsUniqueByStatusKey => true;

    public override float MovementSpeedMultiplier => Math.Max(0.0f, MovementSpeedMultiplierValue);

    public override float AttackSpeedMultiplier => Math.Max(0.0f, AttackSpeedMultiplierValue);

    public override float CastSpeedMultiplier => Math.Max(0.0f, CastSpeedMultiplierValue);

    protected override void CopyConfigurationFrom(StatusEffect replacement)
    {
        base.CopyConfigurationFrom(replacement);

        if (replacement is SlowedEffect slowedEffect)
        {
            MovementSpeedMultiplierValue = slowedEffect.MovementSpeedMultiplierValue;
            AttackSpeedMultiplierValue = slowedEffect.AttackSpeedMultiplierValue;
            CastSpeedMultiplierValue = slowedEffect.CastSpeedMultiplierValue;
        }
    }
}
