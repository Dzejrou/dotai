using Godot;

// Transition step: grant the boss its Enrage buff exactly once, then complete immediately
// so it never blocks the rest of the transition. Reusable across bosses; the Demon boss's
// phase-3 Enrage configures 1000 Haste, speeding up its casts and attacks for the rest of
// that encounter.
//
// Enrage is applied through the actor's existing StatusEffectController as a permanent,
// undispellable, unique Buff (a StatModifierEffect) rather than mutating a base stat. It is
// never reverted here: BossEncounter re-instantiates the boss for every encounter and
// forced reset/teardown clears all effects, so a fresh boss naturally starts un-enraged.
// The status feedback path reports "<boss> gains Enrage." in the combat log.
[GlobalClass]
public partial class ApplyHasteTransitionAction : BossTransitionAction
{
    private const string EnrageStatusKey = "enrage";

    // Flat Haste granted by Enrage. Exported so the amount is a scene/tuning decision
    // (the Demon boss configures 1000).
    [Export]
    public int HasteAmount { get; set; } = 1000;

    // Buff display/log label. Drives the "<boss> gains <BuffName>." status feedback line and
    // the floating text shown on application.
    [Export]
    public string BuffName { get; set; } = "Enrage";

    protected override void OnBegin(Actor actor)
    {
        ApplyEnrage(actor);

        // Pure buff application with nothing to drive over time: complete right away so the
        // transition does not stall or extend any invulnerability window.
        IsComplete = true;
    }

    public override ActorIntent BuildIntent(Actor actor)
    {
        // Completes on begin, so this is effectively never queried; hold in the
        // transition state to stay consistent with the other transition actions.
        return ActorIntent.Hold(CombatUnitState.Transitioning);
    }

    private void ApplyEnrage(Actor actor)
    {
        if (actor == null || HasteAmount == 0)
            return;

        var controller = actor.GetNodeOrNull<StatusEffectController>("StatusEffectController");
        if (controller == null)
            return;

        var enrage = new StatModifierEffect
        {
            StatusKeyName = EnrageStatusKey,
            DisplayName = BuffName,
            FloatingTextLabel = BuffName,
            Category = StatusCategory.Buff,
            Lifetime = StatusLifetime.Permanent,
            Dispellable = false,
            Unique = true,
            // Pure stat buff: no periodic tick.
            TickIntervalSeconds = 0.0f,
            HasteFlat = HasteAmount,
        };

        controller.ApplyStatusEffect(enrage, actor, actor.GetInstanceId());
    }
}
