using Godot;

public readonly struct ActorIntent
{
    public bool ChangeTarget { get; init; }
    public Node2D Target { get; init; }
    public Vector2? Destination { get; init; }
    public float SpeedMultiplier { get; init; }
    public CombatUnitState State { get; init; }
    public bool StopMovement { get; init; }
    public bool UsePrimaryAction { get; init; }

    public bool HasExecutionDirective => Destination.HasValue || StopMovement || UsePrimaryAction;

    public static ActorIntent None => default;

    public static ActorIntent WithTarget(Node2D target)
    {
        return new ActorIntent
        {
            ChangeTarget = true,
            Target = target,
        };
    }

    public static ActorIntent ClearTarget()
    {
        return new ActorIntent
        {
            ChangeTarget = true,
            Target = null,
        };
    }

    public static ActorIntent MoveTo(Vector2 destination, CombatUnitState state, float speedMultiplier = 1.0f)
    {
        return new ActorIntent
        {
            Destination = destination,
            SpeedMultiplier = speedMultiplier,
            State = state,
        };
    }

    public static ActorIntent Hold(CombatUnitState state)
    {
        return new ActorIntent
        {
            StopMovement = true,
            State = state,
        };
    }

    public static ActorIntent UseAction()
    {
        return new ActorIntent
        {
            StopMovement = true,
            UsePrimaryAction = true,
            State = CombatUnitState.Attacking,
        };
    }
}
