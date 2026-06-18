// Reusable, read-only view of an actor's boss-style phase state. Implemented by a
// phase controller (e.g. BossPhaseController) and discovered/cached by Actor during
// behavior configuration, so behaviors can gate on phase without depending on the
// concrete controller. Actors with no provider report phase 1 and not-transitioning.
public interface IActorPhaseState
{
    int CurrentPhase { get; }
    bool IsTransitioning { get; }
}
