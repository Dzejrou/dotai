using Godot;

using System;

[GlobalClass]
public partial class TargetCombatBehavior : Node, IActorBehavior
{
    // Tracks an explicit-range instance's repositioning so the boss does not jitter
    // around a single threshold: a correction runs until the distance is back at the
    // preferred radius, not merely back inside the triggering edge.
    private enum RangeBandMode
    {
        Hold,
        Approaching,
        Retreating,
    }

    [Export]
    public CombatUnitState MoveState { get; set; } = CombatUnitState.PursuingTarget;

    [Export]
    public CombatUnitState HoldState { get; set; } = CombatUnitState.Engaged;

    [Export]
    public float MovementSpeedMultiplier { get; set; } = 1.0f;

    // Opt-in: when true this instance positions using the explicit distance band below
    // instead of the primary action controller's Minimum/PreferredRange. Existing
    // instances leave this false and behave exactly as before.
    [Export]
    public bool UseExplicitCombatRange { get; set; } = false;

    [Export(PropertyHint.Range, "0,4096,0.1,or_greater")]
    public float MinimumDistance { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "0,4096,0.1,or_greater")]
    public float PreferredDistance { get; set; } = 150.0f;

    [Export(PropertyHint.Range, "0,4096,0.1,or_greater")]
    public float MaximumDistance { get; set; } = 240.0f;

    // When true, dropping below MinimumDistance makes the actor retreat before it casts;
    // when false, casting outranks retreat even inside the minimum distance.
    [Export]
    public bool RetreatBelowMinimum { get; set; } = true;

    // If a retreat fails to gain at least RetreatProgressEpsilon of distance for this
    // long, the actor gives up and holds/casts for CorneredHoldSeconds instead of
    // fleeing forever when cornered or blocked.
    [Export(PropertyHint.Range, "0,30,0.05,or_greater")]
    public float RetreatStuckTimeoutSeconds { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0,512,0.1,or_greater")]
    public float RetreatProgressEpsilon { get; set; } = 4.0f;

    [Export(PropertyHint.Range, "0,30,0.05,or_greater")]
    public float CorneredHoldSeconds { get; set; } = 2.5f;

    private RangeBandMode _rangeMode = RangeBandMode.Hold;
    private float _retreatStuckElapsed;
    private float _retreatReferenceDistance;
    private float _corneredHoldRemaining;

    public TargetCombatBehavior() { }

    public TargetCombatBehavior(
        CombatUnitState moveState = CombatUnitState.PursuingTarget,
        CombatUnitState holdState = CombatUnitState.Engaged,
        float movementSpeedMultiplier = 1.0f)
    {
        MoveState = moveState;
        HoldState = holdState;
        MovementSpeedMultiplier = Math.Max(0.0f, movementSpeedMultiplier);
    }

    public bool TryCreateIntent(Actor actor, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        var target = actor.Target;
        if (target == null)
        {
            ResetRangeBandState();
            return false;
        }

        if (UseExplicitCombatRange)
            return TryCreateRangeBandIntent(actor, target, delta, out intent);

        return TryCreateCombatIntentForTarget(
            actor,
            target,
            out intent,
            changeTarget: false,
            MoveState,
            HoldState,
            MovementSpeedMultiplier);
    }

    public static bool TryCreateCombatIntentForTarget(
        Actor actor,
        Node2D target,
        out ActorIntent intent,
        bool changeTarget = false,
        CombatUnitState moveState = CombatUnitState.PursuingTarget,
        CombatUnitState holdState = CombatUnitState.Engaged,
        float movementSpeedMultiplier = 1.0f)
    {
        intent = ActorIntent.None;

        var actionController = actor?.PrimaryActionController;
        if (actor == null || target == null || actionController == null)
            return false;

        if (!Actor.IsStructurallyValidTarget(target) ||
            target is not IAttackable ||
            target is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            intent = ActorIntent.ClearTarget();
            return true;
        }

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        var distance = toTarget.Length();
        var facingDirection = toTarget != Vector2.Zero ? toTarget : (Vector2?)null;
        var clampedSpeedMultiplier = Math.Max(0.0f, movementSpeedMultiplier);

        if (actionController.CanStartAction(actor, target))
        {
            intent = changeTarget
                ? ActorIntent.RetargetAndUseAction(target, facingDirection)
                : ActorIntent.UseAction(facingDirection);
            return true;
        }

        if (distance > actionController.PreferredRange)
        {
            intent = changeTarget
                ? ActorIntent.RetargetAndMoveTo(target, target.GlobalPosition, moveState, clampedSpeedMultiplier, facingDirection)
                : new ActorIntent
                {
                    FacingDirection = facingDirection,
                    Destination = target.GlobalPosition,
                    SpeedMultiplier = clampedSpeedMultiplier,
                    State = moveState,
                };
            return true;
        }

        if (distance < actionController.MinimumRange && toTarget != Vector2.Zero)
        {
            var destination = actor.GlobalPosition + -toTarget.Normalized() * actionController.PreferredRange;
            intent = changeTarget
                ? ActorIntent.RetargetAndMoveTo(target, destination, moveState, clampedSpeedMultiplier, facingDirection)
                : new ActorIntent
                {
                    FacingDirection = facingDirection,
                    Destination = destination,
                    SpeedMultiplier = clampedSpeedMultiplier,
                    State = moveState,
                };
            return true;
        }

        intent = changeTarget
            ? ActorIntent.RetargetAndHold(target, holdState, facingDirection)
            : ActorIntent.Hold(holdState, facingDirection);
        return true;
    }

    // Ranged variant of the combat loop. It reuses the same cast/target-validation path
    // as the default behavior but positions the actor inside an explicit distance band
    // (Minimum/Preferred/Maximum) so a ranged actor kites instead of closing to melee.
    private bool TryCreateRangeBandIntent(Actor actor, Node2D target, double delta, out ActorIntent intent)
    {
        intent = ActorIntent.None;

        var actionController = actor?.PrimaryActionController;
        if (actor == null || target == null || actionController == null)
            return false;

        if (!Actor.IsStructurallyValidTarget(target) ||
            target is not IAttackable ||
            target is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            ResetRangeBandState();
            intent = ActorIntent.ClearTarget();
            return true;
        }

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        var distance = toTarget.Length();
        var facingDirection = toTarget != Vector2.Zero ? toTarget : (Vector2?)null;
        var clampedSpeedMultiplier = Math.Max(0.0f, MovementSpeedMultiplier);

        // Re-derive an ordered, non-negative band even if the exported values were
        // authored out of order.
        var minDistance = Math.Max(0.0f, MinimumDistance);
        var preferredDistance = Math.Max(minDistance, PreferredDistance);
        var maxDistance = Math.Max(preferredDistance, MaximumDistance);

        var step = (float)Math.Max(0.0, delta);
        UpdateRangeMode(distance, minDistance, preferredDistance, maxDistance, step);

        switch (_rangeMode)
        {
            case RangeBandMode.Approaching:
                // Closing the gap: walk straight at the target until back at preferred.
                intent = MoveTowards(target.GlobalPosition, clampedSpeedMultiplier, facingDirection);
                return true;

            case RangeBandMode.Retreating when toTarget != Vector2.Zero:
                // Back off only to the preferred radius (a point on the actor's own side
                // of the target) instead of overshooting by a full preferred distance.
                var retreatDestination = target.GlobalPosition + -toTarget.Normalized() * preferredDistance;
                intent = MoveTowards(retreatDestination, clampedSpeedMultiplier, facingDirection);
                return true;

            default:
                // Hold position inside the band and cast whenever the action controller
                // can act (phase filtering decides which spell that is).
                intent = actionController.CanStartAction(actor, target)
                    ? ActorIntent.UseAction(facingDirection)
                    : ActorIntent.Hold(HoldState, facingDirection);
                return true;
        }
    }

    private ActorIntent MoveTowards(Vector2 destination, float speedMultiplier, Vector2? facingDirection)
    {
        return new ActorIntent
        {
            FacingDirection = facingDirection,
            Destination = destination,
            SpeedMultiplier = speedMultiplier,
            State = MoveState,
        };
    }

    private void UpdateRangeMode(float distance, float minDistance, float preferredDistance, float maxDistance, float step)
    {
        // A cornered actor that gave up retreating holds and casts for a bounded window
        // before it is allowed to test an escape again. If something else restored the
        // range in the meantime, end the hold early and resume normal positioning.
        if (_corneredHoldRemaining > 0.0f)
        {
            _corneredHoldRemaining = Math.Max(0.0f, _corneredHoldRemaining - step);
            if (distance < preferredDistance)
            {
                _rangeMode = RangeBandMode.Hold;
                _retreatStuckElapsed = 0.0f;
                return;
            }

            _corneredHoldRemaining = 0.0f;
        }

        switch (_rangeMode)
        {
            case RangeBandMode.Approaching:
                // Distinct exit threshold (preferred, not max) gives a dead-band so the
                // actor does not toggle move/hold on the maximum boundary.
                if (distance <= preferredDistance)
                    EnterHold();
                break;

            case RangeBandMode.Retreating:
                if (distance >= preferredDistance)
                {
                    EnterHold();
                    break;
                }

                AdvanceRetreatStuck(distance, step);
                break;

            default:
                if (distance > maxDistance)
                {
                    _rangeMode = RangeBandMode.Approaching;
                }
                else if (distance < minDistance && RetreatBelowMinimum)
                {
                    _rangeMode = RangeBandMode.Retreating;
                    _retreatReferenceDistance = distance;
                    _retreatStuckElapsed = 0.0f;
                }

                break;
        }
    }

    private void AdvanceRetreatStuck(float distance, float step)
    {
        // Any meaningful gain in distance counts as progress and refreshes the timer.
        if (distance > _retreatReferenceDistance + Math.Max(0.0f, RetreatProgressEpsilon))
        {
            _retreatReferenceDistance = distance;
            _retreatStuckElapsed = 0.0f;
            return;
        }

        _retreatStuckElapsed += step;
        if (_retreatStuckElapsed < Math.Max(0.0f, RetreatStuckTimeoutSeconds))
            return;

        // Cornered or blocked: stop fleeing and hold/cast for a bounded window.
        _corneredHoldRemaining = Math.Max(0.0f, CorneredHoldSeconds);
        _rangeMode = RangeBandMode.Hold;
        _retreatStuckElapsed = 0.0f;
    }

    private void EnterHold()
    {
        _rangeMode = RangeBandMode.Hold;
        _retreatStuckElapsed = 0.0f;
    }

    private void ResetRangeBandState()
    {
        _rangeMode = RangeBandMode.Hold;
        _retreatStuckElapsed = 0.0f;
        _retreatReferenceDistance = 0.0f;
        _corneredHoldRemaining = 0.0f;
    }
}
