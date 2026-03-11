using Godot;

using System;

public abstract partial class CombatUnitBase : CharacterBody2D
{
    protected AnimatedSprite2D AnimatedSprite { get; private set; }
    protected CollisionShape2D CollisionShape { get; private set; }
    protected Node2D CurrentTarget { get; private set; }
    protected float MovementSpeed { get; private set; } = 1.0f;
    protected string LastDirection { get; set; } = "south";
    protected bool IsAttacking { get; set; }

    protected void InitializeCombatUnit(AnimatedSprite2D animatedSprite, CollisionShape2D collisionShape)
    {
        AnimatedSprite = animatedSprite;
        CollisionShape = collisionShape;
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
        IsAttacking = false;
        CurrentTarget = null;
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

        if (IsAttacking)
        {
            Velocity = Vector2.Zero;
            return;
        }

        if (CanAttackNow(toTarget, delta))
        {
            StartAttack();
            return;
        }

        var desiredDirection = GetDesiredMovementDirection(toTarget, delta);
        if (desiredDirection == Vector2.Zero)
        {
            Velocity = Vector2.Zero;
            PlayIdleIfAvailable();
            return;
        }

        LastDirection = DirectionHelper.GetDirectionName(desiredDirection);
        var walkAnimation = $"walk_{LastDirection}";
        if (AnimatedSprite?.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(walkAnimation) &&
            (!AnimatedSprite.IsPlaying() || AnimatedSprite.Animation != walkAnimation))
        {
            AnimatedSprite.Play(walkAnimation);
        }

        Velocity = desiredDirection * MovementSpeed;
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

    protected virtual Vector2 GetDesiredMovementDirection(Vector2 toTarget, double delta)
    {
        if (toTarget == Vector2.Zero)
            return Vector2.Zero;

        return toTarget.Normalized();
    }

    protected abstract void AcquireTarget();

    protected abstract bool CanAttackNow(Vector2 toTarget, double delta);

    protected abstract void StartAttack();

    protected virtual bool HandleNoTarget(double delta) => false;

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
