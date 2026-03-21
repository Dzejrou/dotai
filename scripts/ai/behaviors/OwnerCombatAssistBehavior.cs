using Godot;

[GlobalClass]
public partial class OwnerCombatAssistBehavior : Node, IActorBehavior
{
    [Export]
    public bool AlliedSummonsOnly { get; set; } = true;

    [Export]
    public float MaxTargetDistanceFromSummoner { get; set; } = -1.0f;

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor.Target != null)
            return false;

        var ownerCombatTarget = SummonBehaviorPresets.GetOwnerCombatAssistTarget(actor, ValidateTarget);
        if (ownerCombatTarget == null)
            return false;

        intent = ActorIntent.WithTarget(ownerCombatTarget);
        return true;
    }

    private bool ValidateTarget(ActorBase actor, Node2D target)
    {
        if (AlliedSummonsOnly && !ReferenceEquals(actor.Faction, Factions.Allies))
            return false;

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
