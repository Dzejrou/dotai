using Godot;

using System;

public sealed class MeleeAttackController : ICombatActionController
{
    private readonly StringName _attackAnimation;
    private readonly int _minimumDamage;
    private readonly int _maximumDamage;
    private readonly RandomNumberGenerator _randomNumberGenerator = new();
    private float _cooldownTimer;

    public MeleeAttackController(float preferredRange, float attackCooldown, StringName attackAnimation, int minimumDamage, int maximumDamage)
    {
        PreferredRange = Math.Max(0.0f, preferredRange);
        MinimumRange = 0.0f;
        AttackCooldown = Math.Max(0.0f, attackCooldown);
        _attackAnimation = attackAnimation;
        _minimumDamage = minimumDamage;
        _maximumDamage = Math.Max(minimumDamage, maximumDamage);
        _randomNumberGenerator.Randomize();
    }

    public float MinimumRange { get; }
    public float PreferredRange { get; }
    public float AttackCooldown { get; }

    public void Update(ActorBase actor, double delta)
    {
        if (_cooldownTimer > 0.0f)
            _cooldownTimer -= (float)delta;
    }

    public bool CanStartAction(ActorBase actor, Node2D target)
    {
        if (_cooldownTimer > 0.0f)
            return false;

        if (target == null || !ActorBase.IsStructurallyValidTarget(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        return actor.GlobalPosition.DistanceTo(target.GlobalPosition) <= PreferredRange;
    }

    public void StartAction(ActorBase actor, Node2D target)
    {
        if (!CanStartAction(actor, target) ||
            target is not IAttackable attackable ||
            target is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            actor.ClearTarget();
            return;
        }

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        actor.SetState(CombatUnitState.Attacking);
        _cooldownTimer = AttackCooldown;

        var attackAnimation = $"{_attackAnimation}_{actor.LastDirection}";
        if (actor.AnimatedSprite?.SpriteFrames != null &&
            actor.AnimatedSprite.SpriteFrames.HasAnimation(attackAnimation) &&
            actor.AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimation) > 0)
        {
            actor.AnimatedSprite.Play(attackAnimation);
        }
        else
        {
            actor.SetState(CombatUnitState.PursuingTarget);
        }

        var damage = _randomNumberGenerator.RandiRange(Math.Min(_minimumDamage, _maximumDamage), _maximumDamage);
        attackable.ApplyDamage(new DamageInfo(damage, actor));
    }

    public bool HandleAnimationFinished(ActorBase actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(_attackAnimation.ToString(), StringComparison.Ordinal))
            return false;

        actor.FinishAttackState();
        return true;
    }

    public void Cancel(ActorBase actor)
    {
        _cooldownTimer = 0.0f;
    }
}
