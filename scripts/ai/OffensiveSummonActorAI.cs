public sealed class OffensiveSummonActorAI : ActorAI
{
    public override bool TryAcquireTarget()
    {
        if (Actor is not IOffensiveSummonActorAIHost summonHost)
            return false;

        if (!summonHost.ShouldAttemptOffensiveSummonTargetAcquisition())
            return false;

        var commandedTarget = summonHost.GetCommandedOffensiveSummonTarget();
        if (commandedTarget != null)
        {
            summonHost.ApplyOffensiveSummonTarget(commandedTarget);
            return true;
        }

        var autonomousTarget = summonHost.SelectAutonomousOffensiveSummonTarget();
        if (autonomousTarget == null)
            return false;

        summonHost.ApplyOffensiveSummonTarget(autonomousTarget);
        return true;
    }

    public override bool TryHandleNoTarget(double delta)
    {
        return Actor is IOffensiveSummonActorAIHost summonHost &&
               summonHost.TryHandleOffensiveSummonNoTarget(delta);
    }
}
