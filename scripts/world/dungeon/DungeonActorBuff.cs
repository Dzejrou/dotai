using Godot;

// Stamps a run's difficulty enemy buffs onto actors created by managed dungeon spawn points. Built
// once per run from the immutable difficulty snapshot and handed to every managed ActorSpawnPoint,
// so initial-room actors, bosses, later boss summons, and respawns all receive the same modifiers
// automatically through the spawn lifecycle.
//
// The buff is a single permanent, undispellable, unique StatModifierEffect applied silently (no
// floating text or combat-log line). Being unique by status key, re-applying on room restoration or
// spawn reconciliation refreshes rather than stacks, so duplicates never accumulate. Base Stats are
// never mutated; everything flows through the status-effect aggregation path.
public sealed class DungeonActorBuff
{
    // Status key for the difficulty buff. Distinct from other buffs (e.g. boss "enrage") so they
    // coexist without refreshing one another.
    public const string StatusKey = "dungeon_difficulty";

    private readonly float _healthPowerBonus;
    private readonly float _resistanceBonus;
    private readonly float _damageBonus;

    public DungeonActorBuff(float healthPowerBonus, float resistanceBonus, float damageBonus)
    {
        _healthPowerBonus = healthPowerBonus;
        _resistanceBonus = resistanceBonus;
        _damageBonus = damageBonus;
    }

    // Builds the buff from a difficulty snapshot, or null when the snapshot grants no actor buffs at
    // all (so spawn points skip buff work entirely on a no-buff run).
    public static DungeonActorBuff FromSelection(DungeonDifficultySelection selection)
    {
        if (selection == null || !selection.HasActorBuffs)
            return null;

        return new DungeonActorBuff(selection.HealthPowerBonus, selection.ResistanceBonus, selection.DamageBonus);
    }

    // Applies the difficulty buff to a freshly established actor. Idempotent: an actor that already
    // carries the buff is left untouched (no re-apply, no extra heal), which keeps repeated room
    // restoration or reconciliation from stacking copies. The player is never buffed.
    //
    // When initializeHealth is true the actor is filled to its full boosted Max Health after the buff
    // applies - the deliberate spawn-initialization exception. This never runs for buffs applied
    // during active combat, so the ordinary no-free-heal behavior is preserved.
    public void ApplyTo(CombatCharacter actor, bool initializeHealth)
    {
        if (actor == null || !GodotObject.IsInstanceValid(actor) || actor is Player)
            return;

        var controller = actor.GetNodeOrNull<StatusEffectController>("StatusEffectController");
        if (controller == null || controller.HasStatus(StatusKey))
            return;

        controller.ApplyStatusEffect(CreateEffect(), actor, actor.GetInstanceId());

        if (initializeHealth)
            actor.InitializeHealthToFullBoostedMax();
    }

    private StatModifierEffect CreateEffect()
    {
        return new StatModifierEffect
        {
            StatusKeyName = StatusKey,
            // Intentionally no DisplayName/FloatingTextLabel: buff application must be silent, with no
            // per-actor floating text or combat-log spam.
            DisplayName = string.Empty,
            FloatingTextLabel = string.Empty,
            Category = StatusCategory.Buff,
            Lifetime = StatusLifetime.Permanent,
            Dispellable = false,
            Unique = true,
            // Pure stat buff: never ticks.
            TickIntervalSeconds = 0.0f,
            MaxHealthPercent = _healthPowerBonus,
            PowerPercent = _healthPowerBonus,
            DamageBonusFlat = _damageBonus,
            PhysicalResistanceFlat = _resistanceBonus,
            FireResistanceFlat = _resistanceBonus,
            IceResistanceFlat = _resistanceBonus,
            PoisonResistanceFlat = _resistanceBonus,
            ArcaneResistanceFlat = _resistanceBonus,
        };
    }
}
