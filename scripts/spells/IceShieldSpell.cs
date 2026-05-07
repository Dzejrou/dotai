using Godot;

using System;

[GlobalClass]
public partial class IceShieldSpell : Spell
{
    [Export]
    public PackedScene ShieldScene { get; set; }

    public override bool ShouldFaceCastRequest => false;

    public override bool TryCast(ISpellCaster caster, SpellCastRequest request)
    {
        if (!base.CanCast(caster, request))
            return false;

        var shieldHost = caster?.SpellOrigin;
        if (shieldHost == null || !GodotObject.IsInstanceValid(shieldHost))
            return false;

        if (TryFindExistingShield(shieldHost, out var existingShield))
        {
            if (!TrySpendCastMana(caster))
                return false;

            existingShield.RefreshShield();
            StartCooldown();
            return true;
        }

        if (ShieldScene == null)
            return LogMissingCastRequestData("Ice Shield spell is missing ShieldScene.");

        if (ShieldScene.Instantiate() is not IceShield iceShield)
            return LogMissingCastRequestData("Ice Shield spell failed to instantiate IceShield.");

        if (!TrySpendCastMana(caster))
        {
            iceShield.QueueFree();
            return false;
        }

        shieldHost.AddChild(iceShield);
        iceShield.Position = Vector2.Zero;
        StartCooldown();
        return true;
    }

    private static bool TryFindExistingShield(Node2D shieldHost, out IceShield shield)
    {
        shield = null;
        if (shieldHost == null)
            return false;

        foreach (var child in shieldHost.GetChildren())
        {
            if (child is not IceShield candidate)
                continue;

            if (shield == null)
            {
                shield = candidate;
                continue;
            }

            candidate.QueueFree();
        }

        return shield != null && GodotObject.IsInstanceValid(shield);
    }
}
