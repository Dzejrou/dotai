using Godot;

using System;

[GlobalClass]
public partial class FrostNovaSpell : Spell
{
    private float _cooldownRemaining;

    [Export]
    public PackedScene VfxScene { get; set; }

    [Export]
    public float Range { get; set; } = 72.0f;

    [Export]
    public int ManaCost { get; set; } = 20;

    [Export]
    public float Cooldown { get; set; } = 15.0f;

    [Export]
    public int DirectDamage { get; set; } = 5;

    [Export]
    public float SlowDuration { get; set; } = 6.0f;

    [Export]
    public float SlowMovementSpeedMultiplier { get; set; } = 0.5f;

    [Export]
    public float SlowAttackSpeedMultiplier { get; set; } = 0.33f;

    [Export]
    public float SlowCastSpeedMultiplier { get; set; } = 0.2f;

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
        if (!base.CanCast(caster) || VfxScene == null || _cooldownRemaining > 0.0f)
            return false;

        var manaState = caster.ManaState;
        return manaState != null &&
               caster.Faction != null &&
               manaState.Current >= Math.Max(0, ManaCost);
    }

    public override bool TryCast(ISpellCaster caster)
    {
        if (!CanCast(caster))
            return false;

        if (!TrySpendCastMana(caster, ManaCost))
            return false;

        var resolvedRange = Math.Max(0.0f, Range);
        if (resolvedRange <= 0.0f)
        {
            _cooldownRemaining = Math.Max(0.0f, Cooldown);
            return true;
        }

        SpawnVfx(caster.SpellOrigin, resolvedRange);
        ApplyNovaEffects(caster, resolvedRange);
        _cooldownRemaining = Math.Max(0.0f, Cooldown);
        return true;
    }

    private void SpawnVfx(Node2D spellOrigin, float range)
    {
        if (VfxScene?.Instantiate<FireNovaVfx>() is not FireNovaVfx frostNovaVfx)
            return;

        var parent = spellOrigin.GetParent();
        if (parent == null)
        {
            frostNovaVfx.QueueFree();
            return;
        }

        parent.AddChild(frostNovaVfx);
        frostNovaVfx.GlobalPosition = spellOrigin.GlobalPosition;
        frostNovaVfx.Play(range);
    }

    private void ApplyNovaEffects(ISpellCaster caster, float range)
    {
        // TODO: share this target sweep with FireNovaSpell once nova variants are unified.
        var sourceFaction = caster.Faction;
        var source = caster.SpellOrigin;
        foreach (var node in TargetingHelper.EnumerateCandidateTargets(source))
        {
            if (node is not IAttackable attackable)
                continue;

            var targetFactionState = FactionState.ResolveFor(node);
            if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(sourceFaction))
                continue;

            if (source.GlobalPosition.DistanceTo(node.GlobalPosition) > range)
                continue;

            attackable.ApplyDamage(new DamageInfo(Math.Max(1, DirectDamage), source));

            var statusEffectController = node.GetNodeOrNull<StatusEffectController>("StatusEffectController");
            if (statusEffectController == null)
                continue;

            var slowedEffect = new SlowedEffect
            {
                DurationSeconds = Math.Max(0.0f, SlowDuration),
                MovementSpeedMultiplierValue = Math.Max(0.0f, SlowMovementSpeedMultiplier),
                AttackSpeedMultiplierValue = Math.Max(0.0f, SlowAttackSpeedMultiplier),
                CastSpeedMultiplierValue = Math.Max(0.0f, SlowCastSpeedMultiplier),
            };

            statusEffectController.ApplyStatusEffect(slowedEffect, source);
        }
    }
}
