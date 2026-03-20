using Godot;

using System;

[GlobalClass]
public partial class TargetCombatBehavior : Node, IActorBehavior
{
    [Export]
    public bool DropTargetWhileSummonerNeedsLeashReturn { get; set; } = false;

    [Export]
    public CombatUnitState MoveState { get; set; } = CombatUnitState.PursuingTarget;

    [Export]
    public CombatUnitState HoldState { get; set; } = CombatUnitState.Engaged;

    [Export]
    public float MovementSpeedMultiplier { get; set; } = 1.0f;

    private readonly Func<ActorBase, Node2D, bool> _shouldDropTarget;

    public TargetCombatBehavior() { }

    public TargetCombatBehavior(
        Func<ActorBase, Node2D, bool> shouldDropTarget = null,
        CombatUnitState moveState = CombatUnitState.PursuingTarget,
        CombatUnitState holdState = CombatUnitState.Engaged,
        float movementSpeedMultiplier = 1.0f)
    {
        _shouldDropTarget = shouldDropTarget;
        MoveState = moveState;
        HoldState = holdState;
        MovementSpeedMultiplier = Math.Max(0.0f, movementSpeedMultiplier);
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
            ShouldDropTarget(actor, target))
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
            intent = ActorIntent.MoveTo(target.GlobalPosition, MoveState, Math.Max(0.0f, MovementSpeedMultiplier));
            return true;
        }

        if (distance < actionController.MinimumRange && toTarget != Vector2.Zero)
        {
            var destination = actor.GlobalPosition + -toTarget.Normalized() * actionController.PreferredRange;
            intent = ActorIntent.MoveTo(destination, MoveState, Math.Max(0.0f, MovementSpeedMultiplier));
            return true;
        }

        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        intent = ActorIntent.Hold(HoldState);
        return true;
    }

    private bool ShouldDropTarget(ActorBase actor, Node2D target)
    {
        if (_shouldDropTarget != null)
            return _shouldDropTarget(actor, target);

        if (!DropTargetWhileSummonerNeedsLeashReturn)
            return false;

        var followSummonerBehavior = actor.GetNodeOrNull<FollowSummonerBehavior>("Behaviors/Tier90_Recovery/FollowSummonerBehavior");
        return followSummonerBehavior != null && followSummonerBehavior.ShouldPrioritizeLeashReturn(actor);
    }
}
