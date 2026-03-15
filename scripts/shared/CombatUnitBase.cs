using Godot;

using System;

public abstract partial class CombatUnitBase : CharacterBody2D
{
    private const float NavigationTargetUpdateThreshold = 8.0f;
    private const float DefaultPathDesiredDistance = 6.0f;
    private const float DefaultTargetDesiredDistance = 8.0f;

    protected AnimatedSprite2D AnimatedSprite { get; private set; }
    protected CollisionShape2D CollisionShape { get; private set; }
    protected NavigationAgent2D NavigationAgent { get; private set; }
    protected Node2D CurrentTarget { get; private set; }
    protected bool IsUsingNavigationPath { get; private set; }
    protected Vector2 LastNavigationPathPosition { get; private set; }
    protected float MovementSpeed { get; private set; } = 1.0f;
    protected string LastDirection { get; set; } = "south";
    [Export]
    public CombatUnitState CurrentState { get; private set; } = CombatUnitState.Idle;

    private bool _hasNavigationDestination;
    private Vector2 _lastNavigationDestination;

    protected void InitializeCombatUnit(
        AnimatedSprite2D animatedSprite,
        CollisionShape2D collisionShape,
        NavigationAgent2D navigationAgent = null)
    {
        AnimatedSprite = animatedSprite;
        CollisionShape = collisionShape;
        NavigationAgent = navigationAgent;

        if (NavigationAgent != null)
        {
            NavigationAgent.PathDesiredDistance = DefaultPathDesiredDistance;
            NavigationAgent.TargetDesiredDistance = DefaultTargetDesiredDistance;
        }
    }

    protected void SetMovementSpeed(float speed)
    {
        MovementSpeed = Math.Max(0.0f, speed);
    }

    public void SetTarget(Node2D target)
    {
        CurrentTarget = target;
    }

    protected void ClearTarget()
    {
        CurrentTarget = null;
    }

    protected void SetCombatState(CombatUnitState state)
    {
        CurrentState = state;
    }

    protected void MarkDead()
    {
        CurrentState = CombatUnitState.Dead;
    }

    protected void FinishAttackState()
    {
        if (CurrentState != CombatUnitState.Attacking)
            return;

        SetCombatState(CurrentTarget != null ? CombatUnitState.PursuingTarget : CombatUnitState.Idle);
    }

    protected bool ValidateCurrentTarget()
    {
        if (!IsCurrentTargetStructurallyValid(CurrentTarget))
        {
            ClearTarget();
            return false;
        }

        if (ShouldLoseCurrentTarget(CurrentTarget))
        {
            ClearTarget();
            return false;
        }

        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        PrePhysicsProcess(delta);

        if (!TryEnsureActiveTarget(delta))
            return;

        var toTarget = CurrentTarget.GlobalPosition - GlobalPosition;
        var desiredMovementTarget = GetDesiredMovementTarget(CurrentTarget.GlobalPosition, delta);

        if (CurrentState == CombatUnitState.Attacking)
        {
            Velocity = Vector2.Zero;
            return;
        }

        if (CanAttackNow(toTarget, delta))
        {
            StartAttack();
            if (CurrentState == CombatUnitState.Attacking)
                SetCombatState(CombatUnitState.Attacking);
            return;
        }

        if (ShouldStayEngaged(toTarget, delta))
        {
            if (toTarget != Vector2.Zero)
                LastDirection = DirectionHelper.GetDirectionName(toTarget);

            SetCombatState(CombatUnitState.Engaged);
            Velocity = Vector2.Zero;
            PlayIdleIfAvailable();
            return;
        }

        SetCombatState(CombatUnitState.PursuingTarget);

        if (!TryMoveTowardDestination(desiredMovementTarget, 1.0f, CombatUnitState.PursuingTarget, delta))
        {
            Velocity = Vector2.Zero;
            PlayIdleIfAvailable();
            return;
        }
        MoveAndSlide();
    }

    protected void PlayIdleIfAvailable()
    {
        if (AnimatedSprite?.SpriteFrames == null)
            return;

        var idleAnimation = $"breathing-idle_{LastDirection}";
        if (AnimatedSprite.SpriteFrames.HasAnimation(idleAnimation))
        {
            if (!AnimatedSprite.IsPlaying() || AnimatedSprite.Animation != idleAnimation)
                AnimatedSprite.Play(idleAnimation);
        }
    }

    protected bool TryFinalizeDeathAnimation(StringName deathAnimation)
    {
        if (AnimatedSprite?.SpriteFrames == null)
            return false;

        var animationName = AnimatedSprite.Animation.ToString();
        if (!animationName.StartsWith(deathAnimation.ToString(), StringComparison.Ordinal))
            return false;

        var finalFrame = Math.Max(0, AnimatedSprite.SpriteFrames.GetFrameCount(animationName) - 1);
        AnimatedSprite.Stop();
        AnimatedSprite.SetFrame(finalFrame);
        SetPhysicsProcess(false);
        return true;
    }

    protected bool TryPlayDeathAnimation(StringName deathAnimation, bool disableCollisionOnDeath, bool queueFreeOnMissingAnimation = false)
    {
        if (disableCollisionOnDeath && CollisionShape != null)
            CollisionShape.CallDeferred("set", "disabled", true);

        var animationName = $"{deathAnimation}_{LastDirection}";
        if (AnimatedSprite?.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(animationName) &&
            AnimatedSprite.SpriteFrames.GetFrameCount(animationName) > 0)
        {
            AnimatedSprite.Play(animationName);
            return true;
        }

        if (queueFreeOnMissingAnimation)
            QueueFree();
        else
            SetPhysicsProcess(false);

        return false;
    }

    protected virtual bool ShouldLoseCurrentTarget(Node2D target) => false;

    protected virtual void PrePhysicsProcess(double delta) { }

    protected virtual Vector2 GetDesiredMovementTarget(Vector2 targetPosition, double delta) => targetPosition;

    protected Vector2 ResolveMovementDirection(Vector2 desiredDestination, double delta)
    {
        if (desiredDestination == GlobalPosition)
        {
            ResetNavigationPathState();
            return Vector2.Zero;
        }

        var agentInsideTree = NavigationAgent != null && NavigationAgent.IsInsideTree();
        if (!agentInsideTree)
        {
            ResetNavigationPathState();
            return desiredDestination - GlobalPosition;
        }

        var navigationMapValid = NavigationAgent.GetNavigationMap().IsValid;
        if (!navigationMapValid)
        {
            ResetNavigationPathState();
            return desiredDestination - GlobalPosition;
        }

        if (ShouldRefreshNavigationTarget(desiredDestination))
            RefreshNavigationTarget(desiredDestination);

        var nextPathPosition = NavigationAgent.GetNextPathPosition();
        IsUsingNavigationPath = true;
        LastNavigationPathPosition = nextPathPosition;

        var movementToPath = nextPathPosition - GlobalPosition;
        if (movementToPath == Vector2.Zero)
            movementToPath = desiredDestination - GlobalPosition;

        return movementToPath;
    }

    protected bool TryMoveTowardDestination(Vector2 destinationPosition, float speedMultiplier, CombatUnitState movingState, double delta)
    {
        var movement = ResolveMovementDirection(destinationPosition, delta);
        if (movement == Vector2.Zero)
            return false;

        SetCombatState(movingState);

        var normalizedMovement = movement.Normalized();
        LastDirection = DirectionHelper.GetDirectionName(normalizedMovement);
        var walkAnimation = $"walk_{LastDirection}";
        if (AnimatedSprite?.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(walkAnimation) &&
            (!AnimatedSprite.IsPlaying() || AnimatedSprite.Animation != walkAnimation))
        {
            AnimatedSprite.Play(walkAnimation);
        }

        var movementMultiplier = Math.Max(0.0f, speedMultiplier);
        Velocity = normalizedMovement * MovementSpeed * movementMultiplier;
        return true;
    }

    protected abstract void AcquireTarget();

    protected abstract bool CanAttackNow(Vector2 toTarget, double delta);

    protected abstract void StartAttack();

    protected virtual bool ShouldStayEngaged(Vector2 toTarget, double delta) => false;

    protected virtual bool HandleNoTarget(double delta) => false;

    private bool ShouldRefreshNavigationTarget(Vector2 desiredDestination)
    {
        if (!_hasNavigationDestination)
            return true;

        if (_lastNavigationDestination.DistanceTo(desiredDestination) > NavigationTargetUpdateThreshold)
            return true;

        return false;
    }

    private void RefreshNavigationTarget(Vector2 desiredDestination)
    {
        if (NavigationAgent == null)
            return;

        NavigationAgent.TargetPosition = desiredDestination;
        _hasNavigationDestination = true;
        _lastNavigationDestination = desiredDestination;
    }

    private void ResetNavigationPathState()
    {
        _hasNavigationDestination = false;
        IsUsingNavigationPath = false;
        LastNavigationPathPosition = Vector2.Zero;
    }

    private bool TryEnsureActiveTarget(double delta)
    {
        if (!ValidateCurrentTarget())
        {
            AcquireTarget();

            if (!ValidateCurrentTarget())
            {
                if (HandleNoTarget(delta))
                {
                    MoveAndSlide();
                    return false;
                }

                ResetNavigationPathState();
                SetCombatState(CombatUnitState.Idle);
                Velocity = Vector2.Zero;
                PlayIdleIfAvailable();
                return false;
            }
        }

        return true;
    }

    private static bool IsCurrentTargetStructurallyValid(Node2D target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
            return false;

        if (target.GetParent() == null)
            return false;

        if (target is not IAttackable || target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        return true;
    }
}
