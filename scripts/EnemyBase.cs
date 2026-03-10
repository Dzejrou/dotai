using Godot;

using System;

public abstract partial class EnemyBase : CharacterBody2D
{
    [Export]
    public NodePath PlayerPath { get; set; } = new NodePath("../Player");

    [Export]
    public StringName DeathAnimation { get; set; } = "falling-back-death";

    [Export]
    public bool DisableCollisionOnDeath { get; set; } = true;

    protected AnimatedSprite2D AnimatedSprite { get; private set; }
    protected CollisionShape2D CollisionShape { get; private set; }
    protected Node2D CurrentTarget { get; private set; }
    protected string LastDirection { get; set; } = "south";

    protected void InitializeEnemy(AnimatedSprite2D animatedSprite, CollisionShape2D collisionShape, string enemyName)
    {
        AnimatedSprite = animatedSprite;
        CollisionShape = collisionShape;
        AddToGroup(CombatGroups.Enemies);

        var resolvedPlayer = CurrentTarget as Player;
        if (resolvedPlayer == null)
        {
            if (!PlayerPath.IsEmpty && HasNode(PlayerPath))
                resolvedPlayer = GetNode<Player>(PlayerPath);
            else
                resolvedPlayer = GetParent()?.GetNodeOrNull<Player>("Player");
        }

        if (resolvedPlayer != null)
        {
            SetPlayer(resolvedPlayer);
        }
        else
        {
            AcquireTarget();
        }

        if (resolvedPlayer == null && CurrentTarget == null)
            GD.PrintErr($"{enemyName} could not find Player node.");
    }

    protected void ClearPlayer()
    {
        ClearTarget();
    }

    protected bool ValidateCurrentTarget()
    {
        if (CurrentTarget == null)
        {
            return false;
        }

        if (!GodotObject.IsInstanceValid(CurrentTarget) || !CurrentTarget.IsInsideTree())
        {
            ClearTarget();
            return false;
        }

        if (CurrentTarget is not IAttackable)
        {
            ClearTarget();
            return false;
        }

        return true;
    }


    protected void AcquireTarget()
    {
        CurrentTarget = TargetingHelper.FindClosestTarget(
            this,
            CombatGroups.Allies,
            node => node is Node2D && node is IAttackable);
    }

    protected void ClearTarget()
    {
        CurrentTarget = null;
    }

    public void SetPlayer(Player player)
    {
        CurrentTarget = player;
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
            CollisionShape.Disabled = true;

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
}
