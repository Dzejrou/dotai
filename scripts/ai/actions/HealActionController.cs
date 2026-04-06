using Godot;

using System;

[GlobalClass]
public partial class HealActionController : Node, ICombatActionController
{
    private Node2D _pendingTarget;
    private Spell _pendingSpell;
    private HealSpell _healSpell;
    private HealOverTimeSpell _healOverTimeSpell;

    [Export]
    public NodePath HealSpellNodePath { get; set; } = new("../Spells/Heal");

    [Export]
    public NodePath HealOverTimeSpellNodePath { get; set; } = new("../Spells/Rejuvenation");

    [Export]
    public StringName CastAnimation { get; set; } = "cast";

    [Export]
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    public float MinimumRange => 0.0f;
    public float PreferredRange => Math.Max(_healSpell?.Range ?? 0.0f, _healOverTimeSpell?.Range ?? 0.0f);

    public override void _Ready()
    {
        AnimationSpeedMultiplier = Math.Max(0.0f, AnimationSpeedMultiplier);
        _healSpell = ResolveSpell<HealSpell>(HealSpellNodePath);
        _healOverTimeSpell = ResolveSpell<HealOverTimeSpell>(HealOverTimeSpellNodePath);
    }

    public void Update(Actor actor, double delta)
    {
    }

    public bool CanStartAction(Actor actor, Node2D target)
    {
        if (actor is not ISpellCaster caster || !IsValidSupportTarget(actor, target))
            return false;

        return ResolveSpell(caster, target) != null;
    }

    public void StartAction(Actor actor, Node2D target)
    {
        if (actor is not ISpellCaster caster)
            return;

        if (!CanStartAction(actor, target))
        {
            if (target == null || !Actor.IsStructurallyValidTarget(target))
                actor.ClearTarget();
            return;
        }

        ClearPendingAction();

        var spell = ResolveSpell(caster, target);
        if (spell == null)
            return;

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        actor.SetState(CombatUnitState.Attacking);
        _pendingTarget = target;
        _pendingSpell = spell;

        if (actor.TryPlayDirectionalAnimation(
                CastAnimation.ToString(),
                AnimationSpeedMultiplier * Math.Max(0.0f, actor.CastSpeedMultiplier)))
        {
            return;
        }

        TryCast(actor, target, spell);
        actor.FinishAttackState();
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(CastAnimation.ToString(), StringComparison.Ordinal))
            return false;

        if (_pendingSpell != null && _pendingTarget != null)
            TryCast(actor, _pendingTarget, _pendingSpell);

        ClearPendingAction();
        actor.FinishAttackState();
        return true;
    }

    public void Cancel(Actor actor)
    {
        ClearPendingAction();
    }

    private void ClearPendingAction()
    {
        _pendingTarget = null;
        _pendingSpell = null;
    }

    private Spell ResolveSpell(ISpellCaster caster, Node2D target)
    {
        if (_healOverTimeSpell != null && _healOverTimeSpell.CanCastOn(caster, target))
            return _healOverTimeSpell;

        if (_healSpell != null && _healSpell.CanCastOn(caster, target))
            return _healSpell;

        return null;
    }

    private bool TryCast(Actor actor, Node2D target, Spell spell)
    {
        if (actor is not ISpellCaster caster || !IsValidSupportTarget(actor, target))
            return false;

        return spell switch
        {
            HealOverTimeSpell healOverTimeSpell => healOverTimeSpell.TryCastOn(caster, target),
            HealSpell healSpell => healSpell.TryCastOn(caster, target),
            _ => false,
        };
    }

    private T ResolveSpell<T>(NodePath spellNodePath) where T : class
    {
        if (spellNodePath.IsEmpty)
            return null;

        var spellNode = GetNodeOrNull<Node>(spellNodePath);
        if (spellNode == null)
        {
            GD.PushError($"{GetPath()}: Spell node not found at {spellNodePath}.");
            return null;
        }

        if (spellNode is not T spell)
        {
            GD.PushError($"{spellNode.GetPath()}: Spell node must inherit {typeof(T).Name}.");
            return null;
        }

        return spell;
    }

    private static bool IsValidSupportTarget(Actor actor, Node2D target)
    {
        if (actor == null || !Actor.IsStructurallyValidTarget(target))
            return false;

        if (!actor.IsFriendlyTo(target))
            return false;

        return target is ITargetable targetable &&
               targetable.CanBeTargeted &&
               target is IHealable healable &&
               healable.CanReceiveHealing;
    }
}
