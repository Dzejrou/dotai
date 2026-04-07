using Godot;

using System;

[GlobalClass]
public partial class SpellCastActionController : Node, ICombatActionController
{
    private enum SpellSlot
    {
        None,
        Basic,
        CloseRange,
        LongRange,
    }

    private SpellSlot _pendingSpell = SpellSlot.None;
    private Spell _basicSpell;
    private Spell _closeRangeSpell;
    private Spell _longRangeSpell;

    [Export]
    public NodePath SpellNodePath { get; set; } = new("../Spells/FireBall");

    [Export]
    public float MinimumRange { get; set; } = 70.0f;

    [Export]
    public float PreferredRange { get; set; } = 120.0f;

    [Export]
    public StringName AttackAnimation { get; set; } = "cast";

    [Export]
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public NodePath CloseRangeSpellNodePath { get; set; }

    [Export]
    public float CloseRangeMaxDistance { get; set; } = 56.0f;

    [Export]
    public NodePath LongRangeSpellNodePath { get; set; }

    public override void _Ready()
    {
        MinimumRange = Math.Max(0.0f, MinimumRange);
        PreferredRange = Math.Max(MinimumRange, PreferredRange);
        AnimationSpeedMultiplier = Math.Max(0.0f, AnimationSpeedMultiplier);
        CloseRangeMaxDistance = Math.Max(0.0f, CloseRangeMaxDistance);
        _basicSpell = ResolveSpell(SpellNodePath);
        _closeRangeSpell = ResolveSpell(CloseRangeSpellNodePath);
        _longRangeSpell = ResolveSpell(LongRangeSpellNodePath);
    }

    public void Update(Actor actor, double delta)
    {
    }

    public bool CanStartAction(Actor actor, Node2D target)
    {
        if (actor is not ISpellCaster caster)
            return false;

        if (target == null || !Actor.IsStructurallyValidTarget(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        var targetFactionState = FactionState.ResolveFor(target);
        if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(actor.Faction))
            return false;

        var distance = actor.GlobalPosition.DistanceTo(target.GlobalPosition);
        return ResolveSpellSlot(caster, distance) != SpellSlot.None;
    }

    public void StartAction(Actor actor, Node2D target)
    {
        if (actor is not ISpellCaster caster)
            return;

        var spellSlot = ResolveSpellSlot(caster, target);
        if (spellSlot == SpellSlot.None)
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

        if (actor.TryPlayDirectionalAnimation(AttackAnimation.ToString(), AnimationSpeedMultiplier * Math.Max(0.0f, actor.CastSpeedMultiplier)))
        {
            _pendingSpell = spellSlot;
            return;
        }

        TryCast(actor, spellSlot);
        actor.FinishAttackState();
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
            return false;

        if (_pendingSpell != SpellSlot.None)
        {
            TryCast(actor, _pendingSpell);
            ClearPendingCast();
        }

        actor.FinishAttackState();
        return true;
    }

    public void Cancel(Actor actor)
    {
        ClearPendingCast();
    }

    private void ClearPendingCast()
    {
        _pendingSpell = SpellSlot.None;
    }

    private Spell ResolveSpell(NodePath spellNodePath)
    {
        if (spellNodePath.IsEmpty)
            return null;

        var spellNode = GetNodeOrNull<Node>(spellNodePath);
        if (spellNode == null)
        {
            GD.PushError($"{GetPath()}: Spell node not found at {spellNodePath}.");
            return null;
        }

        if (spellNode is not Spell spell)
        {
            GD.PushError($"{spellNode.GetPath()}: Spell node must inherit Spell.");
            return null;
        }

        return spell;
    }

    private SpellSlot ResolveSpellSlot(ISpellCaster caster, Node2D target)
    {
        if (caster == null || target == null || !Actor.IsStructurallyValidTarget(target))
            return SpellSlot.None;

        var distance = caster.SpellOrigin.GlobalPosition.DistanceTo(target.GlobalPosition);
        return ResolveSpellSlot(caster, distance);
    }

    private SpellSlot ResolveSpellSlot(ISpellCaster caster, float distance)
    {
        if (CanUseCloseRangeSpell(caster, distance))
            return SpellSlot.CloseRange;

        if (CanUseLongRangeSpell(caster, distance))
            return SpellSlot.LongRange;

        if (CanUseBasicSpell(caster, distance))
            return SpellSlot.Basic;

        return SpellSlot.None;
    }

    private bool CanUseBasicSpell(ISpellCaster caster, float distance)
    {
        return _basicSpell != null &&
               distance >= MinimumRange &&
               distance <= PreferredRange &&
               _basicSpell.CanCast(caster);
    }

    private bool CanUseCloseRangeSpell(ISpellCaster caster, float distance)
    {
        return _closeRangeSpell != null &&
               distance <= CloseRangeMaxDistance &&
               _closeRangeSpell.CanCast(caster);
    }

    private bool CanUseLongRangeSpell(ISpellCaster caster, float distance)
    {
        return _longRangeSpell != null &&
               distance > PreferredRange &&
               _longRangeSpell.CanCast(caster);
    }

    private void TryCast(Actor actor, SpellSlot spellSlot)
    {
        if (actor is not ISpellCaster caster)
            return;

        var spell = ResolveSpellForSlot(spellSlot);
        if (spell == null)
            return;

        spell.TryCast(caster);
    }

    private Spell ResolveSpellForSlot(SpellSlot spellSlot)
    {
        if (spellSlot == SpellSlot.CloseRange)
        {
            if (_closeRangeSpell == null)
                _closeRangeSpell = ResolveSpell(CloseRangeSpellNodePath);

            return _closeRangeSpell;
        }

        if (spellSlot == SpellSlot.LongRange)
        {
            if (_longRangeSpell == null)
                _longRangeSpell = ResolveSpell(LongRangeSpellNodePath);

            return _longRangeSpell;
        }

        if (_basicSpell == null)
            _basicSpell = ResolveSpell(SpellNodePath);

        return _basicSpell;
    }
}
