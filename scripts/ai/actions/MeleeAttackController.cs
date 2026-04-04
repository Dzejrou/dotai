using Godot;

using System;

[GlobalClass]
public partial class MeleeAttackController : Node, ICombatActionController
{
    private readonly RandomNumberGenerator _randomNumberGenerator = new();
    private float _cooldownTimer;

    [Export]
    public float PreferredRange { get; set; } = 18.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.0f;

    [Export]
    public StringName AttackAnimation { get; set; } = "attack";

    [Export]
    public int MinimumDamage { get; set; } = 1;

    [Export]
    public int MaximumDamage { get; set; } = 1;

    [Export]
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    public float MinimumRange => 0.0f;

    public override void _Ready()
    {
        PreferredRange = Math.Max(0.0f, PreferredRange);
        AttackCooldown = Math.Max(0.0f, AttackCooldown);
        MaximumDamage = Math.Max(MinimumDamage, MaximumDamage);
        AnimationSpeedMultiplier = Math.Max(0.0f, AnimationSpeedMultiplier);
        _randomNumberGenerator.Randomize();
    }

    public void Update(Actor actor, double delta)
    {
        if (_cooldownTimer > 0.0f)
            _cooldownTimer -= (float)delta * Math.Max(0.0f, actor.AttackSpeedMultiplier);
    }

    public bool CanStartAction(Actor actor, Node2D target)
    {
        if (_cooldownTimer > 0.0f)
            return false;

        if (target == null || !Actor.IsStructurallyValidTarget(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        return actor.GlobalPosition.DistanceTo(target.GlobalPosition) <= PreferredRange;
    }

    public void StartAction(Actor actor, Node2D target)
    {
        if (!CanStartAction(actor, target) ||
            target is not IAttackable attackable ||
            target is not ITargetable targetable ||
            !targetable.CanBeTargeted ||
            FactionState.ResolveFor(target) is not FactionState targetFactionState ||
            !targetFactionState.CanBeDamagedBy(actor.Faction))
        {
            actor.ClearTarget();
            return;
        }

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        actor.SetState(CombatUnitState.Attacking);
        _cooldownTimer = AttackCooldown;

        if (!actor.TryPlayDirectionalAnimation(AttackAnimation.ToString(), AnimationSpeedMultiplier * Math.Max(0.0f, actor.AttackSpeedMultiplier)))
        {
            actor.SetState(CombatUnitState.PursuingTarget);
        }

        var damage = _randomNumberGenerator.RandiRange(Math.Min(MinimumDamage, MaximumDamage), MaximumDamage);
        attackable.ApplyDamage(new DamageInfo(damage, actor));
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
            return false;

        actor.FinishAttackState();
        return true;
    }

    public void Cancel(Actor actor)
    {
        _cooldownTimer = 0.0f;
    }
}
