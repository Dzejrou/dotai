using Godot;

using System;

public sealed class CommandedTargetBehavior : IActorBehavior
{
    private readonly Func<ActorBase, Node2D, bool> _targetValidator;

    public CommandedTargetBehavior(Func<ActorBase, Node2D, bool> targetValidator)
    {
        _targetValidator = targetValidator ?? throw new ArgumentNullException(nameof(targetValidator));
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor.CurrentTarget != null)
            return false;

        var summonState = SummonState.ResolveFor(actor);
        if (summonState == null)
            return false;

        var commandedTarget = summonState.GetCommandedTarget(target => _targetValidator(actor, target));
        if (commandedTarget == null)
            return false;

        intent = ActorIntent.WithTarget(commandedTarget);
        return true;
    }
}
