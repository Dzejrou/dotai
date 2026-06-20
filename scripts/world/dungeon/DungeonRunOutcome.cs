// Explicit terminal outcome of a finalized dungeon run.
//
// Completion is structural: it is set only when the player traverses the terminal Boss room's
// dedicated completion exit, never inferred from a boss kill, ActorRank.Boss, or
// BossEncounter.EncounterCompleted. Future modes may contain multiple or non-terminal bosses,
// so "a boss died" must never imply "the run completed".
public enum DungeonRunOutcome
{
    // The current linear run reached the terminal Boss room and left through its completion
    // exit, returning the player to the captured launch origin.
    Completed,

    // The run was abandoned: the HUB Give Up button, or an ordinary return/abandonment door.
    GaveUp,
}
