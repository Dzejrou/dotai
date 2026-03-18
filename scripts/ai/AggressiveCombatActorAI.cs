public class AggressiveCombatActorAI : ActorAI
{
    public override bool TryAcquireTarget()
    {
        if (Actor is not IAggressiveCombatActorAIHost aggressiveHost)
            return false;

        if (!aggressiveHost.ShouldAttemptAggressiveTargetAcquisition())
            return false;

        var candidate = aggressiveHost.SelectAggressiveTargetCandidate();
        if (candidate == null)
            return false;

        aggressiveHost.ApplyAggressiveTargetCandidate(candidate);
        return true;
    }
}
