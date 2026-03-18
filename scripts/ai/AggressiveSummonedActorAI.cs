public sealed class AggressiveSummonedActorAI : ActorAI
{
    public override bool TryAcquireTarget()
    {
        if (Actor is not IAggressiveSummonedActorAIHost summonHost)
            return false;

        if (!summonHost.ShouldAttemptAggressiveSummonedTargetAcquisition())
            return false;

        var target = summonHost.SelectAggressiveSummonedTarget();
        if (target == null)
            return false;

        summonHost.ApplyAggressiveSummonedTarget(target);
        return true;
    }

    public override bool TryHandleNoTarget(double delta)
    {
        return Actor is IAggressiveSummonedActorAIHost summonHost &&
               summonHost.TryHandleAggressiveSummonedNoTarget(delta);
    }
}
