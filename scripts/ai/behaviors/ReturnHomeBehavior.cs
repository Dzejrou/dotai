using Godot;

using System;

public sealed class ReturnHomeBehavior : IActorBehavior
{
    private readonly Func<ActorBase, Vector2> _destinationGetter;
    private readonly Func<ActorBase, bool> _isAtDestination;
    private readonly CombatUnitState _moveState;
    private readonly float _speedMultiplier;

    public ReturnHomeBehavior(
        Func<ActorBase, Vector2> destinationGetter,
        Func<ActorBase, bool> isAtDestination,
        CombatUnitState moveState = CombatUnitState.ReturningHome,
        float speedMultiplier = 1.0f)
    {
        _destinationGetter = destinationGetter ?? throw new ArgumentNullException(nameof(destinationGetter));
        _isAtDestination = isAtDestination ?? throw new ArgumentNullException(nameof(isAtDestination));
        _moveState = moveState;
        _speedMultiplier = Math.Max(0.0f, speedMultiplier);
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor.CurrentTarget != null)
            return false;

        if (_isAtDestination(actor))
        {
            intent = ActorIntent.Hold(CombatUnitState.Idle);
            return true;
        }

        intent = ActorIntent.MoveTo(_destinationGetter(actor), _moveState, _speedMultiplier);
        return true;
    }
}
