using Godot;

using System;

public sealed class OwnerCombatAssistBehavior : IActorBehavior
{
    private readonly Func<ActorBase, Node2D, bool> _targetValidator;

    public OwnerCombatAssistBehavior(
        Func<ActorBase, Node2D, bool> targetValidator = null)
    {
        _targetValidator = targetValidator;
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor.CurrentTarget != null)
            return false;

        var ownerCombatTarget = SummonBehaviorPresets.GetOwnerCombatAssistTarget(actor, _targetValidator);
        if (ownerCombatTarget == null)
            return false;

        intent = ActorIntent.WithTarget(ownerCombatTarget);
        return true;
    }
}
