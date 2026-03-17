public sealed class AggressiveCombatActorAI : ActorAI
{
    public override bool TryAcquireTarget()
    {
        return Actor is IAggressiveCombatActorAIHost aggressiveHost &&
               aggressiveHost.TryAcquireAggressiveTarget();
    }
}
