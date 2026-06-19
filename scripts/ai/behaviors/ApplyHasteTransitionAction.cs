using Godot;

// Transition step: add a flat Haste bonus to the boss exactly once, then complete
// immediately so it never blocks the rest of the transition. Reusable across bosses;
// the Demon boss's phase-3 Enrage configures it to add 1000 Haste, speeding up its
// casts and attacks for the remainder of that encounter.
//
// TODO(buff-system): this mutates the boss's base Haste stat directly because there is
// no buff/status framework yet. Once buffs exist, Enrage should apply an *undispellable*
// Haste buff instead of a base-stat mutation. The bonus is intentionally never reverted
// here - BossEncounter re-instantiates the boss for every encounter, so a fresh/reset
// boss naturally starts again from its original base Haste.
[GlobalClass]
public partial class ApplyHasteTransitionAction : BossTransitionAction
{
    // Flat Haste added to the boss when this action runs. Exported so the amount is a
    // scene/tuning decision (the Demon boss's Enrage configures 1000).
    [Export]
    public int HasteAmount { get; set; } = 1000;

    protected override void OnBegin(Actor actor)
    {
        // Apply exactly once on begin; the controller arms each transition a single time.
        actor?.AddBaseHaste(HasteAmount);

        // Pure stat change with nothing to drive over time: complete right away so the
        // transition does not stall or extend any invulnerability window.
        IsComplete = true;
    }

    public override ActorIntent BuildIntent(Actor actor)
    {
        // Completes on begin, so this is effectively never queried; hold in the
        // transition state to stay consistent with the other transition actions.
        return ActorIntent.Hold(CombatUnitState.Transitioning);
    }
}
