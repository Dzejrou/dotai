using Godot;

// One selectable difficulty option: a gameplay Value paired with the additive reward adjustment
// granted for picking it. Used by DungeonDifficultyRules to keep the option-to-reward tables
// data-driven and inspector-editable rather than hardcoded in the HUB. The same option type backs
// every row (starting level, level increase, and the three enemy stat categories); the row decides
// how Value is interpreted (an absolute level, a per-room increase, or an additive-percent bonus).
[GlobalClass]
public partial class DungeonDifficultyOption : Resource
{
    // Gameplay magnitude of this option. Interpreted per row: an absolute starting level (e.g. 10),
    // a per-room level increase (e.g. 1), or an additive-percent actor bonus (e.g. 0.2 = +20%).
    [Export]
    public float Value { get; set; }

    // Additive reward adjustment contributed to the run's difficulty multiplier when this option is
    // selected (e.g. 0.25 = +25%, -0.75 = -75%). All selected adjustments are summed, never
    // multiplied.
    [Export]
    public float RewardAdjustment { get; set; }

    public DungeonDifficultyOption()
    {
    }

    public DungeonDifficultyOption(float value, float rewardAdjustment)
    {
        Value = value;
        RewardAdjustment = rewardAdjustment;
    }
}
