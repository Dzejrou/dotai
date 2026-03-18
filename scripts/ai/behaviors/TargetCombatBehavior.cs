using Godot;

using System;

public sealed class TargetCombatBehavior : IActorBehavior
{
    private readonly Func<ActorBase, Node2D, bool> _shouldDropTarget;
    private readonly CombatUnitState _moveState;
    private readonly CombatUnitState _holdState;
    private readonly float _movementSpeedMultiplier;

    public TargetCombatBehavior(
        Func<ActorBase, Node2D, bool> shouldDropTarget = null,
        CombatUnitState moveState = CombatUnitState.PursuingTarget,
        CombatUnitState holdState = CombatUnitState.Engaged,
        float movementSpeedMultiplier = 1.0f)
    {
        _shouldDropTarget = shouldDropTarget;
        _moveState = moveState;
        _holdState = holdState;
        _movementSpeedMultiplier = Math.Max(0.0f, movementSpeedMultiplier);
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        var target = actor.CurrentTarget;
        var actionController = actor.PrimaryActionController;
        if (target == null || actionController == null)
            return false;

        if (!ActorBase.IsStructurallyValidTarget(target) ||
            target is not IAttackable ||
            target is not ITargetable targetable ||
            !targetable.CanBeTargeted ||
            (_shouldDropTarget != null && _shouldDropTarget(actor, target)))
        {
            intent = ActorIntent.ClearTarget();
            return true;
        }

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        var distance = toTarget.Length();

        if (actionController.CanStartAction(actor, target))
        {
            intent = ActorIntent.UseAction();
            return true;
        }

        if (distance > actionController.PreferredRange)
        {
            intent = ActorIntent.MoveTo(target.GlobalPosition, _moveState, _movementSpeedMultiplier);
            return true;
        }

        if (distance < actionController.MinimumRange && toTarget != Vector2.Zero)
        {
            var destination = actor.GlobalPosition + -toTarget.Normalized() * actionController.PreferredRange;
            intent = ActorIntent.MoveTo(destination, _moveState, _movementSpeedMultiplier);
            return true;
        }

        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        intent = ActorIntent.Hold(_holdState);
        return true;
    }
}
