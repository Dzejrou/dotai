using Godot;

using System;

public sealed class CommandedTargetBehavior : IActorBehavior
{
    private readonly Func<ActorBase, Node2D> _commandedTargetGetter;

    public CommandedTargetBehavior(Func<ActorBase, Node2D> commandedTargetGetter)
    {
        _commandedTargetGetter = commandedTargetGetter ?? throw new ArgumentNullException(nameof(commandedTargetGetter));
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor.CurrentTarget != null)
            return false;

        var commandedTarget = _commandedTargetGetter(actor);
        if (commandedTarget == null)
            return false;

        intent = ActorIntent.WithTarget(commandedTarget);
        return true;
    }
}
