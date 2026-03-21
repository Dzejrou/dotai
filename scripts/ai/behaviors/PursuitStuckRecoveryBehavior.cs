using Godot;

using System;

public sealed class PursuitStuckRecoveryBehavior : IActorBehavior, IActorTickBehavior
{
    private readonly float _progressThreshold;
    private readonly float _timeoutSeconds;
    private readonly float _waypointDistance;
    private readonly Func<ActorBase, bool> _shouldTrack;
    private readonly Action<ActorBase> _onStuck;
    private bool _hasProgressPosition;
    private Vector2 _lastProgressPosition;
    private float _stuckTimer;
    private Node2D _trackedTarget;

    public PursuitStuckRecoveryBehavior(
        float progressThreshold,
        float timeoutSeconds,
        float waypointDistance,
        Func<ActorBase, bool> shouldTrack,
        Action<ActorBase> onStuck)
    {
        _progressThreshold = Math.Max(0.0f, progressThreshold);
        _timeoutSeconds = Math.Max(0.0f, timeoutSeconds);
        _waypointDistance = Math.Max(0.0f, waypointDistance);
        _shouldTrack = shouldTrack ?? throw new ArgumentNullException(nameof(shouldTrack));
        _onStuck = onStuck ?? throw new ArgumentNullException(nameof(onStuck));
    }

    public bool TryCreateIntent(ActorBase actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;
        return false;
    }

    public void Update(ActorBase actor, double delta)
    {
        if (!_shouldTrack(actor) ||
            !actor.IsUsingNavigationPath ||
            actor.Velocity == Vector2.Zero ||
            actor.GlobalPosition.DistanceTo(actor.LastNavigationPathPosition) <= _waypointDistance)
        {
            Reset();
            return;
        }

        if (!ReferenceEquals(_trackedTarget, actor.Target))
        {
            _trackedTarget = actor.Target;
            _hasProgressPosition = true;
            _lastProgressPosition = actor.GlobalPosition;
            _stuckTimer = 0.0f;
            return;
        }

        if (!_hasProgressPosition)
        {
            _hasProgressPosition = true;
            _lastProgressPosition = actor.GlobalPosition;
            _stuckTimer = 0.0f;
            return;
        }

        if (actor.GlobalPosition.DistanceTo(_lastProgressPosition) > _progressThreshold)
        {
            _lastProgressPosition = actor.GlobalPosition;
            _stuckTimer = 0.0f;
            return;
        }

        _stuckTimer += Math.Max(0.0f, (float)delta);
        if (_stuckTimer < _timeoutSeconds)
            return;

        _onStuck(actor);
        Reset();
    }

    private void Reset()
    {
        _hasProgressPosition = false;
        _lastProgressPosition = Vector2.Zero;
        _stuckTimer = 0.0f;
        _trackedTarget = null;
    }
}
