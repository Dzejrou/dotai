using Godot;

using System;

// Transition step: walk the boss back to a transition anchor using normal
// navigation/pathing, completing once it is within tolerance. The anchor is an
// optional exported marker (resolved relative to the actor); otherwise it falls back
// to the actor's home/spawn position.
[GlobalClass]
public partial class MoveToAnchorTransitionAction : BossTransitionAction
{
    [Export]
    public NodePath AnchorPath { get; set; } = new NodePath();

    [Export]
    public float SpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public float ArrivalTolerance { get; set; } = 8.0f;

    private Node2D _resolvedAnchor;
    private bool _anchorResolved;

    protected override void OnBegin(Actor actor) { }

    public override void Update(Actor actor, double delta)
    {
        if (actor == null)
            return;

        if (actor.GlobalPosition.DistanceTo(ResolveAnchorPosition(actor)) <= Math.Max(0.0f, ArrivalTolerance))
            IsComplete = true;
    }

    public override ActorIntent BuildIntent(Actor actor)
    {
        return ActorIntent.MoveTo(
            ResolveAnchorPosition(actor),
            CombatUnitState.Transitioning,
            Math.Max(0.0f, SpeedMultiplier));
    }

    private Vector2 ResolveAnchorPosition(Actor actor)
    {
        var anchor = ResolveAnchorNode(actor);
        return anchor != null && GodotObject.IsInstanceValid(anchor)
            ? anchor.GlobalPosition
            : actor.HomePosition;
    }

    private Node2D ResolveAnchorNode(Actor actor)
    {
        if (_anchorResolved)
            return _resolvedAnchor;

        _anchorResolved = true;
        if (AnchorPath != null && !AnchorPath.IsEmpty && actor.HasNode(AnchorPath))
            _resolvedAnchor = actor.GetNodeOrNull<Node2D>(AnchorPath);

        return _resolvedAnchor;
    }
}
