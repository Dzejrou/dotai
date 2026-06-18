using System;

// Room-agnostic contract for an injected encounter (e.g. a boss fight) that owns its
// own combat lifecycle. A generic room drives BeginEncounter on entry and
// AbandonEncounter on exit, and unlocks/completes its objective when EncounterCompleted
// fires. This keeps room mechanics free of any encounter-specific (e.g. boss) wiring.
public interface IRoomEncounter
{
    // Raised once when the encounter objective is met (e.g. the boss dies), regardless
    // of any remaining encounter state.
    event Action EncounterCompleted;

    // Begin a fresh encounter. Implementations must produce a clean starting state even
    // when called again on a re-entered, persistent room instance.
    void BeginEncounter(Room room);

    // Abandon the encounter (room exit): release ownership and reset all encounter state
    // so a later BeginEncounter starts clean.
    void AbandonEncounter();
}
