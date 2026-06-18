using Godot;

using System;

[GlobalClass]
public partial class WanderBehavior : Node, IActorBehavior
{
    private const float ArrivalTolerance = 6.0f;
    private readonly RandomNumberGenerator _random = new();
    private bool _isMoving;
    private float _timer;
    private Vector2 _destination;

    [Export]
    public float WanderRadius { get; set; } = 48.0f;

    [Export]
    public float MoveDurationMin { get; set; } = 0.8f;

    [Export]
    public float MoveDurationMax { get; set; } = 1.8f;

    [Export]
    public float PauseDurationMin { get; set; } = 0.8f;

    [Export]
    public float PauseDurationMax { get; set; } = 1.8f;

    [Export]
    public float SpeedMultiplier { get; set; } = 0.6f;

    public override void _Ready()
    {
        _random.Randomize();
        BeginPause();
    }

    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        if (actor == null ||
            actor.IsDead ||
            actor.IsEncounterControlled ||
            actor.Target != null ||
            actor.InCombat ||
            actor.CurrentState == CombatUnitState.ReturningHome ||
            WanderRadius <= 0.0f)
        {
            return false;
        }

        _timer = Math.Max(0.0f, _timer - Math.Max(0.0f, (float)delta));

        if (_isMoving)
        {
            if (actor.GlobalPosition.DistanceTo(_destination) <= ArrivalTolerance || _timer <= 0.0f)
            {
                BeginPause();
                intent = ActorIntent.Hold(CombatUnitState.Idle);
                return true;
            }

            intent = ActorIntent.MoveTo(_destination, CombatUnitState.Wandering, Math.Max(0.0f, SpeedMultiplier));
            return true;
        }

        if (_timer > 0.0f)
        {
            intent = ActorIntent.Hold(CombatUnitState.Idle);
            return true;
        }

        BeginMove(actor);
        intent = ActorIntent.MoveTo(_destination, CombatUnitState.Wandering, Math.Max(0.0f, SpeedMultiplier));
        return true;
    }

    private void BeginMove(Actor actor)
    {
        _destination = ChooseDestination(actor);
        _timer = RandomRange(MoveDurationMin, MoveDurationMax);
        _isMoving = true;
    }

    private void BeginPause()
    {
        _timer = RandomRange(PauseDurationMin, PauseDurationMax);
        _isMoving = false;
    }

    private Vector2 ChooseDestination(Actor actor)
    {
        var home = actor.HomePosition;
        if (actor.GlobalPosition.DistanceTo(home) > WanderRadius)
            return home;

        var angle = _random.RandfRange(0.0f, Mathf.Tau);
        var distance = _random.RandfRange(WanderRadius * 0.25f, WanderRadius);
        return home + Vector2.Right.Rotated(angle) * distance;
    }

    private float RandomRange(float minimum, float maximum)
    {
        var resolvedMin = Math.Max(0.0f, Math.Min(minimum, maximum));
        var resolvedMax = Math.Max(resolvedMin, Math.Max(minimum, maximum));
        if (Mathf.IsEqualApprox(resolvedMin, resolvedMax))
            return resolvedMin;

        return _random.RandfRange(resolvedMin, resolvedMax);
    }
}
