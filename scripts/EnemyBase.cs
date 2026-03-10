using Godot;

using System;

public abstract partial class EnemyBase : CharacterBody2D
{
    [Export]
    public NodePath InitialTargetPath { get; set; } = new NodePath("../Player");

    [Export]
    public StringName DeathAnimation { get; set; } = "falling-back-death";

    [Export]
    public bool DisableCollisionOnDeath { get; set; } = true;

    [Export]
    public float AggroAcquisitionRange { get; set; } = 150.0f;

    [Export]
    public float AggroLossRange { get; set; } = 220.0f;

    [Export]
    public float HomeReturnTolerance { get; set; } = 4.0f;

    protected AnimatedSprite2D AnimatedSprite { get; private set; }
    protected CollisionShape2D CollisionShape { get; private set; }
    protected Node2D CurrentTarget { get; private set; }
    protected Vector2 HomePosition { get; private set; }
    protected float MovementSpeed { get; private set; } = 1.0f;
    protected string LastDirection { get; set; } = "south";
    protected bool IsAttacking { get; set; }

    protected void InitializeEnemy(AnimatedSprite2D animatedSprite, CollisionShape2D collisionShape, string enemyName)
    {
        AnimatedSprite = animatedSprite;
        CollisionShape = collisionShape;
        AddToGroup(CombatGroups.Enemies);
        HomePosition = GlobalPosition;

        var resolvedTarget = CurrentTarget;
        if (resolvedTarget == null)
        {
            if (!InitialTargetPath.IsEmpty && HasNode(InitialTargetPath))
                resolvedTarget = GetNode<Node2D>(InitialTargetPath);
            else
                resolvedTarget = GetParent()?.GetNodeOrNull<Node2D>("Player");
        }

        if (resolvedTarget != null && CanAcquireTarget(resolvedTarget))
        {
            SetTarget(resolvedTarget);
        }
        else if (resolvedTarget != null && !CanAcquireTarget(resolvedTarget))
            GD.PrintErr($"{enemyName} did not acquire initial target (not in aggro range).");
    }


    protected bool ValidateCurrentTarget()
    {
        if (CurrentTarget == null)
        {
            return false;
        }

        if (ShouldLoseCurrentTarget(CurrentTarget))
        {
            ClearTarget();
            return false;
        }

        return true;
    }


    protected void AcquireTarget()
    {
        var candidate = TargetingHelper.FindClosestTarget(
            this,
            CombatGroups.Allies,
            node => node is Node2D && node is IAttackable);

        if (candidate != null && CanAcquireTarget(candidate))
            CurrentTarget = candidate;
    }

    protected void ClearTarget()
    {
        CurrentTarget = null;
    }

    public void SetTarget(Node2D target)
    {
        CurrentTarget = target;
    }

    protected void SetMovementSpeed(float speed)
    {
        MovementSpeed = Math.Max(0.0f, speed);
    }

    protected bool CanAcquireTarget(Node2D target)
    {
        return target is IAttackable && IsTargetWithinAcquisitionRange(target);
    }

    protected bool ShouldLoseCurrentTarget(Node2D target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target))
            return true;

        if (!target.IsInsideTree())
            return true;

        if (target is not IAttackable)
            return true;

        return !IsTargetWithinLossRange(target);
    }

    private bool IsTargetWithinAcquisitionRange(Node2D target)
    {
        return IsTargetWithinRange(target, Math.Max(0.0f, AggroAcquisitionRange));
    }

    private bool IsTargetWithinLossRange(Node2D target)
    {
        return IsTargetWithinRange(target, Math.Max(AggroLossRange, AggroAcquisitionRange));
    }

    private bool IsTargetWithinRange(Node2D target, float range)
    {
        if (target == null)
            return false;

        return GlobalPosition.DistanceTo(target.GlobalPosition) <= range;
    }

    protected bool IsAtHome()
    {
        return GlobalPosition.DistanceTo(HomePosition) <= Math.Max(0.0f, HomeReturnTolerance);
    }

    public override void _PhysicsProcess(double delta)
    {
        PrePhysicsProcess(delta);

        if (!TryEnsureActiveTarget())
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

        if (toTarget == Vector2.Zero)
        {
            Velocity = Vector2.Zero;
            AnimatedSprite.Stop();
            return;
        }

        LastDirection = DirectionHelper.GetDirectionName(toTarget);
        var walkAnimation = $"walk_{LastDirection}";
        if (AnimatedSprite.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(walkAnimation) &&
            (!AnimatedSprite.IsPlaying() || AnimatedSprite.Animation != walkAnimation))
            AnimatedSprite.Play(walkAnimation);

        Velocity = toTarget.Normalized() * MovementSpeed;
        MoveAndSlide();
    }

    protected virtual void PrePhysicsProcess(double delta) { }

    protected abstract bool CanAttackNow(Vector2 toTarget, double delta);

    protected abstract void StartAttack();

    protected bool TryReturnHome()
    {
        if (IsAtHome())
            return false;

        var toHome = HomePosition - GlobalPosition;
        if (toHome == Vector2.Zero)
            return false;

        LastDirection = DirectionHelper.GetDirectionName(toHome);
        var walkAnimation = $"walk_{LastDirection}";
        if (AnimatedSprite.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(walkAnimation) &&
            (!AnimatedSprite.IsPlaying() || AnimatedSprite.Animation != walkAnimation))
        {
            AnimatedSprite.Play(walkAnimation);
        }

        Velocity = toHome.Normalized() * MovementSpeed;
        return true;
    }

    protected bool TryFinalizeDeathAnimation()
    {
        var animationName = AnimatedSprite.Animation.ToString();
        if (!animationName.StartsWith(DeathAnimation.ToString(), StringComparison.Ordinal))
            return false;

        var finalFrame = Math.Max(0, AnimatedSprite.SpriteFrames.GetFrameCount(animationName) - 1);
        AnimatedSprite.Stop();
        AnimatedSprite.SetFrame(finalFrame);
        SetPhysicsProcess(false);
        return true;
    }

    protected bool TryPlayDeathAnimation()
    {
        if (DisableCollisionOnDeath && CollisionShape != null)
            CollisionShape.CallDeferred("set", "disabled", true);

        var deathAnimation = $"{DeathAnimation}_{LastDirection}";
        if (AnimatedSprite.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(deathAnimation) &&
            AnimatedSprite.SpriteFrames.GetFrameCount(deathAnimation) > 0)
        {
            AnimatedSprite.Play(deathAnimation);
            return true;
        }

        AnimatedSprite.Stop();
        SetPhysicsProcess(false);
        return false;
    }

    private bool TryEnsureActiveTarget()
    {
        if (!ValidateCurrentTarget())
        {
            AcquireTarget();

            if (!ValidateCurrentTarget())
            {
                if (TryReturnHome())
                {
                    MoveAndSlide();
                    return false;
                }

                Velocity = Vector2.Zero;
                AnimatedSprite.Stop();
                return false;
            }
        }

        return true;
    }
}
