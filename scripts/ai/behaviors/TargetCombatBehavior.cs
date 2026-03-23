using Godot;

using System;

[GlobalClass]
public partial class TargetCombatBehavior : Node, IActorBehavior
{
    [Export]
    public CombatUnitState MoveState { get; set; } = CombatUnitState.PursuingTarget;

    [Export]
    public CombatUnitState HoldState { get; set; } = CombatUnitState.Engaged;

    [Export]
    public float MovementSpeedMultiplier { get; set; } = 1.0f;

    public TargetCombatBehavior() { }

    public TargetCombatBehavior(
        CombatUnitState moveState = CombatUnitState.PursuingTarget,
        CombatUnitState holdState = CombatUnitState.Engaged,
        float movementSpeedMultiplier = 1.0f)
    {
        MoveState = moveState;
        HoldState = holdState;
        MovementSpeedMultiplier = Math.Max(0.0f, movementSpeedMultiplier);
    }

    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        var target = actor.Target;
        if (target == null)
            return false;

        return TryCreateCombatIntentForTarget(
            actor,
            target,
            out intent,
            changeTarget: false,
            MoveState,
            HoldState,
            MovementSpeedMultiplier);
    }

    public static bool TryCreateCombatIntentForTarget(
        Actor actor,
        Node2D target,
        out ActorIntent intent,
        bool changeTarget = false,
        CombatUnitState moveState = CombatUnitState.PursuingTarget,
        CombatUnitState holdState = CombatUnitState.Engaged,
        float movementSpeedMultiplier = 1.0f)
    {
        intent = ActorIntent.None;

        var actionController = actor?.PrimaryActionController;
        if (actor == null || target == null || actionController == null)
            return false;

        if (!Actor.IsStructurallyValidTarget(target) ||
            target is not IAttackable ||
            target is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            intent = ActorIntent.ClearTarget();
            return true;
        }

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        var distance = toTarget.Length();
        var facingDirection = toTarget != Vector2.Zero ? toTarget : (Vector2?)null;
        var clampedSpeedMultiplier = Math.Max(0.0f, movementSpeedMultiplier);

        if (actionController.CanStartAction(actor, target))
        {
            intent = changeTarget
                ? ActorIntent.RetargetAndUseAction(target, facingDirection)
                : ActorIntent.UseAction(facingDirection);
            return true;
        }

        if (distance > actionController.PreferredRange)
        {
            intent = changeTarget
                ? ActorIntent.RetargetAndMoveTo(target, target.GlobalPosition, moveState, clampedSpeedMultiplier, facingDirection)
                : new ActorIntent
                {
                    FacingDirection = facingDirection,
                    Destination = target.GlobalPosition,
                    SpeedMultiplier = clampedSpeedMultiplier,
                    State = moveState,
                };
            return true;
        }

        if (distance < actionController.MinimumRange && toTarget != Vector2.Zero)
        {
            var destination = actor.GlobalPosition + -toTarget.Normalized() * actionController.PreferredRange;
            intent = changeTarget
                ? ActorIntent.RetargetAndMoveTo(target, destination, moveState, clampedSpeedMultiplier, facingDirection)
                : new ActorIntent
                {
                    FacingDirection = facingDirection,
                    Destination = destination,
                    SpeedMultiplier = clampedSpeedMultiplier,
                    State = moveState,
                };
            return true;
        }

        intent = changeTarget
            ? ActorIntent.RetargetAndHold(target, holdState, facingDirection)
            : ActorIntent.Hold(holdState, facingDirection);
        return true;
    }
}
