using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class PatrolBehavior : Node, IActorBehavior
{
    private const string PatrolPathNodeName = "PatrolPath";
    private const float ArrivalTolerance = 8.0f;

    private int _currentTargetIndex;
    private int _travelDirection = 1;
    private float _pauseRemainingSeconds;
    private bool _isPaused;
    private bool _initialized;

    [Export]
    public bool LoopMode { get; set; } = false;

    [Export]
    public float SpeedMultiplier { get; set; } = 0.7f;

    [Export]
    public float PointPauseSeconds { get; set; } = 0.0f;

    [Export]
    public float EndpointPauseSeconds { get; set; } = 1.0f;

    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor == null || actor.IsDead)
        {
            ResetTraversal();
            return false;
        }

        // Yield while higher-priority combat behaviors are active so patrol can coexist with future upgrades.
        if (actor.Target != null)
            return false;

        var patrolPoints = ResolvePatrolPoints(actor);
        if (patrolPoints.Count <= 1)
        {
            ResetTraversal();
            return false;
        }

        EnsureTraversalState(patrolPoints.Count);

        if (_isPaused)
        {
            _pauseRemainingSeconds = Math.Max(0.0f, _pauseRemainingSeconds - Math.Max(0.0f, (float)delta));
            if (_pauseRemainingSeconds > 0.0f)
            {
                intent = ActorIntent.Hold(CombatUnitState.Idle);
                return true;
            }

            _isPaused = false;
            AdvanceToNextPoint(patrolPoints.Count);
        }

        for (var transitionCount = 0; transitionCount < patrolPoints.Count; transitionCount++)
        {
            var targetPosition = patrolPoints[_currentTargetIndex].GlobalPosition;
            if (actor.GlobalPosition.DistanceTo(targetPosition) > ArrivalTolerance)
            {
                intent = ActorIntent.MoveTo(
                    targetPosition,
                    CombatUnitState.Wandering,
                    Math.Max(0.0f, SpeedMultiplier));
                return true;
            }

            var pauseSeconds = ResolvePauseSeconds(patrolPoints.Count, _currentTargetIndex);
            if (pauseSeconds > 0.0f)
            {
                _pauseRemainingSeconds = pauseSeconds;
                _isPaused = true;
                intent = ActorIntent.Hold(CombatUnitState.Idle);
                return true;
            }

            AdvanceToNextPoint(patrolPoints.Count);
        }

        intent = ActorIntent.Hold(CombatUnitState.Idle);
        return true;
    }

    private List<Marker2D> ResolvePatrolPoints(Actor actor)
    {
        var patrolPath = actor.GetNodeOrNull<Node>(PatrolPathNodeName);
        var patrolPoints = new List<Marker2D>();
        if (patrolPath == null)
            return patrolPoints;

        foreach (var child in patrolPath.GetChildren())
        {
            if (child is Marker2D marker)
                patrolPoints.Add(marker);
        }

        return patrolPoints;
    }

    private void EnsureTraversalState(int pointCount)
    {
        if (!_initialized)
        {
            _currentTargetIndex = 0;
            _travelDirection = 1;
            _pauseRemainingSeconds = 0.0f;
            _isPaused = false;
            _initialized = true;
            return;
        }

        _currentTargetIndex = Math.Clamp(_currentTargetIndex, 0, pointCount - 1);
        _travelDirection = _travelDirection >= 0 ? 1 : -1;
    }

    private float ResolvePauseSeconds(int pointCount, int pointIndex)
    {
        if (!LoopMode && (pointIndex == 0 || pointIndex == pointCount - 1))
            return Math.Max(0.0f, EndpointPauseSeconds);

        return Math.Max(0.0f, PointPauseSeconds);
    }

    private void AdvanceToNextPoint(int pointCount)
    {
        if (pointCount <= 1)
            return;

        if (LoopMode)
        {
            _currentTargetIndex = (_currentTargetIndex + 1) % pointCount;
            _travelDirection = 1;
            return;
        }

        if (_currentTargetIndex <= 0)
            _travelDirection = 1;
        else if (_currentTargetIndex >= pointCount - 1)
            _travelDirection = -1;

        _currentTargetIndex = Math.Clamp(_currentTargetIndex + _travelDirection, 0, pointCount - 1);
    }

    private void ResetTraversal()
    {
        _currentTargetIndex = 0;
        _travelDirection = 1;
        _pauseRemainingSeconds = 0.0f;
        _isPaused = false;
        _initialized = false;
    }
}
