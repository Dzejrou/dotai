using Godot;

using System;

[GlobalClass]
public partial class CommandedTargetBehavior : Node, IActorBehavior
{
    [Export]
    public float MaxTargetDistanceFromSummoner { get; set; } = -1.0f;

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor.Target != null)
            return false;

        var summonState = SummonState.ResolveFor(actor);
        if (summonState == null)
            return false;

        var commandedTarget = summonState.GetCommandedTarget(target => ValidateTarget(actor, target));
        if (commandedTarget == null)
            return false;

        intent = ActorIntent.WithTarget(commandedTarget);
        return true;
    }

    private bool ValidateTarget(ActorBase actor, Node2D target)
    {
        if (!ActorBase.IsStructurallyValidTarget(target) ||
            target is not IAttackable ||
            target is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            return false;
        }

        if (MaxTargetDistanceFromSummoner < 0.0f)
            return true;

        var summonerNode = SummonState.ResolveFor(actor)?.SummonerNode;
        if (summonerNode == null || !GodotObject.IsInstanceValid(summonerNode) || !summonerNode.IsInsideTree())
            return false;

        return summonerNode.GlobalPosition.DistanceTo(target.GlobalPosition) <= MaxTargetDistanceFromSummoner;
    }
}
