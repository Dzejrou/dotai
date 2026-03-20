using Godot;

using System;

[GlobalClass]
public partial class ReturnHomeBehavior : Node, IActorBehavior
{
    [Export]
    public CombatUnitState MoveState { get; set; } = CombatUnitState.ReturningHome;

    [Export]
    public float SpeedMultiplier { get; set; } = 1.0f;

    private readonly Func<ActorBase, Vector2> _destinationGetter;
    private readonly Func<ActorBase, bool> _isAtDestination;

    public ReturnHomeBehavior() { }

    public ReturnHomeBehavior(
        Func<ActorBase, Vector2> destinationGetter,
        Func<ActorBase, bool> isAtDestination,
        CombatUnitState moveState = CombatUnitState.ReturningHome,
        float speedMultiplier = 1.0f)
    {
        _destinationGetter = destinationGetter ?? throw new ArgumentNullException(nameof(destinationGetter));
        _isAtDestination = isAtDestination ?? throw new ArgumentNullException(nameof(isAtDestination));
        MoveState = moveState;
        SpeedMultiplier = Math.Max(0.0f, speedMultiplier);
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor.CurrentTarget != null)
            return false;

        if (IsAtDestination(actor))
        {
            intent = ActorIntent.Hold(CombatUnitState.Idle);
            return true;
        }

        intent = ActorIntent.MoveTo(GetDestination(actor), MoveState, Math.Max(0.0f, SpeedMultiplier));
        return true;
    }

    private bool IsAtDestination(ActorBase actor)
    {
        return _isAtDestination?.Invoke(actor) ?? actor.IsAtHome();
    }

    private Vector2 GetDestination(ActorBase actor)
    {
        return _destinationGetter?.Invoke(actor) ?? actor.HomePosition;
    }
}
