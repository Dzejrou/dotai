using Godot;

using System;

[GlobalClass]
public partial class FireNovaSpell : Spell
{
    private readonly RandomNumberGenerator _random = new();

    [Export]
    public PackedScene VfxScene { get; set; }

    [Export]
    public float Range { get; set; } = 72.0f;

    [Export]
    public int ManaCost { get; set; } = 20;

    [Export]
    public int MinimumDamage { get; set; } = 6;

    [Export]
    public int MaximumDamage { get; set; } = 10;

    public override int DisplayManaCost => Math.Max(0, ManaCost);

    public override void _Ready()
    {
        _random.Randomize();
    }

    public override bool CanCast(ISpellCaster caster)
    {
        if (!base.CanCast(caster))
            return false;

        var sourceFaction = caster.Faction;
        if (caster.ManaState == null || sourceFaction == null)
            return false;

        return caster.ManaState.Current >= Math.Max(0, ManaCost);
    }

    public override bool TryCast(ISpellCaster caster)
    {
        if (!CanCast(caster))
            return false;

        var sourceFaction = caster.Faction;

        if (!TrySpendCastMana(caster, ManaCost))
            return false;

        var resolvedRange = Math.Max(0.0f, Range);
        if (resolvedRange <= 0.0f)
            return true;

        SpawnVfx(caster.SpellOrigin, resolvedRange);
        ApplyAreaDamage(caster, resolvedRange, sourceFaction);
        return true;
    }

    private void SpawnVfx(Node2D spellOrigin, float range)
    {
        if (VfxScene?.Instantiate<FireNovaVfx>() is not FireNovaVfx fireNovaVfx)
            return;

        var parent = spellOrigin.GetParent();
        if (parent == null)
        {
            fireNovaVfx.QueueFree();
            return;
        }

        parent.AddChild(fireNovaVfx);
        fireNovaVfx.GlobalPosition = spellOrigin.GlobalPosition;
        fireNovaVfx.Play(range);
    }

    private void ApplyAreaDamage(ISpellCaster caster, float range, Faction sourceFaction)
    {
        var maximumDamage = Math.Max(MinimumDamage, MaximumDamage);
        foreach (var node in TargetingHelper.EnumerateCandidateTargets(caster.SpellOrigin))
        {
            if (node is not IAttackable attackable)
                continue;

            var targetFactionState = FactionState.ResolveFor(node);
            if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(sourceFaction))
                continue;

            if (caster.SpellOrigin.GlobalPosition.DistanceTo(node.GlobalPosition) > range)
                continue;

            var damage = _random.RandiRange(Math.Min(MinimumDamage, maximumDamage), maximumDamage);
            attackable.ApplyDamage(new DamageInfo(damage, caster.SpellOrigin));
        }
    }
}
