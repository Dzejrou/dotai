using Godot;

public readonly struct ActorIntent
{
    public bool ChangeTarget { get; init; }
    public Node2D Target { get; init; }
    public Vector2? FacingDirection { get; init; }
    public Vector2? Destination { get; init; }
    public Vector2? TeleportDestination { get; init; }
    public float SpeedMultiplier { get; init; }
    public CombatUnitState State { get; init; }
    public bool StopMovement { get; init; }
    public bool UsePrimaryAction { get; init; }
    public bool RemoveNow { get; init; }

    public bool HasExecutionDirective => Destination.HasValue || TeleportDestination.HasValue || StopMovement || UsePrimaryAction || RemoveNow;

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

    public static ActorIntent Hold(CombatUnitState state, Vector2? facingDirection = null)
    {
        return new ActorIntent
        {
            FacingDirection = facingDirection,
            StopMovement = true,
            State = state,
        };
    }

    public static ActorIntent UseAction(Vector2? facingDirection = null)
    {
        return new ActorIntent
        {
            FacingDirection = facingDirection,
            StopMovement = true,
            UsePrimaryAction = true,
            State = CombatUnitState.Attacking,
        };
    }

    public static ActorIntent RetargetAndMoveTo(Node2D target, Vector2 destination, CombatUnitState state, float speedMultiplier = 1.0f, Vector2? facingDirection = null)
    {
        return new ActorIntent
        {
            ChangeTarget = true,
            Target = target,
            FacingDirection = facingDirection,
            Destination = destination,
            SpeedMultiplier = speedMultiplier,
            State = state,
        };
    }

    public static ActorIntent RetargetAndHold(Node2D target, CombatUnitState state, Vector2? facingDirection = null)
    {
        return new ActorIntent
        {
            ChangeTarget = true,
            Target = target,
            FacingDirection = facingDirection,
            StopMovement = true,
            State = state,
        };
    }

    public static ActorIntent RetargetAndUseAction(Node2D target, Vector2? facingDirection = null)
    {
        return new ActorIntent
        {
            ChangeTarget = true,
            Target = target,
            FacingDirection = facingDirection,
            StopMovement = true,
            UsePrimaryAction = true,
            State = CombatUnitState.Attacking,
        };
    }

    public static ActorIntent ClearTargetAndMoveTo(Vector2 destination, CombatUnitState state, float speedMultiplier = 1.0f)
    {
        return new ActorIntent
        {
            ChangeTarget = true,
            Target = null,
            Destination = destination,
            SpeedMultiplier = speedMultiplier,
            State = state,
        };
    }

    public static ActorIntent TeleportAndHold(Vector2 destination, CombatUnitState state)
    {
        return new ActorIntent
        {
            TeleportDestination = destination,
            StopMovement = true,
            State = state,
        };
    }

    public static ActorIntent Remove()
    {
        return new ActorIntent
        {
            RemoveNow = true,
        };
    }
}
