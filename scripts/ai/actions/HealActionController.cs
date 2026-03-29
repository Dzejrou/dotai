using Godot;

using System;

[GlobalClass]
public partial class HealActionController : Node, ICombatActionController
{
    private float _cooldownTimer;
    private Node2D _pendingHealTarget;

    [Export]
    public float PreferredRange { get; set; } = 148.0f;

    [Export]
    public float ActionCooldown { get; set; } = 1.4f;

    [Export]
    public StringName HealAnimation { get; set; } = "cast";

    [Export]
    public int HealAmount { get; set; } = 3;

    [Export]
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    public float MinimumRange => 0.0f;

    public override void _Ready()
    {
        PreferredRange = Math.Max(0.0f, PreferredRange);
        ActionCooldown = Math.Max(0.0f, ActionCooldown);
        HealAmount = Math.Max(1, HealAmount);
        AnimationSpeedMultiplier = Math.Max(0.0f, AnimationSpeedMultiplier);
    }

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

        if (actor.TryPlayDirectionalAnimation(HealAnimation.ToString(), AnimationSpeedMultiplier))
        {
            _pendingHealTarget = target;
        }
        else
        {
            ApplyPendingHeal(healable);
            actor.FinishAttackState();
        }
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(HealAnimation.ToString(), StringComparison.Ordinal))
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

        healable.ApplyHealing(HealAmount);
    }
}
