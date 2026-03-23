using Godot;

using System;

public sealed class HealActionController : ICombatActionController
{
    private readonly StringName _healAnimation;
    private readonly int _healAmount;
    private float _cooldownTimer;
    private Node2D _pendingHealTarget;

    public HealActionController(float preferredRange, float actionCooldown, StringName healAnimation, int healAmount)
    {
        PreferredRange = Math.Max(0.0f, preferredRange);
        MinimumRange = 0.0f;
        ActionCooldown = Math.Max(0.0f, actionCooldown);
        _healAnimation = healAnimation;
        _healAmount = Math.Max(1, healAmount);
    }

    public float MinimumRange { get; }
    public float PreferredRange { get; }
    public float ActionCooldown { get; }

    public void Update(Actor actor, double delta)
    {
        if (_cooldownTimer > 0.0f)
            _cooldownTimer -= (float)delta;
    }

    public bool CanStartAction(Actor actor, Node2D target)
    {
        if (_cooldownTimer > 0.0f)
            return false;

        if (target == null || !Actor.IsStructurallyValidTarget(target))
            return false;

        if (target is not IHealable healable || !healable.CanReceiveHealing)
            return false;

        return actor.GlobalPosition.DistanceTo(target.GlobalPosition) <= PreferredRange;
    }

    public void StartAction(Actor actor, Node2D target)
    {
        if (!CanStartAction(actor, target) || target is not IHealable healable)
        {
            if (target == null || !Actor.IsStructurallyValidTarget(target))
                actor.ClearTarget();
            return;
        }

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        actor.SetState(CombatUnitState.Attacking);
        _cooldownTimer = ActionCooldown;

        var animationName = $"{_healAnimation}_{actor.LastDirection}";
        if (actor.AnimatedSprite?.SpriteFrames != null &&
            actor.AnimatedSprite.SpriteFrames.HasAnimation(animationName) &&
            actor.AnimatedSprite.SpriteFrames.GetFrameCount(animationName) > 0)
        {
            _pendingHealTarget = target;
            actor.AnimatedSprite.Play(animationName);
        }
        else
        {
            ApplyPendingHeal(healable);
            actor.FinishAttackState();
        }
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(_healAnimation.ToString(), StringComparison.Ordinal))
            return false;

        if (_pendingHealTarget is IHealable healable &&
            Actor.IsStructurallyValidTarget(_pendingHealTarget) &&
            healable.CanReceiveHealing)
        {
            ApplyPendingHeal(healable);
        }

        _pendingHealTarget = null;
        actor.FinishAttackState();
        return true;
    }

    public void Cancel(Actor actor)
    {
        _cooldownTimer = 0.0f;
        _pendingHealTarget = null;
    }

    private void ApplyPendingHeal(IHealable healable)
    {
        if (healable == null || !healable.CanReceiveHealing)
            return;

        healable.ApplyHealing(_healAmount);
    }
}
