using Godot;

using System;

[GlobalClass]
public partial class RejuvenationActionController : Node, ICombatActionController
{
    private enum PendingAction
    {
        None,
        Heal,
        Rejuvenation,
    }

    private float _cooldownTimer;
    private float _rejuvenationCooldownTimer;
    private Node2D _pendingTarget;
    private PendingAction _pendingAction = PendingAction.None;

    [Export]
    public float PreferredRange { get; set; } = 256.0f;

    [Export]
    public float HealCooldown { get; set; } = 1.4f;

    [Export]
    public StringName CastAnimation { get; set; } = "cast";

    [Export]
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public float RejuvenationDuration { get; set; } = 16.0f;

    [Export]
    public float RejuvenationTickInterval { get; set; } = 2.0f;

    [Export]
    public int RejuvenationHealPerTick { get; set; } = 3;

    [Export]
    public float RejuvenationCooldown { get; set; } = 20.0f;

    [Export]
    public int HealAmount { get; set; } = 3;

    public float MinimumRange => 0.0f;

    public override void _Ready()
    {
        PreferredRange = Math.Max(0.0f, PreferredRange);
        HealCooldown = Math.Max(0.0f, HealCooldown);
        AnimationSpeedMultiplier = Math.Max(0.0f, AnimationSpeedMultiplier);
        RejuvenationDuration = Math.Max(0.1f, RejuvenationDuration);
        RejuvenationTickInterval = Math.Max(0.1f, RejuvenationTickInterval);
        RejuvenationHealPerTick = Math.Max(1, RejuvenationHealPerTick);
        RejuvenationCooldown = Math.Max(0.0f, RejuvenationCooldown);
        HealAmount = Math.Max(1, HealAmount);
    }

    public void Update(Actor actor, double delta)
    {
        var castSpeedMultiplier = Math.Max(0.0f, actor.CastSpeedMultiplier);
        if (_cooldownTimer > 0.0f)
            _cooldownTimer -= (float)delta * castSpeedMultiplier;

        if (_rejuvenationCooldownTimer > 0.0f)
            _rejuvenationCooldownTimer -= (float)delta * castSpeedMultiplier;
    }

    public bool CanStartAction(Actor actor, Node2D target)
    {
        if (target == null || !Actor.IsStructurallyValidTarget(target))
            return false;

        if (!actor.IsFriendlyTo(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        if (target is not IHealable healable || !healable.CanReceiveHealing)
            return false;

        if (actor.GlobalPosition.DistanceTo(target.GlobalPosition) > PreferredRange)
            return false;

        if (CanCastRejuvenation(target) || CanCastHeal())
            return true;

        return false;
    }

    public void StartAction(Actor actor, Node2D target)
    {
        if (!CanStartAction(actor, target))
        {
            if (target == null || !Actor.IsStructurallyValidTarget(target))
                actor.ClearTarget();
            return;
        }

        ClearPendingAction();

        var pendingAction = ResolvePendingAction(target);
        if (pendingAction == PendingAction.None)
            return;

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        actor.SetState(CombatUnitState.Attacking);
        _pendingTarget = target;
        _pendingAction = pendingAction;
        if (pendingAction == PendingAction.Rejuvenation)
            _rejuvenationCooldownTimer = RejuvenationCooldown;
        else
            _cooldownTimer = HealCooldown;

        if (actor.TryPlayDirectionalAnimation(CastAnimation.ToString(), AnimationSpeedMultiplier * Math.Max(0.0f, actor.CastSpeedMultiplier)))
        {
            return;
        }

        ApplyPendingAction(actor, target, pendingAction);
        actor.FinishAttackState();
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(CastAnimation.ToString(), StringComparison.Ordinal))
            return false;

        if (_pendingTarget is Node2D target &&
            IsValidPendingTarget(actor, target) &&
            _pendingAction != PendingAction.None)
        {
            ApplyPendingAction(actor, target, _pendingAction);
        }

        ClearPendingAction();
        actor.FinishAttackState();
        return true;
    }

    public void Cancel(Actor actor)
    {
        _cooldownTimer = 0.0f;
        _rejuvenationCooldownTimer = 0.0f;
        ClearPendingAction();
    }

    private void ClearPendingAction()
    {
        _pendingTarget = null;
        _pendingAction = PendingAction.None;
    }

    private static bool IsValidPendingTarget(Actor actor, Node2D target)
    {
        if (actor == null ||
            target == null ||
            !Actor.IsStructurallyValidTarget(target) ||
            !GodotObject.IsInstanceValid(target) ||
            !target.IsInsideTree())
        {
            return false;
        }

        if (!actor.IsFriendlyTo(target))
            return false;

        return target is ITargetable targetable &&
               targetable.CanBeTargeted &&
               target is IHealable healable &&
               healable.CanReceiveHealing;
    }

    private PendingAction ResolvePendingAction(Node2D target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
            return PendingAction.None;

        var canRejuvenate = CanCastRejuvenation(target);
        if (canRejuvenate)
            return PendingAction.Rejuvenation;

        if (CanCastHeal())
            return PendingAction.Heal;

        return PendingAction.None;
    }

    private bool CanCastHeal()
    {
        return _cooldownTimer <= 0.0f;
    }

    private bool CanCastRejuvenation(Node2D target)
    {
        if (_rejuvenationCooldownTimer > 0.0f)
            return false;

        if (target == null ||
            !GodotObject.IsInstanceValid(target) ||
            !target.IsInsideTree() ||
            target is not IHealable healable ||
            !healable.CanReceiveHealing)
        {
            return false;
        }

        var statusEffectController = target.GetNodeOrNull<StatusEffectController>("StatusEffectController");
        if (statusEffectController == null || statusEffectController.HasStatus(RejuvenationEffect.StatusKeyName))
            return false;

        return true;
    }

    private void ApplyPendingAction(Actor actor, Node2D target, PendingAction pendingAction)
    {
        if (target == null ||
            !GodotObject.IsInstanceValid(target) ||
            !target.IsInsideTree() ||
            target is not IHealable healable ||
            !healable.CanReceiveHealing)
        {
            return;
        }

        if (pendingAction == PendingAction.Heal)
        {
            healable.ApplyHealing(HealAmount);
            return;
        }

        if (pendingAction != PendingAction.Rejuvenation)
            return;

        var statusEffectController = target.GetNodeOrNull<StatusEffectController>("StatusEffectController");
        if (statusEffectController == null)
            return;

        var effect = new RejuvenationEffect
        {
            DurationSeconds = RejuvenationDuration,
            TickIntervalSeconds = RejuvenationTickInterval,
            HealPerTick = RejuvenationHealPerTick,
        };

        statusEffectController.ApplyStatusEffect(effect, actor);
    }
}
