using Godot;

using System;

public sealed class OwnerCombatAssistBehavior : IActorBehavior
{
    private readonly Func<ActorBase, Node2D> _ownerCombatTargetGetter;
    private readonly Func<ActorBase, Node2D, bool> _targetValidator;

    public OwnerCombatAssistBehavior(
        Func<ActorBase, Node2D> ownerCombatTargetGetter,
        Func<ActorBase, Node2D, bool> targetValidator = null)
    {
        _ownerCombatTargetGetter = ownerCombatTargetGetter ?? throw new ArgumentNullException(nameof(ownerCombatTargetGetter));
        _targetValidator = targetValidator;
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor.CurrentTarget != null)
            return false;

        var ownerCombatTarget = _ownerCombatTargetGetter(actor);
        if (ownerCombatTarget == null)
            return false;

        if (_targetValidator != null && !_targetValidator(actor, ownerCombatTarget))
            return false;

        intent = ActorIntent.WithTarget(ownerCombatTarget);
        return true;
    }
}
