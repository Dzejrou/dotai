using Godot;

using System;

[GlobalClass]
public partial class RingOfFireSpell : Spell, IPlacementSpell
{
    private float _cooldownRemaining;

    [Export]
    public PackedScene AreaScene { get; set; }

    [Export]
    public int ManaCost { get; set; } = 30;

    [Export]
    public float Cooldown { get; set; } = 6.0f;

    [Export]
    public float Radius { get; set; } = 56.0f;

    [Export]
    public float Duration { get; set; } = 5.0f;

    [Export]
    public float TickInterval { get; set; } = 1.0f;

    [Export]
    public int DamagePerTick { get; set; } = 8;

    public bool IsAwaitingPlacement { get; private set; }
    public override int DisplayManaCost => Math.Max(0, ManaCost);
    public override float CooldownDuration => Math.Max(0.0f, Cooldown);
    public override float CooldownRemaining => Math.Max(0.0f, _cooldownRemaining);

    public override void _Process(double delta)
    {
        if (_cooldownRemaining > 0.0f)
            _cooldownRemaining = Math.Max(0.0f, _cooldownRemaining - (float)delta);
    }

    public override bool CanCast(ISpellCaster caster)
    {
        if (!base.CanCast(caster) || AreaScene == null || _cooldownRemaining > 0.0f)
            return false;

        var manaState = caster.ManaState;
        return manaState != null &&
               caster.Faction != null &&
               manaState.Current >= Math.Max(0, ManaCost);
    }

    public override bool TryCast(ISpellCaster caster)
    {
        return TryBeginPlacement(caster);
    }

    public bool TryBeginPlacement(ISpellCaster caster)
    {
        if (!CanCast(caster))
            return false;

        IsAwaitingPlacement = true;
        return true;
    }

    public bool TryPlace(ISpellCaster caster, Vector2 worldPosition)
    {
        if (!IsAwaitingPlacement)
            return false;

        if (!CanCast(caster))
        {
            CancelPlacement();
            return false;
        }

        var parent = caster.SpellOrigin.GetParent();
        if (parent == null)
        {
            CancelPlacement();
            return false;
        }

        var ringArea = AreaScene.Instantiate<RingOfFireArea>();
        if (ringArea == null)
        {
            CancelPlacement();
            return false;
        }

        if (!TrySpendCastMana(caster, ManaCost))
        {
            ringArea.QueueFree();
            CancelPlacement();
            return false;
        }

        parent.AddChild(ringArea);
        ringArea.GlobalPosition = worldPosition;
        ringArea.Initialize(caster.SpellOrigin, caster.Faction, Radius, Duration, TickInterval, DamagePerTick);

        _cooldownRemaining = Math.Max(0.0f, Cooldown);
        IsAwaitingPlacement = false;
        return true;
    }

    public void CancelPlacement()
    {
        IsAwaitingPlacement = false;
    }
}
