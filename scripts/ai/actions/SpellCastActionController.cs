using Godot;

using System;

[GlobalClass]
public partial class SpellCastActionController : Node, ICombatActionController
{
    private float _cooldownTimer;
    private bool _hasPendingCast;
    private Spell _spell;

    [Export]
    public NodePath SpellNodePath { get; set; } = new("../Spells/Fireball");

    [Export]
    public float MinimumRange { get; set; } = 70.0f;

    [Export]
    public float PreferredRange { get; set; } = 120.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.2f;

    [Export]
    public StringName AttackAnimation { get; set; } = "cast_spell";

    [Export]
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    public override void _Ready()
    {
        MinimumRange = Math.Max(0.0f, MinimumRange);
        PreferredRange = Math.Max(MinimumRange, PreferredRange);
        AttackCooldown = Math.Max(0.0f, AttackCooldown);
        AnimationSpeedMultiplier = Math.Max(0.0f, AnimationSpeedMultiplier);
        _spell = ResolveSpell();
    }

    public void Update(Actor actor, double delta)
    {
        if (_cooldownTimer > 0.0f)
            _cooldownTimer -= (float)delta;
    }

    public bool CanStartAction(Actor actor, Node2D target)
    {
        if (_cooldownTimer > 0.0f || _spell == null || actor is not ISpellCaster)
            return false;

        if (target == null || !Actor.IsStructurallyValidTarget(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        var targetFactionState = FactionState.ResolveFor(target);
        if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(actor.Faction))
            return false;

        var distance = actor.GlobalPosition.DistanceTo(target.GlobalPosition);
        return distance >= MinimumRange && distance <= PreferredRange;
    }

    public void StartAction(Actor actor, Node2D target)
    {
        if (!CanStartAction(actor, target))
        {
            if (target == null || !Actor.IsStructurallyValidTarget(target))
                actor.ClearTarget();
            return;
        }

        ClearPendingCast();

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        actor.SetState(CombatUnitState.Attacking);
        _cooldownTimer = AttackCooldown;

        var attackAnimationName = $"{AttackAnimation}_{actor.LastDirection}";
        if (actor.AnimatedSprite?.SpriteFrames != null &&
            actor.AnimatedSprite.SpriteFrames.HasAnimation(attackAnimationName) &&
            actor.AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimationName) > 0)
        {
            _hasPendingCast = true;
            actor.AnimatedSprite.Play(attackAnimationName, customSpeed: AnimationSpeedMultiplier);
            return;
        }

        TryCast(actor);
        actor.FinishAttackState();
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
            return false;

        if (_hasPendingCast)
        {
            TryCast(actor);
            ClearPendingCast();
        }

        actor.FinishAttackState();
        return true;
    }

    public void Cancel(Actor actor)
    {
        _cooldownTimer = 0.0f;
        ClearPendingCast();
    }

    private void ClearPendingCast()
    {
        _hasPendingCast = false;
    }

    private Spell ResolveSpell()
    {
        if (SpellNodePath.IsEmpty)
            return null;

        var spellNode = GetNodeOrNull<Node>(SpellNodePath);
        if (spellNode == null)
        {
            GD.PushError($"{GetPath()}: Spell node not found at {SpellNodePath}.");
            return null;
        }

        if (spellNode is not Spell spell)
        {
            GD.PushError($"{spellNode.GetPath()}: Spell node must inherit Spell.");
            return null;
        }

        return spell;
    }

    private void TryCast(Actor actor)
    {
        if (_spell == null)
            _spell = ResolveSpell();

        if (_spell == null || actor is not ISpellCaster caster)
            return;

        _spell.TryCast(caster);
    }
}
